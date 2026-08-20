using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ParamArrayBinderTests
{
    [TestMethod]
    public void Bind_BindsParamArraySymbolAndPacksRestArguments()
    {
        const string source = """
            Sub Collect(ByVal prefix As String, ParamArray values() As Variant)
            End Sub

            Sub Main()
                Collect "p", 1, "x"
                Collect "empty"
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var collect = model.Procedures.Single(procedure => procedure.Symbol.Name == "Collect");
        var parameter = collect.Symbol.Parameters[1];
        Assert.IsTrue(parameter.IsParamArray);
        var parameterType = (ArrayTypeSymbol)parameter.Type;
        Assert.AreEqual(TypeSymbol.Variant, parameterType.ElementType);

        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var filled = (BoundInvocationStatement)main.Body.Statements[0];
        var filledParamArray = (BoundParamArrayExpression)filled.Arguments[1].Expression;
        Assert.AreEqual(2, filledParamArray.Values.Length);
        Assert.IsTrue(filledParamArray.Values.All(value => value.Type == TypeSymbol.Variant));

        var empty = (BoundInvocationStatement)main.Body.Statements[1];
        var emptyParamArray = (BoundParamArrayExpression)empty.Arguments[1].Expression;
        Assert.AreEqual(0, emptyParamArray.Values.Length);
    }

    [TestMethod]
    public void Bind_ReportsParamArrayThatIsNotLast()
    {
        const string source = """
            Sub Collect(ParamArray values() As Variant, ByVal tail As Long)
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual("VB6S0042", model.Diagnostics.Single().Code);
    }
}
