namespace VB6.Compiler.Tests;

/// <summary>
/// The contract surface of card <c>l1-02-j</c>: nested handlers, every Resume form, and the
/// Err/Erl state around them.
///
/// The resume dispatch is a switch into the per-statement continuations, which sit outside every
/// protected region. A Resume with nothing to return from therefore cannot raise there - the IL
/// would not verify - so it records the documented error 20 instead and falls through, which is
/// what an enclosing On Error Resume Next observes. Only a procedure without any protected
/// region at all still raises it.
/// </summary>
[TestClass]
public sealed class NestedErrorResumeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ResumeNextWithoutAnActiveErrorRecordsError20AndContinues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Resume Next
                Debug.Print "after " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "after 20" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_BareResumeWithoutAnActiveErrorRecordsError20AndContinues()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Resume
                Debug.Print "after " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "after 20" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_BareResumeRetriesTheFailingStatement()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim tries As Long
                On Error GoTo Failed
                Err.Raise 5
                Debug.Print "done " & tries
                Exit Sub
            Failed:
                tries = tries + 1
                If tries >= 3 Then Resume Next
                Resume
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "done 3" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LetsAnInnerHandlerShadowTheOuterOne()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "back"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "INNER 5", "back" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PropagatesAnErrorWithoutAnInnerHandlerToTheCaller()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                Err.Raise 9
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 9" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RestoresTheOuterHandlerAfterTheInnerCallReturns()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Err.Raise 11
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER " & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "INNER 5", "OUTER 11" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_OnErrorGotoZeroDisablesOnlyTheCurrentProcedure()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "unreached"
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                On Error GoTo 0
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Debug.Print "INNER"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ResumeNextInsideTheInnerProcedureContinuesThere()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Debug.Print "back " & Err.Number
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Debug.Print "inner continues"
                Exit Sub
            InnerFailed:
                Resume Next
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "inner continues", "back 0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RaisingFromInsideAHandlerReachesTheCaller()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo OuterFailed
                Inner
                Exit Sub
            OuterFailed:
                Debug.Print "OUTER " & Err.Number
            End Sub

            Sub Inner()
                On Error GoTo InnerFailed
                Err.Raise 5
                Exit Sub
            InnerFailed:
                Err.Raise 6
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "OUTER 6" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsEveryErrFieldInsideTheHandler()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo Failed
            100
                Err.Raise 13, "srcname", "descr"
                Exit Sub
            Failed:
                Debug.Print Err.Number & "|" & Err.Source & "|" & Err.Description & "|" & Erl
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "13|srcname|descr|100" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ClearsEveryErrFieldOnErrClear()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Err.Raise 13, "srcname", "descr"
                Err.Clear
                Debug.Print Err.Number & "|" & Err.Source & "|" & Err.Description & "|" & Erl
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0|||0" }, output);
    }
}
