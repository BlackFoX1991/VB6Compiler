using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class CallSiteByValParserTests
{
    [TestMethod]
    public void Parse_PreservesCallSiteByValArguments()
    {
        const string source = """
            Sub Main()
                CopyMemory target, ByVal VarPtr(source), 4
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var invocation = (InvocationStatementSyntax)procedure.Statements.Single();
        Assert.AreEqual(3, invocation.Arguments.Length);
        var byVal = (ByValArgumentExpressionSyntax)invocation.Arguments[1];
        Assert.AreEqual("ByVal", byVal.ByValKeyword.Text);
        Assert.IsInstanceOfType<InvocationExpressionSyntax>(byVal.Expression);
    }

    [TestMethod]
    public void Parse_StillTreatsByValAsParameterPassingMode()
    {
        const string source = """
            Sub CopyValue(ByVal value As Long)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual("ByVal", procedure.Parameters.Single().PassingModeKeyword?.Text);
    }
}
