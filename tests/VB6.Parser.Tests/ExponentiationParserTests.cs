using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ExponentiationParserTests
{
    [TestMethod]
    public void Parse_ExponentiationBindsMoreTightlyThanUnaryMinus()
    {
        var expression = ParseAssignmentExpression("result = -2 ^ 2");

        var negation = (UnaryExpressionSyntax)expression;
        Assert.AreEqual(SyntaxKind.MinusToken, negation.OperatorToken.Kind);
        var power = (BinaryExpressionSyntax)negation.Operand;
        Assert.AreEqual(SyntaxKind.CaretToken, power.OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_RepeatedExponentiationIsLeftAssociative()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = 3 ^ 3 ^ 3");

        Assert.AreEqual(SyntaxKind.CaretToken, expression.OperatorToken.Kind);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(expression.Left);
        Assert.AreEqual(
            SyntaxKind.CaretToken,
            ((BinaryExpressionSyntax)expression.Left).OperatorToken.Kind);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(expression.Right);
    }

    [TestMethod]
    public void Parse_AllowsSignedExponentWithoutParentheses()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = 2 ^ -3");

        Assert.AreEqual(SyntaxKind.CaretToken, expression.OperatorToken.Kind);
        var exponent = (UnaryExpressionSyntax)expression.Right;
        Assert.AreEqual(SyntaxKind.MinusToken, exponent.OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_ExponentiationBindsMoreTightlyThanMultiplication()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = 2 * 3 ^ 2");

        Assert.AreEqual(SyntaxKind.StarToken, expression.OperatorToken.Kind);
        var power = (BinaryExpressionSyntax)expression.Right;
        Assert.AreEqual(SyntaxKind.CaretToken, power.OperatorToken.Kind);
    }

    private static ExpressionSyntax ParseAssignmentExpression(string assignment)
    {
        var source = $"""
            Sub Main()
                Dim result As Double
                {assignment}
            End Sub
            """;
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return ((AssignmentStatementSyntax)procedure.Statements[1]).Expression;
    }
}