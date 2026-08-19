using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class OptionDirectiveLexerTests
{
    [TestMethod]
    public void Lex_RecognizesOptionDirectiveKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("oPtIoN bAsE 0\nOpTiOn CoMpArE Text")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.OptionKeyword,
                SyntaxKind.BaseKeyword,
                SyntaxKind.IntegerLiteralToken,
                SyntaxKind.NewLineToken,
                SyntaxKind.OptionKeyword,
                SyntaxKind.CompareKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
