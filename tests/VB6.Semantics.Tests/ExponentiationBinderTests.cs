using VB6.Parser;
using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ExponentiationBinderTests
{
    [TestMethod]
    public void Bind_ExponentiationConvertsNumericOperandsToDouble()
    {
        var model = BindSource("""
            Sub Main()
                Dim result As Double
                result = 2 ^ 3
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var power = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.CaretToken, power.OperatorKind);
        Assert.AreEqual(TypeSymbol.Double, power.ResultType);
        Assert.AreEqual(TypeSymbol.Double, power.Left.Type);
        Assert.AreEqual(TypeSymbol.Double, power.Right.Type);
        Assert.IsInstanceOfType<BoundConversionExpression>(power.Left);
        Assert.IsInstanceOfType<BoundConversionExpression>(power.Right);
    }

    [TestMethod]
    public void Bind_RejectsNonNumericExponentiation()
    {
        var model = BindSource("""
            Sub Main()
                Dim result As Double
                result = "2" ^ 3
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0022"));
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
}