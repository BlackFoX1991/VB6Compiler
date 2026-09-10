using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// The other way a control keeps its designer state: as one opaque stream instead of named
/// properties.
///
/// A container cannot choose which of the two a control uses -- the control does, and it says so by
/// which interface it implements. Measured against the eleven registered stock controls, every one
/// of them offers <c>IPersistStreamInit</c>, none offers plain <c>IPersistStream</c> (the two are
/// separate interfaces; the Init variant does not derive from the other), and every one answers
/// <c>GetSizeMax</c> with <c>E_NOTIMPL</c>. A container that sized a buffer from that answer would
/// get nothing -- so the stream grows as the control writes.
///
/// The bytes mean nothing to the container. That is the point of this contract: the control writes
/// what it wants and reads its own writing back, and the container only has to keep the block and
/// hand it over unchanged.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VBComStreamPersistence
{
    /// <summary>
    /// Asks a control for its state as a stream. Answers <see langword="null"/> when the control
    /// does not use stream persistence, which is not an error -- it keeps its state elsewhere.
    /// </summary>
    public static byte[]? TrySaveState(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control is not IVBPersistStreamInit persist)
        {
            return null;
        }

        var stream = new VBMemoryStream();
        persist.Save(stream, clearDirty: true);
        return stream.ToArray();
    }

    /// <summary>
    /// Gives a control its state back.
    ///
    /// <c>InitNew</c> comes first when there is nothing to load: VB6 calls it for a control that has
    /// no persisted state, and a control that was never initialised either way is in no defined
    /// state at all. A control that does not use stream persistence answers <see langword="false"/>.
    /// </summary>
    public static bool TryApplyState(object control, byte[]? state)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control is not IVBPersistStreamInit persist)
        {
            return false;
        }

        if (state is null || state.Length == 0)
        {
            persist.InitNew();
            return true;
        }

        persist.Load(new VBMemoryStream(state));
        return true;
    }

    /// <summary>True when the control keeps its state as a stream.</summary>
    public static bool IsStreamPersistent(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control is IVBPersistStreamInit;
    }

    /// <summary>
    /// Whether the control has unsaved changes. <c>S_OK</c> means dirty, <c>S_FALSE</c> means clean
    /// -- the inverted pair that reads wrong at a glance and is worth stating once.
    /// </summary>
    public static bool? TryGetDirty(object control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control is IVBPersistStreamInit persist ? persist.IsDirty() == 0 : null;
    }
}

/// <summary>
/// The control's side of stream persistence. It derives from <c>IPersist</c>, so
/// <c>GetClassID</c> holds the first slot -- invisible in C# and fatal to get wrong in the vtable.
/// <c>InitNew</c> is the last slot, which is the only difference to <c>IPersistStream</c>.
/// </summary>
[ComVisible(true)]
[Guid("7FD52380-4E07-101B-AE2D-08002B2EC713")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBPersistStreamInit
{
    void GetClassID(out Guid classId);

    [PreserveSig]
    int IsDirty();

    void Load(IStream stream);

    void Save(IStream stream, [MarshalAs(UnmanagedType.Bool)] bool clearDirty);

    void GetSizeMax(out long size);

    void InitNew();
}

/// <summary>
/// The stream the container hands to the control: a byte block that grows as it is written.
///
/// It is deliberately not a wrapper around <see cref="MemoryStream"/> with the COM methods bolted
/// on -- the COM contract differs in the places that matter. <c>Read</c> and <c>Write</c> report
/// their counts through a pointer that may be null, <c>Seek</c> takes an origin and answers the new
/// position the same way, and everything this container has no use for answers <c>E_NOTIMPL</c>
/// rather than pretending.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class VBMemoryStream : IStream
{
    private const int ENotImpl = unchecked((int)0x80004001);

    private byte[] _buffer;
    private int _length;
    private int _position;

    public VBMemoryStream() => _buffer = new byte[256];

    public VBMemoryStream(byte[] state)
    {
        _buffer = (byte[])state.Clone();
        _length = state.Length;
    }

    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    public void Read(byte[] pv, int cb, IntPtr pcbRead)
    {
        var read = Math.Max(0, Math.Min(cb, _length - _position));
        if (read > 0)
        {
            Array.Copy(_buffer, _position, pv, 0, read);
            _position += read;
        }

        // Ein Aufrufer, den die Zahl nicht interessiert, uebergibt null. Bedingungslos
        // hindurchzuschreiben laesst genau den mit einer Zugriffsverletzung stehen.
        if (pcbRead != IntPtr.Zero)
        {
            Marshal.WriteInt32(pcbRead, read);
        }
    }

    public void Write(byte[] pv, int cb, IntPtr pcbWritten)
    {
        if (cb > 0)
        {
            EnsureCapacity(_position + cb);
            Array.Copy(pv, 0, _buffer, _position, cb);
            _position += cb;
            _length = Math.Max(_length, _position);
        }

        if (pcbWritten != IntPtr.Zero)
        {
            Marshal.WriteInt32(pcbWritten, Math.Max(0, cb));
        }
    }

    public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
    {
        var target = dwOrigin switch
        {
            0 => dlibMove,                 // STREAM_SEEK_SET
            1 => _position + dlibMove,     // STREAM_SEEK_CUR
            2 => _length + dlibMove,       // STREAM_SEEK_END
            _ => throw new ArgumentOutOfRangeException(nameof(dwOrigin))
        };

        // Hinter das Ende zu springen ist erlaubt und macht den Strom nicht laenger -- erst ein
        // Schreiben dort tut das.
        _position = (int)Math.Max(0, Math.Min(int.MaxValue, target));
        if (plibNewPosition != IntPtr.Zero)
        {
            Marshal.WriteInt64(plibNewPosition, _position);
        }
    }

    public void SetSize(long libNewSize)
    {
        var size = (int)Math.Max(0, Math.Min(int.MaxValue, libNewSize));
        EnsureCapacity(size);
        if (size > _length)
        {
            Array.Clear(_buffer, _length, size - _length);
        }

        _length = size;
        _position = Math.Min(_position, _length);
    }

    public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
    {
        pstatstg = new System.Runtime.InteropServices.ComTypes.STATSTG
        {
            type = 2,                 // STGTY_STREAM
            cbSize = _length,
            grfMode = 0x00000002      // STGM_READWRITE
        };
    }

    public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
    {
        var count = (int)Math.Max(0, Math.Min(cb, _length - _position));
        var block = new byte[count];
        Array.Copy(_buffer, _position, block, 0, count);
        _position += count;
        pstm.Write(block, count, pcbWritten);
        if (pcbRead != IntPtr.Zero)
        {
            Marshal.WriteInt64(pcbRead, count);
        }
    }

    // Transaktionen, Sperren und Klone braucht die Designer-Persistenz nicht. Ein E_NOTIMPL sagt
    // das; ein stillschweigendes Nichtstun sähe für den Aufrufer aus wie Erfolg.
    public void Clone(out IStream ppstm) => throw new COMException(nameof(Clone), ENotImpl);

    public void Commit(int grfCommitFlags) => throw new COMException(nameof(Commit), ENotImpl);

    public void LockRegion(long libOffset, long cb, int dwLockType) =>
        throw new COMException(nameof(LockRegion), ENotImpl);

    public void Revert() => throw new COMException(nameof(Revert), ENotImpl);

    public void UnlockRegion(long libOffset, long cb, int dwLockType) =>
        throw new COMException(nameof(UnlockRegion), ENotImpl);

    private void EnsureCapacity(int required)
    {
        if (required <= _buffer.Length)
        {
            return;
        }

        // Verdoppeln statt genau zuschneiden: GetSizeMax antwortet bei jedem gemessenen Control
        // E_NOTIMPL, der Container weiss also vorher nicht, wie viel kommt.
        var capacity = Math.Max(_buffer.Length * 2, required);
        Array.Resize(ref _buffer, capacity);
    }
}
