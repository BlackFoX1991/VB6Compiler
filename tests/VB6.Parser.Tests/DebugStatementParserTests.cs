using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class DebugStatementParserTests
{
    [TestMethod]
    public void Parse_RecognizesDebugAssertWithoutMakingAssertAReservedIdentifier()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                Debug.Assert False
            End Sub
            """, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (DebugAssertStatementSyntax)sub.Statements.Single();

        Assert.AreEqual(SyntaxKind.DebugKeyword, statement.DebugKeyword.Kind);
        Assert.AreEqual(SyntaxKind.DotToken, statement.DotToken.Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, statement.AssertIdentifier.Kind);
        Assert.AreEqual("Assert", statement.AssertIdentifier.Text);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(statement.Expression);
    }
}
