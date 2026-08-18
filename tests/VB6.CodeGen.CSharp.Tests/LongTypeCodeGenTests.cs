using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class LongTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsInt32LongOperationsAndLongForIncrement()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Long
                Dim i As Long
                value = 40000 + 20000
                For i = 1 To 3
                    value = value + 1
                Next i
                Debug.Print value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "int __vb6_value = 0;");
        StringAssert.Contains(source, "int __vb6_i = 0;");
        StringAssert.Contains(source, "VBOperators.AddLong(VBConversions.CLng(40000L), VBConversions.CLng(20000L))");
        StringAssert.Contains(source, "VBOperators.AddLong(__vb6_value, VBConversions.CLng(VBConversions.CInt(1L)))");
        StringAssert.Contains(source, "__vb6_i = VBOperators.AddLong(__vb6_i, __vb6_for_step_");
    }
}
