using System.Runtime.InteropServices;
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
    private const int DispIdPropertyPut = -3;
    private const int VariantSize = 16;

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
            var hr = Invoke(dispatch, dispId, arguments, flags, out result);
            if (hr < 0 && setProperty)
            {
                // Automation servers disagree on whether object-valued properties require
                // PROPERTYPUT or PROPERTYPUTREF. Retry with the other contract before the
                // reflection fallback gets a chance to touch an old OCX RCW.
                var fallbackFlags = flags == DispatchPropertyPutRef
                    ? DispatchPropertyPut
                    : DispatchPropertyPutRef;
                hr = Invoke(dispatch, dispId, arguments, fallbackFlags, out result);
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

    private static bool ShouldUsePropertyPutRef(object?[] arguments) =>
        arguments.Length > 0 &&
        UnwrapComValue(arguments[^1]) is { } value &&
        Marshal.IsComObject(value);

    private static object? UnwrapComValue(object? value) =>
        value is IVBComObjectProvider provider && provider.ComObject is { } comObject
            ? comObject
            : value;

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
        out object? result)
    {
        var variants = arguments.Length == 0
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(VariantSize * arguments.Length);
        var namedArguments = flags is DispatchPropertyPut or DispatchPropertyPutRef
            ? Marshal.AllocCoTaskMem(sizeof(int))
            : IntPtr.Zero;
        var initialized = 0;

        try
        {
            for (var index = 0; index < arguments.Length; index++)
            {
                var variant = IntPtr.Add(
                    variants,
                    index * VariantSize);
                Marshal.GetNativeVariantForObject(
                    UnwrapComValue(arguments[arguments.Length - index - 1]),
                    variant);
                initialized++;
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
            return dispatch.Invoke(
                dispId,
                ref iid,
                1033,
                flags,
                ref parameters,
                out result,
                out exception,
                out _);
        }
        finally
        {
            for (var index = 0; index < initialized; index++)
            {
                _ = VariantClear(IntPtr.Add(variants, index * VariantSize));
            }

            if (namedArguments != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(namedArguments);
            }

            if (variants != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(variants);
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
