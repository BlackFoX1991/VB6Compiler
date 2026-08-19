using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ExponentiationCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsPowerWithDoubleOperands()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim result As Double
                result = 2 ^ -3
                Debug.Print result
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "double __vb6_result = 0d;");
        StringAssert.Contains(source, "VBOperators.Power(");
        StringAssert.Contains(source, "VBConversions.CDbl(VBConversions.CInt(2L))");
        StringAssert.Contains(source, "VBConversions.CDbl(VBOperators.NegateInteger(VBConversions.CInt(3L)))");
    }
}