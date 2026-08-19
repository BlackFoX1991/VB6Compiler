using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class RadixLiteralLexerTests
{
    private static SyntaxToken[] LexTokens(string source)
    {
        var result = new LexerType(SourceText.From(source, "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        return result.Tokens.Where(token => token.Kind != SyntaxKind.EndOfFileToken).ToArray();
    }

    [TestMethod]
    public void Lex_RecognizesHexadecimalLiterals()
    {
        var tokens = LexTokens("&H1F &hff &H0");

        Assert.AreEqual(3, tokens.Length);
        Assert.IsTrue(tokens.All(token => token.Kind == SyntaxKind.IntegerLiteralToken));
        Assert.AreEqual((short)31, (short)tokens[0].Value!);
        Assert.AreEqual((short)255, (short)tokens[1].Value!);
        Assert.AreEqual((short)0, (short)tokens[2].Value!);
        Assert.AreEqual("&H1F", tokens[0].Text);
    }

    [TestMethod]
    public void Lex_RecognizesOctalLiterals()
    {
        var tokens = LexTokens("&O17 &o7");

        Assert.AreEqual(2, tokens.Length);
        Assert.AreEqual((short)15, (short)tokens[0].Value!);
        Assert.AreEqual((short)7, (short)tokens[1].Value!);
    }

    [TestMethod]
    public void Lex_WrapsRadixLiteralsIntoSmallestSignedType()
    {
        var tokens = LexTokens("&HFFFF &H8000 &H7FFF &H10000 &HFFFFFFFF");

        // VB6 radix literals wrap instead of growing.
        Assert.AreEqual((short)-1, (short)tokens[0].Value!);
        Assert.AreEqual((short)-32768, (short)tokens[1].Value!);
        Assert.AreEqual((short)32767, (short)tokens[2].Value!);
        Assert.AreEqual(65536, (int)tokens[3].Value!);
        Assert.AreEqual(-1, (int)tokens[4].Value!);
    }

    [TestMethod]
    public void Lex_AppliesLongSuffixToRadixLiterals()
    {
        var tokens = LexTokens("&H1& &HFFFF&");

        Assert.AreEqual(1, (int)tokens[0].Value!);
        Assert.AreEqual(65535, (int)tokens[1].Value!);
        Assert.AreEqual("&HFFFF&", tokens[1].Text);
    }

    [TestMethod]
    public void Lex_AppliesTypeSuffixesToDecimalLiterals()
    {
        var tokens = LexTokens("100& 100% 100");

        Assert.AreEqual(100, (int)tokens[0].Value!);
        Assert.AreEqual((short)100, (short)tokens[1].Value!);
        Assert.AreEqual(100L, (long)tokens[2].Value!);
    }

    [TestMethod]
    public void Lex_KeepsAmpersandConcatenationIntact()
    {
        var tokens = LexTokens("a & \"x\"");

        Assert.AreEqual(3, tokens.Length);
        Assert.AreEqual(SyntaxKind.IdentifierToken, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.AmpersandToken, tokens[1].Kind);
        Assert.AreEqual(SyntaxKind.StringLiteralToken, tokens[2].Kind);
    }

    [TestMethod]
    public void Lex_ReportsRadixLiteralOutsideSuffixRange()
    {
        var result = new LexerType(SourceText.From("&H1FFFF%", "test.bas")).Lex();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6L0006", result.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Lex_ReportsDecimalLiteralOutsideSuffixRange()
    {
        var result = new LexerType(SourceText.From("70000%", "test.bas")).Lex();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6L0007", result.Diagnostics[0].Code);
    }
}
