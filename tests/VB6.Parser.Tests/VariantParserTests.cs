using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class VariantParserTests
{
    [TestMethod]
    public void Parse_RecognizesVariantLiteralKeywords()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                Debug.Print Empty
                Debug.Print Null
                Debug.Print Nothing
                Debug.Print Missing
            End Sub
            """)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.EmptyKeyword,
                SyntaxKind.NullKeyword,
                SyntaxKind.NothingKeyword,
                SyntaxKind.MissingKeyword
            },
            procedure.Statements
                .Cast<DebugPrintStatementSyntax>()
                .Select(statement => ((LiteralExpressionSyntax)statement.Expression).LiteralToken.Kind)
                .ToArray());
    }
}
