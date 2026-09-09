using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// One call arriving through <c>IDispatch.Invoke</c>: arguments out of <c>DISPPARAMS</c>, the member
/// called, the answer written back into the caller's <c>VARIANT</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class VBComInvocation
{
    private const int SOk = 0;
    private const int DispEBadParameterCount = unchecked((int)0x8002000E);
    private const int DispETypeMismatch = unchecked((int)0x80020005);

    private const ushort DispatchMethod = 1;
    private const ushort DispatchPropertyGet = 2;
    private const ushort DispatchPropertyPut = 4;
    private const ushort DispatchPropertyPutRef = 8;

    public static int Invoke(object instance, VBComMember member, ushort flags, IntPtr parameters, IntPtr result)
    {
        var arguments = ReadArguments(parameters, out var variants);
        var writing = (flags & (DispatchPropertyPut | DispatchPropertyPutRef)) != 0;

        if (member.Field is { } field)
        {
            if (writing)
            {
                if (arguments.Length != 1)
                {
                    return DispEBadParameterCount;
                }

                field.SetValue(instance, VBDynamicDispatch.ConvertArgument(arguments[0], field.FieldType));
                return SOk;
            }

            return WriteResult(result, field.GetValue(instance), field.FieldType);
        }

        if (member.Property is { } property)
        {
            if (writing)
            {
                if (arguments.Length < 1 || !property.CanWrite)
                {
                    return DispEBadParameterCount;
                }

                var value = VBDynamicDispatch.ConvertArgument(arguments[^1], property.PropertyType);
                property.SetValue(instance, value, Convert(arguments[..^1], property.GetIndexParameters()));
                return SOk;
            }

            if (!property.CanRead)
            {
                return DispETypeMismatch;
            }

            return WriteResult(
                result,
                property.GetValue(instance, Convert(arguments, property.GetIndexParameters())),
                property.PropertyType);
        }

        var method = member.Method!;
        var expected = method.GetParameters();
        if (arguments.Length > expected.Length)
        {
            return DispEBadParameterCount;
        }

        // Ein ausgelassenes optionales Argument kommt als fehlender Eintrag an, nicht als Missing:
        // Ein Client, der die Bibliothek gelesen hat, weiss aus PARAMFLAG_FOPT, dass er es
        // weglassen darf, und schickt es einfach nicht mit.
        var call = new object?[expected.Length];
        for (var index = 0; index < expected.Length; index++)
        {
            call[index] = index < arguments.Length
                ? VBDynamicDispatch.ConvertArgument(arguments[index], expected[index].ParameterType)
                : expected[index].HasDefaultValue ? expected[index].DefaultValue : Missing(expected[index]);
        }

        var returned = method.Invoke(instance, call);

        // ByRef bedeutet: Der Aufgerufene schreibt in den Speicher des Aufrufers. method.Invoke
        // legt das Ergebnis in das Argumentarray zurück, und von dort muss es in die VARIANT des
        // Clients -- sonst gelingt der Aufruf und die Zuweisung verschwindet.
        for (var index = 0; index < expected.Length && index < variants.Length; index++)
        {
            if (expected[index].ParameterType.IsByRef)
            {
                WriteBack(variants[index], call[index]);
            }
        }

        return method.ReturnType == typeof(void)
            ? SOk
            : WriteResult(result, returned, method.ReturnType);
    }

    private static object? Missing(ParameterInfo parameter) =>
        parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;

    private static object?[] Convert(object?[] arguments, ParameterInfo[] expected)
    {
        var converted = new object?[Math.Min(arguments.Length, expected.Length)];
        for (var index = 0; index < converted.Length; index++)
        {
            converted[index] = VBDynamicDispatch.ConvertArgument(arguments[index], expected[index].ParameterType);
        }

        return converted;
    }

    /// <summary>
    /// Reads DISPPARAMS. The argument array is in **reverse** order -- rgvarg[0] is the last
    /// argument -- which is the single most common way to get a dispatch server subtly wrong.
    /// The variant addresses are kept: a ByRef argument is written back into the caller's own
    /// storage after the call, and only these addresses lead there.
    /// </summary>
    private static object?[] ReadArguments(IntPtr parameters) => ReadArguments(parameters, out _);

    private static object?[] ReadArguments(IntPtr parameters, out IntPtr[] variants)
    {
        variants = Array.Empty<IntPtr>();
        if (parameters == IntPtr.Zero)
        {
            return Array.Empty<object?>();
        }

        var arguments = Marshal.ReadIntPtr(parameters);
        var count = Marshal.ReadInt32(parameters, IntPtr.Size * 2);
        if (arguments == IntPtr.Zero || count <= 0)
        {
            return Array.Empty<object?>();
        }

        var variantSize = VBComVariant.Size;
        var values = new object?[count];
        variants = new IntPtr[count];
        for (var index = 0; index < count; index++)
        {
            var variant = IntPtr.Add(arguments, variantSize * (count - 1 - index));
            variants[index] = variant;
            values[index] = Marshal.GetObjectForNativeVariant(variant);
        }

        return values;
    }

    /// <summary>
    /// Writes one ByRef argument back into the caller's storage.
    ///
    /// The variant the client passed carries VT_BYREF and a pointer to where the value lives; the
    /// value has to land *there*, not in a copy. Without this a ByRef argument silently keeps its
    /// old value -- the client sees the call succeed and the write disappear.
    /// </summary>
    private static void WriteBack(IntPtr variant, object? value)
    {
        if (variant == IntPtr.Zero)
        {
            return;
        }

        var type = Marshal.ReadInt16(variant);
        if ((type & VariantByRef) == 0)
        {
            return;
        }

        var target = Marshal.ReadIntPtr(variant, VBComVariant.DataOffset);
        if (target == IntPtr.Zero)
        {
            return;
        }

        switch (type & VariantTypeMask)
        {
            case VtVariant:
                // Der Zeiger zeigt auf einen VARIANT des Aufrufers: erst dessen alten Inhalt
                // freigeben, sonst bleibt ein BSTR oder ein SAFEARRAY liegen.
                VariantClear(target);
                Marshal.GetNativeVariantForObject(value, target);
                break;

            case VtBstr:
                var old = Marshal.ReadIntPtr(target);
                Marshal.WriteIntPtr(target, value is null ? IntPtr.Zero : Marshal.StringToBSTR(VBConversions.ConvertCStr(value)));
                if (old != IntPtr.Zero) { Marshal.FreeBSTR(old); }
                break;

            case VtI1 or VtUi1:
                Marshal.WriteByte(target, VBConversions.ConvertCByte(value ?? (byte)0));
                break;

            case VtI2 or VtUi2 or VtBool:
                Marshal.WriteInt16(target, (type & VariantTypeMask) == VtBool
                    ? (short)(VBConversions.ConvertCBool(value ?? false) ? -1 : 0)
                    : VBConversions.ConvertCInt(value ?? (short)0));
                break;

            case VtI4 or VtUi4 or VtInt or VtUint or VtError:
                Marshal.WriteInt32(target, VBConversions.ConvertCLng(value ?? 0));
                break;

            case VtR4:
                Marshal.WriteInt32(target, BitConverter.SingleToInt32Bits(VBConversions.ConvertCSng(value ?? 0f)));
                break;

            case VtR8 or VtDate:
                Marshal.WriteInt64(target, BitConverter.DoubleToInt64Bits(VBConversions.ConvertCDbl(value ?? 0d)));
                break;

            case VtI8 or VtUi8:
                Marshal.WriteInt64(target, VBConversions.ConvertCLngLng(value ?? 0L));
                break;

            // Alles andere bleibt unberührt. Ein stilles Halbschreiben wäre schlimmer als eine
            // Grenze, die man sieht.
        }
    }

    private const short VariantByRef = 0x4000;
    private const short VariantTypeMask = 0x0FFF;
    private const short VtI2 = 2;
    private const short VtI4 = 3;
    private const short VtR4 = 4;
    private const short VtR8 = 5;
    private const short VtDate = 7;
    private const short VtBstr = 8;
    private const short VtError = 10;
    private const short VtBool = 11;
    private const short VtVariant = 12;
    private const short VtI1 = 16;
    private const short VtUi1 = 17;
    private const short VtUi2 = 18;
    private const short VtUi4 = 19;
    private const short VtI8 = 20;
    private const short VtUi8 = 21;
    private const short VtInt = 22;
    private const short VtUint = 23;

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);

    private static int WriteResult(IntPtr result, object? value, Type declared)
    {
        if (result == IntPtr.Zero)
        {
            return SOk;
        }

        if (VBComRecordInfo.TryWriteRecord(result, value, declared))
        {
            return SOk;
        }

        Marshal.GetNativeVariantForObject(value, result);
        return SOk;
    }

    /// <summary>
    /// Fills the caller's EXCEPINFO. VB6 reports a server error through it, and a client that only
    /// sees DISP_E_EXCEPTION without one has a number and no reason.
    /// </summary>
    public static void WriteExceptionInfo(IntPtr exception, Exception error)
    {
        if (exception == IntPtr.Zero)
        {
            return;
        }

        var raised = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
        var number = raised is VB6RaisedError vb6 ? vb6.Number : 5;

        // wCode, wReserved, then three BSTRs, then dwHelpContext, pvReserved, pfnDeferredFillIn,
        // scode. The reserved slot between dwHelpContext and pfnDeferredFillIn is real; without it
        // scode lands in the wrong place.
        Marshal.WriteInt16(exception, 0, 0);
        Marshal.WriteInt16(exception, 2, 0);
        var pointer = IntPtr.Size;
        Marshal.WriteIntPtr(exception, pointer, Marshal.StringToBSTR("VB6"));
        Marshal.WriteIntPtr(exception, pointer * 2, Marshal.StringToBSTR(raised.Message));
        Marshal.WriteIntPtr(exception, pointer * 3, IntPtr.Zero);
        Marshal.WriteInt32(exception, pointer * 4, 0);
        Marshal.WriteIntPtr(exception, pointer * 5, IntPtr.Zero);
        Marshal.WriteIntPtr(exception, pointer * 6, IntPtr.Zero);
        Marshal.WriteInt32(exception, pointer * 7, unchecked((int)(0x800A0000 + number)));
    }
}
