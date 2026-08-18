using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class BinderTests
{
    [TestMethod]
    public void Bind_ResolvesLocalVariablesCaseInsensitively()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Integer
                VALUE = 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        var declaration = (BoundVariableDeclarationStatement)procedure.Body.Statements[0];
        var assignment = (BoundAssignmentStatement)procedure.Body.Statements[1];

        Assert.AreEqual(declaration.Variable, assignment.Variable);
        Assert.AreEqual(TypeSymbol.Integer, assignment.Variable.Type);
    }

    [TestMethod]
    public void Bind_InsertsExplicitVbConversionForAssignments()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                x = "10"
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        Assert.IsTrue(assignment.Expression is BoundConversionExpression);
        var conversion = (BoundConversionExpression)assignment.Expression;

        Assert.AreEqual(TypeSymbol.Integer, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.String, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_ReportsUndefinedVariable()
    {
        var model = BindSource("""
            Sub Main()
                missing = 10
            End Sub
            """);

        Assert.AreEqual(1, model.Diagnostics.Length);
        Assert.AreEqual("VB6S0001", model.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Bind_ProducesBooleanConditionForComparison()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                x = 10
                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var ifStatement = (BoundIfStatement)model.Procedures.Single().Body.Statements[2];

        Assert.AreEqual(TypeSymbol.Boolean, ifStatement.Condition.Type);
        Assert.IsTrue(ifStatement.Condition is BoundBinaryExpression);
    }

    [TestMethod]
    public void Bind_ResolvesProcedureCallsCaseInsensitively()
    {
        var model = BindSource("""
            Sub Main()
                HELPER
            End Sub

            Sub Helper()
                Debug.Print 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var helper = model.Procedures.Single(procedure => procedure.Symbol.Name == "Helper");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();

        Assert.AreEqual(helper.Symbol, invocation.Procedure);
    }

    [TestMethod]
    public void Bind_ReportsUndefinedProcedure()
    {
        var model = BindSource("""
            Sub Main()
                MissingProcedure
            End Sub
            """);

        Assert.AreEqual(1, model.Diagnostics.Length);
        Assert.AreEqual("VB6S0005", model.Diagnostics[0].Code);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
