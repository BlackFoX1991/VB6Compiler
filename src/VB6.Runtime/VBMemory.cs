using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Memory intrinsics whose exact contract depends on the selected native ABI. The managed runtime
/// keeps explicit failure semantics until the x86/x64 backend supplies stable addresses and UDT
/// layout operations.
/// </summary>
public static class VBMemory
{
    public static int VarPtr(object? value) =>
        throw new PlatformNotSupportedException("VarPtr requires a native VB6 memory backend.");

    /// <summary>
    /// Returns the native identity pointer of an object. COM identity is represented by the
    /// object's controlling <c>IUnknown</c>; the temporary reference acquired by interop is
    /// released before returning so repeated calls do not leak a COM reference.
    /// </summary>
    public static IntPtr ObjPtr(object? value)
    {
        if (value is null || VBVariants.IsNothing(value))
        {
            return IntPtr.Zero;
        }

        if (!VBVariants.IsObject(value))
        {
            throw new VB6TypeMismatchException("ObjPtr requires an object value.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ObjPtr requires the Windows COM backend.");
        }

        var unknown = Marshal.GetIUnknownForObject(value);
        try
        {
            return unknown;
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    public static int StrPtr(string? value) =>
        throw new PlatformNotSupportedException("StrPtr requires a native string memory backend.");

    public static void LSet(object? target, object? source) =>
        throw new PlatformNotSupportedException("LSet requires native UDT layout semantics.");
}
