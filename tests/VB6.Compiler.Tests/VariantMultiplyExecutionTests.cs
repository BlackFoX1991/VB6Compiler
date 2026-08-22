namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantMultiplyExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantMultiplyWithEmptyStringsAndNumericTargets()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value
                Dim number As Long

                Debug.Print value * 7

                value = 3
                Debug.Print value * 4
                number = value * 5
                Debug.Print number

                value = "2.5"
                Debug.Print value * 2

                value = True
                Debug.Print value * 2
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { "0", "12", "15", "5", "-2" },
            lines);
    }
}
