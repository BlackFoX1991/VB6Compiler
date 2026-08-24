namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FloatingExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesSingleDivisionAndSingleLongPromotion()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim singleValue As Single
                Dim longValue As Long
                Dim doubleValue As Double

                singleValue = 1 / 2
                longValue = 40000
                doubleValue = singleValue + longValue

                If doubleValue = 40000.5 Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("1", standardOutput.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_UsesVb6RoundingForVariantModOperands()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim left As Variant
                Dim right As Variant

                left = 12
                right = 4.3
                Debug.Print left Mod right

                left = 12.6
                right = 5
                Debug.Print left Mod right
            End Sub
        """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.AreEqual(2, lines.Length);
        Assert.AreEqual("0", lines[0].Trim());
        Assert.AreEqual("3", lines[1].Trim());
    }
}
