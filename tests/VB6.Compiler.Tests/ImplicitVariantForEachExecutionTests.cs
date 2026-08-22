namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ImplicitVariantForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesImplicitVariantForArrayForEachControlVariable()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim item
                Dim values(1 To 2) As Long
                values(1) = 4
                values(2) = 5

                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        CollectionAssert.AreEqual(new[] { "4", "5" }, lines);
    }
}
