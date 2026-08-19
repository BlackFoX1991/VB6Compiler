using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class ArrayCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsFixedArrayWithVbBoundsAndElementAccess()
    {
        var analysis = VBCompilation.Create("""
            Option Base 1
            Sub Main()
                Dim values(3) As Long
                values(1) = 42
                Debug.Print values(1)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<int> __vb6_values = new VBArray<int>(");
        StringAssert.Contains(source, "new VBArrayBound(VBConversions.CLng(1L), VBConversions.CLng(VBConversions.CInt(3L)))");
        StringAssert.Contains(source, "__vb6_values[VBConversions.CLng(VBConversions.CInt(1L))] = VBConversions.CLng(VBConversions.CInt(42L));");
        StringAssert.Contains(source, "VBDebug.Print(__vb6_values[VBConversions.CLng(VBConversions.CInt(1L))]);");
    }

    [TestMethod]
    public void Generate_EmitsMultidimensionalExplicitBounds()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim grid(-2 To 2, 1 To 4) As Integer
                grid(-2, 4) = 7
                Debug.Print grid(-2, 4)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "VBArray<short> __vb6_grid = new VBArray<short>(");
        StringAssert.Contains(source, "new VBArrayBound(VBConversions.CLng(VBOperators.NegateInteger(VBConversions.CInt(2L))), VBConversions.CLng(VBConversions.CInt(2L)))");
        StringAssert.Contains(source, "new VBArrayBound(VBConversions.CLng(VBConversions.CInt(1L)), VBConversions.CLng(VBConversions.CInt(4L)))");
        StringAssert.Contains(source, "__vb6_grid[");
    }

    [TestMethod]
    public void Generate_EmitsArrayParametersAsVbArrays()
    {
        var analysis = VBCompilation.Create("""
            Function First(values() As Long) As Long
                First = values(1)
            End Function

            Sub Main()
                Dim values(1 To 2) As Long
                values(1) = 5
                Debug.Print First(values)
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
        var source = new CSharpGenerator().Generate(analysis.SemanticModel!);

        StringAssert.Contains(source, "ref VBArray<int> __vb6_arg_values");
        StringAssert.Contains(source, "__vb6_return = __vb6_arg_values[VBConversions.CLng(VBConversions.CInt(1L))];");
        StringAssert.Contains(source, "__vb6_First(ref __vb6_values)");
    }

    private static string FormatDiagnostics(CompilationAnalysis analysis) =>
        string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
