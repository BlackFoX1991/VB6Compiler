namespace VB6.Compiler.Tests;

/// <summary>
/// The everyday statement shapes, executed rather than assumed.
///
/// These are the forms the grammar inventory for <c>managed-r1-grammar</c> measured and found
/// already correct. That is the usual outcome here -- the implementation is ahead of its proof --
/// so the deliverable is the running program, not a code change. What they protect against is a
/// later refactor that quietly drops one of the less common spellings: a post-test <c>Loop Until</c>
/// that stops running its body once, an <c>Is</c> comparison that stops matching, a numeric label
/// that stops being a jump target.
/// </summary>
[TestClass]
public sealed class StatementShapeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_CallsASubWithAndWithoutParentheses()
    {
        var output = VB6TestProgram.RunLines("""
            Private Sub Show(ByVal tag As String, ByVal value As Long)
                Debug.Print tag & "|" & value
            End Sub

            Private Sub Bare()
                Debug.Print "bare"
            End Sub

            Public Sub Main()
                Show "plain", 1
                Call Show("call", 2)
                Bare
                Call Bare
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "plain|1", "call|2", "bare", "bare" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_SelectsTheSameBranchInEveryIfForm()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim a As Long
                Dim r As String

                a = 5

                If a > 3 Then r = "gt" Else r = "le"
                Debug.Print "single-line|" & r

                If a > 9 Then
                    r = "big"
                ElseIf a > 3 Then
                    r = "mid"
                Else
                    r = "small"
                End If
                Debug.Print "block|" & r
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "single-line|gt", "block|mid" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_MatchesSelectCaseListsRangesAndComparisons()
    {
        var output = VB6TestProgram.RunLines("""
            Private Function Classify(ByVal value As Long) As String
                Select Case value
                    Case 1, 2
                        Classify = "list"
                    Case 3 To 6
                        Classify = "range"
                    Case Is > 100
                        Classify = "is"
                    Case Else
                        Classify = "else"
                End Select
            End Function

            Public Sub Main()
                Debug.Print Classify(2) & "," & Classify(5) & "," & Classify(200) & "," & Classify(50)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "list,range,is,else" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RunsEveryDoLoopForm()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim i As Long
                Dim n As Long

                i = 0
                Do While i < 3
                    i = i + 1
                Loop
                Debug.Print "do-while|" & i

                i = 0
                Do Until i >= 3
                    i = i + 1
                Loop
                Debug.Print "do-until|" & i

                ' A pre-test loop may run zero times; a post-test loop always runs once.
                i = 9
                Do While i < 3
                    i = i + 1
                Loop
                Debug.Print "pre-test-zero|" & i

                n = 0
                Do
                    n = n + 1
                Loop While False
                Debug.Print "loop-while-false|" & n

                n = 0
                Do
                    n = n + 1
                Loop Until True
                Debug.Print "loop-until-true|" & n

                n = 0
                Do
                    n = n + 1
                    If n >= 2 Then Exit Do
                Loop
                Debug.Print "exit-do|" & n
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "do-while|3",
                "do-until|3",
                "pre-test-zero|9",
                "loop-while-false|1",
                "loop-until-true|1",
                "exit-do|2",
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_HonoursSeparatorsContinuationsAndLabels()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim a As Long
                Dim b As Long

                a = 1: b = 2
                Debug.Print "separator|" & a & "," & b

                a = 1 + _
                    2
                Debug.Print "continuation|" & a

                GoTo 10
                a = 999
            10
                Debug.Print "numeric-label|" & a

                GoTo Skip
                a = 888
            Skip:
                Debug.Print "named-label|" & a
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "separator|1,2", "continuation|3", "numeric-label|3", "named-label|3" },
            output);
    }
}
