using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>The width of a native VARIANT. On x64 it is 24, not 16: the union carries BRECORD.</summary>
internal static class VBComVariant
{
    public static readonly int Size = IntPtr.Size == 8 ? 24 : 16;

    /// <summary>Offset der Union: hinter vt und drei reservierten WORDs, auf Zeigerbreite gerichtet.</summary>
    public static readonly int DataOffset = 8;
}

/// <summary>
/// A VB6 <c>Type</c> value on its way to a COM client.
///
/// A record travels as <c>VT_RECORD</c>, and such a variant carries two things: the data and an
/// <c>IRecordInfo</c> that describes it. The client uses that interface to read fields and to free
/// the copy afterwards. The CLR builds one only from a **registered** type library, which is why a
/// record return failed outright before -- measured as <c>0x80131515</c>, and unchanged even with
/// the library registered, because the CLR then looked its types up under the CLR names.
///
/// So the server brings its own. Nothing has to be registered for a record to cross the boundary,
/// and the description a client sees is the VB6 one.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class VBComRecordInfo
{
    private const int SOk = 0;
    private const int EPointer = unchecked((int)0x80004003);
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int ENotImpl = unchecked((int)0x80004001);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const short VtRecord = 36;

    private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid RecordInfoId = new("0000002F-0000-0000-C000-000000000046");

    private static readonly ConcurrentDictionary<Type, IntPtr> Descriptions = new();
    private static readonly IntPtr* VTable = CreateVTable();

    /// <summary>
    /// The state behind one IRecordInfo pointer. One instance serves every value of its type, so it
    /// is created once and never released -- a record description outlives any single value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct RecordBlock
    {
        public IntPtr VTable;
        public IntPtr Handle;
        public int References;
    }

    /// <summary>
    /// Writes a record value into the caller's VARIANT, or answers false when the value is not a
    /// published record and the ordinary variant path applies.
    /// </summary>
    public static bool TryWriteRecord(IntPtr result, object? value, Type declared)
    {
        var type = value?.GetType() ?? declared;
        if (value is null || !IsPublishedRecord(type))
        {
            return false;
        }

        // Der Client bekommt eine eigene Kopie und gibt sie über RecordDestroy wieder frei -- die
        // Lebensdauer des Serverwertes hängt danach an nichts mehr.
        var size = Marshal.SizeOf(type);
        var data = Marshal.AllocCoTaskMem(size);
        NativeMemory.Clear((void*)data, (nuint)size);
        Marshal.StructureToPtr(value, data, false);

        var info = For(type);
        AddRefBlock(info);
        Marshal.WriteInt16(result, VtRecord);
        Marshal.WriteInt16(result, 2, 0);
        Marshal.WriteInt16(result, 4, 0);
        Marshal.WriteInt16(result, 6, 0);
        Marshal.WriteIntPtr(result, VBComVariant.DataOffset, data);
        Marshal.WriteIntPtr(result, VBComVariant.DataOffset + IntPtr.Size, info);
        return true;
    }

    /// <summary>
    /// Reads a record the client passed in, or <c>null</c> when this argument is not one.
    ///
    /// The value is copied out of the client's memory: what the server keeps must survive the call,
    /// and the client frees its own copy when it likes. ByRef arrives as VT_BYREF|VT_RECORD with a
    /// pointer to the data pointer.
    /// </summary>
    public static object? TryReadRecord(IntPtr variant, Type expected)
    {
        if (variant == IntPtr.Zero || !IsPublishedRecord(expected))
        {
            return null;
        }

        var type = Marshal.ReadInt16(variant);
        if ((type & VariantTypeMask) != VtRecord)
        {
            return null;
        }

        var data = Marshal.ReadIntPtr(variant, VBComVariant.DataOffset);
        if ((type & VariantByRef) != 0 && data != IntPtr.Zero)
        {
            data = Marshal.ReadIntPtr(data);
        }

        return data == IntPtr.Zero ? null : Marshal.PtrToStructure(data, expected);
    }

    private const short VariantByRef = 0x4000;
    private const short VariantTypeMask = 0x0FFF;

    /// <summary>
    /// True for a type the emitter published as a COM record: a ComVisible value type with an
    /// identity of its own. A Private Type carries neither and stays out of COM entirely.
    /// </summary>
    public static bool IsPublishedRecord(Type type) =>
        type.IsValueType &&
        !type.IsEnum &&
        !type.IsPrimitive &&
        type.GetCustomAttributes(typeof(ComVisibleAttribute), false) is [ComVisibleAttribute { Value: true }] &&
        type.GetCustomAttributes(typeof(GuidAttribute), false).Length == 1;

    private static IntPtr For(Type type) => Descriptions.GetOrAdd(type, static key =>
    {
        var block = (RecordBlock*)NativeMemory.Alloc((nuint)sizeof(RecordBlock));
        block->VTable = (IntPtr)VTable;
        block->Handle = GCHandle.ToIntPtr(GCHandle.Alloc(key));
        block->References = 1;
        return (IntPtr)block;
    });

    private static void AddRefBlock(IntPtr self) =>
        Interlocked.Increment(ref ((RecordBlock*)self)->References);

    private static Type? TypeOf(IntPtr self) =>
        self == IntPtr.Zero ? null : GCHandle.FromIntPtr(((RecordBlock*)self)->Handle).Target as Type;

    private static IntPtr* CreateVTable()
    {
        // IUnknown, then IRecordInfo in its declared order. Every slot is filled: a client is
        // allowed to call any of them, and an empty slot is a crash rather than an error code.
        var table = (IntPtr*)NativeMemory.Alloc(19, (nuint)sizeof(IntPtr));
        table[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&QueryInterface;
        table[1] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&AddRef;
        table[2] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&Release;
        table[3] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&RecordInit;
        table[4] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&RecordClear;
        table[5] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, int>)&RecordCopy;
        table[6] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, int>)&GetGuid;
        table[7] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr*, int>)&GetName;
        table[8] = (IntPtr)(delegate* unmanaged<IntPtr, uint*, int>)&GetSize;
        table[9] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr*, int>)&GetTypeInfo;
        table[10] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, int>)&GetField;
        table[11] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, IntPtr, IntPtr*, int>)&GetFieldNoCopy;
        table[12] = (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr, int>)&PutField;
        table[13] = (IntPtr)(delegate* unmanaged<IntPtr, uint, IntPtr, IntPtr, IntPtr, int>)&PutFieldNoCopy;
        table[14] = (IntPtr)(delegate* unmanaged<IntPtr, uint*, IntPtr, int>)&GetFieldNames;
        table[15] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&IsMatchingType;
        table[16] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr>)&RecordCreate;
        table[17] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr*, int>)&RecordCreateCopy;
        table[18] = (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&RecordDestroy;
        return table;
    }

    [UnmanagedCallersOnly]
    private static int QueryInterface(IntPtr self, Guid* iid, IntPtr* result)
    {
        if (result is null || iid is null)
        {
            return EPointer;
        }

        if (*iid == UnknownId || *iid == RecordInfoId)
        {
            AddRefBlock(self);
            *result = self;
            return SOk;
        }

        *result = IntPtr.Zero;
        return ENoInterface;
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(IntPtr self) =>
        (uint)Interlocked.Increment(ref ((RecordBlock*)self)->References);

    [UnmanagedCallersOnly]
    private static uint Release(IntPtr self)
    {
        // Die Beschreibung ist typgebunden und wird bewusst nie freigegeben: Sie gilt für jeden
        // Wert dieses Typs, und ein zweiter Wert bekäme sonst eine tote Schnittstelle.
        var remaining = Interlocked.Decrement(ref ((RecordBlock*)self)->References);
        return (uint)Math.Max(0, remaining);
    }

    [UnmanagedCallersOnly]
    private static int RecordInit(IntPtr self, IntPtr record)
    {
        if (record == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        NativeMemory.Clear((void*)record, (nuint)Marshal.SizeOf(type));
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int RecordClear(IntPtr self, IntPtr record)
    {
        if (record == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        Marshal.DestroyStructure(record, type);
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int RecordCopy(IntPtr self, IntPtr source, IntPtr destination)
    {
        if (source == IntPtr.Zero || destination == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        var value = Marshal.PtrToStructure(source, type);
        Marshal.StructureToPtr(value!, destination, false);
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetGuid(IntPtr self, Guid* guid)
    {
        if (guid is null || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        *guid = type.GUID;
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetName(IntPtr self, IntPtr* name)
    {
        if (name is null || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        *name = Marshal.StringToBSTR(VBComNames.OfRecord(type));
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetSize(IntPtr self, uint* size)
    {
        if (size is null || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        *size = (uint)Marshal.SizeOf(type);
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetTypeInfo(IntPtr self, IntPtr* info)
    {
        if (info is not null)
        {
            *info = IntPtr.Zero;
        }

        // Die Beschreibung steht in der geschriebenen Typbibliothek; dieses Objekt gibt keine
        // ITypeInfo heraus. Ein Client, der Felder lesen will, benutzt GetField.
        return ENotImpl;
    }

    [UnmanagedCallersOnly]
    private static int GetField(IntPtr self, IntPtr record, IntPtr name, IntPtr value) =>
        GetFieldCore(self, record, name, value);

    private static int GetFieldCore(IntPtr self, IntPtr record, IntPtr name, IntPtr value)
    {
        if (record == IntPtr.Zero || value == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        var fieldName = Marshal.PtrToStringUni(name);
        if (fieldName is null || type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not { } field)
        {
            return EInvalidArg;
        }

        var offset = (int)Marshal.OffsetOf(type, field.Name);
        var boxed = Marshal.PtrToStructure(IntPtr.Add(record, offset), field.FieldType);
        Marshal.GetNativeVariantForObject(boxed, value);
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetFieldNoCopy(IntPtr self, IntPtr record, IntPtr name, IntPtr value, IntPtr* raw)
    {
        if (raw is not null)
        {
            *raw = IntPtr.Zero;
        }

        return GetFieldCore(self, record, name, value);
    }

    [UnmanagedCallersOnly]
    private static int PutField(IntPtr self, uint flags, IntPtr record, IntPtr name, IntPtr value) =>
        PutFieldCore(self, record, name, value);

    private static int PutFieldCore(IntPtr self, IntPtr record, IntPtr name, IntPtr value)
    {
        if (record == IntPtr.Zero || value == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        var fieldName = Marshal.PtrToStringUni(name);
        if (fieldName is null || type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance) is not { } field)
        {
            return EInvalidArg;
        }

        var converted = VBDynamicDispatch.ConvertArgument(Marshal.GetObjectForNativeVariant(value), field.FieldType);
        var offset = (int)Marshal.OffsetOf(type, field.Name);
        Marshal.StructureToPtr(converted!, IntPtr.Add(record, offset), false);
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int PutFieldNoCopy(IntPtr self, uint flags, IntPtr record, IntPtr name, IntPtr value) =>
        PutFieldCore(self, record, name, value);

    [UnmanagedCallersOnly]
    private static int GetFieldNames(IntPtr self, uint* count, IntPtr names)
    {
        if (count is null || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (names == IntPtr.Zero)
        {
            *count = (uint)fields.Length;
            return SOk;
        }

        var written = Math.Min((int)*count, fields.Length);
        for (var index = 0; index < written; index++)
        {
            Marshal.WriteIntPtr(names, index * IntPtr.Size, Marshal.StringToBSTR(fields[index].Name));
        }

        *count = (uint)written;
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int IsMatchingType(IntPtr self, IntPtr other)
    {
        // VARIANT_TRUE/FALSE as an int: the interface declares BOOL, which is 32 bit here.
        return self == other ? 1 : 0;
    }

    [UnmanagedCallersOnly]
    private static IntPtr RecordCreate(IntPtr self)
    {
        if (TypeOf(self) is not { } type)
        {
            return IntPtr.Zero;
        }

        var size = Marshal.SizeOf(type);
        var record = Marshal.AllocCoTaskMem(size);
        NativeMemory.Clear((void*)record, (nuint)size);
        return record;
    }

    [UnmanagedCallersOnly]
    private static int RecordCreateCopy(IntPtr self, IntPtr source, IntPtr* destination)
    {
        if (destination is null || source == IntPtr.Zero || TypeOf(self) is not { } type)
        {
            return EInvalidArg;
        }

        var size = Marshal.SizeOf(type);
        var record = Marshal.AllocCoTaskMem(size);
        NativeMemory.Clear((void*)record, (nuint)size);
        var value = Marshal.PtrToStructure(source, type);
        Marshal.StructureToPtr(value!, record, false);
        *destination = record;
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int RecordDestroy(IntPtr self, IntPtr record)
    {
        if (record == IntPtr.Zero)
        {
            return EInvalidArg;
        }

        if (TypeOf(self) is { } type)
        {
            Marshal.DestroyStructure(record, type);
        }

        Marshal.FreeCoTaskMem(record);
        return SOk;
    }
}

/// <summary>
/// The VB6 name of an emitted type. The emitter prefixes its own types so that two VB6 names can
/// never collide in one assembly; a COM client must see the VB6 name, not that prefix.
/// </summary>
internal static class VBComNames
{
    public static string OfRecord(Type type) =>
        type.Name.StartsWith("__vb6_udt_", StringComparison.Ordinal)
            ? type.Name["__vb6_udt_".Length..]
            : type.Name;
}
