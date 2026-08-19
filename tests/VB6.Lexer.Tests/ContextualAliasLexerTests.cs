using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class ContextualAliasLexerTests
{
    [TestMethod]
    public void Lex_TreatsAliasAsIdentifierOutsideDeclareAliasClause()
    {
        const string source = """
            Sub AddImport(Optional Alias As String)
                Debug.Print Alias
            End Sub
            """;

        var result = new LexerType(SourceText.From(source, "Module1.bas")).Lex();
        var aliasTokens = result.Tokens
            .Where(token => string.Equals(token.Text, "Alias", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.AreEqual(2, aliasTokens.Length);
        Assert.IsTrue(aliasTokens.All(token => token.Kind == SyntaxKind.IdentifierToken));
        Assert.AreEqual(0, result.Diagnostics.Length);
    }

    [TestMethod]
    public void Lex_KeepsAliasKeywordAcrossDeclareLineContinuations()
    {
        const string source = """
            Public Declare Function GetWindowLong _
                Lib "user32" _
                Alias "GetWindowLongA" _
                (ByVal hWnd As Long) As Long
            """;

        var result = new LexerType(SourceText.From(source, "Module1.bas")).Lex();
        var aliasToken = result.Tokens.Single(token =>
            string.Equals(token.Text, "Alias", StringComparison.OrdinalIgnoreCase));

        Assert.AreEqual(SyntaxKind.AliasKeyword, aliasToken.Kind);
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
