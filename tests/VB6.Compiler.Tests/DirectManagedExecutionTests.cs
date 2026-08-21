namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DirectManagedExecutionTests
{
    [TestMethod]
    public void DirectManagedBackend_ExecutesScalarProgramWithoutCSharp()
    {
        Run("""
            Sub AddOne(ByRef Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim value As Long
                value = 40 + 1
                AddOne value
                Debug.Print value
            End Sub
            """, "42");
    }

    [TestMethod]
    public void DirectManagedBackend_DiscardsByRefTemporaryWriteBack()
    {
        Run("""
            Sub Bump(ByRef Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim value As Long
                value = 10
                Bump value
                Bump value + 10
                Debug.Print value
            End Sub
            """, "11");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var lines = VB6TestProgram.RunDirectLines(source);
        CollectionAssert.AreEqual(expectedLines, lines, string.Join(Environment.NewLine, lines));
    }
}
