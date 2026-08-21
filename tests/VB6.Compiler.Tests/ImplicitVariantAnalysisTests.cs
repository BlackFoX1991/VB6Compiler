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

        var values = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type;
        Assert.AreSame(TypeSymbol.Variant, values.ElementType);
        Assert.AreEqual(1, values.Rank);
    }

    [TestMethod]
    public void Analyze_DefaultsUntypedStaticToVariantWhileKeepingLifetimeGuard()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Static cached
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0020"));
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0021"));

        var main = analysis.SemanticModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "cached").Type);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesImplicitVariantStorageAndArrays()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim value
                Dim values(1 To 2)
                value = 42
                values(1) = value
                Debug.Print values(1)
            End Sub
            """, "test.bas");

        var main = VB6TestIr.Procedures(program).Single(procedure => procedure.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "value").Type);
        Assert.AreSame(
            TypeSymbol.Variant,
            ((ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type).ElementType);

        // An untyped declaration has to survive all the way into a running program, not just into
        // the type table: Variant storage is where a wrong element type shows up as a crash.
        Assert.AreEqual("42", VB6TestProgram.Run("""
            Sub Main()
                Dim value
                Dim values(1 To 2)
                value = 42
                values(1) = value
                Debug.Print values(1)
            End Sub
            """, "test.bas").Trim());
    }
}
