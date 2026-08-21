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
    private static BoundExpression Unwrap(BoundExpression expression) =>
        expression is BoundConversionExpression conversion ? Unwrap(conversion.Expression) : expression;
}
