using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// The explicit <c>Let</c> form of an assignment.
///
/// <c>Let x = 1</c> means exactly <c>x = 1</c>, so the keyword is consumed and the statement that
/// follows goes through the ordinary dispatch. That is the contract these tests pin: every
/// assignable form keeps the node it would have had without the keyword, rather than growing a
/// parallel <c>Let</c> node that would have to be taught to the binder, the lowerer and the
/// emitter one at a time.
/// </summary>
[TestClass]
public sealed class LetAssignmentParserTests
{
    [TestMethod]
    public void Parse_LetVariableAsAnOrdinaryAssignment()
    {
        var statement = ParseSingleStatement("Let x = 1");

        var assignment = statement as AssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual("x", assignment.Identifier.Text);
    }

    [TestMethod]
    public void Parse_LetArrayElementAsAnArrayElementAssignment()
    {
        var statement = ParseSingleStatement("Let values(1) = 2");

        Assert.IsInstanceOfType<ArrayElementAssignmentStatementSyntax>(statement);
    }

    [TestMethod]
    public void Parse_LetMemberAsAMemberAssignment()
    {
        var statement = ParseSingleStatement("Let target.Member = 3");

        Assert.IsInstanceOfType<MemberAssignmentStatementSyntax>(statement);
    }

    [TestMethod]
    public void Parse_LetWithoutAnAssignmentStaysACall()
    {
        // No top-level '=' before the end of the statement, so this is not the Let form. It has to
        // stay a call, or a procedure named Let would become unreachable.
        var statement = ParseSingleStatement("Let value");

        Assert.IsInstanceOfType<InvocationStatementSyntax>(statement);
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
