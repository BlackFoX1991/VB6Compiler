using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class CurrencyLiteralLexerTests
{
    [TestMethod]
    public void Lex_RecognizesCurrencySuffixAndBankersRoundsToFourPlaces()
    {
        var result = new LexerType(SourceText.From("1.23445@ 1.23455@ 5@", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var tokens = result.Tokens.Where(token => token.Kind != SyntaxKind.EndOfFileToken).ToArray();
        Assert.AreEqual(3, tokens.Length);
        Assert.IsTrue(tokens.All(token => token.Kind == SyntaxKind.FloatingLiteralToken));
        Assert.AreEqual(1.2344m, (decimal)tokens[0].Value!);
        Assert.AreEqual(1.2346m, (decimal)tokens[1].Value!);
        Assert.AreEqual(5m, (decimal)tokens[2].Value!);
        Assert.AreEqual("5@", tokens[2].Text);
    }

    [TestMethod]
    public void Lex_ReportsOutOfRangeCurrencyLiteral()
    {
        var result = new LexerType(SourceText.From("922337203685477.5808@", "test.bas")).Lex();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6L0005", result.Diagnostics[0].Code);
    }
}
