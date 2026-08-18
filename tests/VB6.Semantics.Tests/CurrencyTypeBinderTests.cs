using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class CurrencyTypeBinderTests
{
    [TestMethod]
    public void Bind_RecognizesCurrencyAndConvertsAssignments()
    {
        var model = BindSource("""
            Sub Main()
                Dim amount As Currency
                amount = 1.25
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Currency, procedure.Locals.Single().Type);

        var assignment = (BoundAssignmentStatement)procedure.Body.Statements[1];
        var conversion = (BoundConversionExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Currency, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Double, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_CurrencyDominatesRegularArithmeticButDivisionIsDouble()
    {
        var model = BindSource("""
            Sub Main()
                Dim amount As Currency
                amount = 1.25
                Debug.Print amount + 2.5
                Debug.Print amount / 2
                Debug.Print amount \ 2
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var statements = model.Procedures.Single().Body.Statements;

        var add = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[2]).Expression;
        Assert.AreEqual(TypeSymbol.Currency, add.Type);
        Assert.AreEqual(TypeSymbol.Currency, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Currency, add.Right.Type);

        var divide = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[3]).Expression;
        Assert.AreEqual(TypeSymbol.Double, divide.Type);
        Assert.AreEqual(TypeSymbol.Double, divide.Left.Type);
        Assert.AreEqual(TypeSymbol.Double, divide.Right.Type);

        var integerDivide = (BoundBinaryExpression)((BoundDebugPrintStatement)statements[4]).Expression;
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Type);
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Left.Type);
        Assert.AreEqual(TypeSymbol.Long, integerDivide.Right.Type);
    }

    [TestMethod]
    public void Bind_AllowsCurrencyParametersAndFunctionReturns()
    {
        var model = BindSource("""
            Function AddTax(ByVal value As Currency) As Currency
                AddTax = value + 1.25
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Currency, procedure.Symbol.ReturnType);
        Assert.AreEqual(TypeSymbol.Currency, procedure.Symbol.Parameters.Single().Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
