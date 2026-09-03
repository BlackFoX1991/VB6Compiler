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

    [TestMethod]
    public void Parse_RecognizesCircleWithItsOptionalArguments()
    {
        var result = new ParserType(SourceText.From(
            "Sub Main()\nCircle (x, y), r, color, 0, 3.14, 0.5\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var statement = (CircleStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        Assert.IsNull(statement.StepKeyword);
        Assert.AreEqual("x", ((NameExpressionSyntax)statement.Center.XExpression).IdentifierToken.Text);
        Assert.AreEqual("r", ((NameExpressionSyntax)statement.Radius).IdentifierToken.Text);
        Assert.AreEqual("color", ((NameExpressionSyntax)statement.ColorExpression!).IdentifierToken.Text);
        Assert.IsNotNull(statement.StartExpression);
        Assert.IsNotNull(statement.EndExpression);
        Assert.IsNotNull(statement.AspectExpression);
    }

    [TestMethod]
    public void Parse_KeepsOmittedCircleArgumentsApart()
    {
        var result = new ParserType(SourceText.From(
            "Sub Main()\nCircle Step (x, y), r, , 0, 3.14\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var statement = (CircleStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        // Eine ausgelassene Farbe darf die folgenden Winkel nicht verschieben -- sonst zeichnete
        // VB6 einen Vollkreis in der Farbe des Startwinkels.
        Assert.IsNotNull(statement.StepKeyword);
        Assert.IsNull(statement.ColorExpression);
        Assert.IsNotNull(statement.StartExpression);
        Assert.IsNotNull(statement.EndExpression);
        Assert.IsNull(statement.AspectExpression);
    }

    [TestMethod]
    public void Parse_RecognizesPSetCoordinateAndColor()
    {
        var result = new ParserType(SourceText.From("Sub Main()\nPSet (x, y), color\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var statement = (PSetStatementSyntax)result.Root.Members
            .OfType<SubDeclarationSyntax>()
            .Single()
            .Statements[0];

        Assert.IsNull(statement.StepKeyword);
        Assert.AreEqual("x", ((NameExpressionSyntax)statement.Point.XExpression).IdentifierToken.Text);
        Assert.AreEqual("y", ((NameExpressionSyntax)statement.Point.YExpression).IdentifierToken.Text);
        Assert.AreEqual("color", ((NameExpressionSyntax)statement.ColorExpression!).IdentifierToken.Text);
    }

    [TestMethod]
    public void Parse_RecognizesSteppedAndQualifiedPSet()
    {
        var result = new ParserType(SourceText.From(
            "Sub Main()\nPSet Step (1, 2)\nPicture1.PSet (3, 4)\nEnd Sub")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var statements = result.Root.Members.OfType<SubDeclarationSyntax>().Single().Statements;

        var stepped = (PSetStatementSyntax)statements[0];
        Assert.IsNotNull(stepped.StepKeyword);
        Assert.IsNull(stepped.Target);
        Assert.IsNull(stepped.ColorExpression);

        // Die Koordinatenklammer ist das einzige, was die Anweisungsform vom gewoehnlichen Aufruf
        // trennt -- genau wie bei Line.
        var qualified = (PSetStatementSyntax)statements[1];
        Assert.AreEqual("Picture1", ((NameExpressionSyntax)qualified.Target!).IdentifierToken.Text);
        Assert.AreEqual("PSet", qualified.PSetKeyword.Text);
    }
}
