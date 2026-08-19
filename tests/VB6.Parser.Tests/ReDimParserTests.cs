using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ReDimParserTests
{
    [TestMethod]
    public void Parse_RecognizesReDimWithExplicitBounds()
    {
        var statement = ParseSingleStatement("ReDim values(1 To 10)");

        var reDim = statement as ReDimStatementSyntax;
        Assert.IsNotNull(reDim);
        Assert.IsNull(reDim.PreserveKeyword);
        Assert.AreEqual(1, reDim.Declarators.Length);
        Assert.AreEqual("values", reDim.Declarators[0].Identifier.Text);
        Assert.AreEqual(1, reDim.Declarators[0].Dimensions.Length);
        Assert.IsNotNull(reDim.Declarators[0].Dimensions[0].LowerBound);
    }

    [TestMethod]
    public void Parse_RecognizesReDimPreserve()
    {
        var statement = ParseSingleStatement("ReDim Preserve values(1 To 20)");

        var reDim = statement as ReDimStatementSyntax;
        Assert.IsNotNull(reDim);
        Assert.IsNotNull(reDim.PreserveKeyword);
        Assert.AreEqual("Preserve", reDim.PreserveKeyword.Text, ignoreCase: true);
    }

    [TestMethod]
    public void Parse_ReDimPreservesMultipleAndMultidimensionalDeclarators()
    {
        var statement = ParseSingleStatement("ReDim grid(0 To 2, 4 To 6), other(3) As Long");

        var reDim = statement as ReDimStatementSyntax;
        Assert.IsNotNull(reDim);
        Assert.AreEqual(2, reDim.Declarators.Length);
        Assert.AreEqual(2, reDim.Declarators[0].Dimensions.Length);
        Assert.AreEqual(1, reDim.Declarators[1].Dimensions.Length);
        Assert.AreEqual("Long", reDim.Declarators[1].TypeToken?.Text, ignoreCase: true);
    }

    private static StatementSyntax ParseSingleStatement(string statement)
    {
        var source = $"Sub Main()\n    {statement}\nEnd Sub";
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return procedure.Statements.Single();
    }

    private static string FormatDiagnostics(VB6.Parser.ParseResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
