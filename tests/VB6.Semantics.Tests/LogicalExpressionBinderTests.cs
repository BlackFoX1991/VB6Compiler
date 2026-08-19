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
    public void Bind_BindsNumericOperandsAsBitwise()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Integer
                value = 12 And 10
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var and = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.AndKeyword, and.OperatorKind);
        Assert.AreEqual(TypeSymbol.Integer, and.Type);
    }

    [TestMethod]
    public void Bind_WidensBitwiseResultToTheCommonIntegerType()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Long
                Dim wide As Long
                wide = 70000
                value = wide Or 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        Assert.AreEqual(TypeSymbol.Long, assignment.Expression.Type);
    }

    [TestMethod]
    public void Bind_BindsNumericNotAsBitwise()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Integer
                value = Not 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var not = (BoundUnaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.NotKeyword, not.OperatorKind);
        Assert.AreEqual(TypeSymbol.Integer, not.Type);
    }

    [TestMethod]
    public void Bind_WidensEqvOnBytesToInteger()
    {
        // The complement produced by Eqv does not fit the unsigned Byte range.
        var model = BindSource("""
            Sub Main()
                Dim left As Byte
                Dim right As Byte
                Dim value As Integer
                value = left Eqv right
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        Assert.AreEqual(TypeSymbol.Integer, assignment.Expression.Type);
    }

    [TestMethod]
    public void Bind_ReportsNonNumericBitwiseOperand()
    {
        var model = BindSource("""
            Sub Main()
                Dim text As String
                Dim value As Integer
                value = text And 1
            End Sub
            """);

        Assert.IsTrue(model.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0018"));
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
