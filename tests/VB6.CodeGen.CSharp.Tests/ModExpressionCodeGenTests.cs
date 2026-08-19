using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ModExpressionCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsIntegerModRuntimeCall()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Integer
                value = 17 Mod 5
                Debug.Print value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBOperators.ModInteger(VBConversions.CInt(17L), VBConversions.CInt(5L))");
    }
}
