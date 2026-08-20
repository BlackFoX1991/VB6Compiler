using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ParamArrayCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsParamArrayFactoryForRestArguments()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Log 1, "x"
                Log
            End Sub

            Sub Log(ParamArray values() As Variant)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "private static void __vb6_Log(VBArray<VBVariant> __vb6_arg_values)");
        StringAssert.Contains(source, "__vb6_Log(VBArray<VBVariant>.FromValues(VBVariant.From(VBConversions.CInt(1L)), VBVariant.From(\"x\")).Clone());");
        StringAssert.Contains(source, "__vb6_Log(VBArray<VBVariant>.FromValues().Clone());");
    }
}
