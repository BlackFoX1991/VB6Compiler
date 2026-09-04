using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// Calls a member of a COM interface through its vtable.
///
/// This is the half of a type library that <see cref="VBComDispatch"/> cannot reach. A dispinterface
/// or a dual interface answers <c>IDispatch::Invoke</c>, so a member call needs nothing but a name;
/// an interface derived from <c>IUnknown</c> alone has no such entry point, and its members exist
/// only as vtable slots. <c>stdole.IFont.Clone</c> is the documented example: the dispatch twin
/// <c>IFontDisp</c> carries the properties but not the methods, so a call to <c>Clone</c> reported
/// 438 — "member not found" — although the member is right there in the library.
///
/// The slot index is what the compiler carries, never the byte offset: an offset is a function of
/// the pointer size of the process that read the library, the index is not.
///
/// Every method of such an interface returns an <c>HRESULT</c>, and the value the program wants
/// travels in a trailing out-parameter. That shape is the reason the delegate built here always has
/// one more argument than the VB6 call.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VBComVTable
{
    private static readonly AssemblyBuilder VTableAssembly = AssemblyBuilder.DefineDynamicAssembly(
        new AssemblyName("VB6.Runtime.ComVTable"),
        AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder VTableModule =
        VTableAssembly.DefineDynamicModule("VB6.Runtime.ComVTable");

    private static readonly ConcurrentDictionary<string, Type> DelegateTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Invokes the member in <paramref name="slot"/> of the interface <paramref name="interfaceId"/>
    /// on <paramref name="target"/>. The declared VARIANT types decide how the arguments and the
    /// result cross the boundary.
    /// </summary>
    public static object? Invoke(
        object? target,
        string interfaceId,
        int slot,
        string parameterTypes,
        short returnType,
        VBArray<object> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceId);
        ArgumentNullException.ThrowIfNull(parameterTypes);
        ArgumentNullException.ThrowIfNull(arguments);

        var declaredTypes = ParseParameterTypes(parameterTypes);
        var values = arguments.EnumerateValues().ToArray();

        if (target is null || VBVariants.IsNothing(target))
        {
            VBErrors.Raise(91, "COM", "Object variable or With block variable not set", string.Empty, 0);
        }

        var instance = (target as IVBComObjectProvider)?.ComObject ?? target!;
        var unknown = IntPtr.Zero;
        var self = IntPtr.Zero;
        try
        {
            unknown = Marshal.GetIUnknownForObject(instance);
            var iid = Guid.Parse(interfaceId);
            var hr = Marshal.QueryInterface(unknown, in iid, out self);
            if (hr < 0 || self == IntPtr.Zero)
            {
                // The object does not offer the interface the program declared. VB6 answers 430
                // there -- "Class does not support Automation or does not support expected
                // interface" -- rather than reporting a missing member.
                VBErrors.Raise(430, interfaceId, "Class does not support the expected interface", string.Empty, 0);
            }

            return InvokeSlot(self, slot, declaredTypes, returnType, values);
        }
        finally
        {
            if (self != IntPtr.Zero)
            {
                Marshal.Release(self);
            }

            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }
        }
    }

    private static readonly ConcurrentDictionary<string, short[]> ParameterTypeCache =
        new(StringComparer.Ordinal);

    private static short[] ParseParameterTypes(string parameterTypes) =>
        parameterTypes.Length == 0
            ? []
            : ParameterTypeCache.GetOrAdd(parameterTypes, key => key
                .Split(',')
                .Select(part => short.Parse(part, System.Globalization.CultureInfo.InvariantCulture))
                .ToArray());

    private static object? InvokeSlot(
        IntPtr self,
        int slot,
        short[] parameterTypes,
        short returnType,
        object?[] arguments)
    {
        var vtable = Marshal.ReadIntPtr(self);
        var function = Marshal.ReadIntPtr(vtable, slot * IntPtr.Size);
        var delegateType = GetDelegateType(parameterTypes, returnType);
        var callable = Marshal.GetDelegateForFunctionPointer(function, delegateType);

        var hasResult = returnType != (short)VarEnum.VT_VOID;
        var callArguments = new object?[1 + parameterTypes.Length + (hasResult ? 1 : 0)];
        callArguments[0] = self;
        for (var index = 0; index < parameterTypes.Length; index++)
        {
            callArguments[index + 1] = ConvertArgument(
                index < arguments.Length ? arguments[index] : null,
                parameterTypes[index]);
        }

        if (hasResult)
        {
            callArguments[^1] = DefaultOf(returnType);
        }

        var hresult = (int)callable.DynamicInvoke(callArguments)!;
        if (hresult < 0)
        {
            // The same mapping the dispatch path uses, so a failure through the vtable reports the
            // number a VB6 program would see through IDispatch.
            var error = VBComDispatch.MapComException(0, hresult, "COM", string.Empty, string.Empty, 0);
            VBErrors.Raise(error.Number, error.Source, error.Description, error.HelpFile, error.HelpContext);
        }

        return hasResult ? ConvertResult(callArguments[^1], returnType) : null;
    }

    private static object? ConvertArgument(object? value, short variantType) => (VarEnum)variantType switch
    {
        VarEnum.VT_I2 => VBConversions.CInt(value),
        VarEnum.VT_I4 or VarEnum.VT_INT => (int)VBConversions.CLng(value),
        VarEnum.VT_R4 => VBConversions.CSng(value),
        VarEnum.VT_R8 => VBConversions.CDbl(value),
        VarEnum.VT_BOOL => VBConversions.CBool(value) ? (short)-1 : (short)0,
        VarEnum.VT_BSTR => Marshal.StringToBSTR(VBConversions.CStr(value)),
        VarEnum.VT_CY => VBConversions.CCur(value).ScaledValue,
        VarEnum.VT_DATE => VBConversions.CDbl(value),
        VarEnum.VT_DISPATCH or VarEnum.VT_UNKNOWN or VarEnum.VT_PTR =>
            value is null || VBVariants.IsNothing(value)
                ? IntPtr.Zero
                : Marshal.GetIUnknownForObject(value),
        _ => value
    };

    private static object? ConvertResult(object? value, short variantType) => (VarEnum)variantType switch
    {
        VarEnum.VT_BSTR => value is IntPtr text && text != IntPtr.Zero
            ? Marshal.PtrToStringBSTR(text)
            : string.Empty,
        VarEnum.VT_BOOL => value is short flag && flag != 0,
        VarEnum.VT_CY => value is long scaled ? VBCurrency.FromScaled(scaled) : value,
        VarEnum.VT_DISPATCH or VarEnum.VT_UNKNOWN or VarEnum.VT_PTR =>
            value is IntPtr pointer && pointer != IntPtr.Zero
                ? Marshal.GetObjectForIUnknown(pointer)
                : VBVariants.NothingValue(),
        _ => value
    };

    /// <summary>
    /// The zero of the marshalling type, **boxed by hand**. Without the casts every arm of this
    /// switch is a number, and since .NET 7 IntPtr is nint with an implicit conversion to double --
    /// so the switch takes double as its natural type, IntPtr.Zero silently becomes 0.0, and the
    /// call fails with "Object of type System.Double cannot be converted to type System.IntPtr&amp;".
    /// Assigning the result to object? does not prevent that: a natural type wins over the target.
    /// </summary>
    private static object DefaultOf(short variantType) => (VarEnum)variantType switch
    {
        VarEnum.VT_I2 or VarEnum.VT_BOOL => (object)(short)0,
        VarEnum.VT_I4 or VarEnum.VT_INT => (object)0,
        VarEnum.VT_R4 => (object)0f,
        VarEnum.VT_R8 or VarEnum.VT_DATE => (object)0d,
        VarEnum.VT_CY => (object)0L,
        _ => IntPtr.Zero
    };

    private static Type MarshalTypeOf(short variantType) => (VarEnum)variantType switch
    {
        VarEnum.VT_I2 or VarEnum.VT_BOOL => typeof(short),
        VarEnum.VT_I4 or VarEnum.VT_INT => typeof(int),
        VarEnum.VT_R4 => typeof(float),
        VarEnum.VT_R8 or VarEnum.VT_DATE => typeof(double),
        VarEnum.VT_CY => typeof(long),
        _ => typeof(IntPtr)
    };

    /// <summary>
    /// Builds -- and caches -- the delegate that matches one vtable signature: the interface
    /// pointer, the declared arguments, and a trailing out-parameter for the value, all returning
    /// the HRESULT.
    /// </summary>
    private static Type GetDelegateType(short[] parameterTypes, short returnType)
    {
        var key = string.Join(",", parameterTypes) + "|" + returnType;
        return DelegateTypes.GetOrAdd(key, _ =>
        {
            var hasResult = returnType != (short)VarEnum.VT_VOID;
            var signature = new Type[1 + parameterTypes.Length + (hasResult ? 1 : 0)];
            signature[0] = typeof(IntPtr);
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                signature[index + 1] = MarshalTypeOf(parameterTypes[index]);
            }

            if (hasResult)
            {
                signature[^1] = MarshalTypeOf(returnType).MakeByRefType();
            }

            var type = VTableModule.DefineType(
                "VB6ComVTable_" + DelegateTypes.Count,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.AutoClass,
                typeof(MulticastDelegate));

            type.SetCustomAttribute(new CustomAttributeBuilder(
                typeof(UnmanagedFunctionPointerAttribute).GetConstructor([typeof(CallingConvention)])!,
                [CallingConvention.StdCall]));

            var constructor = type.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard,
                [typeof(object), typeof(IntPtr)]);
            constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var invoke = type.DefineMethod(
                "Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot |
                MethodAttributes.Virtual,
                typeof(int),
                signature);
            invoke.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            return type.CreateType();
        });
    }
}
