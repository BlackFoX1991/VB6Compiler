using VB6.Parser;
using VB6.Syntax.Nodes;
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
            Option Explicit

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
    public void Bind_UsesDynamicDispatchForVariantMemberStatements()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Variant
                value.Navigate 1
                value.ListImages(1).Draw 0, 0, 0, 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var statements = model.Procedures.Single().Body.Statements
            .OfType<BoundMemberInvocationStatement>()
            .ToArray();
        Assert.AreEqual(2, statements.Length);
        Assert.IsTrue(statements.All(statement => statement.Procedure.IsLateBound));
        Assert.AreEqual("Navigate", statements[0].Procedure.Name);
        Assert.AreEqual("Draw", statements[1].Procedure.Name);
    }

    [TestMethod]
    public void Bind_CombinesWhitespaceSeparatedIndexedMemberArguments()
    {
        var model = BindSource("""
            Sub Main()
                Dim form As Form
                form.Cells (1, 2), 0
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var invocation = model.Procedures.Single().Body.Statements
            .OfType<BoundMemberInvocationStatement>()
            .Single();

        // Das Beispiel war frueher PSet. Das ist keine spaet gebundene Membermethode, sondern eine
        // eigene Zeichenanweisung -- und weil der Host kein PSet-Mitglied kennt, lief dieser Pfad
        // ins Leere. Die hier geprueffte Zusage ist die Argumentregel, nicht der Membername.
        Assert.AreEqual("Cells", invocation.Procedure.Name);
        var dynamicArguments = invocation.Arguments.Single().Expression as BoundArrayLiteralExpression;
        Assert.IsNotNull(dynamicArguments);
        Assert.AreEqual(3, dynamicArguments.Elements.Length);
        Assert.IsTrue(invocation.Procedure.IsLateBound);
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
    public void CreateProcedureSymbol_RecordsModuleVisibility()
    {
        var text = SourceText.From("""
            Public Sub Exported()
            End Sub

            Global Function GlobalValue() As Long
            End Function

            Private Sub Hidden()
            End Sub

            Function DefaultValue() As Long
            End Function
            """, "visibility.bas");
        var root = new ParserType(text).ParseCompilationUnit().Root;

        var symbols = root.Members
            .Select(member => member switch
            {
                SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                _ => null
            })
            .Where(symbol => symbol is not null)
            .Cast<ProcedureSymbol>()
            .ToDictionary(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase);

        Assert.IsTrue(symbols["Exported"].IsPublic);
        Assert.IsTrue(symbols["GlobalValue"].IsPublic);
        Assert.IsFalse(symbols["Hidden"].IsPublic);
        Assert.IsTrue(symbols["DefaultValue"].IsPublic);
    }

    [TestMethod]
    public void Bind_RecordsOptionPrivateModuleExportPolicy()
    {
        var model = BindSource("""
            Option Private Module

            Public Sub Main()
            End Sub
            """);

        Assert.IsTrue(model.IsPrivateModule);
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
    public void Bind_InfersIdentifierTypeSuffixesForDeclaredAndImplicitVariables()
    {
        var model = BindSource("""
            Sub Main()
                Dim declared&
                implicit& = 0
                Call Update(declared&)
                Call Update(implicit&)
            End Sub

            Sub Update(value&)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var assignment = main.Body.Statements.OfType<BoundAssignmentStatement>().Single();
        Assert.AreEqual(TypeSymbol.Long, assignment.Variable.Type);

        var update = model.Procedures.Single(procedure => procedure.Symbol.Name == "Update");
        Assert.AreEqual(TypeSymbol.Long, update.Symbol.Parameters.Single().Type);

        var invocations = main.Body.Statements.OfType<BoundInvocationStatement>().ToArray();
        Assert.AreEqual(2, invocations.Length);
        Assert.IsTrue(invocations.All(invocation => invocation.Arguments.Single().Expression.Type == TypeSymbol.Long));
        Assert.IsTrue(invocations.All(invocation => !invocation.Arguments.Single().RequiresByRefTemporary));
    }

    [TestMethod]
    public void Bind_PassesConstantsByRefThroughATemporary()
    {
        var model = BindSource("""
            Const DefaultTimeout = 1

            Sub Main()
                Call Update(DefaultTimeout)
            End Sub

            Sub Update(value As Long)
                value = 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var main = model.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        var argument = invocation.Arguments.Single();
        Assert.IsTrue(argument.RequiresByRefTemporary);
        Assert.IsInstanceOfType<BoundConversionExpression>(argument.Expression);
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

    /// <summary>
    /// The two halves of the VB6 rule. A literal has no storage, so VB6 supplies a temporary and
    /// drops the write-back. A variable of the wrong type would need the write-back to go
    /// somewhere, so VB6 reports a ByRef argument type mismatch instead of converting.
    /// </summary>
    [TestMethod]
    public void Bind_PassesNonVariableByRefArgumentsThroughATemporary()
    {
        var literalModel = BindSource("""
            Sub Main()
                Call Update(10)
            End Sub

            Sub Update(value As Integer)
            End Sub
            """);

        Assert.AreEqual(0, literalModel.Diagnostics.Length);
        var main = literalModel.Procedures.Single(procedure => procedure.Symbol.Name == "Main");
        var invocation = (BoundInvocationStatement)main.Body.Statements.Single();
        Assert.IsTrue(invocation.Arguments.Single().RequiresByRefTemporary);
    }

    [TestMethod]
    public void Bind_ReportsByRefArgumentTypeMismatch()
    {
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

    /// <summary>
    /// When the caller supplies a module variable table, the bound model has to report those very
    /// symbols. Procedure bodies bind against the caller's table, so returning equal-looking copies
    /// leaves the two halves of the model unmatchable by identity - which a name-based backend
    /// never notices and an identity-based one breaks on.
    /// </summary>
    [TestMethod]
    public void Bind_ReusesTheSuppliedModuleVariableSymbols()
    {
        var text = SourceText.From("""
            Dim Counter As Long

            Sub Main()
                Counter = 1
            End Sub
            """, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var supplied = Binder.CreateModuleVariableSymbols(text, parseResult.Root)
            .ToDictionary(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase);
        var model = new Binder(text).BindCompilationUnit(
            parseResult.Root,
            new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase),
            supplied);

        var reported = model.ModuleVariables.Single(variable =>
            string.Equals(variable.Symbol.Name, "Counter", StringComparison.OrdinalIgnoreCase));
        Assert.AreSame(supplied["Counter"], reported.Symbol);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }

    /// <summary>
    /// Every bound statement carries where it was written. The mapping is referential - the
    /// position travels with the node the binder produced - so it cannot drift the way a later
    /// pass that re-derives positions by walking the tree in parallel would.
    /// </summary>
    [TestMethod]
    public void Bind_AttachesTheSourcePositionOfEveryStatement()
    {
        var text = SourceText.From("""
            Sub Main()
                Dim value As Long
                value = 1
                Debug.Print value
            End Sub
            """, "Module1.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        var statements = model.Procedures.Single().Body.Statements;
        Assert.AreEqual(3, statements.Length);
        foreach (var statement in statements)
        {
            Assert.IsNotNull(statement.SourceLocation, $"{statement.Kind} has no source location.");
            Assert.AreEqual("Module1.bas", statement.SourceLocation!.FilePath);
        }

        // Each position names the statement's own first token, in source order.
        var source = text.ToString();
        var starts = statements.Select(statement => statement.SourceLocation!.Span.Start).ToArray();
        CollectionAssert.AreEqual(starts.OrderBy(start => start).ToArray(), starts);
        StringAssert.StartsWith(source[starts[0]..], "Dim value");
        StringAssert.StartsWith(source[starts[1]..], "value = 1");
        StringAssert.StartsWith(source[starts[2]..], "Debug.Print");
    }
}
