using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class DecimalTypeBinderTests
{
    [TestMethod]
    public void Bind_RecognizesDecimalAndConvertsAssignments()
    {
        var model = BindSource("""
            Sub Main()
                Dim amount As Decimal
                amount = 1.25
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Decimal, procedure.Locals.Single().Type);

        var assignment = (BoundAssignmentStatement)procedure.Body.Statements[1];
        var conversion = (BoundConversionExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Decimal, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Double, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_DecimalDominatesArithmeticAndDivision()
    {
        var model = BindSource("""
            Sub Main()
                Dim amount As Decimal
                amount = 1.25
                Debug.Print amount + 2
                Debug.Print amount / 2
                Debug.Print amount \ 2
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var statements = model.Procedures.Single().Body.Statements;

        var add = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[2]).Expression;
        Assert.AreEqual(TypeSymbol.Decimal, add.Type);
        Assert.AreEqual(TypeSymbol.Decimal, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Decimal, add.Right.Type);

        var divide = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[3]).Expression;
        Assert.AreEqual(TypeSymbol.Decimal, divide.Type);
        Assert.AreEqual(TypeSymbol.Decimal, divide.Left.Type);
        Assert.AreEqual(TypeSymbol.Decimal, divide.Right.Type);

        var integerDivide = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[4]).Expression;
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Type);
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Left.Type);
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Right.Type);
    }

    [TestMethod]
    public void Bind_AllowsDecimalParametersAndFunctionReturns()
    {
        var model = BindSource("""
            Function AddTax(ByVal value As Decimal) As Decimal
                AddTax = value + 1.25
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Decimal, procedure.Symbol.ReturnType);
        Assert.AreEqual(TypeSymbol.Decimal, procedure.Symbol.Parameters.Single().Type);
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
