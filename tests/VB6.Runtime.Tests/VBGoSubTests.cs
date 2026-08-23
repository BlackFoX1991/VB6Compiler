namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBGoSubTests
{
    [TestMethod]
    public void Frames_IsolateReturnStacksAcrossProcedures()
    {
        VBGoSub.Enter();
        VBGoSub.Push(7);

        VBGoSub.Enter();
        Assert.ThrowsException<InvalidOperationException>(() => VBGoSub.Pop());
        VBGoSub.Push(3);
        Assert.AreEqual(3, VBGoSub.Pop());
        VBGoSub.Leave();

        Assert.AreEqual(7, VBGoSub.Pop());
        VBGoSub.Leave();
    }

    [TestMethod]
    public void OnGoToIndex_MapsNonPositiveValuesToTheFallthroughPath()
    {
        Assert.AreEqual(-1, VBControlFlow.OnGoToIndex(0));
        Assert.AreEqual(-1, VBControlFlow.OnGoToIndex(int.MinValue));
        Assert.AreEqual(0, VBControlFlow.OnGoToIndex(1));
        Assert.AreEqual(1, VBControlFlow.OnGoToIndex(2));
    }

    [TestMethod]
    public void EndProgram_UsesHostSinkWhenInstalled()
    {
        var called = false;
        var previousSink = VBControlFlow.EndProgramSink;
        try
        {
            VBControlFlow.EndProgramSink = () => called = true;
            VBControlFlow.EndProgram();
        }
        finally
        {
            VBControlFlow.EndProgramSink = previousSink;
        }

        Assert.IsTrue(called);
    }
}
