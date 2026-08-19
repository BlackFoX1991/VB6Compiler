using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class BracketedIdentifierParserTests
{
    [TestMethod]
    public void Parse_AllowsBracketedEnumMemberNames()
    {
        const string source = """
            Public Enum GradientDirection
                [GR_Fill_None] = -1
                [gr_Fill_Horizontal] = 0
                [GR_Fill_Vertical] = 1
            End Enum
            """;

        var result = new ParserType(SourceText.From(source, "Module1.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var declaration = (EnumDeclarationSyntax)result.Root.Members.Single();
        CollectionAssert.AreEqual(
            new[] { "GR_Fill_None", "gr_Fill_Horizontal", "GR_Fill_Vertical" },
            declaration.Members.Select(member => member.Identifier.Text).ToArray());
    }
}
