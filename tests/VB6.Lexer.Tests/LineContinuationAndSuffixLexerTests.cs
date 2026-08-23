using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class LineContinuationAndSuffixLexerTests
{
    private static SyntaxToken[] LexTokens(string source)
    {
        var result = new LexerType(SourceText.From(source, "test.bas")).Lex();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        return result.Tokens.Where(token => token.Kind != SyntaxKind.EndOfFileToken).ToArray();
    }

    [TestMethod]
    public void Lex_JoinsContinuedLines()
    {
        var tokens = LexTokens("Dim a _\r\n    As Long");

        // No NewLineToken: the continuation makes this one logical line.
        Assert.IsFalse(tokens.Any(token => token.Kind == SyntaxKind.NewLineToken));
        Assert.AreEqual(SyntaxKind.DimKeyword, tokens[0].Kind);
        Assert.AreEqual(SyntaxKind.AsKeyword, tokens[2].Kind);
        Assert.AreEqual(SyntaxKind.LongKeyword, tokens[3].Kind);
    }

    [TestMethod]
    public void Lex_KeepsRealLineBreaks()
    {
        var tokens = LexTokens("Dim a\r\nDim b");

        Assert.AreEqual(1, tokens.Count(token => token.Kind == SyntaxKind.NewLineToken));
    }

    [TestMethod]
    public void Lex_TreatsATrailingUnderscoreInsideAnIdentifierAsPartOfIt()
    {
        var tokens = LexTokens("value_\r\nnext_one");

        Assert.AreEqual("value_", tokens[0].Text);
        Assert.AreEqual(SyntaxKind.NewLineToken, tokens[1].Kind);
        Assert.AreEqual("next_one", tokens[2].Text);
    }

    [TestMethod]
    public void Lex_DropsIdentifierTypeSuffixesFromTheName()
    {
        var tokens = LexTokens("Mid$ count& flag% ratio! wide# money@");

        Assert.IsTrue(tokens.All(token => token.Kind == SyntaxKind.IdentifierToken));
        CollectionAssert.AreEqual(
            new[] { "Mid", "count", "flag", "ratio", "wide", "money" },
            tokens.Select(token => token.Text).ToArray());
    }

    [TestMethod]
    public void Lex_KeepsTheSuffixInsideTheTokenSpan()
    {
        var tokens = LexTokens("Mid$");

        // Text is the bare name, but the span still covers the suffix so the source round-trips.
        Assert.AreEqual("Mid", tokens[0].Text);
        Assert.AreEqual(4, tokens[0].Span.Length);
    }

    [TestMethod]
    public void Lex_KeepsConcatenationSeparateFromASuffix()
    {
        var tokens = LexTokens("a & b");

        Assert.AreEqual(3, tokens.Length);
        Assert.AreEqual(SyntaxKind.AmpersandToken, tokens[1].Kind);
    }

    [TestMethod]
    public void Lex_TreatsCommentedContinuationLinesAsCommentTrivia()
    {
        var tokens = LexTokens("'Call DrawLine _\r\n    x = 1\r\nvalue");

        Assert.IsFalse(tokens.Any(token => token.Text is "x" or "=" or "1"));
        Assert.IsTrue(tokens.Any(token => token.Text == "value"));
    }
}
