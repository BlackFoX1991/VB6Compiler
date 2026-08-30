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
    public void Parse_RecognizesOptionPrivateModuleAtModuleLevel()
    {
        const string source = """
            Option Private Module
            Sub Main()
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var option = (OptionPrivateModuleSyntax)result.Root.Members[0];
        Assert.AreEqual(SyntaxKind.OptionPrivateModuleStatement, option.Kind);
        Assert.AreEqual("Option", option.OptionKeyword.Text);
        Assert.AreEqual("Private", option.PrivateKeyword.Text);
        Assert.AreEqual("Module", option.ModuleKeyword.Text);
    }

    [TestMethod]
    public void Parse_RejectsOptionPrivateModuleInsideProcedure()
    {
        const string source = """
            Sub Main()
                Option Private Module
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6P0001", result.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Parse_RecognizesDefTypeDirectiveAndLetterRanges()
    {
        const string source = """
            DefInt A-Z
            DefStr M, X-Z
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(2, result.Root.Members.Length);

        var defInt = (DefaultTypeStatementSyntax)result.Root.Members[0];
        Assert.AreEqual("DefInt", defInt.DirectiveToken.Text);
        Assert.AreEqual(1, defInt.Ranges.Length);
        Assert.AreEqual("A", defInt.Ranges[0].FirstLetter.Text);
        Assert.AreEqual("-", defInt.Ranges[0].HyphenToken!.Text);
        Assert.AreEqual("Z", defInt.Ranges[0].LastLetter!.Text);

        var defStr = (DefaultTypeStatementSyntax)result.Root.Members[1];
        Assert.AreEqual("DefStr", defStr.DirectiveToken.Text);
        Assert.AreEqual(2, defStr.Ranges.Length);
        Assert.AreEqual("M", defStr.Ranges[0].FirstLetter.Text);
        Assert.AreEqual(",", defStr.Ranges[0].CommaToken!.Text);
        Assert.AreEqual("X", defStr.Ranges[1].FirstLetter.Text);
        Assert.AreEqual("Z", defStr.Ranges[1].LastLetter!.Text);
    }

    [TestMethod]
    public void Parse_RejectsMalformedDefTypeLetterRange()
    {
        const string source = "DefInt A-1";

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6P0001", result.Diagnostics[0].Code);
    }

    [TestMethod]
    public void Parse_RejectsDefTypeDirectiveInsideProcedure()
    {
        const string source = """
            Sub Main()
                DefInt A-Z
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(1, result.Diagnostics.Length);
        Assert.AreEqual("VB6P0001", result.Diagnostics[0].Code);
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
