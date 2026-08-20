using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class VariantBinderTests
{
    [TestMethod]
    public void Bind_VariantLiteralsAndBuiltins()
    {
        var model = BindSource("""
            Sub Main()
                Dim value
                value = Null
                Debug.Print VarType(value)
                Debug.Print IsEmpty(Empty)
                Debug.Print IsNull(value)
                Debug.Print IsError(CVErr(5))
                Debug.Print IsMissing(Missing)
                Debug.Print IsNumeric(10)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Variant, procedure.Locals.Single().Type);
        var assignment = (BoundAssignmentStatement)procedure.Body.Statements[1];
        Assert.AreEqual(TypeSymbol.Variant, assignment.Expression.Type);
        var varType = (BoundDebugPrintStatement)procedure.Body.Statements[2];
        Assert.AreEqual(TypeSymbol.Integer, varType.Expression.Type);
        var isEmpty = (BoundDebugPrintStatement)procedure.Body.Statements[3];
        Assert.AreEqual(TypeSymbol.Boolean, isEmpty.Expression.Type);
        var isNull = (BoundDebugPrintStatement)procedure.Body.Statements[4];
        Assert.AreEqual(TypeSymbol.Boolean, isNull.Expression.Type);
        var isError = (BoundDebugPrintStatement)procedure.Body.Statements[5];
        Assert.AreEqual(TypeSymbol.Boolean, isError.Expression.Type);
        var isMissing = (BoundDebugPrintStatement)procedure.Body.Statements[6];
        Assert.AreEqual(TypeSymbol.Boolean, isMissing.Expression.Type);
        var isNumeric = (BoundDebugPrintStatement)procedure.Body.Statements[7];
        Assert.AreEqual(TypeSymbol.Boolean, isNumeric.Expression.Type);
    }

    [TestMethod]
    public void Bind_VariantBinaryOperators()
    {
        var model = BindSource("""
            Sub Main()
                Dim value
                value = 2
                Debug.Print value + 3
                Debug.Print value & "x"
                Debug.Print value = 2
                Debug.Print value And 3
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        var add = (BoundBinaryExpression)((BoundDebugPrintStatement)procedure.Body.Statements[2]).Expression;
        var concat = (BoundBinaryExpression)((BoundDebugPrintStatement)procedure.Body.Statements[3]).Expression;
        var equal = (BoundBinaryExpression)((BoundDebugPrintStatement)procedure.Body.Statements[4]).Expression;
        var and = (BoundBinaryExpression)((BoundDebugPrintStatement)procedure.Body.Statements[5]).Expression;

        Assert.AreEqual(TypeSymbol.Variant, add.Type);
        Assert.AreEqual(TypeSymbol.Variant, concat.Type);
        Assert.AreEqual(TypeSymbol.Variant, equal.Type);
        Assert.AreEqual(TypeSymbol.Variant, and.Type);
    }

    [TestMethod]
    public void Bind_ConvertsVariantComparisonConditionsToBoolean()
    {
        var model = BindSource("""
            Sub Main()
                Dim value
                value = 2
                If value = 2 Then
                    Debug.Print "ok"
                End If
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        var ifStatement = (BoundIfStatement)procedure.Body.Statements[2];
        Assert.AreEqual(TypeSymbol.Boolean, ifStatement.Condition.Type);
        var condition = (BoundConversionExpression)ifStatement.Condition;
        Assert.AreEqual(TypeSymbol.Variant, condition.Expression.Type);
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
