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
public abstract class VBComEventSource : IConnectionPointContainer
{
    private readonly List<VBComConnectionPoint> _connectionPoints = new();
    private readonly object _sync = new();

    void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum) =>
        throw new NotImplementedException(
            "Enumerating connection points is not supported; ask for one by interface id.");

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

    public void EnumConnections(out IEnumConnections ppEnum) =>
        throw new NotImplementedException(
            "Enumerating connections is not supported; the source raises events itself.");

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
