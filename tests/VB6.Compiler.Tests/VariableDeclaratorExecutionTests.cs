namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariableDeclaratorExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesTypedLocalAndModuleDeclaratorLists()
    {
        Run("""
            Public LeftValue As Integer, RightValue As Long

            Sub Main()
                Dim small As Integer, wide As Long
                LeftValue = 3
                RightValue = 40000
                small = 4
                wide = 5
                Debug.Print LeftValue + small
                Debug.Print RightValue + wide
            End Sub
            """,
            "7", "40005");
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
