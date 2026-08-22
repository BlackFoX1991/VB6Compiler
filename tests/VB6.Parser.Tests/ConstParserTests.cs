using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ConstParserTests
{
    [TestMethod]
    public void Parse_RecognizesProcedureLevelConstant()
    {
        const string source = """
            Sub Main()
                Const RetryCount As Long = 3
                Debug.Print RetryCount
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var constant = (ConstStatementSyntax)sub.Statements[0];
        Assert.AreEqual("RetryCount", constant.Identifier.Text);
        Assert.AreEqual("Long", constant.TypeToken!.Text);
    }
}
