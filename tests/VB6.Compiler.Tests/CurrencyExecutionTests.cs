namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CurrencyExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesCurrencyArithmetic()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim amount As Currency
                amount = 1.2345
                amount = amount * 1.2345

                If amount = 1.524 Then
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
    public void EmitManagedApplication_ExecutesCCurConversion()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CCur("1.23455")

                Debug.Print CDbl(value)
                Debug.Print VarType(value)
                Debug.Print CDbl(CCur(True))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1.2346", "6", "-1" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesTypedScalarComparisons()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim signed As Long
                signed = 4
                Debug.Print signed = 4
                Debug.Print signed < 5

                Dim unsigned As UInteger
                unsigned = 7
                Debug.Print unsigned > 6

                Dim amount As Currency
                amount = 1.25
                Debug.Print amount >= 1.25

                Dim text As String
                text = "vb6"
                Debug.Print text = "vb6"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True", "True", "True" }, output);
    }
}
