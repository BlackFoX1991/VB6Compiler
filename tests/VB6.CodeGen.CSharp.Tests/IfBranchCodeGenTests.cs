using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class IfBranchCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsElseIfAndElseBranches()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                If x = 1 Then
                    x = 10
                ElseIf x = 2 Then
                    x = 20
                Else
                    x = 30
                End If
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "if (VBOperators.Equal(__vb6_x, VBConversions.CInt(1L)))");
        StringAssert.Contains(source, "else if (VBOperators.Equal(__vb6_x, VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "else");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(30L);");
    }

    [TestMethod]
    public void Generate_SingleLineIfUsesNormalBranchCode()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim x As Integer
                x = 1
                If x = 1 Then x = 2 Else x = 3
                Debug.Print x
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "if (VBOperators.Equal(__vb6_x, VBConversions.CInt(1L)))");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(2L);");
        StringAssert.Contains(source, "__vb6_x = VBConversions.CInt(3L);");
    }
}
