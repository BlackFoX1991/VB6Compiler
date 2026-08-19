using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class MemberArrayAccessParserTests
{
    [TestMethod]
    public void Parse_RecognizesArrayIndexingAfterMemberAccess()
    {
        var expression = ParseDebugExpression("record.Values(2)");

        var elementAccess = expression as ElementAccessExpressionSyntax;
        Assert.IsNotNull(elementAccess);
        Assert.AreEqual(1, elementAccess.Indices.Length);
        Assert.AreEqual("2", ((LiteralExpressionSyntax)elementAccess.Indices[0]).LiteralToken.Text);

        var memberAccess = elementAccess.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(memberAccess);
        Assert.AreEqual("Values", memberAccess.MemberToken.Text);
        Assert.AreEqual("record", ((NameExpressionSyntax)memberAccess.Receiver).IdentifierToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesImplicitWithMemberArrayIndexing()
    {
        var source = """
            Sub Main()
                With record
                    Debug.Print .Values(1)
                End With
            End Sub
            """;
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));

        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var withStatement = (WithStatementSyntax)procedure.Statements.Single();
        var debugPrint = (DebugPrintStatementSyntax)withStatement.Statements.Single();
        var elementAccess = debugPrint.Expression as ElementAccessExpressionSyntax;
        Assert.IsNotNull(elementAccess);

        var memberAccess = elementAccess.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(memberAccess);
        Assert.AreEqual("Values", memberAccess.MemberToken.Text);
        Assert.IsInstanceOfType<WithReceiverExpressionSyntax>(memberAccess.Receiver);
    }

    [TestMethod]
    public void Parse_PreservesMemberAccessAfterIndexedMember()
    {
        var expression = ParseDebugExpression("record.Children(1).Value");

        var valueAccess = expression as MemberAccessExpressionSyntax;
        Assert.IsNotNull(valueAccess);
        Assert.AreEqual("Value", valueAccess.MemberToken.Text);

        var elementAccess = valueAccess.Receiver as ElementAccessExpressionSyntax;
        Assert.IsNotNull(elementAccess);
        var childrenAccess = elementAccess.Receiver as MemberAccessExpressionSyntax;
        Assert.IsNotNull(childrenAccess);
        Assert.AreEqual("Children", childrenAccess.MemberToken.Text);
    }

    private static ExpressionSyntax ParseDebugExpression(string expression)
    {
        var source = $"Sub Main()\n    Debug.Print {expression}\nEnd Sub";
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var debugPrint = (DebugPrintStatementSyntax)procedure.Statements.Single();
        return debugPrint.Expression;
    }

    private static string FormatDiagnostics(VB6.Parser.ParseResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
