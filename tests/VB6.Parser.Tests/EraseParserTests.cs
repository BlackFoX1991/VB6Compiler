using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class EraseParserTests
{
    [TestMethod]
    public void Parse_RecognizesSingleEraseTarget()
    {
        var erase = ParseErase("Erase values");

        Assert.AreEqual(1, erase.Identifiers.Length);
        Assert.AreEqual("values", erase.Identifiers[0].Text);
    }

    [TestMethod]
    public void Parse_RecognizesMultipleEraseTargets()
    {
        var erase = ParseErase("Erase first, second, third");

        CollectionAssert.AreEqual(
            new[] { "first", "second", "third" },
            erase.Identifiers.Select(identifier => identifier.Text).ToArray());
    }

    private static EraseStatementSyntax ParseErase(string statement)
    {
        var source = $"Sub Main()\n    {statement}\nEnd Sub";
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return (EraseStatementSyntax)procedure.Statements.Single();
    }
}
