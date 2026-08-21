namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ModuleVariableExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_SharesModuleStateBetweenProcedures()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Counter As Long
            Private Label As String

            Private Sub Bump(ByVal amount As Long)
                Counter = Counter + amount
            End Sub

            Public Sub Main()
                Label = "total="
                Bump 10
                Bump 5
                Debug.Print Label & Counter
            End Sub
            """,
            "total=15");
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsALocalSeparateFromTheModuleVariableItShadows()
    {
        Run("""
            Public Value As Long

            Sub Hide()
                Dim Value As Integer
                Value = 7
                Debug.Print Value
            End Sub

            Sub Main()
                Value = 100
                Hide
                Debug.Print Value
            End Sub
            """,
            "7", "100");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            expectedLines,
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
