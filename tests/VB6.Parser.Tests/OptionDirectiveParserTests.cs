using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class OptionDirectiveParserTests
{
    [TestMethod]
    public void Parse_RecognizesVisiaOptionDirectives()
    {
        const string source = """
            Option Explicit
            Option Base 0
            Option Compare Text
            """;

        var result = new ParserType(SourceText.From(source, "envSort.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(3, result.Root.Members.Length);
        Assert.IsInstanceOfType<OptionExplicitSyntax>(result.Root.Members[0]);

        var optionBase = (OptionBaseSyntax)result.Root.Members[1];
        Assert.AreEqual(SyntaxKind.IdentifierToken, optionBase.BaseIdentifier.Kind);
        Assert.AreEqual("Base", optionBase.BaseIdentifier.Text);
        Assert.AreEqual("0", optionBase.ValueToken.Text);

        var optionCompare = (OptionCompareSyntax)result.Root.Members[2];
        Assert.AreEqual(SyntaxKind.IdentifierToken, optionCompare.CompareIdentifier.Kind);
        Assert.AreEqual("Compare", optionCompare.CompareIdentifier.Text);
        Assert.AreEqual("Text", optionCompare.ModeToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesOtherValidOptionModes()
    {
        const string source = """
            Option Base 1
            Option Compare Binary
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual("1", ((OptionBaseSyntax)result.Root.Members[0]).ValueToken.Text);
        Assert.AreEqual("Binary", ((OptionCompareSyntax)result.Root.Members[1]).ModeToken.Text);
    }

    [TestMethod]
    public void Parse_RejectsInvalidOptionValues()
    {
        const string source = """
            Option Base 2
            Option Compare Database
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(2, result.Diagnostics.Length);
        Assert.IsTrue(result.Diagnostics.All(diagnostic => diagnostic.Code == "VB6P0001"));
    }
}
