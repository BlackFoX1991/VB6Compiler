using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ByteTypeCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsByteConversionsAndOperators()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim left As Byte
                Dim right As Byte
                left = 100
                right = 6
                left = left + right
                left = left Mod right
                Debug.Print left
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success);
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "byte __vb6_left = 0;");
        StringAssert.Contains(source, "byte __vb6_right = 0;");
        StringAssert.Contains(source, "__vb6_left = VBConversions.CByte(VBConversions.CInt(100L));");
        StringAssert.Contains(source, "VBOperators.AddByte(__vb6_left, __vb6_right)");
        StringAssert.Contains(source, "VBOperators.ModByte(__vb6_left, __vb6_right)");
    }
}
