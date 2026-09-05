namespace VB6.Compiler.Tests;

/// <summary>
/// <c>GoSub</c> and <c>Return</c>, including the case with nothing to return to.
///
/// A <c>Return</c> in a procedure that contains no <c>GoSub</c> has no jump table, and the emitted
/// block then left the index <c>Pop</c> pushes with no consumer and ended without a terminator.
/// The CLR rejected the whole method: the program died with an <c>InvalidProgramException</c>
/// before running a line, so the runtime's own "Return without an active GoSub" message — the
/// right one — could never be reached. That is the worst shape a defect can take here, because it
/// looks like a broken compiler rather than a broken program.
/// </summary>
[TestClass]
public sealed class GoSubReturnExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RunsGoSubAndReturnsToTheCallSite()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                GoSub Helper
                Debug.Print "after"
                GoSub Helper
                Debug.Print "done"
                Exit Sub
            Helper:
                Debug.Print "inside"
                Return
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "inside", "after", "inside", "done" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsAReturnWithNoActiveGoSub()
    {
        var error = VB6TestProgram.RunExpectingFailure("""
            Public Sub Main()
                Debug.Print "before"
                Return
            End Sub
            """);

        // The point is which failure: the runtime's own message, reached at the Return, rather
        // than the CLR refusing the method before Main starts.
        StringAssert.Contains(
            error,
            "Return executed without an active GoSub",
            "Erwartet der Laufzeitfehler der Runtime.");
        Assert.IsFalse(
            error.Contains("InvalidProgramException", StringComparison.Ordinal),
            "Das erzeugte IL muss gültig sein: " + error);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsAReturnAfterTheGoSubStackIsEmpty()
    {
        var error = VB6TestProgram.RunExpectingFailure("""
            Public Sub Main()
                GoSub Helper
                Debug.Print "after"
                Return
                Exit Sub
            Helper:
                Return
            End Sub
            """);

        StringAssert.Contains(error, "Return executed without an active GoSub");
        Assert.IsFalse(error.Contains("InvalidProgramException", StringComparison.Ordinal), error);
    }
}
