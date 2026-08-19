using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class MemberArrayAssignmentParserTests
{
    [TestMethod]
    public void Parse_RecognizesUdtArrayMemberAssignment()
    {
        var statement = ParseSingleStatement("record.Values(2) = 9");

        var assignment = statement as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        var elementAccess = assignment.Target as ElementAccessExpressionSyntax;
        Assert.IsNotNull(elementAccess);
        Assert.AreEqual(1, elementAccess.Indices.Length);

        var member = elementAccess.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(member);
        Assert.AreEqual("Values", member.MemberToken.Text);
        Assert.AreEqual("record", ((NameExpressionSyntax)member.Receiver).IdentifierToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesWithUdtArrayMemberAssignment()
    {
        var source = """
            Sub Main()
                With record
                    .Values(1) = 5
                End With
            End Sub
            """;
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var withStatement = (WithStatementSyntax)procedure.Statements.Single();
        var assignment = withStatement.Statements.Single() as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        var elementAccess = assignment.Target as ElementAccessExpressionSyntax;
        Assert.IsNotNull(elementAccess);

        var member = elementAccess.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(member);
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(member.Receiver);
    }

    [TestMethod]
    public void Parse_RecognizesMemberAssignmentAfterIndexedUdtMember()
    {
        var statement = ParseSingleStatement("record.Children(1).Value = 3");

        var assignment = statement as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        var valueMember = assignment.Target as MemberAccessExpressionSyntax;
        Assert.IsNotNull(valueMember);
        Assert.AreEqual("Value", valueMember.MemberToken.Text);

        var childElement = valueMember.Receiver as ElementAccessExpressionSyntax;
        Assert.IsNotNull(childElement);
        var childrenMember = childElement.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(childrenMember);
        Assert.AreEqual("Children", childrenMember.MemberToken.Text);
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
