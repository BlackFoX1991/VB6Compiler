using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class OptionalParameterBinderTests
{
    [TestMethod]
    public void Bind_UntypedOptionalParameterAsVariant()
    {
        const string source = """
            Sub Configure(Optional value)
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var parameter = model.Procedures.Single().Symbol.Parameters.Single();
        Assert.AreEqual(TypeSymbol.Variant, parameter.Type);
        Assert.IsTrue(parameter.IsOptional);
    }

    [TestMethod]
    public void Bind_FillsOmittedOptionalByValArgumentWithDefaultValue()
    {
        const string source = """
            Sub Configure(Optional ByVal retries As Long = 3)
            End Sub

            Sub Main()
                Configure
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.AreEqual("retries", argument.Parameter!.Name);
        Assert.AreEqual(TypeSymbol.Long, argument.Expression.Type);
        var conversion = (BoundConversionExpression)argument.Expression;
        Assert.AreEqual(TypeSymbol.Long, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Integer, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_FillsOmittedOptionalVariantArgumentWithMissing()
    {
        const string source = """
            Sub Configure(Optional ByVal value)
            End Sub

            Sub Main()
                Configure
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.AreEqual(TypeSymbol.Variant, argument.Expression.Type);
        var literal = (BoundLiteralExpression)argument.Expression;
        Assert.AreEqual(VBVariantLiteral.Missing, literal.Value);
    }

    [TestMethod]
    public void Bind_FillsOmittedOptionalByRefArgumentWithTemporaryDefault()
    {
        const string source = """
            Sub Configure(Optional value)
            End Sub

            Sub Main()
                Configure
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.AreEqual(TypeSymbol.Variant, argument.Expression.Type);
        Assert.IsTrue(argument.IsByRefTemporary);
        var literal = (BoundLiteralExpression)argument.Expression;
        Assert.AreEqual(VBVariantLiteral.Missing, literal.Value);
    }

    [TestMethod]
    public void Bind_FillsOmittedOptionalByRefArgumentInFunctionCallExpression()
    {
        const string source = """
            Function Configure(Optional value) As Long
            End Function

            Sub Main()
                Dim result As Long
                result = Configure()
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var assignment = (BoundAssignmentStatement)main.Body.Statements[1];
        var invocation = (BoundInvocationExpression)assignment.Expression;
        Assert.IsTrue(invocation.Arguments.Single().IsByRefTemporary);
    }
}
