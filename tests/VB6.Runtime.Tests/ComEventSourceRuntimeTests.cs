using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

/// <summary>
/// The connection point a generated COM class offers. VB6 events are dispatched by name here, so
/// the container is implemented by the runtime rather than by the CLR's ComSourceInterfaces
/// machinery, which needs CLR events and delegates.
/// </summary>
[TestClass]
public sealed class ComEventSourceRuntimeTests
{
    private sealed class TestSource : VBComEventSource
    {
    }

    private sealed class RecordingSink
    {
        public List<object?[]> Calls { get; } = new();

        public void Fertig(object? value) => Calls.Add(new[] { value });
    }

    private sealed class SilentSink
    {
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ComEventSource_DeliversRaisedEventsToAnAdvisedSink()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Connection points are a Windows contract.");
            return;
        }

        var source = new TestSource();
        var sink = new RecordingSink();
        var container = (IConnectionPointContainer)source;
        var iid = Guid.Empty;
        container.FindConnectionPoint(ref iid, out var point);
        Assert.IsNotNull(point);
        point.Advise(sink, out var cookie);

        VBEvents.Raise(source, "Fertig", new object?[] { 42 });

        Assert.AreEqual(1, sink.Calls.Count);
        Assert.AreEqual(42, sink.Calls[0][0]);

        // Nach Unadvise kommt nichts mehr an, und der Cookie ist danach unbekannt.
        point.Unadvise(cookie);
        VBEvents.Raise(source, "Fertig", new object?[] { 43 });
        Assert.AreEqual(1, sink.Calls.Count);

        var stale = Assert.ThrowsExactly<COMException>(() => point.Unadvise(cookie));
        Assert.AreEqual(unchecked((int)0x80040200), stale.ErrorCode);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ComEventSource_IgnoresASinkThatDoesNotImplementTheEvent()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Connection points are a Windows contract.");
            return;
        }

        var source = new TestSource();
        var listening = new RecordingSink();
        var container = (IConnectionPointContainer)source;
        var iid = Guid.Empty;
        container.FindConnectionPoint(ref iid, out var point);
        Assert.IsNotNull(point);
        point.Advise(new SilentSink(), out _);
        point.Advise(listening, out _);

        // Ein Senke, die das Ereignis nicht kennt, ist kein Fehler -- und darf die anderen nicht
        // um ihre Zustellung bringen.
        VBEvents.Raise(source, "Fertig", new object?[] { 7 });

        Assert.AreEqual(1, listening.Calls.Count);
        Assert.AreEqual(7, listening.Calls[0][0]);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ComEventSource_ReturnsTheSameConnectionPointForTheSameInterfaceId()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Connection points are a Windows contract.");
            return;
        }

        var container = (IConnectionPointContainer)new TestSource();
        var first = Guid.Empty;
        container.FindConnectionPoint(ref first, out var one);
        var second = Guid.Empty;
        container.FindConnectionPoint(ref second, out var two);

        // Ein Client ohne Typbibliothek fragt mit IID_NULL; er muss dieselbe Verbindung bekommen
        // wie beim ersten Mal, sonst hinge seine Senke an einem anderen Punkt als das Ereignis.
        Assert.IsNotNull(one);
        Assert.AreSame(one, two);
        one.GetConnectionInterface(out var reported);
        Assert.AreEqual(Guid.Empty, reported);
    }

    [TestMethod]
    public void EnumConnections_FollowsTheComEnumeratorContract()
    {
        var source = new TestSource();
        var container = (IConnectionPointContainer)source;
        var interfaceId = Guid.Empty;
        container.FindConnectionPoint(ref interfaceId, out var point);
        Assert.IsNotNull(point);

        point.Advise(new RecordingSink(), out var first);
        point.Advise(new RecordingSink(), out var second);

        point.EnumConnections(out var connections);

        // Next fuellt hoechstens so viele Elemente, wie da sind, und antwortet S_OK nur, wenn es
        // die ganze Anfrage bedienen konnte. Beides ist Erfolg -- ein Client laeuft bis S_FALSE.
        var buffer = new CONNECTDATA[2];
        var fetched = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            Assert.AreEqual(0, connections.Next(1, buffer, fetched));
            Assert.AreEqual(1, Marshal.ReadInt32(fetched));
            Assert.AreEqual(first, buffer[0].dwCookie);

            // Eine Anfrage ueber den Rest hinaus liefert S_FALSE mit der tatsaechlichen Zahl.
            Assert.AreEqual(1, connections.Next(2, buffer, fetched));
            Assert.AreEqual(1, Marshal.ReadInt32(fetched));
            Assert.AreEqual(second, buffer[0].dwCookie);

            Assert.AreEqual(1, connections.Next(1, buffer, fetched));
            Assert.AreEqual(0, Marshal.ReadInt32(fetched));
        }
        finally
        {
            Marshal.FreeCoTaskMem(fetched);
        }

        // pceltFetched darf null sein. Bedingungslos hindurchzuschreiben laesst genau diesen
        // Aufrufer mit einer Zugriffsverletzung stehen.
        connections.Reset();
        Assert.AreEqual(0, connections.Next(1, buffer, IntPtr.Zero));
        Assert.AreEqual(first, buffer[0].dwCookie);

        // Ein Klon traegt die Position mit und laeuft danach unabhaengig weiter.
        connections.Clone(out var clone);
        Assert.AreEqual(0, clone.Next(1, buffer, IntPtr.Zero));
        Assert.AreEqual(second, buffer[0].dwCookie);
        Assert.AreEqual(0, connections.Skip(1));
        Assert.AreEqual(1, connections.Skip(1));
    }

    [TestMethod]
    public void EnumConnections_KeepsWalkingAfterTheSinkUnadvises()
    {
        // COM sagt nicht, was ein Enumerator tut, wenn sich die Sammlung darunter aendert. Ein
        // Schnappschuss ist die einzige Antwort, die nicht mitten im Durchlauf eines Clients
        // fehlschlagen kann -- und genau das misst dieser Fall.
        var source = new TestSource();
        var container = (IConnectionPointContainer)source;
        var interfaceId = Guid.Empty;
        container.FindConnectionPoint(ref interfaceId, out var point);
        Assert.IsNotNull(point);
        point.Advise(new RecordingSink(), out var cookie);

        point.EnumConnections(out var connections);
        point.Unadvise(cookie);

        var buffer = new CONNECTDATA[1];
        Assert.AreEqual(0, connections.Next(1, buffer, IntPtr.Zero));
        Assert.AreEqual(cookie, buffer[0].dwCookie);
    }

    [TestMethod]
    public void EnumConnectionPoints_ReturnsTheContainersPoints()
    {
        // Wer die Interface-Id kennt, fragt direkt danach und braucht das hier nie. Ein
        // generischer Client -- ein Objektbrowser, ein Skripthost, der den Container abläuft --
        // faengt hier an, und eine Ablehnung hiesse fuer ihn: kein einziges Ereignis.
        var source = new TestSource();
        var container = (IConnectionPointContainer)source;
        var interfaceId = Guid.Empty;
        container.FindConnectionPoint(ref interfaceId, out var expected);

        container.EnumConnectionPoints(out var points);
        var buffer = new IConnectionPoint[2];
        Assert.AreEqual(0, points.Next(1, buffer, IntPtr.Zero));
        Assert.AreSame(expected, buffer[0]);
        Assert.AreEqual(1, points.Next(1, buffer, IntPtr.Zero));

        points.Reset();
        points.Clone(out var clone);
        Assert.AreEqual(0, clone.Next(1, buffer, IntPtr.Zero));
        Assert.AreSame(expected, buffer[0]);
    }
}
