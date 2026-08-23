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
}
