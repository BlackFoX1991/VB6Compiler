using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ParserTests
{
    [TestMethod]
    public void Parse_FirstAcceptanceProgram()
    {
        const string source = """
            Option Explicit

            Sub Main()
                Dim x As Integer
                x = 10

                If x > 5 Then
                    Debug.Print x
                End If
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        Assert.AreEqual(2, result.Root.Members.Length);
        Assert.IsInstanceOfType<OptionExplicitSyntax>(result.Root.Members[0]);

        var sub = (SubDeclarationSyntax)result.Root.Members[1];
        Assert.AreEqual("Main", sub.Identifier.Text);
        Assert.AreEqual(3, sub.Statements.Length);
        Assert.IsInstanceOfType<DimStatementSyntax>(sub.Statements[0]);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(sub.Statements[1]);
        Assert.IsInstanceOfType<IfStatementSyntax>(sub.Statements[2]);

        var ifStatement = (IfStatementSyntax)sub.Statements[2];
        Assert.AreEqual(1, ifStatement.Statements.Length);
        Assert.IsInstanceOfType<DebugPrintStatementSyntax>(ifStatement.Statements[0]);
    }

    [TestMethod]
    public void Parse_UsesOperatorPrecedence()
    {
        const string source = """
            Sub Main()
                x = 1 + 2 * 3
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var assignment = (AssignmentStatementSyntax)sub.Statements.Single();
        var rootBinary = (BinaryExpressionSyntax)assignment.Expression;

        Assert.AreEqual(SyntaxKind.PlusToken, rootBinary.OperatorToken.Kind);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(rootBinary.Right);
        Assert.AreEqual(SyntaxKind.StarToken, ((BinaryExpressionSyntax)rootBinary.Right).OperatorToken.Kind);
    }

    [TestMethod]
    public void Parse_ReportsMissingEndSub()
    {
        const string source = """
            Sub Main()
                Dim x As Integer
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.IsTrue(result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6P0001"));
    }
}
