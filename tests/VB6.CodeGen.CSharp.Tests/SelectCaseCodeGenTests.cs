using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class SelectCaseCodeGenTests
{
    [TestMethod]
    public void Generate_EvaluatesSelectorOnceAndEmitsCaseTests()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 4
                Select Case x
                    Case 1, 2
                        Debug.Print 1
                    Case 3 To 5
                        Debug.Print 2
                    Case Is > 10
                        Debug.Print 3
                    Case Else
                        Debug.Print 4
                End Select
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "var __vb6_select_0 = __vb6_x;");
        StringAssert.Contains(source, "VBOperators.Equal(__vb6_select_0, VBConversions.CInt(1L)) || VBOperators.Equal(__vb6_select_0, VBConversions.CInt(2L))");
        StringAssert.Contains(source, "VBOperators.GreaterOrEqual(__vb6_select_0, VBConversions.CInt(3L))");
        StringAssert.Contains(source, "VBOperators.LessOrEqual(__vb6_select_0, VBConversions.CInt(5L))");
        StringAssert.Contains(source, "VBOperators.Greater(__vb6_select_0, VBConversions.CInt(10L))");
        StringAssert.Contains(source, "else");
    }
}
