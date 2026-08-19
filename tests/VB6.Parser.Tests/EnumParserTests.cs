using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class EnumParserTests
{
    private static CompilationUnitSyntax Parse(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        return result.Root;
    }

    [TestMethod]
    public void Parse_ReadsVisiaEnumValues()
    {
        var root = Parse("""
            Enum ENUM_APP_TYPE
                GUI = 2
                CUI = 3
            End Enum

            Enum ENUM_SECTION_CHARACTERISTICS
                CH_CODE = &H20
                CH_MEM_READ = &H40000000
                CH_MEM_WRITE = &H80000000
            End Enum
            """);

        Assert.AreEqual(2, root.Members.Length);

        var appType = (EnumDeclarationSyntax)root.Members[0];
        Assert.AreEqual("ENUM_APP_TYPE", appType.Identifier.Text);
        Assert.AreEqual(2, appType.Members.Length);
        Assert.AreEqual("GUI", appType.Members[0].Identifier.Text);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(appType.Members[0].Value);

        var characteristics = (EnumDeclarationSyntax)root.Members[1];
        Assert.AreEqual(3, characteristics.Members.Length);
        var writeValue = (LiteralExpressionSyntax)characteristics.Members[2].Value!;
        Assert.AreEqual("&H80000000", writeValue.LiteralToken.Text);
    }

    [TestMethod]
    public void Parse_ReadsVisibilityAndImplicitEnumValues()
    {
        var root = Parse("""
            Private Enum Flags
                None
                Read = &H1
                All = -1
            End Enum
            """);

        var declaration = (EnumDeclarationSyntax)root.Members.Single();
        Assert.AreEqual("Private", declaration.VisibilityKeyword!.Text);
        Assert.AreEqual("Flags", declaration.Identifier.Text);
        Assert.AreEqual(3, declaration.Members.Length);

        Assert.IsNull(declaration.Members[0].EqualsToken);
        Assert.IsNull(declaration.Members[0].Value);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(declaration.Members[1].Value);
        Assert.IsInstanceOfType<UnaryExpressionSyntax>(declaration.Members[2].Value);
    }
}
