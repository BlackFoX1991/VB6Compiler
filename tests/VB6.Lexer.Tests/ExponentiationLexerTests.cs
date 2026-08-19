using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class ExponentiationLexerTests
{
    [TestMethod]
    public void Lex_RecognizesCaretOperator()
    {
        var result = new LexerType(SourceText.From("2 ^ 3")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.IntegerLiteralToken,
                SyntaxKind.CaretToken,
                SyntaxKind.IntegerLiteralToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}