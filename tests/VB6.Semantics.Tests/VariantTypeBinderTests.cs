using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class VariantTypeBinderTests
{
    [TestMethod]
    public void Bind_RecognizesExplicitVariantValuesArraysAndParameters()
    {
        var text = SourceText.From("""
            Sub Consume(ByVal value As Variant)
            End Sub

            Sub Main()
                Dim value As Variant
                Dim values(1 To 2) As Variant
            End Sub
            """, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        Assert.AreSame(TypeSymbol.Variant, TypeSymbol.Lookup("Variant"));

        var consume = model.Procedures.Single(procedure => procedure.Symbol.Name == "Consume");
        Assert.AreSame(TypeSymbol.Variant, consume.Symbol.Parameters.Single().Type);

        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        Assert.AreSame(TypeSymbol.Variant, main.Locals.Single(local => local.Name == "value").Type);
        var array = (ArrayTypeSymbol)main.Locals.Single(local => local.Name == "values").Type;
        Assert.AreSame(TypeSymbol.Variant, array.ElementType);
        Assert.AreEqual(1, array.Rank);
    }
}
