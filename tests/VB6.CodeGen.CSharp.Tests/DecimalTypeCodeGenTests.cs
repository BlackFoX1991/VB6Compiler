using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class DecimalTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsDecimalRuntimeConversionsAndOperations()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim amount As Decimal
                amount = 1.25
                amount = amount + 2
                amount = amount * 3
                Debug.Print amount / 2
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "decimal __vb6_amount = 0m;");
        StringAssert.Contains(source, "__vb6_amount = VBConversions.CDec(1.25d);");
        StringAssert.Contains(source, "VBOperators.AddDecimal(__vb6_amount, VBConversions.CDec(VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "VBOperators.MultiplyDecimal(__vb6_amount, VBConversions.CDec(VBConversions.CInt(3L)))");
        StringAssert.Contains(source, "VBOperators.DivideDecimal(__vb6_amount, VBConversions.CDec(VBConversions.CInt(2L)))");
    }
}
