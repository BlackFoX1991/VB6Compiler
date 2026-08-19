using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ComparisonOperatorParserTests
{
    [TestMethod]
    public void Parse_RecognizesLikeAsComparisonOperator()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = value Like \"A*\"");

        Assert.AreEqual(SyntaxKind.LikeKeyword, expression.OperatorToken.Kind);
        Assert.IsInstanceOfType<NameExpressionSyntax>(expression.Left);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(expression.Right);
    }

    [TestMethod]
    public void Parse_RecognizesIsAsComparisonOperator()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = left Is right");

        Assert.AreEqual(SyntaxKind.IsKeyword, expression.OperatorToken.Kind);
        Assert.IsInstanceOfType<NameExpressionSyntax>(expression.Left);
        Assert.IsInstanceOfType<NameExpressionSyntax>(expression.Right);
    }

    [TestMethod]
    public void Parse_LikeBindsMoreTightlyThanAnd()
    {
        var expression = (BinaryExpressionSyntax)ParseAssignmentExpression("result = value Like \"A*\" And other Like \"B*\"");

        Assert.AreEqual(SyntaxKind.AndKeyword, expression.OperatorToken.Kind);
        Assert.AreEqual(SyntaxKind.LikeKeyword, ((BinaryExpressionSyntax)expression.Left).OperatorToken.Kind);
        Assert.AreEqual(SyntaxKind.LikeKeyword, ((BinaryExpressionSyntax)expression.Right).OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_CaseIsStillUsesRelationalCaseSyntax()
    {
        const string source = """
            Sub Main()
                Dim value As Integer
                Select Case value
                    Case Is >= 10
                        Debug.Print value
                End Select
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var select = (SelectCaseStatementSyntax)procedure.Statements[1];
        Assert.IsInstanceOfType<CaseRelationalClauseSyntax>(select.Cases.Single().Clauses.Single());
    }

    private static ExpressionSyntax ParseAssignmentExpression(string assignment)
    {
        var source = $"""
            Sub Main()
                Dim result As Boolean
                Dim value As String
                Dim other As String
                Dim left As String
                Dim right As String
                {assignment}
            End Sub
            """;
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return ((AssignmentStatementSyntax)procedure.Statements[^1]).Expression;
    }
}