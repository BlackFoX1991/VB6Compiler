using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime.Tests;

/// <summary>
/// A COM identity built by hand so that its native reference count can be read exactly.
///
/// Every lifetime proof up to here has been indirect: an <see cref="InvalidComObjectException"/>
/// thrown by a released RCW, the exit of an out-of-process server, a member that still answers.
/// Each of those says "something released it" without saying how often, and none of them would
/// notice a reference that is dropped twice or one too few as long as the end state looks right.
///
/// A registered component cannot close that gap either -- its counter belongs to somebody else.
/// So this fixture is the counter: three slots of IUnknown behind a vtable this test owns, with
/// AddRef and Release incrementing numbers the assertions can read. It deliberately answers
/// E_NOINTERFACE for everything but IID_IUnknown; the runtime under test only ever needs the
/// identity, and every extra interface would be another contract nobody measured.
///
/// The delegates are held in fields on purpose: a function pointer handed to native code does not
/// keep its delegate alive, and a collected one turns into a call into freed memory.
///
/// The identity is never freed, and that is deliberate. The CLR caches one RCW per IUnknown
/// address, so a freed block whose address the allocator hands out again can resurrect the
/// previous identity's wrapper -- with its previous lifetime state attached. It was measured:
/// a test that kept a wrapper alive past its identity made the next test read counts that
/// belonged to the one before it, and the numbers looked plausible enough to be believed. Holding
/// every block for the life of the process costs a few dozen bytes per case and removes the whole
/// class of result.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class CountingComIdentity
{
    // Keeps blocks and delegates reachable, and keeps every address unique for the process.
    private static readonly List<CountingComIdentity> Issued = [];
    private const int SOk = 0;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EPointer = unchecked((int)0x80004003);

    private static readonly Guid IidUnknown = new("00000000-0000-0000-C000-000000000046");

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceFn(IntPtr self, ref Guid iid, out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint AddRefFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(IntPtr self);

    private readonly QueryInterfaceFn _queryInterface;
    private readonly AddRefFn _addRef;
    private readonly ReleaseFn _release;
    private readonly IntPtr _vtable;
    private readonly IntPtr _instance;

    private int _references;
    private int _addRefCalls;
    private int _releaseCalls;
    private int _rejectedQueries;

    public CountingComIdentity()
    {
        _queryInterface = OnQueryInterface;
        _addRef = OnAddRef;
        _release = OnRelease;

        _vtable = Marshal.AllocHGlobal(IntPtr.Size * 3);
        Marshal.WriteIntPtr(_vtable, 0 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_queryInterface));
        Marshal.WriteIntPtr(_vtable, 1 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_addRef));
        Marshal.WriteIntPtr(_vtable, 2 * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(_release));

        // An IUnknown is a pointer to a pointer to the vtable. One slot is the whole object:
        // the state this fixture cares about lives on the managed side.
        _instance = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(_instance, _vtable);

        lock (Issued)
        {
            Issued.Add(this);
        }
    }

    /// <summary>The raw IUnknown pointer, at a native reference count of zero.</summary>
    public IntPtr Unknown => _instance;

    /// <summary>The current native reference count: AddRef calls minus Release calls.</summary>
    public int References => Volatile.Read(ref _references);

    public int AddRefCalls => Volatile.Read(ref _addRefCalls);

    public int ReleaseCalls => Volatile.Read(ref _releaseCalls);

    /// <summary>How often an interface other than IUnknown was asked for and refused.</summary>
    public int RejectedQueries => Volatile.Read(ref _rejectedQueries);

    /// <summary>
    /// Wraps the identity in an RCW. The CLR keeps one RCW per identity per context, so calling
    /// this twice hands out the same object -- which is exactly the aliasing the tests need.
    /// </summary>
    public object CreateRuntimeCallableWrapper() => Marshal.GetObjectForIUnknown(_instance);

    private int OnQueryInterface(IntPtr self, ref Guid iid, out IntPtr result)
    {
        if (iid == IidUnknown)
        {
            result = self;
            OnAddRef(self);
            return SOk;
        }

        Interlocked.Increment(ref _rejectedQueries);
        result = IntPtr.Zero;
        return self == IntPtr.Zero ? EPointer : ENoInterface;
    }

    private uint OnAddRef(IntPtr self)
    {
        Interlocked.Increment(ref _addRefCalls);
        return (uint)Interlocked.Increment(ref _references);
    }

    private uint OnRelease(IntPtr self)
    {
        Interlocked.Increment(ref _releaseCalls);
        return (uint)Interlocked.Decrement(ref _references);
    }

}
