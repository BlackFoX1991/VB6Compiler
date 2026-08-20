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
    public void Bind_BindsScalarByRefTypeMismatchAsCopyBackTemporary()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Long
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
        Assert.IsTrue(argument.IsByRefTemporary);
        Assert.IsNotNull(argument.CopyBackTarget);
        Assert.AreEqual(TypeSymbol.Long, argument.CopyBackTarget!.Type);
        Assert.IsInstanceOfType<BoundConversionExpression>(argument.Expression);
    }

    [TestMethod]
    public void Bind_BindsFieldAndArrayElementByRefArguments()
    {
        var model = BindSource("""
            Type Point
                X As Long
            End Type

            Sub Main()
                Dim point As Point
                Dim values(1 To 1) As Long
                Call Update(point.X)
                Call Update(values(1))
            End Sub

            Sub Update(value As Long)
                value = 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var fieldCall = (BoundInvocationStatement)main.Body.Statements[2];
        var arrayCall = (BoundInvocationStatement)main.Body.Statements[3];

        Assert.IsInstanceOfType<BoundMemberAccessExpression>(fieldCall.Arguments.Single().Expression);
        Assert.IsFalse(fieldCall.Arguments.Single().IsByRefTemporary);
        Assert.IsInstanceOfType<BoundArrayElementExpression>(arrayCall.Arguments.Single().Expression);
        Assert.IsFalse(arrayCall.Arguments.Single().IsByRefTemporary);
    }

    [TestMethod]
    public void Bind_BindsUserDefinedTypeArrayFieldElementByRefArgument()
    {
        var model = BindSource("""
            Type Point
                Values(1 To 1) As Long
            End Type

            Sub Main()
                Dim point As Point
                Call Update(point.Values(1))
            End Sub

            Sub Update(value As Long)
                value = 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements[1];

        Assert.IsInstanceOfType<BoundMemberArrayElementExpression>(invocation.Arguments.Single().Expression);
        Assert.IsFalse(invocation.Arguments.Single().IsByRefTemporary);
    }

    [TestMethod]
    public void Bind_BindsParenthesizedByRefArgumentAsTemporaryForStatementCalls()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                Call Update((x))
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
        Assert.IsTrue(argument.IsByRefTemporary);
    }

    [TestMethod]
    public void Bind_BindsCallSiteByValArgumentAsByRefTemporaryForStatementCalls()
    {
        var model = BindSource("""
            Sub Main()
                Dim x As Integer
                Call Update(ByVal x)
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
        Assert.IsTrue(argument.IsByRefTemporary);
    }

    [TestMethod]
    public void Bind_BindsByRefTemporaryInFunctionCallExpression()
    {
        var model = BindSource("""
            Function Identity(value As Integer) As Integer
                Identity = value
            End Function

            Sub Main()
                Dim x As Integer
                x = Identity(ByVal x)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var assignment = (BoundAssignmentStatement)main.Body.Statements[1];
        var invocation = (BoundInvocationExpression)assignment.Expression;
        Assert.IsTrue(invocation.Arguments.Single().IsByRefTemporary);
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
    public void Bind_BindsPropertyAccessorBodies()
    {
        var model = BindSource("""
            Private m_caption As String
            Public Event CaptionChanged(ByVal OldValue As String)

            Public Property Get Caption() As String
                Caption = m_caption
                Exit Property
            End Property

            Public Property Let Caption(ByVal value As String)
                m_caption = value
                RaiseEvent CaptionChanged(value)
            End Property
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        Assert.AreEqual(2, model.Procedures.Length);

        var getter = model.Procedures[0];
        var setter = model.Procedures[1];
        var returnAssignment = (BoundAssignmentStatement)getter.Body.Statements[0];
        var fieldAssignment = (BoundAssignmentStatement)setter.Body.Statements[0];

        Assert.IsTrue(getter.Symbol.IsFunction);
        Assert.AreEqual("Caption", getter.Symbol.Name);
        Assert.AreEqual(TypeSymbol.String, getter.Symbol.ReturnType);
        Assert.IsInstanceOfType<ReturnValueSymbol>(returnAssignment.Variable);
        Assert.IsFalse(setter.Symbol.IsFunction);
        Assert.AreEqual("m_caption", fieldAssignment.Variable.Name);
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
            Type Point
                X As Integer
            End Type

            Sub Main()
                Dim point As Point
                Call Update(point)
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

    [TestMethod]
    public void Bind_BindsLoopConditionsAndForValues()
    {
        var model = BindSource("""
            Sub Main()
                Dim i As Integer
                For i = 3 To 1 Step -1
                    Debug.Print i
                Next i

                While i < 3
                    i = i + 1
                Wend

                Do Until i = 5
                    i = i + 1
                Loop
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var body = model.Procedures.Single().Body;

        var forStatement = (BoundForStatement)body.Statements[1];
        Assert.AreEqual(TypeSymbol.Integer, forStatement.ControlVariable.Type);
        Assert.AreEqual(TypeSymbol.Integer, forStatement.InitialValue.Type);
        Assert.AreEqual(TypeSymbol.Integer, forStatement.Limit.Type);
        Assert.AreEqual(TypeSymbol.Integer, forStatement.Step.Type);

        var whileStatement = (BoundWhileStatement)body.Statements[2];
        Assert.AreEqual(TypeSymbol.Boolean, whileStatement.Condition.Type);

        var doStatement = (BoundDoStatement)body.Statements[3];
        Assert.IsTrue(doStatement.IsUntil);
        Assert.IsFalse(doStatement.ConditionIsPostTest);
        Assert.AreEqual(TypeSymbol.Boolean, doStatement.Condition!.Type);
    }

    [TestMethod]
    public void Bind_ResolvesExitForAcrossNestedDoLoop()
    {
        var model = BindSource("""
            Sub Main()
                Dim i As Integer
                For i = 1 To 3
                    Do
                        Exit For
                    Loop
                Next
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var forStatement = (BoundForStatement)model.Procedures.Single().Body.Statements[1];
        var doStatement = (BoundDoStatement)forStatement.Body.Statements.Single();
        var exitStatement = (BoundExitLoopStatement)doStatement.Body.Statements.Single();

        Assert.AreEqual(BoundLoopKind.For, exitStatement.LoopKind);
        Assert.AreEqual(forStatement.LoopId, exitStatement.TargetLoopId);
        Assert.AreNotEqual(doStatement.LoopId, exitStatement.TargetLoopId);
    }

    [TestMethod]
    public void Bind_ReportsExitOutsideMatchingLoop()
    {
        var model = BindSource("""
            Sub Main()
                Exit Do
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0015"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
