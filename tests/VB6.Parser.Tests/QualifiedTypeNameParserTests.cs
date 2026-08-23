using VB6.Syntax.Nodes;
using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class QualifiedTypeNameParserTests
{
    [TestMethod]
    public void Parse_PreservesQualifiedParameterTypeName()
    {
        const string source = """
            Public Sub NodeClick(ByVal Node As MSComctlLib.Node)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.cls")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var parameter = procedure.Parameters.Single();

        Assert.AreEqual("MSComctlLib", parameter.TypeToken!.Text);
        Assert.AreEqual("MSComctlLib.Node", parameter.TypeName!.Text);
        Assert.AreEqual(3, parameter.TypeName.Tokens.Length);
        Assert.AreEqual(SyntaxKind.DotToken, parameter.TypeName.Tokens[1].Kind);
    }

    [TestMethod]
    public void Parse_PreservesQualifiedVariableTypeName()
    {
        const string source = """
            Sub Main()
                Dim Node As MSComctlLib.Node
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var declaration = (DimStatementSyntax)procedure.Statements.Single();

        Assert.AreEqual("MSComctlLib.Node", declaration.FirstDeclarator.TypeName!.Text);
    }
}
