using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayBinderGuardTests
{
    [TestMethod]
    public void Bind_LocalFixedArrayProducesArrayTypeAndDimensions()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 10) As Long
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var local = model.Procedures.Single().Locals.Single();
        var type = (ArrayTypeSymbol)local.Type;
        Assert.AreEqual(TypeSymbol.Long, type.ElementType);
        Assert.AreEqual(1, type.Rank);

        var declaration = (BoundVariableDeclarationStatement)model.Procedures.Single().Body.Statements.Single();
        Assert.AreEqual(1, declaration.ArrayDimensions.Length);
    }

    [TestMethod]
    public void Bind_ModuleArrayProducesArrayType()
    {
        var model = BindSource("""
            Private values(10) As Integer
            Sub Main()
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var type = (ArrayTypeSymbol)model.ModuleVariables.Single().Symbol.Type;
        Assert.AreEqual(TypeSymbol.Integer, type.ElementType);
        Assert.AreEqual(1, type.Rank);
        Assert.AreEqual(1, model.ModuleVariables.Single().ArrayDimensions.Length);
    }

    [TestMethod]
    public void Bind_ArrayParameterProducesArrayType()
    {
        var model = BindSource("""
            Function Sort(TheArray() As String) As Long
                Sort = 0
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var type = (ArrayTypeSymbol)model.Procedures.Single().Symbol.Parameters.Single().Type;
        Assert.AreEqual(TypeSymbol.String, type.ElementType);
        Assert.AreEqual(1, type.Rank);
    }

    [TestMethod]
    public void Bind_ArrayElementReadAndWriteUseElementType()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 3) As Long
                values(1) = 10
                Debug.Print values(1)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var statements = model.Procedures.Single().Body.Statements;
        var assignment = (BoundArrayElementAssignmentStatement)statements[1];
        Assert.AreEqual(TypeSymbol.Long, assignment.Expression.Type);

        var print = (BoundDebugPrintStatement)statements[2];
        var access = (BoundArrayElementExpression)print.Expression;
        Assert.AreEqual(TypeSymbol.Long, access.Type);
        Assert.AreEqual(1, access.Indices.Length);
    }

    [TestMethod]
    public void Bind_OptionBaseAppliesToImplicitLowerBound()
    {
        var model = BindSource("""
            Option Base 1
            Sub Main()
                Dim values(3) As Long
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var declaration = (BoundVariableDeclarationStatement)model.Procedures.Single().Body.Statements.Single();
        var lowerBound = (BoundLiteralExpression)declaration.ArrayDimensions.Single().LowerBound;
        Assert.AreEqual(1L, lowerBound.Value);
    }

    [TestMethod]
    public void Bind_ArrayRankMismatchProducesDiagnostic()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 3, 1 To 3) As Long
                values(1) = 10
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0027" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_ReDimEraseAndArrayBounds()
    {
        var model = BindSource("""
            Sub Main()
                Dim values() As Long
                ReDim values(2 To 4)
                Debug.Print LBound(values) + UBound(values)
                Erase values
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var statements = model.Procedures.Single().Body.Statements;
        var redim = (BoundReDimStatement)statements[1];
        Assert.AreEqual(1, redim.ArrayDimensions.Length);
        Assert.IsFalse(redim.Preserve);

        var print = (BoundDebugPrintStatement)statements[2];
        var sum = (BoundBinaryExpression)print.Expression;
        Assert.IsInstanceOfType<BoundArrayBoundExpression>(sum.Left);
        Assert.IsInstanceOfType<BoundArrayBoundExpression>(sum.Right);

        var erase = (BoundEraseStatement)statements[3];
        Assert.AreEqual("values", erase.Variables.Single().Name);
    }

    [TestMethod]
    public void Bind_ReDimPreserveSetsPreserveFlag()
    {
        var model = BindSource("""
            Sub Main()
                Dim values() As Long
                ReDim values(1 To 2)
                ReDim Preserve values(1 To 3)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var redim = (BoundReDimStatement)model.Procedures.Single().Body.Statements[2];
        Assert.IsTrue(redim.Preserve);
        Assert.AreEqual(1, redim.ArrayDimensions.Length);
    }

    [TestMethod]
    public void Bind_ForEachArrayUsesElementTypeAndForExitTarget()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(1 To 2) As Long
                Dim value As Long
                For Each value In values
                    Exit For
                Next value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var loop = (BoundForEachStatement)model.Procedures.Single().Body.Statements[2];
        Assert.AreEqual("value", loop.ControlVariable.Name);
        Assert.AreEqual(TypeSymbol.Long, loop.ElementType);
        Assert.IsInstanceOfType<BoundVariableExpression>(loop.Collection);
        var exit = (BoundExitLoopStatement)loop.Body.Statements.Single();
        Assert.AreEqual(loop.LoopId, exit.TargetLoopId);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parseResult.Diagnostics.Length,
            string.Join(Environment.NewLine, parseResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }

    private static string FormatDiagnostics(SemanticModel model) =>
        string.Join(Environment.NewLine, model.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
