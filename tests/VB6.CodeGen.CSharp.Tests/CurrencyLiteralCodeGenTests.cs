using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class CurrencyLiteralCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsCurrencyLiteralThroughRuntimeConversion()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Debug.Print 12.3456@
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBDebug.Print(VBConversions.CCur(12.3456m));");
    }
}
