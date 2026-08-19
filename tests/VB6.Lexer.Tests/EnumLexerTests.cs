using VB6.Syntax;
using VB6.Syntax.Text;
using LexerType = VB6.Lexer.Lexer;

namespace VB6.Lexer.Tests;

[TestClass]
public sealed class EnumLexerTests
{
    [TestMethod]
    public void Lex_RecognizesEnumCaseInsensitively()
    {
        var result = new LexerType(SourceText.From("eNuM End")).Lex();

        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.EnumKeyword,
                SyntaxKind.EndKeyword,
                SyntaxKind.EndOfFileToken
            },
            result.Tokens.Select(token => token.Kind).ToArray());
        Assert.AreEqual(0, result.Diagnostics.Length);
    }
}
