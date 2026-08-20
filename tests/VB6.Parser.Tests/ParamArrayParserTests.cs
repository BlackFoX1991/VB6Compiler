using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ParamArrayParserTests
{
    [TestMethod]
    public void Parse_RecognizesParamArrayParameter()
    {
        const string source = """
            Sub Collect(ParamArray values() As Variant)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var parameter = sub.Parameters.Single();
        Assert.AreEqual(SyntaxKind.ParamArrayKeyword, parameter.ParamArrayKeyword!.Kind);
        Assert.IsTrue(parameter.IsParamArray);
        Assert.IsTrue(parameter.IsArray);
        Assert.AreEqual(SyntaxKind.AsKeyword, parameter.AsKeyword!.Kind);
        Assert.AreEqual(SyntaxKind.IdentifierToken, parameter.TypeToken!.Kind);
        Assert.AreEqual("Variant", parameter.TypeToken.Text);
    }
}
