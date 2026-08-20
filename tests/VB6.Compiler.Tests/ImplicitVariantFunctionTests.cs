using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ImplicitVariantFunctionTests
{
    [TestMethod]
    public void Analyze_BindsUntypedFunctionReturnAsVariant()
    {
        const string source = """
            Function Legacy()
            End Function

            Sub Main()
            End Sub
            """;

        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)));
        Assert.IsNotNull(analysis.SemanticModel);
        var function = analysis.SemanticModel!.Procedures.Single(procedure => procedure.Symbol.Name == "Legacy");
        Assert.AreEqual(TypeSymbol.Variant, function.Symbol.ReturnType);
    }
}
