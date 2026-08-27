using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class LexerTests
{
    [TestMethod]
    public void Lex_RecognizesKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("option EXPLICIT Sub end")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.OptionKeyword,
                SyntaxKind.ExplicitKeyword,
                SyntaxKind.SubKeyword,
                SyntaxKind.EndKeyword,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_RecognizesDeclareKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("dEcLaRe LIB alias")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.DeclareKeyword,
                SyntaxKind.LibKeyword,
                SyntaxKind.AliasKeyword,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_RecognizesReDimKeywordsCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("rEdIm PRESERVE values")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.ReDimKeyword,
                SyntaxKind.PreserveKeyword,
                SyntaxKind.IdentifierToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_PreservesCommentAsTrivia()
    {
        var result = new LexerType(SourceText.From("Dim x ' comment\r\nAs Integer")).Lex();
        var newLine = result.Tokens.Single(token => token.Kind == SyntaxKind.NewLineToken);

        Assert.IsTrue(newLine.LeadingTrivia.Any(trivia =>
            trivia.Kind == SyntaxTriviaKind.Comment && trivia.Text == "' comment"));
    }

    [TestMethod]
    public void Lex_DecodesEscapedQuotesInStrings()
    {
        var result = new LexerType(SourceText.From("\"Hello \"\"VB6\"\"\"")).Lex();
        var token = result.Tokens[0];

        Assert.AreEqual(SyntaxKind.StringLiteralToken, token.Kind);
        Assert.AreEqual("Hello \"VB6\"", token.Value);
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_ReportsBadCharacter()
    {
        var result = new LexerType(SourceText.From("?", "test.bas")).Lex();

        Assert.AreEqual(SyntaxKind.BadToken, result.Tokens[0].Kind);
        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6L0001", result.Diagnostics[0].Code);
        Assert.AreEqual("test.bas", result.Diagnostics[0].FilePath);
    }

    [TestMethod]
    public void Lex_RecognizesComparisonOperators()
    {
        var result = new LexerType(SourceText.From("<= <> >= < > =")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.LessOrEqualsToken,
                SyntaxKind.LessGreaterToken,
                SyntaxKind.GreaterOrEqualsToken,
                SyntaxKind.LessToken,
                SyntaxKind.GreaterToken,
                SyntaxKind.EqualsToken,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
    }

    [TestMethod]
    [DataRow("VB6L0002", "\"unterminated")]
    [DataRow("VB6L0003", "999999999999999999999999")]
    [DataRow("VB6L0004", "1e9999")]
    [DataRow("VB6L0007", "40000%")]
    public void Lex_ReportsMalformedNumericAndStringLiterals(string code, string source)
    {
        var result = new LexerType(SourceText.From(source, "test.bas")).Lex();

        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == code),
            $"Expected {code}, got: {string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code))}; tokens: {string.Join(", ", result.Tokens.Select(token => $"{token.Kind}:{token.Text}"))}");
    }
}
