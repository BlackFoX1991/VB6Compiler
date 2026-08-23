using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class FloatingLiteralLexerTests
{
    [TestMethod]
    public void Lex_RecognizesDecimalAndExponentLiterals()
    {
        var result = new LexerType(SourceText.From("1.5 .5 1E3 1.2E-3", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var tokens = result.Tokens.Where(token => token.Kind != SyntaxKind.EndOfFileToken).ToArray();
        Assert.AreEqual(4, tokens.Length);
        Assert.IsTrue(tokens.All(token => token.Kind == SyntaxKind.FloatingLiteralToken));
        Assert.AreEqual(1.5d, (double)tokens[0].Value!, 0d);
        Assert.AreEqual(0.5d, (double)tokens[1].Value!, 0d);
        Assert.AreEqual(1000d, (double)tokens[2].Value!, 0d);
        Assert.AreEqual(0.0012d, (double)tokens[3].Value!, 0d);
    }

    [TestMethod]
    public void Lex_RecognizesSingleAndDoubleLiteralTypeSuffixes()
    {
        var result = new LexerType(SourceText.From("1! 1#", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var tokens = result.Tokens.Where(token => token.Kind != SyntaxKind.EndOfFileToken).ToArray();
        Assert.AreEqual(2, tokens.Length);
        Assert.AreEqual(1f, (float)tokens[0].Value!, 0f);
        Assert.AreEqual(1d, (double)tokens[1].Value!, 0d);
        Assert.AreEqual("1!", tokens[0].Text);
        Assert.AreEqual("1#", tokens[1].Text);
    }

    [TestMethod]
    public void Lex_KeepsDebugPrintDotSeparateFromFloatingLiteral()
    {
        var result = new LexerType(SourceText.From("Debug.Print 1.5", "test.bas")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var kinds = result.Tokens.Select(token => token.Kind).ToArray();
        CollectionAssert.Contains(kinds, SyntaxKind.DotToken);
        CollectionAssert.Contains(kinds, SyntaxKind.FloatingLiteralToken);
    }
}
