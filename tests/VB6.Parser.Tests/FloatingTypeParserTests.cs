using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class FloatingTypeParserTests
{
    [TestMethod]
    public void Parse_RecognizesSingleDoubleAndFloatingLiteral()
    {
        const string source = """
            Function Scale(ByVal value As Single) As Double
                Dim result As Single
                result = 1.5
                Scale = result
            End Function
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(SyntaxKind.SingleKeyword, function.Parameters.Single().TypeToken!.Kind);
        Assert.AreEqual(SyntaxKind.DoubleKeyword, function.ReturnTypeToken!.Kind);
        Assert.AreEqual(SyntaxKind.SingleKeyword, ((DimStatementSyntax)function.Statements[0]).TypeToken.Kind);
        var assignment = (AssignmentStatementSyntax)function.Statements[1];
        Assert.AreEqual(SyntaxKind.FloatingLiteralToken, ((LiteralExpressionSyntax)assignment.Expression).LiteralToken.Kind);
    }
}
