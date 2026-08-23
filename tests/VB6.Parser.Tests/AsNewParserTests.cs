using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class AsNewParserTests
{
    [TestMethod]
    public void Parse_PreservesAsNewDeclarator()
    {
        const string source = """
            Sub Main()
                Dim item As New Widget
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var declarator = ((DimStatementSyntax)procedure.Statements.Single()).FirstDeclarator;

        Assert.IsNotNull(declarator.NewKeyword);
        Assert.AreEqual("Widget", declarator.TypeName!.Text);
    }
}
