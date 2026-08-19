using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ArrayLibraryCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsFixedAndDynamicEraseOperations()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim fixedValues(1 To 2) As Long
                Dim dynamicValues() As Long
                ReDim dynamicValues(0 To 3)
                Erase fixedValues, dynamicValues
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "__vb6_fixedValues.Clear();");
        StringAssert.Contains(source, "__vb6_dynamicValues = null!;");
    }

    [TestMethod]
    public void Generate_EmitsLBoundAndUBoundWithLongDimensions()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim grid(-1 To 1, 4 To 6) As Long
                Debug.Print LBound(grid)
                Debug.Print UBound(grid, 2)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBDebug.Print(__vb6_grid.LBound(VBConversions.CLng(1L)));" );
        StringAssert.Contains(source, "VBDebug.Print(__vb6_grid.UBound(VBConversions.CLng(VBConversions.CInt(2L))));");
    }

    private static string FormatDiagnostics(CompilationAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
