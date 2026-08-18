using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class FloatingTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsSingleConversionsDivisionAndSingleLongPromotion()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim singleValue As Single
                Dim longValue As Long
                Dim doubleValue As Double
                singleValue = 1.5
                longValue = 40000
                singleValue = 1 / 2
                doubleValue = singleValue + longValue
                Debug.Print doubleValue
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "float __vb6_singleValue = 0f;");
        StringAssert.Contains(source, "double __vb6_doubleValue = 0d;");
        StringAssert.Contains(source, "__vb6_singleValue = VBConversions.CSng(1.5d);");
        StringAssert.Contains(source, "VBOperators.DivideSingle(VBConversions.CSng(VBConversions.CInt(1L)), VBConversions.CSng(VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "VBOperators.AddDouble(VBConversions.CDbl(__vb6_singleValue), VBConversions.CDbl(__vb6_longValue))");
    }
}
