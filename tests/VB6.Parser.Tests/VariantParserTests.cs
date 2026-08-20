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
            End Sub
            """)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        CollectionAssert.AreEqual(
            new[]
            {
                SyntaxKind.EmptyKeyword,
                SyntaxKind.NullKeyword,
                SyntaxKind.NothingKeyword
            },
            procedure.Statements
                .Cast<DebugPrintStatementSyntax>()
                .Select(statement => ((LiteralExpressionSyntax)statement.Expression).LiteralToken.Kind)
                .ToArray());
    }

    /// <summary>
    /// VB6 has no 'Missing' literal — only the IsMissing function. The word must therefore stay
    /// an ordinary identifier, or legacy code that names a variable 'Missing' stops parsing.
    /// Same rule as 'Base' and 'Compare': never reserve a word VB6 leaves free.
    /// </summary>
    [TestMethod]
    public void Parse_TreatsMissingAsAnOrdinaryIdentifier()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                Dim Missing As Long
                Missing = 10
            End Sub
            """)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();

        var declarator = ((DimStatementSyntax)procedure.Statements[0]).Declarators.Single();
        Assert.AreEqual(SyntaxKind.IdentifierToken, declarator.Identifier.Kind);
        Assert.AreEqual("Missing", declarator.Identifier.Text);

        var assignment = (AssignmentStatementSyntax)procedure.Statements[1];
        Assert.AreEqual(SyntaxKind.IdentifierToken, assignment.Identifier.Kind);
        Assert.AreEqual("Missing", assignment.Identifier.Text);
    }
}
