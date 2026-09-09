using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// The connection point a generated COM class offers so that a client can receive its events.
///
/// VB6 events are dispatched by name at run time here -- there is no CLR event and no delegate --
/// so the CLR's own <c>ComSourceInterfaces</c> machinery, which needs both, cannot be used. The
/// container is implemented directly instead, and an event reaches a sink the same way every other
/// late-bound call in this runtime works: the sink is an <c>IDispatch</c>, its DISPID for the event
/// is resolved by name, and the call goes through <c>Invoke</c>.
///
/// Every member is implemented explicitly. A generated class exposes an AutoDual class interface,
/// which would otherwise publish these plumbing methods as if they were VB6 members.
/// </summary>
// ComVisible has to be true: a generated class exposes an AutoDual class interface, and the CLR
// refuses to build one when a base type is invisible to COM. Nothing leaks into that interface
// even so, because every member below is an explicit interface implementation.
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public abstract class VBComEventSource : IConnectionPointContainer, ICustomQueryInterface
{
    private readonly List<VBComConnectionPoint> _connectionPoints = new();
    private readonly object _sync = new();

    /// <summary>
    /// Answers <c>IDispatch</c> with this project's own surface instead of the CLR's class
    /// interface.
    ///
    /// The difference is not cosmetic: the CLR numbers members by its own rule, resolves names
    /// under the CLR type name, and refuses a record value outright. All three are decisions a VB6
    /// server has to make itself, and the type library beside it already states them. Everything
    /// other than IDispatch stays with the CLR, COM identity included.
    /// </summary>
    CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref Guid iid, out IntPtr ppv)
    {
        ppv = IntPtr.Zero;
        if (!OperatingSystem.IsWindows() || iid != DispatchInterfaceId)
        {
            return CustomQueryInterfaceResult.NotHandled;
        }

        ppv = VBComDispatchSurface.Create(this);
        return CustomQueryInterfaceResult.Handled;
    }

    private static readonly Guid DispatchInterfaceId = new("00020400-0000-0000-C000-000000000046");

    /// <summary>
    /// Enumerates the connection points this object has handed out.
    ///
    /// A client that knows the interface id asks for it directly and never needs this; a generic
    /// one -- an object browser, a scripting host walking the container -- starts here. Refusing
    /// it told such a client that the object has no event sources at all, which is a different
    /// statement from "ask me by id".
    ///
    /// The enumerator works on a snapshot. COM does not define what an enumerator does when the
    /// underlying collection changes, and a snapshot is the only answer that cannot fault while a
    /// client is halfway through it.
    /// </summary>
    void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
    {
        lock (_sync)
        {
            ppEnum = new ConnectionPointEnumerator(_connectionPoints.Cast<IConnectionPoint>().ToArray(), 0);
        }
    }

    /// <summary>
    /// One COM enumerator over a fixed snapshot.
    ///
    /// The contract is the part that is easy to get subtly wrong: <c>Next</c> answers S_OK only
    /// when it filled the whole request and S_FALSE when it filled less, and both are success --
    /// a client loops until S_FALSE. <c>pceltFetched</c> may be null, and only when exactly one
    /// element was asked for; writing through it unconditionally faults that caller.
    /// </summary>
    private sealed class ConnectionPointEnumerator : IEnumConnectionPoints
    {
        private const int SFalse = 1;

        private readonly IConnectionPoint[] _items;
        private int _position;

        internal ConnectionPointEnumerator(IConnectionPoint[] items, int position)
        {
            _items = items;
            _position = position;
        }

        public int Next(int celt, IConnectionPoint[] rgelt, IntPtr pceltFetched)
        {
            ArgumentNullException.ThrowIfNull(rgelt);
            var fetched = Math.Max(0, Math.Min(celt, _items.Length - _position));
            for (var index = 0; index < fetched; index++)
            {
                rgelt[index] = _items[_position + index];
            }

            _position += fetched;
            if (pceltFetched != IntPtr.Zero)
            {
                Marshal.WriteInt32(pceltFetched, fetched);
            }

            return fetched == celt ? 0 : SFalse;
        }

        public int Skip(int celt)
        {
            var skipped = Math.Max(0, Math.Min(celt, _items.Length - _position));
            _position += skipped;
            return skipped == celt ? 0 : SFalse;
        }

        public void Reset() => _position = 0;

        public void Clone(out IEnumConnectionPoints ppenum) =>
            ppenum = new ConnectionPointEnumerator(_items, _position);
    }

    /// <summary>
    /// VB6 exposes exactly one event source per class, so any requested interface id resolves to
    /// the same connection point. A client that has no type library -- which is every client
    /// until type library generation exists -- passes IID_NULL or the class id, and both have to
    /// work rather than being refused on a technicality.
    /// </summary>
    void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
    {
        var requested = riid;
        lock (_sync)
        {
            var existing = _connectionPoints.FirstOrDefault(point => point.InterfaceId == requested);
            if (existing is null)
            {
                existing = new VBComConnectionPoint(this, requested);
                _connectionPoints.Add(existing);
            }

            ppCP = existing;
        }
    }

    /// <summary>
    /// Delivers one VB6 event to every connected sink. Called by the event runtime after the
    /// managed handlers have run, so a program that both handles its own event and publishes it
    /// sees the same order it would in VB6.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal void RaiseToSinks(string eventName, object?[] arguments)
    {
        VBComConnectionPoint[] points;
        lock (_sync)
        {
            points = _connectionPoints.ToArray();
        }

        foreach (var point in points)
        {
            point.Raise(eventName, arguments);
        }
    }

    internal bool HasSinks
    {
        get
        {
            lock (_sync)
            {
                return _connectionPoints.Any(point => point.HasSinks);
            }
        }
    }
}

/// <summary>One connection point of a generated COM class. Holds the advised sinks.</summary>
[ComVisible(false)]
internal sealed class VBComConnectionPoint : IConnectionPoint
{
    private readonly VBComEventSource _source;
    private readonly Dictionary<int, object> _sinks = new();
    private readonly object _sync = new();
    private int _nextCookie = 1;

    public VBComConnectionPoint(VBComEventSource source, Guid interfaceId)
    {
        _source = source;
        InterfaceId = interfaceId;
    }

    public Guid InterfaceId { get; }

    public bool HasSinks
    {
        get
        {
            lock (_sync)
            {
                return _sinks.Count > 0;
            }
        }
    }

    public void GetConnectionInterface(out Guid pIID) => pIID = InterfaceId;

    public void GetConnectionPointContainer(out IConnectionPointContainer ppCPC) =>
        ppCPC = (IConnectionPointContainer)_source;

    public void Advise(object pUnkSink, out int pdwCookie)
    {
        ArgumentNullException.ThrowIfNull(pUnkSink);
        lock (_sync)
        {
            pdwCookie = _nextCookie++;
            _sinks.Add(pdwCookie, pUnkSink);
        }
    }

    public void Unadvise(int dwCookie)
    {
        lock (_sync)
        {
            // COM answers an unknown cookie with CONNECT_E_NOCONNECTION rather than ignoring it.
            if (!_sinks.Remove(dwCookie))
            {
                throw new COMException("The connection cookie is not known.", unchecked((int)0x80040200));
            }
        }
    }

    /// <summary>
    /// Enumerates the sinks currently advised on this connection point.
    ///
    /// The source raises events itself, so nothing in this runtime needs the list -- but a client
    /// that wants to know whether anyone is listening, or that manages connections it did not
    /// create, has no other way to ask. Snapshot semantics as above: a client may unadvise while
    /// walking, and the enumerator must survive that.
    /// </summary>
    public void EnumConnections(out IEnumConnections ppEnum)
    {
        lock (_sync)
        {
            ppEnum = new ConnectionEnumerator(
                _sinks.Select(entry => new CONNECTDATA { pUnk = entry.Value, dwCookie = entry.Key }).ToArray(),
                0);
        }
    }

    private sealed class ConnectionEnumerator : IEnumConnections
    {
        private const int SFalse = 1;

        private readonly CONNECTDATA[] _items;
        private int _position;

        internal ConnectionEnumerator(CONNECTDATA[] items, int position)
        {
            _items = items;
            _position = position;
        }

        public int Next(int celt, CONNECTDATA[] rgelt, IntPtr pceltFetched)
        {
            ArgumentNullException.ThrowIfNull(rgelt);
            var fetched = Math.Max(0, Math.Min(celt, _items.Length - _position));
            for (var index = 0; index < fetched; index++)
            {
                rgelt[index] = _items[_position + index];
            }

            _position += fetched;
            if (pceltFetched != IntPtr.Zero)
            {
                Marshal.WriteInt32(pceltFetched, fetched);
            }

            return fetched == celt ? 0 : SFalse;
        }

        public int Skip(int celt)
        {
            var skipped = Math.Max(0, Math.Min(celt, _items.Length - _position));
            _position += skipped;
            return skipped == celt ? 0 : SFalse;
        }

        public void Reset() => _position = 0;

        public void Clone(out IEnumConnections ppenum) =>
            ppenum = new ConnectionEnumerator(_items, _position);
    }

    [SupportedOSPlatform("windows")]
    public void Raise(string eventName, object?[] arguments)
    {
        object[] sinks;
        lock (_sync)
        {
            sinks = _sinks.Values.ToArray();
        }

        foreach (var sink in sinks)
        {
            // A sink that refuses one event must not stop the others from receiving it -- and it
            // must not take down the raising program either, which is what VB6 does here.
            try
            {
                VBDynamicDispatch.TryInvokeSink(sink, eventName, arguments);
            }
            catch (COMException)
            {
            }
            catch (VB6RaisedError)
            {
            }
        }
    }
}
