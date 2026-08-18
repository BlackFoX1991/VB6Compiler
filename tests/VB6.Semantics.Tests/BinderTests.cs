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

    [TestMethod]
    public void Bind_UsesByRefByDefaultAndPreservesExplicitByVal()
    {
        var model = BindSource("""
            Sub Update(value As Integer, ByVal copy As Integer)
                value = copy
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();

        Assert.AreEqual(ParameterPassingMode.ByRef, procedure.Symbol.Parameters[0].PassingMode);
        Assert.AreEqual(ParameterPassingMode.ByVal, procedure.Symbol.Parameters[1].PassingMode);
        Assert.AreEqual(TypeSymbol.Integer, procedure.Symbol.Parameters[0].Type);
    }

    [TestMethod]
    public void Bind_BindsByRefVariableArgument()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                Call Update(x)
            End Sub

            Sub Update(value As Integer)
                value = 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements[1];
        var argument = invocation.Arguments.Single();

        Assert.AreEqual(ParameterPassingMode.ByRef, argument.Parameter!.PassingMode);
        Assert.IsInstanceOfType<BoundVariableExpression>(argument.Expression);
    }

    [TestMethod]
    public void Bind_ConvertsByValArguments()
    {
        var model = BindSource("""
            Sub Main()
                Call Consume("10")
            End Sub

            Sub Consume(ByVal value As Integer)
                Debug.Print value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();

        Assert.IsInstanceOfType<BoundConversionExpression>(invocation.Arguments.Single().Expression);
    }

    [TestMethod]
    public void Bind_ReportsInvalidByRefArguments()
    {
        var literalModel = BindSource("""
            Sub Main()
                Call Update(10)
            End Sub

            Sub Update(value As Integer)
            End Sub
            """);
        Assert.IsTrue(literalModel.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0007"));

        var mismatchModel = BindSource("""
            Sub Main()
                Dim text As String
                Call Update(text)
            End Sub

            Sub Update(value As Integer)
            End Sub
            """);
        Assert.IsTrue(mismatchModel.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0008"));
    }

    [TestMethod]
    public void Bind_ReportsArgumentCountMismatch()
    {
        var model = BindSource("""
            Sub Main()
                Call Update()
            End Sub

            Sub Update(value As Integer)
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0006"));
    }

    [TestMethod]
    public void Bind_BindsFunctionReturnSlotAndInvocationExpression()
    {
        var model = BindSource("""
            Function Add(ByVal left As Integer, ByVal right As Integer) As Integer
                Add = left + right
            End Function

            Sub Main()
                Dim result As Integer
                result = Add(5, 7)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var function = model.Procedures.Single(procedure => procedure.Symbol.Name == "Add");
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");

        Assert.IsTrue(function.Symbol.IsFunction);
        Assert.AreEqual(TypeSymbol.Integer, function.Symbol.ReturnType);
        var returnAssignment = (BoundAssignmentStatement)function.Body.Statements.Single();
        Assert.IsInstanceOfType<ReturnValueSymbol>(returnAssignment.Variable);

        var callAssignment = (BoundAssignmentStatement)main.Body.Statements[1];
        var invocation = (BoundInvocationExpression)callAssignment.Expression;
        Assert.AreEqual(function.Symbol, invocation.Procedure);
        Assert.AreEqual(TypeSymbol.Integer, invocation.Type);
    }

    [TestMethod]
    public void Bind_ReportsSubUsedAsExpression()
    {
        var model = BindSource("""
            Sub Helper()
            End Sub

            Sub Main()
                Dim result As Integer
                result = Helper()
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0010"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
