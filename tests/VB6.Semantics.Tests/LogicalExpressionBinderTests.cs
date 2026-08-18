using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class LogicalExpressionBinderTests
{
    [TestMethod]
    public void Bind_BindsBooleanLogicalOperators()
    {
        var model = BindSource("""
            Sub Main()
                Dim flag As Boolean
                flag = True And Not False Or False Xor True Eqv False Imp True
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        Assert.AreEqual(TypeSymbol.Boolean, assignment.Expression.Type);

        var imp = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.ImpKeyword, imp.OperatorKind);
        Assert.AreEqual(TypeSymbol.Boolean, imp.Left.Type);
        Assert.AreEqual(TypeSymbol.Boolean, imp.Right.Type);
    }

    [TestMethod]
    public void Bind_ReportsNumericLogicalOperandsUntilBitwiseSemanticsAreImplemented()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Integer
                value = 1 And 2
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0018"));
    }

    [TestMethod]
    public void Bind_ReportsNumericNotUntilBitwiseSemanticsAreImplemented()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Integer
                value = Not 1
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0017"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
