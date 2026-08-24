using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class GraphicsLineParserTests
{
    [TestMethod]
    public void Parse_RecognizesLineCoordinatePairAndColor()
    {
        var result = new ParserType(SourceText.From("Sub Main()\nLine (x, y)-(x + 1, y + 2), color\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var statement = (LineStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        Assert.IsNull(statement.StepKeyword);
        Assert.AreEqual("x", ((NameExpressionSyntax)statement.StartPoint.XExpression).IdentifierToken.Text);
        Assert.AreEqual("y", ((NameExpressionSyntax)statement.StartPoint.YExpression).IdentifierToken.Text);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(statement.EndPoint.XExpression);
        Assert.IsInstanceOfType<BinaryExpressionSyntax>(statement.EndPoint.YExpression);
        Assert.AreEqual("color", ((NameExpressionSyntax)statement.ColorExpression!).IdentifierToken.Text);
        Assert.AreEqual(0, statement.Options.Length);
    }

    [TestMethod]
    public void Parse_RecognizesStepAndLineOptions()
    {
        var result = new ParserType(SourceText.From("Sub Main()\nLine Step (0, 0)-(1, 1), vbWhite, B, F\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var statement = (LineStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        Assert.IsNotNull(statement.StepKeyword);
        Assert.AreEqual(2, statement.Options.Length);
        Assert.AreEqual("B", ((NameExpressionSyntax)statement.Options[0]).IdentifierToken.Text);
        Assert.AreEqual("F", ((NameExpressionSyntax)statement.Options[1]).IdentifierToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesQualifiedLineTarget()
    {
        var result = new ParserType(SourceText.From("Sub Main()\nPicture1.Line (x, y)-(x + 1, y + 2), color\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var statement = (LineStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        Assert.IsInstanceOfType<NameExpressionSyntax>(statement.Target);
        Assert.AreEqual("Picture1", ((NameExpressionSyntax)statement.Target!).IdentifierToken.Text);
        Assert.AreEqual("Line", statement.LineKeyword.Text);
    }
}
