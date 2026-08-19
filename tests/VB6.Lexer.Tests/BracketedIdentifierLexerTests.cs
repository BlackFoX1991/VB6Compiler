using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class BracketedIdentifierLexerTests
{
    [TestMethod]
    public void Lex_ReturnsBracketedNamesAsIdentifiersWithoutBrackets()
    {
        var result = new LexerType(SourceText.From(
            "[GR_Fill_None] [End]", "Module1.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var identifiers = result.Tokens
            .Where(token => token.Kind == SyntaxKind.IdentifierToken)
            .ToArray();
        Assert.AreEqual(2, identifiers.Length);
        Assert.AreEqual("GR_Fill_None", identifiers[0].Text);
        Assert.AreEqual("End", identifiers[1].Text);
    }

    [TestMethod]
    public void Lex_ReportsUnterminatedBracketedIdentifier()
    {
        var result = new LexerType(SourceText.From("[Missing\n", "Module1.bas")).Lex();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6L0008", result.Diagnostics[0].Code);
    }
}
