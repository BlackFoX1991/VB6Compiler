using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class FileNumberLexerTests
{
    [TestMethod]
    public void Lex_ProducesAHashTokenForFileNumbers()
    {
        var tokens = Lex("Close #1");

        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.HashToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.IntegerLiteralToken, tokens[2].Kind);
    }

    /// <summary>
    /// A trailing hash on an identifier is the Double type suffix and is consumed with the
    /// identifier, so it must not turn into a file number token.
    /// </summary>
    [TestMethod]
    public void Lex_KeepsTheDoubleTypeSuffixAttachedToTheIdentifier()
    {
        var tokens = Lex("value#");

        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual("value", tokens[0].Text, "The suffix is consumed with the identifier, not kept in its text.");
        Assert.AreEqual(SyntaxKind.EndOfFileToken, tokens[1].Kind, "The suffix must not surface as a file number.");
    }

    private static IReadOnlyList<SyntaxToken> Lex(string source)
    {
        var lexer = new LexerType(SourceText.From(source));
        var result = lexer.Lex();
        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        return result.Tokens;
    }
}
