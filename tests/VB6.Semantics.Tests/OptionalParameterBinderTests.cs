using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class OptionalParameterBinderTests
{
    [TestMethod]
    public void Bind_FillsAnOmittedOptionalArgumentWithItsDeclaredDefault()
    {
        const string source = """
            Sub Configure(Optional retries As Long = 3)
            End Sub

            Sub Main()
                Configure
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length, string.Join(", ", model.Diagnostics.Select(d => d.Message)));

        // The call carries the declared default rather than nothing at all.
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.IsTrue(argument.Parameter!.IsOptional);
        Assert.AreEqual(3L, Convert.ToInt64(((BoundLiteralExpression)Unwrap(argument.Expression)).Value));
    }

    [TestMethod]
    public void Bind_DefaultsAnUntypedOptionalParameterToVariantAndMissing()
    {
        const string source = """
            Sub Consume(Optional value)
            End Sub

            Sub Main()
                Consume
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length, string.Join(", ", model.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var consume = model.Procedures.Single(procedure => procedure.Symbol.Name == "Consume");
        Assert.AreSame(TypeSymbol.Variant, consume.Symbol.Parameters.Single().Type);

        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.IsInstanceOfType<BoundInvocationExpression>(argument.Expression);
        var missing = (BoundInvocationExpression)argument.Expression;
        Assert.AreEqual(VBIntrinsicKind.Missing, missing.Procedure.IntrinsicKind);
    }

    [TestMethod]
    public void Bind_RepresentsParamArrayAsVariantArrayAndCollectsExtraArguments()
    {
        const string source = """
            Sub Collect(ByVal prefix As String, ParamArray values() As Variant)
            End Sub

            Sub Main()
                Collect "x", 1, "two", True
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length, string.Join(", ", model.Diagnostics.Select(d => d.Message)));
        var collect = model.Procedures.Single(procedure => procedure.Symbol.Name == "Collect");
        var parameter = collect.Symbol.Parameters[1];
        Assert.IsTrue(parameter.IsParamArray);
        Assert.AreEqual(TypeSymbol.Variant, ((ArrayTypeSymbol)parameter.Type).ElementType);

        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        Assert.AreEqual(2, invocation.Arguments.Length);
        var array = (BoundArrayLiteralExpression)invocation.Arguments[1].Expression;
        Assert.AreEqual(3, array.Elements.Length);
    }

    [TestMethod]
    public void Bind_ReportsInvalidParamArrayDeclarations()
    {
        const string source = """
            Sub NotLast(ParamArray values() As Variant, ByVal suffix As Long)
            End Sub

            Sub HasModifier(Optional ParamArray values() As Variant)
            End Sub

            Sub HasWrongType(ParamArray values() As Long)
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        CollectionAssert.AreEquivalent(
            new[] { "VB6S0062", "VB6S0063", "VB6S0064" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }
    private static BoundExpression Unwrap(BoundExpression expression) =>
        expression is BoundConversionExpression conversion ? Unwrap(conversion.Expression) : expression;
}
