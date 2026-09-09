using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// The <c>IDispatch</c> a generated VB6 class answers on, implemented here instead of by the CLR.
///
/// The CLR's own class interface is close but not the same contract: it numbers members by its own
/// rule, it looks a type up under its CLR name, and it refuses a record value outright -- measured
/// as <c>0x80131515</c> from a foreign client calling a member that returns a VB6 <c>Type</c>. All
/// three are decisions a VB6 server has to make itself, so this surface takes them over: the DISPIDs
/// are the ones the emitter stamped, the names are the VB6 names, and a record travels as a real
/// <c>VT_RECORD</c>.
///
/// The vtable is built by hand because a managed interface cannot carry IID_IDispatch -- the CLR
/// refuses to hand such an interface out (measured: <c>E_NOINTERFACE</c>). What it does do is ask
/// <see cref="ICustomQueryInterface"/> first, and that is the hook this surface hangs on.
/// </summary>
[SupportedOSPlatform("windows")]
internal static unsafe class VBComDispatchSurface
{
    private const int SOk = 0;
    private const int EPointer = unchecked((int)0x80004003);
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int ENotImpl = unchecked((int)0x80004001);
    private const int DispEMemberNotFound = unchecked((int)0x80020003);
    private const int DispEUnknownName = unchecked((int)0x80020006);
    private const int DispEException = unchecked((int)0x80020009);

    private static readonly Guid UnknownId = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid DispatchId = new("00020400-0000-0000-C000-000000000046");

    /// <summary>
    /// The one shared vtable. Its entries are static functions, so a single table serves every
    /// object; what differs per object is the block in front of it.
    /// </summary>
    private static readonly IntPtr* VTable = CreateVTable();

    /// <summary>
    /// Per-object state behind one dispatch pointer: the vtable, a handle to the managed object and
    /// the reference count COM keeps on this pointer. The block is native memory because the client
    /// holds its address.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DispatchBlock
    {
        public IntPtr VTable;
        public IntPtr Handle;
        public int References;
    }

    /// <summary>
    /// Hands out a dispatch pointer for one object. The caller owns one reference on it, exactly as
    /// <c>QueryInterface</c> promises.
    /// </summary>
    public static IntPtr Create(object instance)
    {
        var block = (DispatchBlock*)NativeMemory.Alloc((nuint)sizeof(DispatchBlock));
        block->VTable = (IntPtr)VTable;
        block->Handle = GCHandle.ToIntPtr(GCHandle.Alloc(instance));
        block->References = 1;
        return (IntPtr)block;
    }

    private static IntPtr* CreateVTable()
    {
        var table = (IntPtr*)NativeMemory.Alloc(7, (nuint)sizeof(IntPtr));
        table[0] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr*, int>)&QueryInterface;
        table[1] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&AddRef;
        table[2] = (IntPtr)(delegate* unmanaged<IntPtr, uint>)&Release;
        table[3] = (IntPtr)(delegate* unmanaged<IntPtr, uint*, int>)&GetTypeInfoCount;
        table[4] = (IntPtr)(delegate* unmanaged<IntPtr, uint, uint, IntPtr*, int>)&GetTypeInfo;
        table[5] = (IntPtr)(delegate* unmanaged<IntPtr, Guid*, IntPtr, uint, uint, int*, int>)&GetIDsOfNames;
        table[6] = (IntPtr)(delegate* unmanaged<IntPtr, int, Guid*, uint, ushort, IntPtr, IntPtr, IntPtr, uint*, int>)&Invoke;
        return table;
    }

    private static object? TargetOf(IntPtr self) =>
        self == IntPtr.Zero ? null : GCHandle.FromIntPtr(((DispatchBlock*)self)->Handle).Target;

    [UnmanagedCallersOnly]
    private static int QueryInterface(IntPtr self, Guid* iid, IntPtr* result)
    {
        if (result is null)
        {
            return EPointer;
        }

        *result = IntPtr.Zero;
        if (iid is null || self == IntPtr.Zero)
        {
            return EPointer;
        }

        // IDispatch is this surface. Everything else -- IUnknown included, because COM identity is
        // decided there -- belongs to the CLR's own wrapper for the same object.
        if (*iid == DispatchId)
        {
            Interlocked.Increment(ref ((DispatchBlock*)self)->References);
            *result = self;
            return SOk;
        }

        if (TargetOf(self) is not { } instance)
        {
            return ENoInterface;
        }

        var unknown = Marshal.GetIUnknownForObject(instance);
        try
        {
            var identity = *iid;
            return Marshal.QueryInterface(unknown, in identity, out *result);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(IntPtr self) =>
        self == IntPtr.Zero ? 0 : (uint)Interlocked.Increment(ref ((DispatchBlock*)self)->References);

    [UnmanagedCallersOnly]
    private static uint Release(IntPtr self)
    {
        if (self == IntPtr.Zero)
        {
            return 0;
        }

        var block = (DispatchBlock*)self;
        var remaining = Interlocked.Decrement(ref block->References);
        if (remaining > 0)
        {
            return (uint)remaining;
        }

        GCHandle.FromIntPtr(block->Handle).Free();
        NativeMemory.Free(block);
        return 0;
    }

    [UnmanagedCallersOnly]
    private static int GetTypeInfoCount(IntPtr self, uint* count)
    {
        if (count is null)
        {
            return EPointer;
        }

        // Zero is the honest answer: the description lives in the written type library, not in a
        // type info this object hands out. A client that wants names asks GetIDsOfNames.
        *count = 0;
        return SOk;
    }

    [UnmanagedCallersOnly]
    private static int GetTypeInfo(IntPtr self, uint index, uint lcid, IntPtr* info)
    {
        if (info is not null)
        {
            *info = IntPtr.Zero;
        }

        return ENotImpl;
    }

    [UnmanagedCallersOnly]
    private static int GetIDsOfNames(IntPtr self, Guid* iid, IntPtr names, uint count, uint lcid, int* dispIds)
    {
        if (names == IntPtr.Zero || dispIds is null)
        {
            return EPointer;
        }

        if (TargetOf(self) is not { } instance)
        {
            return EPointer;
        }

        var members = VBComMemberTable.For(instance.GetType());
        var result = SOk;
        for (var index = 0u; index < count; index++)
        {
            var name = Marshal.PtrToStringUni(Marshal.ReadIntPtr(names, (int)(index * (uint)IntPtr.Size)));

            // Only the member name resolves. A named argument would need the member's parameter
            // list, and VB6 servers built here take positional arguments.
            if (index == 0 && name is not null && members.TryGetDispId(name, out var dispId))
            {
                dispIds[index] = dispId;
                continue;
            }

            dispIds[index] = -1;
            result = DispEUnknownName;
        }

        return result;
    }

    [UnmanagedCallersOnly]
    private static int Invoke(
        IntPtr self,
        int dispId,
        Guid* iid,
        uint lcid,
        ushort flags,
        IntPtr parameters,
        IntPtr result,
        IntPtr exception,
        uint* argumentError)
    {
        if (TargetOf(self) is not { } instance)
        {
            return EPointer;
        }

        try
        {
            var members = VBComMemberTable.For(instance.GetType());
            if (!members.TryGetMember(dispId, out var member))
            {
                return DispEMemberNotFound;
            }

            return VBComInvocation.Invoke(instance, member!, flags, parameters, result);
        }
        catch (Exception exception1) when (exception1 is not OutOfMemoryException)
        {
            VBComInvocation.WriteExceptionInfo(exception, exception1);
            return DispEException;
        }
    }
}
