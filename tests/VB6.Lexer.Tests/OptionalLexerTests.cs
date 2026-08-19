using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class OptionalLexerTests
{
    [TestMethod]
    public void Lex_RecognizesOptionalCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("oPtIoNaL ByVal value As Long")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.OptionalKeyword,
                SyntaxKind.ByValKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.AsKeyword,
                SyntaxKind.LongKeyword,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
