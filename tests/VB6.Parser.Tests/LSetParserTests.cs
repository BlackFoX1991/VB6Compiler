using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class LSetParserTests
{
    [TestMethod]
    public void Parse_LSetAssignmentAsTwoArguments()
    {
        var statement = ParseSingleStatement("LSet target = source");

        var invocation = statement as InvocationStatementSyntax;
        Assert.IsNotNull(invocation);
        Assert.AreEqual("LSet", invocation.Identifier.Text);
        Assert.AreEqual(2, invocation.Arguments.Length);
        Assert.IsInstanceOfType<NameExpressionSyntax>(invocation.Arguments[0]);
        Assert.IsInstanceOfType<NameExpressionSyntax>(invocation.Arguments[1]);
    }

    [TestMethod]
    public void Parse_LSetAssignmentWithQualifiedSource()
    {
        var statement = ParseSingleStatement("LSet target = value.Member");

        var invocation = statement as InvocationStatementSyntax;
        Assert.IsNotNull(invocation);
        Assert.AreEqual(2, invocation.Arguments.Length);
        Assert.IsInstanceOfType<MemberAccessExpressionSyntax>(invocation.Arguments[1]);
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
