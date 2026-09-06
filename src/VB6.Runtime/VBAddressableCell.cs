using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Owns one native, GC-stable storage cell for an unmanaged value.
/// </summary>
/// <remarks>
/// This is the raw storage primitive for the R3 addressable-storage contract. It deliberately
/// does not expose a managed interior pointer: callers receive a separately allocated native
/// address and synchronize through <see cref="Read"/> and <see cref="Write"/>. The emitter has
/// not yet connected arbitrary VB6 slots to this primitive, so creating a cell alone does not
/// widen the public <c>VarPtr</c>/<c>StrPtr</c> surface.
/// </remarks>
public sealed class VBAddressableCell<T> : IDisposable
    where T : unmanaged
{
    private IntPtr _storage;

    private VBAddressableCell(T value)
    {
        _storage = Marshal.AllocCoTaskMem(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(value, _storage, fDeleteOld: false);
    }

    /// <summary>Creates a native cell initialized with <paramref name="value"/>.</summary>
    public static VBAddressableCell<T> Create(T value) => new(value);

    /// <summary>Returns the GC-stable native address owned by this cell.</summary>
    public IntPtr GetNativeAddress()
    {
        ThrowIfDisposed();
        return _storage;
    }

    /// <summary>Reads the value currently held in native storage.</summary>
    public T Read()
    {
        ThrowIfDisposed();
        return Marshal.PtrToStructure<T>(_storage);
    }

    /// <summary>Replaces the native storage contents with <paramref name="value"/>.</summary>
    public void Write(T value)
    {
        ThrowIfDisposed();
        Marshal.StructureToPtr(value, _storage, fDeleteOld: false);
    }

    public void Dispose()
    {
        if (_storage == IntPtr.Zero)
        {
            return;
        }

        Marshal.FreeCoTaskMem(_storage);
        _storage = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }

    ~VBAddressableCell() => Dispose();

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_storage == IntPtr.Zero, this);
}

/// <summary>
/// Non-generic entry points used by emitted code for the initial Int32 addressable-storage slice.
/// </summary>
public static class VBAddressableStorage
{
    public static object CreateInt32(int value) => VBAddressableCell<int>.Create(value);

    public static IntPtr GetInt32NativeAddress(object storage) => GetInt32(storage).GetNativeAddress();

    public static int ReadInt32(object storage) => GetInt32(storage).Read();

    public static void WriteInt32(object storage, int value) => GetInt32(storage).Write(value);

    public static void DisposeInt32(object storage) => GetInt32(storage).Dispose();

    private static VBAddressableCell<int> GetInt32(object storage) => storage as VBAddressableCell<int>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Long.", nameof(storage));
}
