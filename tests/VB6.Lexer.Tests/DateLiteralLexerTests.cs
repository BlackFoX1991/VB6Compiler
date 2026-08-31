using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

/// <summary>
/// A '#' introduces a date literal, a file number and a Double type suffix, so the lexer only
/// commits to a date when a closing '#' follows on the same line and the text between the two
/// parses as a date or time. These cases pin both directions of that decision.
/// </summary>
[TestClass]
public sealed class DateLiteralLexerTests
{
    [TestMethod]
    public void Lex_RecognizesADateLiteralAsAnOleAutomationDate()
    {
        var tokens = LexWithoutEndOfFile("#1/2/2000#");

        Assert.AreEqual(1, tokens.Length);
        Assert.AreEqual(SyntaxKind.DateLiteralToken, tokens[0].Kind);
        Assert.AreEqual(new DateTime(2000, 1, 2).ToOADate(), (double)tokens[0].Value!);
        Assert.AreEqual("#1/2/2000#", tokens[0].Text);
    }

    [TestMethod]
    public void Lex_ReadsTheDateLiteralInUsOrderRegardlessOfTheMachineLocale()
    {
        var tokens = LexWithoutEndOfFile("#3/4/2001#");

        Assert.AreEqual(new DateTime(2001, 3, 4).ToOADate(), (double)tokens[0].Value!);
    }

    [TestMethod]
    public void Lex_RecognizesADateLiteralWithATimePart()
    {
        var tokens = LexWithoutEndOfFile("#1/2/2000 3:04:05 PM#");

        Assert.AreEqual(SyntaxKind.DateLiteralToken, tokens[0].Kind);
        Assert.AreEqual(new DateTime(2000, 1, 2, 15, 4, 5).ToOADate(), (double)tokens[0].Value!);
    }

    [TestMethod]
    public void Lex_KeepsAFileNumberHashAsAHashToken()
    {
        var tokens = LexWithoutEndOfFile("Close #1");

        Assert.AreEqual(SyntaxKind.HashToken, tokens[1].Kind);
    }

    [TestMethod]
    public void Lex_DoesNotTakeAHashInsideAStringAsTheEndOfADateLiteral()
    {
        var tokens = LexWithoutEndOfFile("""
            Print #1, "a#b#c"
            """);

        Assert.AreEqual(SyntaxKind.HashToken, tokens[1].Kind);
        Assert.IsFalse(tokens.Any(token => token.Kind == SyntaxKind.DateLiteralToken));
    }

    [TestMethod]
    public void Lex_LeavesDoubleTypeSuffixesAlone()
    {
        var tokens = LexWithoutEndOfFile("5# - 2#");

        Assert.IsFalse(tokens.Any(token => token.Kind == SyntaxKind.DateLiteralToken));
    }

    private static SyntaxToken[] LexWithoutEndOfFile(string source) =>
        new LexerType(SourceText.From(source, "test.bas")).Lex()
            .Tokens
            .Where(token => token.Kind is not SyntaxKind.EndOfFileToken and not SyntaxKind.NewLineToken)
            .ToArray();
}
