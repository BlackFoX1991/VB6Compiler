namespace VB6.Compiler.Tests;

/// <summary>
/// When VB6 evaluates what, and how often.
///
/// VB6 has no short-circuit operator. <c>And</c> and <c>Or</c> always evaluate both sides, which
/// is why legacy code can put a side effect on the right of an <c>And</c> and rely on it running.
/// The lowering already calls a runtime operation with both operands in hand, so no branch exists
/// to skip one -- but nothing proved it, and a later "optimization" that introduced one would
/// change what old programs do without failing a single test.
///
/// Same reasoning for the loop header: VB6 evaluates the limit and the step once, before the
/// first iteration. A limit re-evaluated per iteration is a behavior change that stays invisible
/// until the expression has a side effect or costs something.
/// </summary>
[TestClass]
public sealed class EvaluationOrderExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_EvaluatesBothOperandsOfAndAndOr()
    {
        var output = VB6TestProgram.RunLines("""
            Private Trace As String

            Private Function Note(ByVal tag As String, ByVal value As Boolean) As Boolean
                Trace = Trace & tag
                Note = value
            End Function

            Public Sub Main()
                Dim result As Boolean

                Trace = ""
                result = Note("L", False) And Note("R", True)
                Debug.Print "and|" & Trace & "|" & result

                Trace = ""
                result = Note("L", True) Or Note("R", False)
                Debug.Print "or|" & Trace & "|" & result
            End Sub
            """);

        // A short-circuiting compiler would print "and|L|False" and "or|L|True".
        CollectionAssert.AreEqual(new[] { "and|LR|False", "or|LR|True" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_EvaluatesTheLoopLimitAndStepOnce()
    {
        var output = VB6TestProgram.RunLines("""
            Private Trace As String

            Private Function Limit() As Long
                Trace = Trace & "L"
                Limit = 3
            End Function

            Private Function Step2() As Long
                Trace = Trace & "S"
                Step2 = 1
            End Function

            Public Sub Main()
                Dim i As Long
                Dim rounds As Long

                Trace = ""
                For i = 1 To Limit() Step Step2()
                    rounds = rounds + 1
                Next i

                Debug.Print "header|" & Trace & "|rounds=" & rounds
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "header|LS|rounds=3" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_EvaluatesTheWithExpressionOnce()
    {
        var output = VB6TestProgram.RunLines("""
            Private Type Slot
                Value As Long
            End Type

            Private Trace As String
            Private Storage(0 To 2) As Slot

            Private Function Chosen() As Long
                Trace = Trace & "C"
                Chosen = 1
            End Function

            Public Sub Main()
                Trace = ""
                With Storage(Chosen())
                    .Value = 1
                    .Value = .Value + 1
                    Debug.Print "with|" & Trace & "|" & .Value
                End With
                Debug.Print "stored|" & Storage(1).Value
            End Sub
            """);

        // Three member accesses, one evaluation of the selector -- and the writes land in the
        // element the selector chose, not in a copy.
        CollectionAssert.AreEqual(new[] { "with|C|2", "stored|2" }, output);
    }
}
