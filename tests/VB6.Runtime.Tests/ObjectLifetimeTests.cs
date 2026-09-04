namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ObjectLifetimeTests
{
    private sealed class Terminable
    {
        public int Runs { get; private set; }

        // The emitted name a generated class carries. Looking for the plain "Class_Terminate" was
        // the bug this test exists to prevent: it found nothing, and nothing is what happened.
        private void __vb6_Class_Terminate() => Runs++;
    }

    private sealed class Throwing
    {
        private void __vb6_Class_Terminate() => throw new InvalidOperationException("boom");
    }

    [TestMethod]
    public void RunTerminator_RunsTheTerminatorOnce()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunTerminator(instance);
        VBObjectLifetime.RunTerminator(instance);

        Assert.AreEqual(1, instance.Runs);
    }

    [TestMethod]
    public void RunPendingTerminators_RunsAnInstanceThatIsStillAlive()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunPendingTerminators();

        Assert.AreEqual(1, instance.Runs);
        GC.KeepAlive(instance);
    }

    [TestMethod]
    public void RunPendingTerminators_DoesNotRunAnInstanceThatAlreadyTerminated()
    {
        var instance = new Terminable();

        VBObjectLifetime.Register(instance);
        VBObjectLifetime.RunTerminator(instance);
        VBObjectLifetime.RunPendingTerminators();

        Assert.AreEqual(1, instance.Runs);
        GC.KeepAlive(instance);
    }

    [TestMethod]
    public void RunPendingTerminators_DrainsNewestFirst()
    {
        // Nesting usually follows creation order, so an object is torn down before the objects it
        // was built from.
        List<string> order = [];
        var first = new Ordered("erst", order);
        var second = new Ordered("dann", order);

        VBObjectLifetime.Register(first);
        VBObjectLifetime.Register(second);
        VBObjectLifetime.RunPendingTerminators();

        CollectionAssert.AreEqual(new[] { "dann", "erst" }, order);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
    }

    [TestMethod]
    public void RunTerminator_SwallowsAnErrorRaisedDuringTeardown()
    {
        var instance = new Throwing();

        VBObjectLifetime.Register(instance);

        // A terminator runs while the program is already ending; on the finalizer thread an
        // escaping exception would take the process down, which no VB6 program does.
        VBObjectLifetime.RunTerminator(instance);
    }

    [TestMethod]
    public void RunTerminator_IgnoresNothing()
    {
        VBObjectLifetime.Register(null);
        VBObjectLifetime.RunTerminator(null);
    }

    private sealed class Ordered(string name, List<string> order)
    {
        private void __vb6_Class_Terminate() => order.Add(name);
    }
}
