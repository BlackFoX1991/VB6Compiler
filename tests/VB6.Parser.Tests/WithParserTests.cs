using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class WithParserTests
{
    [TestMethod]
    public void Parse_RecognizesWithBlockAndImplicitMembers()
    {
        var procedure = ParseProcedure("""
            Sub Main()
                Dim point As Point
                With point
                    .X = 41
                    Debug.Print .X
                End With
            End Sub
            """);

        var withStatement = procedure.Statements.OfType<WithStatementSyntax>().Single();
        Assert.IsInstanceOfType<NameExpressionSyntax>(withStatement.Expression);
        Assert.AreEqual(2, withStatement.Statements.Length);

        var assignment = withStatement.Statements[0] as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(assignment.Target.Receiver);

        var debugPrint = withStatement.Statements[1] as DebugPrintStatementSyntax;
        Assert.IsNotNull(debugPrint);
        var read = debugPrint.Expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(read);
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(read.Receiver);
    }

    [TestMethod]
    public void Parse_RecognizesNestedWithUsingImplicitReceiver()
    {
        var procedure = ParseProcedure("""
            Sub Main()
                With outer
                    With .Child
                        .Value = 1
                    End With
                End With
            End Sub
            """);

        var outer = procedure.Statements.OfType<WithStatementSyntax>().Single();
        var inner = outer.Statements.OfType<WithStatementSyntax>().Single();
        var innerTarget = inner.Expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(innerTarget);
        Assert.AreEqual("Child", innerTarget.MemberToken.Text);
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(innerTarget.Receiver);

        var assignment = inner.Statements.OfType<MemberAssignmentStatementSyntax>().Single();
        Assert.AreEqual("Value", assignment.Target.MemberToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesArrayElementWithTarget()
    {
        var procedure = ParseProcedure("""
            Sub Main()
                With points(1)
                    .X = 7
                End With
            End Sub
            """);

        var withStatement = procedure.Statements.OfType<WithStatementSyntax>().Single();
        Assert.IsInstanceOfType<InvocationExpressionSyntax>(withStatement.Expression);
    }

    [TestMethod]
    public void Parse_LeadingDotOutsideWithStillProducesMemberSyntax()
    {
        var procedure = ParseProcedure("""
            Sub Main()
                .X = 7
            End Sub
            """);

        var assignment = procedure.Statements.OfType<MemberAssignmentStatementSyntax>().Single();
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(assignment.Target.Receiver);
    }

    private static SubDeclarationSyntax ParseProcedure(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        return (SubDeclarationSyntax)result.Root.Members.Single();
    }

    private static string FormatDiagnostics(ParseResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}