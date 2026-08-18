using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class SelectCaseParserTests
{
    [TestMethod]
    public void Parse_RecognizesSelectCaseClauseForms()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
                Select Case x
                    Case 1, 2
                        Debug.Print 1
                    Case 3 To 5
                        Debug.Print 2
                    Case Is >= 10
                        Debug.Print 3
                    Case Else
                        Debug.Print 4
                End Select
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var select = (SelectCaseStatementSyntax)sub.Statements[1];

        Assert.AreEqual(4, select.Cases.Length);
        Assert.AreEqual(2, select.Cases[0].Clauses.Length);
        Assert.IsTrue(select.Cases[0].Clauses.All(clause => clause is CaseValueClauseSyntax));
        Assert.IsInstanceOfType<CaseRangeClauseSyntax>(select.Cases[1].Clauses.Single());

        var relational = (CaseRelationalClauseSyntax)select.Cases[2].Clauses.Single();
        Assert.AreEqual(SyntaxKind.GreaterOrEqualsToken, relational.OperatorToken.Kind);
        Assert.IsInstanceOfType<CaseElseClauseSyntax>(select.Cases[3].Clauses.Single());
    }

    [TestMethod]
    public void Parse_AllowsNestedStatementsInsideCaseBlock()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
                Select Case x
                    Case 1
                        If x = 1 Then
                            Debug.Print x
                        End If
                    Case Else
                        Do
                            Exit Do
                        Loop
                End Select
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var select = (SelectCaseStatementSyntax)sub.Statements[1];
        Assert.IsInstanceOfType<IfStatementSyntax>(select.Cases[0].Statements.Single());
        Assert.IsInstanceOfType<DoStatementSyntax>(select.Cases[1].Statements.Single());
    }
}
