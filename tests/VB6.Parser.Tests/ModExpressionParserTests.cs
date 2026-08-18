using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ModExpressionParserTests
{
    [TestMethod]
    public void Parse_OrdersModBetweenIntegerDivisionAndAddition()
    {
        const string source = """
            Sub Main()
                value = 20 \ 3 Mod 2 + 1
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var assignment = (AssignmentStatementSyntax)sub.Statements.Single();
        var add = (BinaryExpressionSyntax)assignment.Expression;

        Assert.AreEqual(SyntaxKind.PlusToken, add.OperatorToken.Kind);
        var mod = (BinaryExpressionSyntax)add.Left;
        Assert.AreEqual(SyntaxKind.ModKeyword, mod.OperatorToken.Kind);
        Assert.AreEqual(SyntaxKind.BackslashToken, ((BinaryExpressionSyntax)mod.Left).OperatorToken.Kind);
    }
}
