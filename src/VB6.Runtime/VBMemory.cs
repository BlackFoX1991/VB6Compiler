using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Memory intrinsics whose exact contract depends on the selected native ABI. Managed LSet supports
/// a bounded blittable UDT slice; native-only address operations and non-representable layouts keep
/// explicit failure semantics until the x86/x64 backend supplies their contracts.
/// </summary>
public static class VBMemory
{
    /// <summary>
    /// The address of a managed cell is valid only while the cell is held in place, and a returned
    /// pointer does not survive that -- the collector may move the cell right after. Supported is
    /// therefore exactly the position where VB6 passes the pointer straight on: a
    /// <c>ByVal … As Any</c> argument of a Declare, which lowering turns into an address without
    /// ever calling this method.
    ///
    /// Reaching here means the program asked for a pointer it could keep. Answering with a number
    /// would be worse than refusing: it would point somewhere else after the next collection. The
    /// number stays VB6's 5 for an invalid call, but the description says what actually happened
    /// instead of leaving the catch-all unexplained.
    /// </summary>
    public static int VarPtr(object? value)
    {
        _ = value;
        VBErrors.Raise(
            5,
            "VarPtr",
            "VarPtr is supported only as a ByVal As Any argument of a Declare, where the address " +
            "is consumed immediately.",
            string.Empty,
            0);
        return 0;
    }

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

    /// <summary>Same contract as <see cref="VarPtr"/>: only the immediately consumed form.</summary>
    public static int StrPtr(string? value)
    {
        _ = value;
        VBErrors.Raise(
            5,
            "StrPtr",
            "StrPtr is supported only as a ByVal As Any argument of a Declare, where the address " +
            "is consumed immediately.",
            string.Empty,
            0);
        return 0;
    }

    /// <summary>
    /// Executes a managed raw record transfer without boxing the destination. The generic ref
    /// parameter keeps the destination as a tracked managed reference while the temporary native
    /// buffers are allocated, so a moving GC cannot invalidate an interior pointer.
    /// </summary>
    public static void LSet<T>(ref T target, object? source)
        where T : struct
    {
        if (!IsSupportedRawRecord(typeof(T)))
        {
            throw new PlatformNotSupportedException(
                "LSet destination layout is not representable by the managed raw-record backend.");
        }

        if (source is null || !IsSupportedRawRecord(source.GetType()))
        {
            throw new PlatformNotSupportedException(
                "LSet source layout is not representable by the managed raw-record backend.");
        }

        var destinationSize = Marshal.SizeOf<T>();
        var sourceSize = Marshal.SizeOf(source);
        var sourceBuffer = Marshal.AllocHGlobal(sourceSize);
        var destinationBuffer = Marshal.AllocHGlobal(destinationSize);
        try
        {
            Marshal.StructureToPtr(source, sourceBuffer, fDeleteOld: false);

            if (destinationSize > 0)
            {
                Marshal.Copy(new byte[destinationSize], 0, destinationBuffer, destinationSize);
            }

            var copiedSize = Math.Min(destinationSize, sourceSize);
            if (copiedSize > 0)
            {
                var bytes = new byte[copiedSize];
                Marshal.Copy(sourceBuffer, bytes, 0, copiedSize);
                Marshal.Copy(bytes, 0, destinationBuffer, copiedSize);
            }

            target = Marshal.PtrToStructure<T>(destinationBuffer);
        }
        finally
        {
            Marshal.DestroyStructure(sourceBuffer, source.GetType());
            Marshal.FreeHGlobal(sourceBuffer);
            Marshal.FreeHGlobal(destinationBuffer);
        }
    }

    public static void LSet(object? target, object? source) =>
        throw new PlatformNotSupportedException(
            "LSet requires a supported managed UDT destination or the native UDT layout backend.");

    public static void RSet(object? target, object? source) =>
        throw new PlatformNotSupportedException(
            "RSet requires a fixed-length String destination or the native VB6 memory backend.");

    private static bool IsSupportedRawRecord(Type type) =>
        type.IsValueType &&
        type.IsLayoutSequential &&
        string.Equals(type.Namespace, "VB6.Generated", StringComparison.Ordinal) &&
        IsSupportedRawRecord(type, new HashSet<Type>());

    private static bool IsSupportedRawRecord(Type type, HashSet<Type> activePath)
    {
        if (!activePath.Add(type))
        {
            return false;
        }

        foreach (var field in type.GetFields(
                     System.Reflection.BindingFlags.Instance |
                     System.Reflection.BindingFlags.Public |
                     System.Reflection.BindingFlags.NonPublic))
        {
            var fieldType = field.FieldType;
            if (fieldType == typeof(bool) ||
                fieldType == typeof(byte) ||
                fieldType == typeof(short) ||
                fieldType == typeof(int) ||
                fieldType == typeof(long) ||
                fieldType == typeof(IntPtr) ||
                fieldType == typeof(ushort) ||
                fieldType == typeof(uint) ||
                fieldType == typeof(ulong) ||
                fieldType == typeof(float) ||
                fieldType == typeof(double) ||
                fieldType == typeof(VBCurrency))
            {
                continue;
            }

            if (fieldType.IsValueType &&
                fieldType.IsLayoutSequential &&
                string.Equals(fieldType.Namespace, "VB6.Generated", StringComparison.Ordinal) &&
                IsSupportedRawRecord(fieldType, activePath))
            {
                continue;
            }

            activePath.Remove(type);
            return false;
        }

        activePath.Remove(type);
        return true;
    }

}
