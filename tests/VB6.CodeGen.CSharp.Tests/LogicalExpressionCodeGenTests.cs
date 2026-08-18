using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class LogicalExpressionCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsBooleanLogicalRuntimeCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim flag As Boolean
                flag = True
                If Not False And flag Or False Then
                    Debug.Print 1
                End If
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_flag = true;");
        StringAssert.Contains(source, "VBOperators.NotBoolean(false)");
        StringAssert.Contains(source, "VBOperators.AndBoolean(VBOperators.NotBoolean(false), __vb6_flag)");
        StringAssert.Contains(source, "VBOperators.OrBoolean(");
    }

    [TestMethod]
    public void Generate_EmitsXorEqvAndImpRuntimeCalls()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim flag As Boolean
                flag = True Xor False Eqv True Imp False
                Debug.Print flag
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBOperators.XorBoolean(true, false)");
        StringAssert.Contains(source, "VBOperators.EqvBoolean(");
        StringAssert.Contains(source, "VBOperators.ImpBoolean(");
    }
}
