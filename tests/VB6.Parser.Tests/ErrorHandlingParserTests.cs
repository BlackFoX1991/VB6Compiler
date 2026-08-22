using VB6.Syntax.Nodes;
using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// The M6 control flow syntax. Semantics wait for the lowered representation, but an unparsed
/// <c>On Error GoTo</c> derailed seven conformance modules, so the syntax comes first.
/// </summary>
[TestClass]
public sealed class ErrorHandlingParserTests
{
    [TestMethod]
    public void Parse_OnErrorGoToLabel()
    {
        var statement = (OnErrorStatementSyntax)ParseSingleStatement("On Error GoTo NotOptimize");

        Assert.AreEqual("GoTo", statement.ActionKeyword.Text);
        Assert.AreEqual("NotOptimize", statement.TargetToken.Text);
    }

    [TestMethod]
    public void Parse_OnErrorGoToZeroClearsTheHandler()
    {
        var statement = (OnErrorStatementSyntax)ParseSingleStatement("On Error GoTo 0");

        Assert.AreEqual("GoTo", statement.ActionKeyword.Text);
        Assert.AreEqual("0", statement.TargetToken.Text);
    }

    [TestMethod]
    public void Parse_OnErrorResumeNext()
    {
        var statement = (OnErrorStatementSyntax)ParseSingleStatement("On Error Resume Next");

        Assert.AreEqual("Resume", statement.ActionKeyword.Text);
        Assert.AreEqual("Next", statement.TargetToken.Text);
    }

    [TestMethod]
    public void Parse_ResumeVariants()
    {
        Assert.IsNull(((ResumeStatementSyntax)ParseSingleStatement("Resume")).TargetToken);
        Assert.AreEqual(
            "Next",
            ((ResumeStatementSyntax)ParseSingleStatement("Resume Next")).TargetToken?.Text);
        Assert.AreEqual(
            "Retry",
            ((ResumeStatementSyntax)ParseSingleStatement("Resume Retry")).TargetToken?.Text);
    }

    [TestMethod]
    public void Parse_GoToStatement()
    {
        var statement = (GoToStatementSyntax)ParseSingleStatement("GoTo Done");

        Assert.AreEqual("Done", statement.LabelToken.Text);
    }

    [TestMethod]
    public void Parse_GoSubAndReturnStatements()
    {
        var goSub = (GoSubStatementSyntax)ParseSingleStatement("GoSub AddTwo");
        var goSubReturn = (GoSubReturnStatementSyntax)ParseSingleStatement("Return");

        Assert.AreEqual("AddTwo", goSub.LabelToken.Text);
        Assert.AreEqual(SyntaxKind.ReturnKeyword, goSubReturn.ReturnKeyword.Kind);
    }

    [TestMethod]
    public void Parse_OnGoToAndNumericLineLabel()
    {
        var result = new ParserType(SourceText.From("""
            Sub Main()
                On selector GoTo 100, Done
            100
            Done:
            End Sub
            """)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var onGoTo = (OnGoToStatementSyntax)procedure.Statements[0];
        Assert.AreEqual(2, onGoTo.LabelTokens.Length);
        Assert.AreEqual("100", onGoTo.LabelTokens[0].Text);
        Assert.IsNull(((LabelStatementSyntax)procedure.Statements[1]).ColonToken);
    }

    [TestMethod]
    public void Parse_OnGoSub()
    {
        var statement = (OnGoSubStatementSyntax)ParseSingleStatement("On selector GoSub First, Second");

        Assert.AreEqual("selector", ((NameExpressionSyntax)statement.Expression).IdentifierToken.Text);
        CollectionAssert.AreEqual(new[] { "First", "Second" }, statement.LabelTokens.Select(token => token.Text).ToArray());
    }

    [TestMethod]
    public void Parse_LabelOnItsOwnLine()
    {
        var statement = (LabelStatementSyntax)ParseSingleStatement("LinkFail:");

        Assert.AreEqual("LinkFail", statement.Identifier.Text);
    }

    /// <summary>
    /// An identifier followed by a colon with more on the line is a parameterless call and the
    /// statement separator, not a label. Treating it as a label would silently drop the call.
    /// </summary>
    [TestMethod]
    public void Parse_IdentifierColonWithMoreOnTheLineStaysTwoStatements()
    {
        const string source = """
            Sub Main()
                Cleanup: Debug.Print 1
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(2, procedure.Statements.Length);
        Assert.IsInstanceOfType<InvocationStatementSyntax>(procedure.Statements[0]);
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
