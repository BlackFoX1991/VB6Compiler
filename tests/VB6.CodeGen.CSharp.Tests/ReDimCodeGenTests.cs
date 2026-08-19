using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ReDimCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsDynamicAllocationAndPreserveResize()
    {
        var analysis = VBCompilation.Create("""
            Option Base 1
            Sub Main()
                Dim values() As Long
                ReDim values(2)
                values(1) = 42
                ReDim Preserve values(4)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<int> __vb6_values = null!;");
        StringAssert.Contains(
            source,
            "__vb6_values = new VBArray<int>(new VBArrayBound(VBConversions.CLng(1L), VBConversions.CLng(VBConversions.CInt(2L))));");
        StringAssert.Contains(
            source,
            "__vb6_values = __vb6_values.ReDimPreserve(new VBArrayBound(VBConversions.CLng(1L), VBConversions.CLng(VBConversions.CInt(4L))));");
    }

    [TestMethod]
    public void Generate_ReDimWithoutPreserveReplacesStorage()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values() As Integer
                ReDim values(-1 To 2)
                ReDim values(0 To 3)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        Assert.AreEqual(2, CountOccurrences(source, "__vb6_values = new VBArray<short>("));
        Assert.AreEqual(0, CountOccurrences(source, ".ReDimPreserve("));
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FormatDiagnostics(CompilationAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
