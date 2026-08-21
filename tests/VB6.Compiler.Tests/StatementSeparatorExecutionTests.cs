namespace VB6.Compiler.Tests;

[TestClass]
public sealed class StatementSeparatorExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesColonSeparatedStatements()
    {
        const string source = """
            Sub Main()
                Dim x As Integer: Dim y As Integer
                x = 1: y = 2
                If x = 1 Then x = x + 2: y = y + 3 Else x = 99
                Select Case y
                    Case 5: x = x + 4: y = y + 5
                End Select
                Debug.Print x: Debug.Print y
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        CollectionAssert.AreEqual(
            new[] { "7", "10" },
            standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
            standardOutput);
    }
}
