using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ForEachParserTests
{
    [TestMethod]
    public void Parse_RecognizesForEachCollectionAndNextIdentifier()
    {
        const string source = """
            Sub Main()
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (ForEachStatementSyntax)sub.Statements.Single();

        Assert.AreEqual(SyntaxKind.ForKeyword, statement.ForKeyword.Kind);
        Assert.AreEqual(SyntaxKind.EachKeyword, statement.EachKeyword.Kind);
        Assert.AreEqual("item", statement.Identifier.Text);
        Assert.AreEqual(SyntaxKind.InKeyword, statement.InKeyword.Kind);
        Assert.AreEqual("values", ((NameExpressionSyntax)statement.Collection).IdentifierToken.Text);
        Assert.AreEqual(1, statement.Statements.Length);
        Assert.AreEqual("item", statement.NextIdentifier?.Text);
    }

    [TestMethod]
    public void Parse_RecognizesForEachOverMemberExpressionAndExitFor()
    {
        const string source = """
            Sub Main()
                For Each value In holder.Values
                    Exit For
                Next
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (ForEachStatementSyntax)sub.Statements.Single();
        var collection = (MemberAccessExpressionSyntax)statement.Collection;

        Assert.AreEqual("holder", ((NameExpressionSyntax)collection.Receiver).IdentifierToken.Text);
        Assert.AreEqual("Values", collection.MemberToken.Text);
        var exit = (ExitStatementSyntax)statement.Statements.Single();
        Assert.AreEqual(SyntaxKind.ForKeyword, exit.TargetKeyword.Kind);
        Assert.IsNull(statement.NextIdentifier);
    }

    [TestMethod]
    public void Parse_KeepsNumericForAsForStatement()
    {
        const string source = """
            Sub Main()
                For i = 1 To 3
                    Debug.Print i
                Next i
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.IsInstanceOfType<ForStatementSyntax>(sub.Statements.Single());
    }
}
