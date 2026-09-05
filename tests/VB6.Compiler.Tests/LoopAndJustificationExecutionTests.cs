namespace VB6.Compiler.Tests;

/// <summary>
/// Statement forms the parser has long accepted without an executing test behind them.
///
/// <c>While ... Wend</c>, <c>LSet</c> and <c>RSet</c> each had parser coverage and no program that
/// ran them. That is the shape <c>CLAUDE.md</c> warns about: the implementation is usually ahead
/// of its proof, so the deliverable for these is the executing test, not a code change. All three
/// were measured correct before this file existed; the tests keep them that way.
/// </summary>
[TestClass]
public sealed class LoopAndJustificationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RunsWhileWend()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim i As Long
                Dim total As Long

                i = 0
                While i < 3
                    i = i + 1
                    total = total + i
                Wend
                Debug.Print "loop|" & i & "|" & total

                ' A condition that is false on entry runs the body zero times.
                While i < 0
                    total = 999
                Wend
                Debug.Print "skipped|" & total
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "loop|3|6", "skipped|6" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_JustifiesWithinAFixedWidthString()
    {
        var output = VB6TestProgram.RunLines("""
            Public Sub Main()
                Dim s As String * 6

                s = "ab"
                Debug.Print "assign|[" & s & "]"

                LSet s = "xy"
                Debug.Print "lset|[" & s & "]"

                RSet s = "xy"
                Debug.Print "rset|[" & s & "]"

                ' Wider than the field: both forms truncate rather than grow the storage.
                LSet s = "abcdefgh"
                Debug.Print "over|[" & s & "]|" & Len(s)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "assign|[ab    ]", "lset|[xy    ]", "rset|[    xy]", "over|[abcdef]|6" },
            output);
    }
}
