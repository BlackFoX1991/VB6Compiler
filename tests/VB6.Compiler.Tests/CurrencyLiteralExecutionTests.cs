namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CurrencyLiteralExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesCurrencyLiteralArithmetic()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim amount As Currency
                amount = 1.2345@
                amount = amount * 1.2345@

                If amount = 1.524@ Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        Assert.AreEqual("1", standardOutput.Trim());
    }
}
