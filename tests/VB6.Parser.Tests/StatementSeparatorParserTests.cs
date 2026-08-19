using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class StatementSeparatorParserTests
{
    [TestMethod]
    public void Parse_RecognizesColonSeparatedStatements()
    {
        const string source = """
            Sub Main()
                Dim x As Integer: Dim y As Integer
                x = 1: y = 2
                Debug.Print x: Debug.Print y
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var main = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(6, main.Statements.Length);
        Assert.IsInstanceOfType<DimStatementSyntax>(main.Statements[0]);
        Assert.IsInstanceOfType<DimStatementSyntax>(main.Statements[1]);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(main.Statements[2]);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(main.Statements[3]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(main.Statements[4]);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(main.Statements[5]);
    }

    [TestMethod]
    public void Parse_RecognizesSeparatorsInsideSingleLineIfAndCase()
    {
        const string source = """
            Sub Main()
                If x = 1 Then x = 2: y = 3 Else x = 4: y = 5
                Select Case x
                    Case 2: x = 6: y = 7
                End Select
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var main = (SubDeclarationSyntax)result.Root.Members.Single();

        var ifStatement = (IfStatementSyntax)main.Statements[0];
        Assert.IsTrue(ifStatement.IsSingleLine);
        Assert.AreEqual(2, ifStatement.Statements.Length);
        Assert.AreEqual(2, ifStatement.ElseStatements.Length);

        var select = (SelectCaseStatementSyntax)main.Statements[1];
        Assert.AreEqual(1, select.Cases.Length);
        Assert.AreEqual(2, select.Cases[0].Statements.Length);
    }
}
