using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ImplicitVariantFunctionParserTests
{
    [TestMethod]
    public void Parse_FunctionWithoutAsDefaultsReturnTypeToVariant()
    {
        const string source = """
            Function Legacy(value As Long)
                Legacy = value
            End Function
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual("Variant", function.ReturnTypeToken.Text);
        Assert.AreEqual(0, function.AsKeyword.Span.Length);
        Assert.AreEqual(0, function.ReturnTypeToken.Span.Length);
    }
}
