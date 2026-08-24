using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// Small IDispatch ABI bridge used before CLR reflection dispatch. Older OCX collections can
/// expose automation objects whose RCW metadata is unsafe for Type.InvokeMember, while the
/// underlying IDispatch contract remains callable with the native VARIANT layout.
/// </summary>
internal static class VBComDispatch
{
    private const ushort DispatchMethod = 0x1;
    private const ushort DispatchPropertyGet = 0x2;
    private const ushort DispatchPropertyPut = 0x4;
    private const ushort DispatchPropertyPutRef = 0x8;
    private const ushort VariantByRef = 0x4000;
    private const ushort VariantVariant = 0x000C;
    private const int DispIdPropertyPut = -3;
    private const int VariantSize = 16;
    private const int VariantDataOffset = 8;

    public static bool TryInvoke(
        object target,
        string memberName,
        object?[] arguments,
        bool setProperty,
        out object? result)
    {
        result = null;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (!Marshal.IsComObject(target))
        {
            return false;
        }

        try
        {
            var dispatch = (INativeDispatch)target;
            if (!TryGetDispId(dispatch, memberName, out var dispId))
            {
                return false;
            }

            var flags = setProperty
                ? ShouldUsePropertyPutRef(arguments)
                    ? DispatchPropertyPutRef
                    : DispatchPropertyPut
                : (ushort)(DispatchMethod | DispatchPropertyGet);
            var byRefArguments = setProperty
                ? null
                : TryGetByRefArgumentMask(dispatch, dispId, arguments.Length);
            var hr = Invoke(dispatch, dispId, arguments, flags, byRefArguments, out result);
            if (hr < 0 && byRefArguments is not null)
            {
                // A type library can describe an [out] parameter while an individual server
                // still requires its legacy ByVal call shape. Preserve the safe fallback.
                hr = Invoke(dispatch, dispId, arguments, flags, null, out result);
            }
            if (hr < 0 && setProperty)
            {
                // Automation servers disagree on whether object-valued properties require
                // PROPERTYPUT or PROPERTYPUTREF. Retry with the other contract before the
                // reflection fallback gets a chance to touch an old OCX RCW.
                var fallbackFlags = flags == DispatchPropertyPutRef
                    ? DispatchPropertyPut
                    : DispatchPropertyPutRef;
                hr = Invoke(dispatch, dispId, arguments, fallbackFlags, null, out result);
            }

            return hr >= 0;
        }
        catch (COMException)
        {
            result = null;
            return false;
        }
        catch (InvalidCastException)
        {
            result = null;
            return false;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
        catch (NotSupportedException)
        {
            result = null;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    internal static bool TryGetComEventIdentity(
        object target,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        if (!OperatingSystem.IsWindows() ||
            !Marshal.IsComObject(target) ||
            string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        try
        {
            return TryGetComEventIdentity((INativeDispatch)target, eventName, out interfaceId, out dispId);
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryGetComEventIdentity(
        INativeDispatch dispatch,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        if (dispatch.GetTypeInfoCount(out var typeInfoCount) < 0 ||
            typeInfoCount == 0 ||
            dispatch.GetTypeInfo(0, 1033, out var typeInfoPointer) < 0 ||
            typeInfoPointer == IntPtr.Zero)
        {
            return false;
        }

        ITypeInfo? typeInfo = null;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo = (ITypeInfo)Marshal.GetObjectForIUnknown(typeInfoPointer);
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            for (var implementationIndex = 0;
                 implementationIndex < typeAttribute.cImplTypes;
                 implementationIndex++)
            {
                typeInfo.GetImplTypeFlags(implementationIndex, out var flags);
                if ((flags & IMPLTYPEFLAGS.IMPLTYPEFLAG_FSOURCE) == 0)
                {
                    continue;
                }

                typeInfo.GetRefTypeOfImplType(implementationIndex, out var referenceHandle);
                typeInfo.GetRefTypeInfo(referenceHandle, out var sourceTypeInfo);
                try
                {
                    if (TryFindEventInTypeInfo(
                        sourceTypeInfo,
                        eventName,
                        out interfaceId,
                        out dispId))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (Marshal.IsComObject(sourceTypeInfo))
                    {
                        _ = Marshal.ReleaseComObject(sourceTypeInfo);
                    }
                }
            }

            // Some automation servers return the source dispatch interface directly rather
            // than a coclass TYPEATTR. Accept that shape as a final metadata fallback.
            return TryFindEventInTypeInfo(typeInfo, eventName, out interfaceId, out dispId);
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero && typeInfo is not null)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }

            if (typeInfo is not null && Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }

            _ = Marshal.Release(typeInfoPointer);
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TryFindEventInTypeInfo(
        ITypeInfo typeInfo,
        string eventName,
        out Guid interfaceId,
        out int dispId)
    {
        interfaceId = Guid.Empty;
        dispId = 0;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            if (typeAttribute.guid == Guid.Empty)
            {
                return false;
            }

            for (var functionIndex = 0; functionIndex < typeAttribute.cFuncs; functionIndex++)
            {
                typeInfo.GetFuncDesc(functionIndex, out var functionPointer);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    var names = new string[1];
                    typeInfo.GetNames(function.memid, names, names.Length, out var nameCount);
                    if (nameCount > 0 &&
                        string.Equals(names[0], eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        interfaceId = typeAttribute.guid;
                        dispId = function.memid;
                        return true;
                    }
                }
                finally
                {
                    typeInfo.ReleaseFuncDesc(functionPointer);
                }
            }
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }
        }

        return false;
    }

    private static bool ShouldUsePropertyPutRef(object?[] arguments) =>
        arguments.Length > 0 &&
        UnwrapComValue(arguments[^1]) is { } value &&
        Marshal.IsComObject(value);

    private static object? UnwrapComValue(object? value) =>
        value is IVBComObjectProvider provider && provider.ComObject is { } comObject
            ? comObject
            : value;

    [SupportedOSPlatform("windows")]
    private static bool[]? TryGetByRefArgumentMask(
        INativeDispatch dispatch,
        int dispId,
        int argumentCount)
    {
        if (argumentCount == 0 ||
            dispatch.GetTypeInfoCount(out var typeInfoCount) < 0 ||
            typeInfoCount == 0 ||
            dispatch.GetTypeInfo(0, 1033, out var typeInfoPointer) < 0 ||
            typeInfoPointer == IntPtr.Zero)
        {
            return null;
        }

        ITypeInfo? typeInfo = null;
        IntPtr typeAttributePointer = IntPtr.Zero;
        try
        {
            typeInfo = (ITypeInfo)Marshal.GetObjectForIUnknown(typeInfoPointer);
            typeInfo.GetTypeAttr(out typeAttributePointer);
            var typeAttribute = Marshal.PtrToStructure<TYPEATTR>(typeAttributePointer);
            var elementSize = Marshal.SizeOf<ELEMDESC>();
            for (var functionIndex = 0; functionIndex < typeAttribute.cFuncs; functionIndex++)
            {
                typeInfo.GetFuncDesc(functionIndex, out var functionPointer);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(functionPointer);
                    if (function.memid != dispId ||
                        function.cParams < argumentCount ||
                        function.lprgelemdescParam == IntPtr.Zero)
                    {
                        continue;
                    }

                    var mask = new bool[argumentCount];
                    for (var parameterIndex = 0; parameterIndex < argumentCount; parameterIndex++)
                    {
                        var elementPointer = IntPtr.Add(
                            function.lprgelemdescParam,
                            parameterIndex * elementSize);
                        var element = Marshal.PtrToStructure<ELEMDESC>(elementPointer);
                        mask[parameterIndex] =
                            (element.desc.paramdesc.wParamFlags & PARAMFLAG.PARAMFLAG_FOUT) != 0;
                    }

                    return mask.Any(value => value) ? mask : null;
                }
                finally
                {
                    typeInfo.ReleaseFuncDesc(functionPointer);
                }
            }
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        finally
        {
            if (typeAttributePointer != IntPtr.Zero && typeInfo is not null)
            {
                typeInfo.ReleaseTypeAttr(typeAttributePointer);
            }

            if (typeInfo is not null && Marshal.IsComObject(typeInfo))
            {
                _ = Marshal.ReleaseComObject(typeInfo);
            }

            _ = Marshal.Release(typeInfoPointer);
        }

        return null;
    }

    private static bool TryGetDispId(
        INativeDispatch dispatch,
        string memberName,
        out int dispId)
    {
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            dispId = 0;
            return dispatch.GetIDsOfNames(
                ref iid,
                names,
                1,
                1033,
                out dispId) >= 0;
        }
        finally
        {
            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(name);
        }
    }

    [SupportedOSPlatform("windows")]
    private static int Invoke(
        INativeDispatch dispatch,
        int dispId,
        object?[] arguments,
        ushort flags,
        bool[]? byRefArguments,
        out object? result)
    {
        var variants = arguments.Length == 0
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var byRefValues = byRefArguments is null
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var namedArguments = flags is DispatchPropertyPut or DispatchPropertyPutRef
            ? Marshal.AllocCoTaskMem(sizeof(int))
            : IntPtr.Zero;
        var initialized = new bool[arguments.Length];

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                var variant = IntPtr.Add(
                    variants,
                    index * VariantSize);
                var sourceIndex = arguments.Length - index - 1;
                if (byRefArguments is not null && byRefArguments[sourceIndex])
                {
                    var valueVariant = IntPtr.Add(
                        byRefValues,
                        index * VariantSize);
                    Marshal.GetNativeVariantForObject(
                        UnwrapComValue(arguments[sourceIndex]),
                        valueVariant);
                    Marshal.WriteInt16(
                        variant,
                        (short)(VariantByRef | VariantVariant));
                    Marshal.WriteIntPtr(variant, VariantDataOffset, valueVariant);
                }
                else
                {
                    Marshal.GetNativeVariantForObject(
                        UnwrapComValue(arguments[sourceIndex]),
                        variant);
                }

                initialized[index] = true;
            }

            if (namedArguments != IntPtr.Zero)
            {
                Marshal.WriteInt32(namedArguments, DispIdPropertyPut);
            }

            var parameters = new NativeDispParams
            {
                Arguments = variants,
                NamedArguments = namedArguments,
                ArgumentCount = (uint)arguments.Length,
                NamedArgumentCount = namedArguments == IntPtr.Zero ? 0u : 1u
            };
            var exception = default(NativeExcepInfo);
            var iid = Guid.Empty;
            var hr = dispatch.Invoke(
                dispId,
                ref iid,
                1033,
                flags,
                ref parameters,
                out result,
                out exception,
                out _);
            if (hr >= 0 && byRefArguments is not null)
            {
                for (var index = 0; index < arguments.Length; index++)
                {
                    var sourceIndex = arguments.Length - index - 1;
                    if (!byRefArguments[sourceIndex])
                    {
                        continue;
                    }

                    var valueVariant = IntPtr.Add(
                        byRefValues,
                        index * VariantSize);
                    arguments[sourceIndex] = Marshal.GetObjectForNativeVariant(valueVariant);
                }
            }

            return hr;
        }
        finally
        {
            for (var index = 0; index < initialized.Length; index++)
            {
                if (!initialized[index])
                {
                    continue;
                }

                var sourceIndex = arguments.Length - index - 1;
                var values = byRefArguments is not null && byRefArguments[sourceIndex]
                    ? byRefValues
                    : variants;
                _ = VariantClear(IntPtr.Add(values, index * VariantSize));
            }

            if (namedArguments != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(namedArguments);
            }

            if (variants != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(variants);
            }

            if (byRefValues != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(byRefValues);
            }
        }
    }

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);

    [ComImport]
    [Guid("00020400-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface INativeDispatch
    {
        [PreserveSig]
        int GetTypeInfoCount(out uint typeInfoCount);

        [PreserveSig]
        int GetTypeInfo(uint typeInfoIndex, uint lcid, out IntPtr typeInfo);

        [PreserveSig]
        int GetIDsOfNames(
            ref Guid iid,
            IntPtr names,
            uint nameCount,
            uint lcid,
            out int dispId);

        [PreserveSig]
        int Invoke(
            int dispId,
            ref Guid iid,
            uint lcid,
            ushort flags,
            ref NativeDispParams parameters,
            [MarshalAs(UnmanagedType.Struct)] out object? result,
            out NativeExcepInfo exception,
            out uint argumentError);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDispParams
    {
        public IntPtr Arguments;
        public IntPtr NamedArguments;
        public uint ArgumentCount;
        public uint NamedArgumentCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeExcepInfo
    {
        public ushort Code;
        public ushort Reserved;
        public IntPtr Source;
        public IntPtr Description;
        public IntPtr HelpFile;
        public uint HelpContext;
        public IntPtr DeferredFillIn;
        public int Scode;
    }
}
