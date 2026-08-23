using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class IfBranchParserTests
{
    [TestMethod]
    public void Parse_RecognizesElseIfAndElseBlocks()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
                If x = 1 Then
                    x = 10
                ElseIf x = 2 Then
                    x = 20
                ElseIf x = 3 Then
                    x = 30
                Else
                    x = 40
                End If
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (IfStatementSyntax)sub.Statements[1];

        Assert.IsFalse(statement.IsSingleLine);
        Assert.AreEqual(1, statement.Statements.Length);
        Assert.AreEqual(2, statement.ElseIfClauses.Length);
        Assert.AreEqual("2", ((LiteralExpressionSyntax)((BinaryExpressionSyntax)statement.ElseIfClauses[0].Condition).Right).LiteralToken.Text);
        Assert.IsNotNull(statement.ElseKeyword);
        Assert.AreEqual(1, statement.ElseStatements.Length);
        Assert.IsNotNull(statement.EndKeyword);
        Assert.IsNotNull(statement.IfEndKeyword);
    }

    [TestMethod]
    public void Parse_RecognizesSingleLineIfWithElse()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
                If x = 1 Then x = 2: Debug.Print x Else x = 3
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (IfStatementSyntax)sub.Statements[1];

        Assert.IsTrue(statement.IsSingleLine);
        Assert.AreEqual(2, statement.Statements.Length);
        Assert.IsNotNull(statement.ElseKeyword);
        Assert.AreEqual(1, statement.ElseStatements.Length);
        Assert.AreEqual(0, statement.ElseIfClauses.Length);
        Assert.IsNull(statement.EndKeyword);
        Assert.IsNull(statement.IfEndKeyword);
    }

    [TestMethod]
    public void Parse_RecognizesSingleLineIfWithoutElse()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
                If x = 1 Then x = 2
                Debug.Print x
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var statement = (IfStatementSyntax)sub.Statements[1];

        Assert.IsTrue(statement.IsSingleLine);
        Assert.IsNull(statement.ElseKeyword);
        Assert.AreEqual(0, statement.ElseStatements.Length);
        Assert.AreEqual(3, sub.Statements.Length);
    }

    [TestMethod]
    public void Parse_AllowsColonImmediatelyAfterSingleLineElse()
    {
        const string source = "Sub Main()\nIf ready Then result = 1 Else: result = 2\nEnd Sub";

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var statement = (IfStatementSyntax)((SubDeclarationSyntax)result.Root.Members.Single()).Statements.Single();
        Assert.AreEqual(1, statement.ElseStatements.Length);
    }
}
