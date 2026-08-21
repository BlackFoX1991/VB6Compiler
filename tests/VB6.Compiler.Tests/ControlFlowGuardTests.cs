namespace VB6.Compiler.Tests;

/// <summary>
/// Jumps and error handling parse but cannot be lowered: the backend still lowers control flow
/// while emitting, which cannot express a jump into the middle of a block or a handler guarding
/// every statement. Binding drops what it does not understand, so each one has to be reported or
/// the generated program would quietly lose it.
/// </summary>
[TestClass]
public sealed class ControlFlowGuardTests
{
    /// <summary>
    /// GoTo and labels are lowered now; only the error handling still waits for the lowered
    /// representation, because a handler has to guard every statement rather than sit at one point.
    /// </summary>
    [TestMethod]
    public void Analyze_ReportsOnlyTheErrorHandling()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                On Error GoTo Failed
                Debug.Print 1
                GoTo Done
            Failed:
                Debug.Print 2
            Done:
                On Error GoTo 0
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        var reported = analysis.Diagnostics
            .Where(d => d.Code == "VB6S0061")
            .Select(d => d.Message)
            .ToArray();

        Assert.AreEqual(2, reported.Length, string.Join(Environment.NewLine, reported));
        Assert.IsTrue(reported.All(m => m.Contains("On Error", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Analyze_ReportsOnErrorResumeNext()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                On Error Resume Next
                Debug.Print 1
            End Sub
            """, "Module1.bas").Analyze();

        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0061");
        StringAssert.Contains(diagnostic.Message, "On Error Resume Next");
    }

    [TestMethod]
    public void Lower_StopsRatherThanEmittingAProgramWithoutTheHandler()
    {
        var lowering = VBCompilation.Create("""
            Sub Main()
                On Error Resume Next
                Debug.Print 1
            End Sub
            """, "Module1.bas").Lower();

        Assert.IsFalse(lowering.Success);
        Assert.IsNull(lowering.Program);
    }
}
