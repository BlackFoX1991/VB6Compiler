using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Owns a temporary UTF-16 buffer for a <c>ByVal StrPtr(value)</c> argument passed through a
/// Declare <c>As Any</c> pointer. The storage is scoped to the native call because a managed String
/// cannot expose a stable writable address for the lifetime of the generated program.
/// </summary>
public sealed class VBNativeStringPointer : IDisposable
{
    private readonly int _length;
    private IntPtr _storage;

    private VBNativeStringPointer(string value)
    {
        _length = value.Length;
        _storage = Marshal.AllocCoTaskMem(checked((_length + 1) * sizeof(char)));
        if (_length != 0)
        {
            Marshal.Copy(value.ToCharArray(), 0, _storage, _length);
        }

        Marshal.WriteInt16(_storage, _length * sizeof(char), 0);
    }

    public static VBNativeStringPointer Create(string? value) =>
        new(value ?? string.Empty);

    public IntPtr GetNativeAddress()
    {
        ObjectDisposedException.ThrowIf(_storage == IntPtr.Zero, this);
        return _storage;
    }

    public string GetManagedString()
    {
        ObjectDisposedException.ThrowIf(_storage == IntPtr.Zero, this);
        return _length == 0
            ? string.Empty
            : Marshal.PtrToStringUni(_storage, _length) ?? string.Empty;
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

    ~VBNativeStringPointer() => Dispose();
}
