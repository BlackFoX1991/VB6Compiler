using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class MemberAccessParserTests
{
    [TestMethod]
    public void Parse_RecognizesMemberAssignment()
    {
        var statement = ParseSingleStatement("point.X = 42");

        var assignment = statement as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual("X", assignment.Target.MemberToken.Text);
        Assert.IsInstanceOfType<NameExpressionSyntax>(assignment.Target.Receiver);
        Assert.AreEqual("point", ((NameExpressionSyntax)assignment.Target.Receiver).IdentifierToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesNestedMemberRead()
    {
        var statement = ParseSingleStatement("value = outer.Inner.Value");

        var assignment = statement as AssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        var outerMember = assignment.Expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(outerMember);
        Assert.AreEqual("Value", outerMember.MemberToken.Text);
        var innerMember = outerMember.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(innerMember);
        Assert.AreEqual("Inner", innerMember.MemberToken.Text);
    }

    [TestMethod]
    public void Parse_AllowsKeywordMemberNames()
    {
        var statement = ParseSingleStatement("record.Type = record.End");

        var assignment = statement as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual("Type", assignment.Target.MemberToken.Text);
        var read = assignment.Expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(read);
        Assert.AreEqual("End", read.MemberToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesArrayElementMemberAssignment()
    {
        var statement = ParseSingleStatement("points(i).X = 1");

        var assignment = statement as MemberAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual("X", assignment.Target.MemberToken.Text);
        Assert.IsInstanceOfType<InvocationExpressionSyntax>(assignment.Target.Receiver);
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