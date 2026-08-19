using VB6.Compiler;
using VB6.Semantics;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class VariantEqualityCodeGenTests
{
    [TestMethod]
    public void GenerateCSharp_LowersVariantScalarEqualityWithoutBinderCoercion()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 3
                Debug.Print value = 3
                Debug.Print 3 = value
                Debug.Print value = "3"
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        StringAssert.Contains(generation.Source, "VBOperators.Equal(__vb6_value, VBConversions.CInt(3L))");
        StringAssert.Contains(generation.Source, "VBOperators.Equal(VBConversions.CInt(3L), __vb6_value)");
        StringAssert.Contains(generation.Source, "VBOperators.Equal(__vb6_value, \"3\")");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(generation.Source, "GeneratedVariantEqualityProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }

    [TestMethod]
    public void Analyze_RestoresVariantAndScalarOperandsForEquality()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 3
                Debug.Print value = 3
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var debugPrint = (BoundDebugPrintStatement)main.Body.Statements.Last();
        var equality = (BoundBinaryExpression)debugPrint.Expression;
        Assert.AreSame(TypeSymbol.Boolean, equality.Type);
        Assert.AreSame(TypeSymbol.Variant, equality.Left.Type);
        Assert.AreSame(TypeSymbol.Integer, equality.Right.Type);
    }

    [TestMethod]
    public void GenerateCSharp_KeepsVariantToVariantEqualityGuarded()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim leftValue
                Dim rightValue
                Debug.Print leftValue = rightValue
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
    }

    [TestMethod]
    public void GenerateCSharp_KeepsOtherVariantComparisonsGuarded()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value
                value = 3
                Debug.Print value <> 3
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
    }
}
