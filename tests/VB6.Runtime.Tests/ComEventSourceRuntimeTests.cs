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
}
