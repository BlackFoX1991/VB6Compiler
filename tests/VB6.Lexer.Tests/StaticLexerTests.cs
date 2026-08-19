using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class StaticLexerTests
{
    [TestMethod]
    public void Lex_RecognizesStaticKeywordCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("sTaTiC total As Long")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.StaticKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.AsKeyword,
                SyntaxKind.LongKeyword,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual("sTaTiC", result.Tokens[0].Text);
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}