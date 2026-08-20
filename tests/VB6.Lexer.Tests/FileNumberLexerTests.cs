using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class FileNumberLexerTests
{
    [TestMethod]
    public void Lex_RecognizesHashAsFileNumberMarker()
    {
        var result = new LexerType(SourceText.From("Put #1, 1, value", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var hash = result.Tokens.Single(token => token.Kind == SyntaxKind.HashToken);
        Assert.AreEqual("#", hash.Text);
    }

    [TestMethod]
    public void Lex_ConsumesHashAsExplicitDoubleLiteralSuffix()
    {
        var result = new LexerType(SourceText.From("value = -1#", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var literal = result.Tokens.Single(token => token.Kind == SyntaxKind.FloatingLiteralToken);
        Assert.AreEqual("1#", literal.Text);
        Assert.AreEqual(1d, literal.Value);
        Assert.IsFalse(result.Tokens.Any(token => token.Kind == SyntaxKind.HashToken));
    }
}
