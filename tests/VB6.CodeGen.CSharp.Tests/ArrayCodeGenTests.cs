using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ArrayCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsFixedArrayWithVbBoundsAndAccess()
    {
        var analysis = VBCompilation.Create("""
            Option Base 1
            Sub Main()
                Dim values(3) As Long
                Debug.Print values(1)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<int> __vb6_values = new VBArray<int>(");
        StringAssert.Contains(source, "new VBArrayBound(VBConversions.CLng(1L)");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_values[");
    }

    [TestMethod]
    public void Generate_EmitsDynamicArrayAsUnallocatedUntilReDim()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim values() As String
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<string> __vb6_values = null!;");
    }

    [TestMethod]
    public void Generate_EmitsArrayParametersAsVbArrays()
    {
        var analysis = VBCompilation.Create("""
            Function First(values() As Long) As Long
                First = values(1)
            End Function
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "ref VBArray<int> __vb6_arg_values");
        StringAssert.Contains(source, "__vb6_arg_values[");
    }

    private static string FormatDiagnostics(VBCompilationAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}