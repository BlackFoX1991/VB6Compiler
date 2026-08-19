using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class OptionDirectiveLexerTests
{
    [TestMethod]
    public void Lex_KeepsOptionDirectiveWordsAsIdentifiers()
    {
        var result = new LexerType(SourceText.From("oPtIoN bAsE 0\nOpTiOn CoMpArE Text")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.OptionKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.IntegerLiteralToken,
                SyntaxKind.NewLineToken,
                SyntaxKind.OptionKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.IdentifierToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual("bAsE", result.Tokens[1].Text);
        Assert.AreEqual("CoMpArE", result.Tokens[5].Text);
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
