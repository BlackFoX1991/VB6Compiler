using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class CurrencyLiteralBinderTests
{
    [TestMethod]
    public void Bind_CurrencySuffixProducesCurrencyExpression()
    {
        var text = SourceText.From("""
            Sub Main()
                Debug.Print 12.3456@
            End Sub
            """, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();

        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        var model = new Binder(text).BindCompilationUnit(parseResult.Root);
        Assert.AreEqual(0, model.Diagnostics.Length);

        var print = (BoundDebugPrintStatement)model.Procedures.Single().Body.Statements.Single();
        var literal = (BoundLiteralExpression)print.Expression;
        Assert.AreEqual(TypeSymbol.Currency, literal.Type);
        Assert.AreEqual(12.3456m, (decimal)literal.Value!);
    }
}
