using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

/// <summary>
/// <c>Rem</c>, BASIC's older comment introducer.
///
/// It is recognised here rather than in the parser because the comment text is ordinary prose: an
/// apostrophe or an unpaired quote inside it would already have been lexed by the time a parser
/// could react, and an unpaired quote is not something the parser can take back.
///
/// The cost of scanning in the lexer is that <c>Rem</c> is not a keyword in every position --
/// <c>Remainder</c> is an identifier, and so is a <c>Rem</c> that is not where a statement could
/// start. Those are the cases that pin the rule.
/// </summary>
[TestClass]
public sealed class RemCommentLexerTests
{
    [TestMethod]
    public void Lex_PreservesRemAsCommentTrivia()
    {
        var result = new LexerType(SourceText.From("Rem a note\r\nDim x")).Lex();
        var newLine = result.Tokens.First(token => token.Kind == SyntaxKind.NewLineToken);

        Assert.IsTrue(
            newLine.LeadingTrivia.Any(trivia =>
                trivia.Kind == SyntaxTriviaKind.Comment && trivia.Text == "Rem a note"),
            "Rem am Zeilenanfang ist Kommentar-Trivia.");
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_TakesRemCommentTextVerbatim()
    {
        // The reason this belongs in the lexer at all.
        var result = new LexerType(SourceText.From("Rem it's got an unpaired \" quote\r\nDim x")).Lex();

        Assert.AreEqual(0, result.Diagnostics.Length, "Kommentartext erzeugt keine Lexerdiagnose.");
        Assert.IsFalse(
            result.Tokens.Any(token => token.Kind == SyntaxKind.StringLiteralToken),
            "Aus dem Kommentartext entsteht kein String-Literal.");
    }

    [TestMethod]
    public void Lex_KeepsRemAfterAColonOrLineNumberAComment()
    {
        foreach (var source in new[] { "x = 1: Rem note\r\n", "20 Rem note\r\n" })
        {
            var result = new LexerType(SourceText.From(source)).Lex();
            Assert.IsTrue(
                result.Tokens.Any(token => token.LeadingTrivia.Any(trivia =>
                    trivia.Kind == SyntaxTriviaKind.Comment && trivia.Text == "Rem note")),
                source);
        }
    }

    [TestMethod]
    public void Lex_KeepsALongerWordStartingWithRemAnIdentifier()
    {
        var result = new LexerType(SourceText.From("Remainder = 5")).Lex();

        Assert.IsTrue(
            result.Tokens.Any(token =>
                token.Kind == SyntaxKind.IdentifierToken && token.Text == "Remainder"),
            "Remainder bleibt ein Bezeichner.");
        Assert.IsFalse(
            result.Tokens.Any(token => token.LeadingTrivia.Any(trivia =>
                trivia.Kind == SyntaxTriviaKind.Comment)),
            "Remainder erzeugt keinen Kommentar.");
    }

    [TestMethod]
    public void Lex_DoesNotStartACommentWhereNoStatementCanBegin()
    {
        var result = new LexerType(SourceText.From("x = Rem")).Lex();

        Assert.IsFalse(
            result.Tokens.Any(token => token.LeadingTrivia.Any(trivia =>
                trivia.Kind == SyntaxTriviaKind.Comment)),
            "Rem hinter '=' steht nicht an einem Anweisungsanfang.");
    }
}
