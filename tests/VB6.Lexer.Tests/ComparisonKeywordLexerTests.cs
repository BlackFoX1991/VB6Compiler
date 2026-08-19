using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class ComparisonKeywordLexerTests
{
    [TestMethod]
    public void Lex_RecognizesLikeAndIsKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("value lIkE pattern Or left iS right")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.IdentifierToken,
                SyntaxKind.LikeKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.OrKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.IsKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}