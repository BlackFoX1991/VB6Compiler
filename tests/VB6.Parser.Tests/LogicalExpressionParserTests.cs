using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class LogicalExpressionParserTests
{
    [TestMethod]
    public void Parse_RecognizesBooleanLiterals()
    {
        const string source = """
            Sub Main()
                flag = True
                other = False
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var trueAssignment = (AssignmentStatementSyntax)sub.Statements[0];
        var falseAssignment = (AssignmentStatementSyntax)sub.Statements[1];

        Assert.AreEqual(SyntaxKind.TrueKeyword, ((LiteralExpressionSyntax)trueAssignment.Expression).LiteralToken.Kind);
        Assert.AreEqual(SyntaxKind.FalseKeyword, ((LiteralExpressionSyntax)falseAssignment.Expression).LiteralToken.Kind);
    }

    [TestMethod]
    public void Parse_UsesVbLogicalOperatorPrecedence()
    {
        const string source = """
            Sub Main()
                flag = Not True = False And True Or False Xor True Eqv False Imp True
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length);

        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var assignment = (AssignmentStatementSyntax)sub.Statements.Single();

        var imp = (BinaryExpressionSyntax)assignment.Expression;
        Assert.AreEqual(SyntaxKind.ImpKeyword, imp.OperatorToken.Kind);

        var eqv = (BinaryExpressionSyntax)imp.Left;
        Assert.AreEqual(SyntaxKind.EqvKeyword, eqv.OperatorToken.Kind);

        var xor = (BinaryExpressionSyntax)eqv.Left;
        Assert.AreEqual(SyntaxKind.XorKeyword, xor.OperatorToken.Kind);

        var or = (BinaryExpressionSyntax)xor.Left;
        Assert.AreEqual(SyntaxKind.OrKeyword, or.OperatorToken.Kind);

        var and = (BinaryExpressionSyntax)or.Left;
        Assert.AreEqual(SyntaxKind.AndKeyword, and.OperatorToken.Kind);

        var not = (UnaryExpressionSyntax)and.Left;
        Assert.AreEqual(SyntaxKind.NotKeyword, not.OperatorToken.Kind);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(not.Operand);
        Assert.AreEqual(SyntaxKind.EqualsToken, ((BinaryExpressionSyntax)not.Operand).OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_OrdersIntegerDivisionAndConcatenationCorrectly()
    {
        const string source = """
            Sub Main()
                value = 8 / 2 \ 2 + 1 & "x"
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length);

        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var assignment = (AssignmentStatementSyntax)sub.Statements.Single();
        var concat = (BinaryExpressionSyntax)assignment.Expression;

        Assert.AreEqual(SyntaxKind.AmpersandToken, concat.OperatorToken.Kind);
        var add = (BinaryExpressionSyntax)concat.Left;
        Assert.AreEqual(SyntaxKind.PlusToken, add.OperatorToken.Kind);
        var integerDivide = (BinaryExpressionSyntax)add.Left;
        Assert.AreEqual(SyntaxKind.BackslashToken, integerDivide.OperatorToken.Kind);
        Assert.AreEqual(SyntaxKind.SlashToken, ((BinaryExpressionSyntax)integerDivide.Left).OperatorToken.Kind);
    }
}
