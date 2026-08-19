using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ImplicitVariantAnalysisTests
{
    [TestMethod]
    public void Analyze_DefaultsUntypedDeclarationsToVariant()
    {
        var analysis = VBCompilation.Create("""
            Public Current

            Sub Main()
                Dim first, second As Long
                Dim values(1 To 2)
                Static cached
            End Sub
            """, "test.bas").Analyze();

        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0020"));

        var current = analysis.SemanticModel.ModuleVariables.Single(variable => variable.Symbol.Name == "Current");
        Assert.AreSame(TypeSymbol.Variant, current.Symbol.Type);

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "first").Type);
        Assert.AreSame(TypeSymbol.Long, main.Locals.Single(local => local.Name == "second").Type);
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "cached").Type);

        var values = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type;
        Assert.AreSame(TypeSymbol.Variant, values.ElementType);
        Assert.AreEqual(1, values.Rank);
    }

    [TestMethod]
    public void GenerateCSharp_EmitsImplicitVariantStorageAndArrays()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value
                Dim values(1 To 2)
                value = 42
                values(1) = value
                Debug.Print values(1)
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "object? __vb6_value = default;");
        StringAssert.Contains(generation.Source, "VBArray<object?> __vb6_values = new VBArray<object?>");

        using var peStream = new MemoryStream();
        var emitResult = new VB6.CodeGen.CSharp.CSharpAssemblyEmitter().Emit(
            generation.Source,
            "GeneratedImplicitVariantProgram",
            peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }
}
