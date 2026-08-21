namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ExponentiationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVb6ExponentiationRules()
    {
        Run("""
            Sub Main()
                Debug.Print 2 ^ 3
                Debug.Print 3 ^ 3 ^ 3
                Debug.Print -2 ^ 2
                Debug.Print 2 ^ -3
            End Sub
            """,
            "8", "19683", "-4", "0.125");
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