using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// Calling a method on an object, as in <c>frmMain.SelectObjectObject "Frames"</c>. The semantics
/// need the object model, but these were the last widespread parser gap in the corpus.
/// </summary>
[TestClass]
public sealed class QualifiedCallParserTests
{
    [TestMethod]
    public void Parse_QualifiedCallWithArguments()
    {
        var statement = (QualifiedInvocationStatementSyntax)ParseSingleStatement(
            "aControl.SubclassedMessage uMsg, wParam, lParam");

        Assert.AreEqual(3, statement.Arguments.Length);
        Assert.IsInstanceOfType<MemberAccessExpressionSyntax>(statement.Target);
    }

    [TestMethod]
    public void Parse_QualifiedCallThroughSeveralMembers()
    {
        var statement = (QualifiedInvocationStatementSyntax)ParseSingleStatement(
            """frmMain.cmbObject.ComboItems.Add , , "General", "Object" """.TrimEnd());

        Assert.AreEqual(4, statement.Arguments.Length);
        Assert.IsInstanceOfType<OmittedArgumentExpressionSyntax>(statement.Arguments[0]);
        Assert.IsInstanceOfType<OmittedArgumentExpressionSyntax>(statement.Arguments[1]);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(statement.Arguments[2]);
    }

    [TestMethod]
    public void Parse_QualifiedCallWithoutArguments()
    {
        var statement = (QualifiedInvocationStatementSyntax)ParseSingleStatement("frmMain.Refresh");

        Assert.AreEqual(0, statement.Arguments.Length);
    }

    /// <summary>
    /// An ordinary call whose argument happens to be qualified must stay an ordinary call, or the
    /// receiver would be mistaken for the callee.
    /// </summary>
    [TestMethod]
    public void Parse_CallWithAQualifiedArgumentStaysAnOrdinaryCall()
    {
        var statement = ParseSingleStatement("Consume record.Value");

        Assert.IsInstanceOfType<InvocationStatementSyntax>(statement);
    }

    /// <summary>
    /// Whitespace decides the rest, as it does in VB6: inside a With, <c>Consume .Value</c> passes
    /// the With member as an argument rather than calling a member of Consume.
    /// </summary>
    [TestMethod]
    public void Parse_SpaceBeforeTheDotMakesItAWithArgument()
    {
        const string source = """
            Sub Main()
                With target
                    Consume .Value
                End With
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var with = (WithStatementSyntax)procedure.Statements.Single();
        Assert.IsInstanceOfType<InvocationStatementSyntax>(with.Statements.Single());
    }

    private static StatementSyntax ParseSingleStatement(string statement)
    {
        var source = $"""
            Sub Main()
                {statement}
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(", ", result.Diagnostics.Select(d => d.Message)));

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return procedure.Statements.Single();
    }
}
