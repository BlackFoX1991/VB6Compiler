namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantFoundationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantStorageAndScalarConversions()
    {
        var compilation = VBCompilation.Create("""
            Sub Consume(ByVal value As Variant)
                Debug.Print value
            End Sub

            Sub Main()
                Dim value As Variant
                Dim number As Long
                Dim values(1 To 2) As Variant

                Debug.Print value
                value = 42
                number = value
                Debug.Print number

                value = "hello"
                Debug.Print value

                values(1) = 7
                Debug.Print values(1)
                Consume 9
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
        CollectionAssert.AreEqual(
            new[] { string.Empty, "42", "hello", "7", "9" },
            lines);
    }
}
