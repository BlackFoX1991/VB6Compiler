namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesMultidimensionalForEachExitAndValueControlVariable()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim item As Variant
                Dim count As Long
                Dim values(1 To 2, 5 To 6) As Long

                values(1, 5) = 15
                values(1, 6) = 16
                values(2, 5) = 25
                values(2, 6) = 26

                For Each item In values
                    count = count + 1
                    Debug.Print item
                    If count = 3 Then Exit For
                Next item

                Debug.Print count
                item = 99
                Debug.Print values(2, 5)
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
        CollectionAssert.AreEqual(
            new[] { "15", "16", "25", "3", "25" },
            lines);
    }
}
