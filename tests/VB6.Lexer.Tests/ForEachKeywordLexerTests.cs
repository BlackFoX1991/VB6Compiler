using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class ForEachKeywordLexerTests
{
    [TestMethod]
    public void Lex_RecognizesEachAndInKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("For eAcH item iN values")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.ForKeyword,
                SyntaxKind.EachKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.InKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
