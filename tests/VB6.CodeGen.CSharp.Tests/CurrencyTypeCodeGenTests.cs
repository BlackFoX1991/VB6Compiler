using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class CurrencyTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsCurrencyRuntimeTypeAndOperations()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim amount As Currency
                amount = 1.25
                amount = amount + 2.5
                amount = amount * 2
                Debug.Print amount / 2
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBCurrency __vb6_amount = default;");
        StringAssert.Contains(source, "VBConversions.CCur(1.25d)");
        // 2.5 is a Double literal, and Double dominates Currency — the addition runs in Double
        // and only the assignment converts back to Currency.
        StringAssert.Contains(source, "VBConversions.CCur(VBOperators.AddDouble(VBConversions.CDbl(__vb6_amount), 2.5d))");
        StringAssert.Contains(source, "VBOperators.MultiplyCurrency(__vb6_amount, VBConversions.CCur(VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "VBOperators.DivideDouble(VBConversions.CDbl(__vb6_amount), VBConversions.CDbl(VBConversions.CInt(2L)))");
    }
}
