using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class Int64TypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsSystemInt64OperationsAndForIncrement()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Int64
                Dim i As LongLong
                value = 3000000000 + 1
                For i = 3000000000 To 3000000002
                    value = value + 1
                Next i
                Debug.Print value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "long __vb6_value = 0;");
        StringAssert.Contains(source, "long __vb6_i = 0;");
        StringAssert.Contains(source, "VBConversions.CLngLng(3000000000L)");
        StringAssert.Contains(source, "VBOperators.AddLongLong(");
        StringAssert.Contains(source, "__vb6_i = VBOperators.AddLongLong(__vb6_i, __vb6_for_step_");
    }
}
