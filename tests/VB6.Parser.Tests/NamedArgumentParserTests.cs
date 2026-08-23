using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class NamedArgumentParserTests
{
    [TestMethod]
    public void Parse_PreservesNamedArgumentNameAndExpression()
    {
        var result = new ParserType(SourceText.From("Sub Main()\n    Configure count:=3, title:=\"named\"\nEnd Sub", "test.bas"))
            .ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)procedure.Statements.Single();
        var count = (NamedArgumentExpressionSyntax)invocation.Arguments[0];

        Assert.AreEqual("count", count.NameToken.Text);
        Assert.AreEqual("3", ((LiteralExpressionSyntax)count.Expression).LiteralToken.Text);
    }
}
