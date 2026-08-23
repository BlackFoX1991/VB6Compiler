using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ConditionalCompilationParserTests
{
    [TestMethod]
    public void Parse_PreservesConditionalCompilationDirectiveTokens()
    {
        var result = new ParserType(SourceText.From("#Const DEBUGMODE = 0\nSub Main()\nEnd Sub\n"))
            .ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var directive = (ConditionalCompilationDirectiveSyntax)result.Root.Members[0];
        Assert.AreEqual(SyntaxKind.HashToken, directive.HashToken.Kind);
        CollectionAssert.AreEqual(
            new[] { "Const", "DEBUGMODE", "=", "0" },
            directive.Tokens.Select(token => token.Text).ToArray());
    }
}
