using VB6.Compiler;
using VB6.Semantics;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class VariantMultiplyCodeGenTests
{
    [TestMethod]
    public void GenerateCSharp_LowersVariantMultiplyAndRestoresTargetConversions()
    {
        var generation = VBCompilation.Create("""
            Sub Consume(ByVal value As Integer)
                Debug.Print value
            End Sub

            Sub Main()
                Dim value
                Dim result As Long
                value = 3
                Debug.Print value * 4
                result = value * 5
                Consume value * 6
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        StringAssert.Contains(generation.Source, "VBOperators.MultiplyInteger(__vb6_value, VBConversions.CInt(4L))");
        StringAssert.Contains(generation.Source, "VBConversions.CLng(VBOperators.MultiplyInteger(__vb6_value, VBConversions.CInt(5L)))");
        StringAssert.Contains(generation.Source, "VBConversions.CInt(VBOperators.MultiplyInteger(__vb6_value, VBConversions.CInt(6L)))");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(generation.Source, "GeneratedVariantMultiplyProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }

    [TestMethod]
    public void Analyze_MarksVariantMultiplyResultAsVariant()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 3
                Debug.Print value * 4
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var debugPrint = (BoundDebugPrintStatement)main.Body.Statements.Last();
        Assert.AreSame(TypeSymbol.Variant, debugPrint.Expression.Type);
    }

    [TestMethod]
    public void GenerateCSharp_KeepsOtherVariantOperatorsGuarded()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 3
                Debug.Print value + 1
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
    }
}
