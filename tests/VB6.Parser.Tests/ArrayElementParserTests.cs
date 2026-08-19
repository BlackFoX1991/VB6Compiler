using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ArrayElementParserTests
{
    [TestMethod]
    public void Parse_RecognizesArrayElementAssignment()
    {
        var statement = ParseSingleStatement("values(i) = 42");

        var assignment = statement as ArrayElementAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual("values", assignment.Identifier.Text);
        Assert.AreEqual(1, assignment.Indices.Length);
        Assert.IsInstanceOfType<NameExpressionSyntax>(assignment.Indices[0]);
        Assert.AreEqual("42", ((LiteralExpressionSyntax)assignment.Expression).LiteralToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesMultidimensionalArrayElementAssignmentWithNestedExpression()
    {
        var statement = ParseSingleStatement("grid(i + First(j), k) = value");

        var assignment = statement as ArrayElementAssignmentStatementSyntax;
        Assert.IsNotNull(assignment);
        Assert.AreEqual(2, assignment.Indices.Length);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(assignment.Indices[0]);
        Assert.IsInstanceOfType<NameExpressionSyntax>(assignment.Indices[1]);
        Assert.IsInstanceOfType<NameExpressionSyntax>(assignment.Expression);
    }

    [TestMethod]
    public void Parse_ParenthesizedProcedureCallRemainsInvocationStatement()
    {
        var statement = ParseSingleStatement("Foo(i)");

        Assert.IsInstanceOfType<InvocationStatementSyntax>(statement);
    }

    [TestMethod]
    public void Parse_ScalarAssignmentRemainsAssignmentStatement()
    {
        var statement = ParseSingleStatement("value = 1");

        Assert.IsInstanceOfType<AssignmentStatementSyntax>(statement);
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
