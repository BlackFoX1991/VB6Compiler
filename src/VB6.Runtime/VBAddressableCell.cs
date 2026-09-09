using System.Reflection;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>
/// Identifies native cells that are owned by generated addressable storage rather than by a
/// user-visible CLR object.  Object teardown uses this narrow marker so it never mistakes an
/// arbitrary <see cref="IDisposable"/> field for a VB6 resource it may close.
/// </summary>
internal interface IVBAddressableStorageCell : IDisposable;

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
public sealed class VBAddressableCell<T> : IVBAddressableStorageCell
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
/// Non-generic entry points used by emitted code for scalar addressable-storage slices.
/// </summary>
public static class VBAddressableStorage
{
    /// <summary>
    /// A VB6 String slot is two things, and both have their own intrinsic. The variable holds a
    /// pointer to a BSTR: <c>StrPtr</c> answers the BSTR, <c>VarPtr</c> answers the address of
    /// the variable. The documented relationship between them is exact -- <c>StrPtr(s)</c> is the
    /// Long stored at <c>VarPtr(s)</c> -- so this cell owns both, and the descriptor slot is the
    /// authority for which BSTR is current. A native write that swaps the pointer is therefore
    /// visible on the next VB6 read, exactly like a native write into the characters is.
    /// </summary>
    private sealed class BStrCell : IVBAddressableStorageCell
    {
        private IntPtr _descriptor;
        private bool _disposed;

        public BStrCell(string? value)
        {
            _descriptor = Marshal.AllocCoTaskMem(IntPtr.Size);
            Marshal.WriteIntPtr(_descriptor, Marshal.StringToBSTR(value ?? string.Empty));
        }

        public IntPtr GetNativeAddress()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return Marshal.ReadIntPtr(_descriptor);
        }

        public IntPtr GetDescriptorAddress()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _descriptor;
        }

        public string Read()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var storage = Marshal.ReadIntPtr(_descriptor);
            return storage == IntPtr.Zero ? string.Empty : Marshal.PtrToStringBSTR(storage);
        }

        public void Write(string? value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var storage = Marshal.ReadIntPtr(_descriptor);
            if (storage != IntPtr.Zero)
            {
                Marshal.FreeBSTR(storage);
            }

            Marshal.WriteIntPtr(_descriptor, Marshal.StringToBSTR(value ?? string.Empty));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            var storage = Marshal.ReadIntPtr(_descriptor);
            if (storage != IntPtr.Zero)
            {
                Marshal.FreeBSTR(storage);
            }

            Marshal.FreeCoTaskMem(_descriptor);
            _descriptor = IntPtr.Zero;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~BStrCell() => Dispose();
    }
    /// <summary>
    /// One native block for a whole VB6 user-defined type.
    ///
    /// The size and the member offsets come from the interop marshaller, which is the same
    /// source <c>LenB</c> already answers from and the same one a <c>Declare</c> sees. Anything
    /// else would let <c>VarPtr(record) + n</c> and <c>VarPtr(record.member)</c> disagree.
    ///
    /// The block is zeroed before the first store: <see cref="Marshal.StructureToPtr"/> writes
    /// the fields but not the padding between them, and a record copied whole would otherwise
    /// carry whatever the allocator left there. VB6 hands out a zeroed record.
    ///
    /// This one is deliberately not covered by a test. The suite cannot make the allocator return
    /// a dirty block -- it was tried, and removing the loop left the assertion green, which is
    /// worse than no assertion at all. The guarantee is kept because relying on the allocator
    /// would be relying on nothing.
    /// </summary>
    private sealed class RecordCell : IVBAddressableStorageCell
    {
        private readonly Type _type;
        private readonly int _size;
        private IntPtr _storage;

        public RecordCell(object value)
        {
            _type = value.GetType();
            _size = Marshal.SizeOf(value);
            _storage = Marshal.AllocCoTaskMem(_size);
            for (var offset = 0; offset < _size; offset++)
            {
                Marshal.WriteByte(_storage, offset, 0);
            }

            Marshal.StructureToPtr(value, _storage, fDeleteOld: false);
        }

        public IntPtr GetNativeAddress()
        {
            ThrowIfDisposed();
            return _storage;
        }

        public IntPtr GetMemberAddress(string path)
        {
            ThrowIfDisposed();
            var type = _type;
            var offset = 0L;
            foreach (var name in path.Split('.'))
            {
                offset += Marshal.OffsetOf(type, name).ToInt64();

                // NonPublic gehoert dazu: Ein VB6-UDT-Member wird als FieldAttributes.Assembly
                // emittiert, und die Standardsuche von GetField findet nur oeffentliche Felder.
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) ??
                    throw new ArgumentException($"The record has no member '{name}'.", nameof(path));
                type = field.FieldType;
            }

            return new IntPtr(_storage.ToInt64() + offset);
        }

        public object Read()
        {
            ThrowIfDisposed();
            return Marshal.PtrToStructure(_storage, _type)!;
        }

        public void Write(object value)
        {
            ThrowIfDisposed();
            if (value.GetType() != _type)
            {
                throw new ArgumentException(
                    $"The addressable storage cell holds a '{_type.Name}'.",
                    nameof(value));
            }

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

        ~RecordCell() => Dispose();

        private void ThrowIfDisposed() =>
            ObjectDisposedException.ThrowIf(_storage == IntPtr.Zero, this);
    }

    public static object CreateString(string value) => new BStrCell(value);
    public static object CreateRecord(object value) => new RecordCell(value);

    public static object CreateBoolean(bool value) => VBAddressableCell<short>.Create(value ? (short)-1 : (short)0);

    public static object CreateByte(byte value) => VBAddressableCell<byte>.Create(value);

    public static object CreateInt32(int value) => VBAddressableCell<int>.Create(value);

    public static object CreateUInt32(uint value) => VBAddressableCell<uint>.Create(value);

    public static object CreateInt16(short value) => VBAddressableCell<short>.Create(value);

    public static object CreateUShort(ushort value) => VBAddressableCell<ushort>.Create(value);

    public static object CreateSingle(float value) => VBAddressableCell<float>.Create(value);

    public static object CreateDouble(double value) => VBAddressableCell<double>.Create(value);

    public static object CreateDate(double value) => VBAddressableCell<double>.Create(value);

    public static object CreateCurrency(VBCurrency value) => VBAddressableCell<long>.Create(value.ScaledValue);

    public static object CreateInt64(long value) => VBAddressableCell<long>.Create(value);

    public static object CreateUInt64(ulong value) => VBAddressableCell<ulong>.Create(value);

    public static object CreateIntPtr32(IntPtr value) => VBAddressableCell<int>.Create(checked((int)value.ToInt64()));

    public static IntPtr GetBooleanNativeAddress(object storage) => GetBoolean(storage).GetNativeAddress();

    public static IntPtr GetStringNativeAddress(object storage) => GetString(storage).GetNativeAddress();

    /// <summary>
    /// The address of the String variable itself, which is what <c>VarPtr</c> answers. Reading a
    /// Long there gives the same value as <c>StrPtr</c>.
    /// </summary>
    public static IntPtr GetStringDescriptorNativeAddress(object storage) =>
        GetString(storage).GetDescriptorAddress();
    public static IntPtr GetRecordNativeAddress(object storage) => GetRecord(storage).GetNativeAddress();

    /// <summary>
    /// The address of one member, named by a dotted path from the record root. The offsets come
    /// from the marshaller so a nested member lands where a Declare would find it.
    /// </summary>
    public static IntPtr GetRecordMemberNativeAddress(object storage, string path) =>
        GetRecord(storage).GetMemberAddress(path);

    public static IntPtr GetByteNativeAddress(object storage) => GetByte(storage).GetNativeAddress();

    public static IntPtr GetInt32NativeAddress(object storage) => GetInt32(storage).GetNativeAddress();

    public static IntPtr GetUInt32NativeAddress(object storage) => GetUInt32(storage).GetNativeAddress();

    public static IntPtr GetInt16NativeAddress(object storage) => GetInt16(storage).GetNativeAddress();

    public static IntPtr GetUShortNativeAddress(object storage) => GetUShort(storage).GetNativeAddress();

    public static IntPtr GetSingleNativeAddress(object storage) => GetSingle(storage).GetNativeAddress();

    public static IntPtr GetDoubleNativeAddress(object storage) => GetDouble(storage).GetNativeAddress();

    public static IntPtr GetDateNativeAddress(object storage) => GetDate(storage).GetNativeAddress();

    public static IntPtr GetCurrencyNativeAddress(object storage) => GetCurrency(storage).GetNativeAddress();

    public static IntPtr GetInt64NativeAddress(object storage) => GetInt64(storage).GetNativeAddress();

    public static IntPtr GetUInt64NativeAddress(object storage) => GetUInt64(storage).GetNativeAddress();

    public static IntPtr GetIntPtr32NativeAddress(object storage) => GetIntPtr32(storage).GetNativeAddress();

    public static bool ReadBoolean(object storage) => GetBoolean(storage).Read() != 0;

    public static string ReadString(object storage) => GetString(storage).Read();

    /// <summary>
    /// Reads a VB6 String through the address of its BSTR descriptor.  A controlled ByRef alias
    /// passes this address as a CLR <c>string&amp;</c> solely so the callee can keep the caller's
    /// VarPtr identity; CLR must never dereference that native descriptor as an object reference.
    /// </summary>
    public static string ReadStringDescriptor(IntPtr descriptor)
    {
        if (descriptor == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        var storage = Marshal.ReadIntPtr(descriptor);
        return storage == IntPtr.Zero ? string.Empty : Marshal.PtrToStringBSTR(storage);
    }

    /// <summary>
    /// Replaces the BSTR named by a native descriptor address.  Ownership stays with the
    /// addressable cell, exactly like <see cref="WriteString"/> on the cell object itself.
    /// </summary>
    public static void WriteStringDescriptor(IntPtr descriptor, string? value)
    {
        if (descriptor == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        var storage = Marshal.ReadIntPtr(descriptor);
        if (storage != IntPtr.Zero)
        {
            Marshal.FreeBSTR(storage);
        }

        Marshal.WriteIntPtr(descriptor, Marshal.StringToBSTR(value ?? string.Empty));
    }

    public static object ReadRecord(object storage) => GetRecord(storage).Read();

    public static byte ReadByte(object storage) => GetByte(storage).Read();

    public static int ReadInt32(object storage) => GetInt32(storage).Read();

    public static uint ReadUInt32(object storage) => GetUInt32(storage).Read();

    public static short ReadInt16(object storage) => GetInt16(storage).Read();

    public static ushort ReadUShort(object storage) => GetUShort(storage).Read();

    public static float ReadSingle(object storage) => GetSingle(storage).Read();

    public static double ReadDouble(object storage) => GetDouble(storage).Read();

    public static double ReadDate(object storage) => GetDate(storage).Read();

    public static VBCurrency ReadCurrency(object storage) => VBCurrency.FromScaled(GetCurrency(storage).Read());

    public static long ReadInt64(object storage) => GetInt64(storage).Read();

    public static ulong ReadUInt64(object storage) => GetUInt64(storage).Read();

    public static IntPtr ReadIntPtr32(object storage) => new(GetIntPtr32(storage).Read());

    public static void WriteBoolean(object storage, bool value) => GetBoolean(storage).Write(value ? (short)-1 : (short)0);

    public static void WriteString(object storage, string value) => GetString(storage).Write(value);
    public static void WriteRecord(object storage, object value) => GetRecord(storage).Write(value);

    public static void WriteByte(object storage, byte value) => GetByte(storage).Write(value);

    public static void WriteInt32(object storage, int value) => GetInt32(storage).Write(value);

    public static void WriteUInt32(object storage, uint value) => GetUInt32(storage).Write(value);

    public static void WriteInt16(object storage, short value) => GetInt16(storage).Write(value);

    public static void WriteUShort(object storage, ushort value) => GetUShort(storage).Write(value);

    public static void WriteSingle(object storage, float value) => GetSingle(storage).Write(value);

    public static void WriteDouble(object storage, double value) => GetDouble(storage).Write(value);

    public static void WriteDate(object storage, double value) => GetDate(storage).Write(value);

    public static void WriteCurrency(object storage, VBCurrency value) => GetCurrency(storage).Write(value.ScaledValue);

    public static void WriteInt64(object storage, long value) => GetInt64(storage).Write(value);

    public static void WriteUInt64(object storage, ulong value) => GetUInt64(storage).Write(value);

    public static void WriteIntPtr32(object storage, IntPtr value) => GetIntPtr32(storage).Write(checked((int)value.ToInt64()));

    // Eine Modulvariable lebt so lange wie das Programm, ihre Zelle entsteht aber erst beim ersten
    // VarPtr. Ein Modulinitialisierer müsste dafür über Modulgrenzen hinweg geordnet werden -- der
    // Lowerer baut die Module nacheinander, und eine später entdeckte Zelle käme im früheren Modul
    // zu spät. Deshalb sind die Zugriffspfade null-tolerant: Solange keine Adresse angefordert
    // wurde, ist das gewöhnliche statische Feld allein maßgeblich.

    public static object EnsureBoolean(object? storage, bool value) => storage ?? CreateBoolean(value);

    public static object EnsureString(object? storage, string value) => storage ?? CreateString(value);
    public static object EnsureRecord(object? storage, object value) => storage ?? CreateRecord(value);

    public static object EnsureByte(object? storage, byte value) => storage ?? CreateByte(value);

    public static object EnsureInt32(object? storage, int value) => storage ?? CreateInt32(value);

    public static object EnsureUInt32(object? storage, uint value) => storage ?? CreateUInt32(value);

    public static object EnsureInt16(object? storage, short value) => storage ?? CreateInt16(value);

    public static object EnsureUShort(object? storage, ushort value) => storage ?? CreateUShort(value);

    public static object EnsureSingle(object? storage, float value) => storage ?? CreateSingle(value);

    public static object EnsureDouble(object? storage, double value) => storage ?? CreateDouble(value);

    public static object EnsureDate(object? storage, double value) => storage ?? CreateDate(value);

    public static object EnsureCurrency(object? storage, VBCurrency value) => storage ?? CreateCurrency(value);

    public static object EnsureInt64(object? storage, long value) => storage ?? CreateInt64(value);

    public static object EnsureUInt64(object? storage, ulong value) => storage ?? CreateUInt64(value);

    public static object EnsureIntPtr32(object? storage, IntPtr value) => storage ?? CreateIntPtr32(value);

    public static bool ReadBooleanOr(object? storage, bool current) => storage is null ? current : ReadBoolean(storage);

    public static string ReadStringOr(object? storage, string current) => storage is null ? current : ReadString(storage);
    public static object ReadRecordOr(object? storage, object current) => storage is null ? current : ReadRecord(storage);

    public static byte ReadByteOr(object? storage, byte current) => storage is null ? current : ReadByte(storage);

    public static int ReadInt32Or(object? storage, int current) => storage is null ? current : ReadInt32(storage);

    public static uint ReadUInt32Or(object? storage, uint current) => storage is null ? current : ReadUInt32(storage);

    public static short ReadInt16Or(object? storage, short current) => storage is null ? current : ReadInt16(storage);

    public static ushort ReadUShortOr(object? storage, ushort current) => storage is null ? current : ReadUShort(storage);

    public static float ReadSingleOr(object? storage, float current) => storage is null ? current : ReadSingle(storage);

    public static double ReadDoubleOr(object? storage, double current) => storage is null ? current : ReadDouble(storage);

    public static double ReadDateOr(object? storage, double current) => storage is null ? current : ReadDate(storage);

    public static VBCurrency ReadCurrencyOr(object? storage, VBCurrency current) => storage is null ? current : ReadCurrency(storage);

    public static long ReadInt64Or(object? storage, long current) => storage is null ? current : ReadInt64(storage);

    public static ulong ReadUInt64Or(object? storage, ulong current) => storage is null ? current : ReadUInt64(storage);

    public static IntPtr ReadIntPtr32Or(object? storage, IntPtr current) => storage is null ? current : ReadIntPtr32(storage);

    public static void WriteBooleanIfPresent(object? storage, bool value)
    {
        if (storage is not null)
        {
            WriteBoolean(storage, value);
        }
    }

    public static void WriteStringIfPresent(object? storage, string value)
    {
        if (storage is not null)
        {
            WriteString(storage, value);
        }
    }

    public static void WriteRecordIfPresent(object? storage, object value)
    {
        if (storage is not null)
        {
            WriteRecord(storage, value);
        }
    }

    public static void WriteByteIfPresent(object? storage, byte value)
    {
        if (storage is not null)
        {
            WriteByte(storage, value);
        }
    }

    public static void WriteInt32IfPresent(object? storage, int value)
    {
        if (storage is not null)
        {
            WriteInt32(storage, value);
        }
    }

    public static void WriteUInt32IfPresent(object? storage, uint value)
    {
        if (storage is not null)
        {
            WriteUInt32(storage, value);
        }
    }

    public static void WriteInt16IfPresent(object? storage, short value)
    {
        if (storage is not null)
        {
            WriteInt16(storage, value);
        }
    }

    public static void WriteUShortIfPresent(object? storage, ushort value)
    {
        if (storage is not null)
        {
            WriteUShort(storage, value);
        }
    }

    public static void WriteSingleIfPresent(object? storage, float value)
    {
        if (storage is not null)
        {
            WriteSingle(storage, value);
        }
    }

    public static void WriteDoubleIfPresent(object? storage, double value)
    {
        if (storage is not null)
        {
            WriteDouble(storage, value);
        }
    }

    public static void WriteDateIfPresent(object? storage, double value)
    {
        if (storage is not null)
        {
            WriteDate(storage, value);
        }
    }

    public static void WriteCurrencyIfPresent(object? storage, VBCurrency value)
    {
        if (storage is not null)
        {
            WriteCurrency(storage, value);
        }
    }

    public static void WriteInt64IfPresent(object? storage, long value)
    {
        if (storage is not null)
        {
            WriteInt64(storage, value);
        }
    }

    public static void WriteUInt64IfPresent(object? storage, ulong value)
    {
        if (storage is not null)
        {
            WriteUInt64(storage, value);
        }
    }

    public static void WriteIntPtr32IfPresent(object? storage, IntPtr value)
    {
        if (storage is not null)
        {
            WriteIntPtr32(storage, value);
        }
    }

    public static void Dispose(object storage)
    {
        if (storage is not IDisposable disposable)
        {
            throw new ArgumentException("The addressable storage cell must be disposable.", nameof(storage));
        }

        disposable.Dispose();
    }

    private static VBAddressableCell<short> GetBoolean(object storage) => storage as VBAddressableCell<short>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Boolean.", nameof(storage));

    private static BStrCell GetString(object storage) => storage as BStrCell
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 String BSTR.", nameof(storage));

    private static RecordCell GetRecord(object storage) => storage as RecordCell
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 record.", nameof(storage));

    private static VBAddressableCell<byte> GetByte(object storage) => storage as VBAddressableCell<byte>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Byte.", nameof(storage));

    private static VBAddressableCell<int> GetInt32(object storage) => storage as VBAddressableCell<int>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Long.", nameof(storage));

    private static VBAddressableCell<uint> GetUInt32(object storage) => storage as VBAddressableCell<uint>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 UInteger.", nameof(storage));

    private static VBAddressableCell<short> GetInt16(object storage) => storage as VBAddressableCell<short>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Integer.", nameof(storage));

    private static VBAddressableCell<ushort> GetUShort(object storage) => storage as VBAddressableCell<ushort>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 UShort.", nameof(storage));

    private static VBAddressableCell<float> GetSingle(object storage) => storage as VBAddressableCell<float>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Single.", nameof(storage));

    private static VBAddressableCell<double> GetDouble(object storage) => storage as VBAddressableCell<double>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Double.", nameof(storage));

    private static VBAddressableCell<double> GetDate(object storage) => storage as VBAddressableCell<double>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Date.", nameof(storage));

    private static VBAddressableCell<long> GetCurrency(object storage) => storage as VBAddressableCell<long>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 Currency.", nameof(storage));

    private static VBAddressableCell<long> GetInt64(object storage) => storage as VBAddressableCell<long>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 LongLong.", nameof(storage));

    private static VBAddressableCell<ulong> GetUInt64(object storage) => storage as VBAddressableCell<ulong>
        ?? throw new ArgumentException("The addressable storage cell must hold a VB6 ULong.", nameof(storage));

    private static VBAddressableCell<int> GetIntPtr32(object storage) => storage as VBAddressableCell<int>
        ?? throw new ArgumentException("The addressable storage cell must hold an x86 VB6 LongPtr.", nameof(storage));
}
