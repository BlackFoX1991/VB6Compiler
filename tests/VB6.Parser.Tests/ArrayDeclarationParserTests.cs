using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ArrayDeclarationParserTests
{
    [TestMethod]
    public void Parse_PreservesDynamicArrayParameter()
    {
        var procedure = ParseFunction("Function Sort(TheArray() As String) As Long\nEnd Function");

        var parameter = procedure.Parameters.Single();
        Assert.IsTrue(parameter.IsArray);
        Assert.AreEqual(0, parameter.Dimensions.Length);
        Assert.IsNotNull(parameter.OpenParenthesisToken);
        Assert.IsNotNull(parameter.CloseParenthesisToken);
        Assert.AreEqual("String", parameter.TypeToken.Text);
    }

    [TestMethod]
    public void Parse_PreservesExplicitArrayBounds()
    {
        var declaration = ParseLocal("Dim values(1 To 64) As Long");
        var declarator = declaration.Declarators.Single();

        Assert.IsTrue(declarator.IsArray);
        Assert.AreEqual(1, declarator.Dimensions.Length);
        var dimension = declarator.Dimensions[0];
        Assert.AreEqual("1", ((LiteralExpressionSyntax)dimension.LowerBound!).LiteralToken.Text);
        Assert.AreEqual("To", dimension.ToKeyword!.Text);
        Assert.AreEqual("64", ((LiteralExpressionSyntax)dimension.UpperBound).LiteralToken.Text);
    }

    [TestMethod]
    public void Parse_PreservesImplicitLowerBound()
    {
        var declaration = ParseLocal("Dim values(10) As Integer");
        var dimension = declaration.Declarators.Single().Dimensions.Single();

        Assert.IsNull(dimension.LowerBound);
        Assert.IsNull(dimension.ToKeyword);
        Assert.AreEqual("10", ((LiteralExpressionSyntax)dimension.UpperBound).LiteralToken.Text);
    }

    [TestMethod]
    public void Parse_PreservesMultipleArrayDimensions()
    {
        var declaration = ParseLocal("Dim pixels(0 To 10, 1 To 20) As Long");
        var dimensions = declaration.Declarators.Single().Dimensions;

        Assert.AreEqual(2, dimensions.Length);
        Assert.AreEqual("0", ((LiteralExpressionSyntax)dimensions[0].LowerBound!).LiteralToken.Text);
        Assert.AreEqual("10", ((LiteralExpressionSyntax)dimensions[0].UpperBound).LiteralToken.Text);
        Assert.IsNotNull(dimensions[0].CommaToken);
        Assert.AreEqual("1", ((LiteralExpressionSyntax)dimensions[1].LowerBound!).LiteralToken.Text);
        Assert.AreEqual("20", ((LiteralExpressionSyntax)dimensions[1].UpperBound).LiteralToken.Text);
        Assert.IsNull(dimensions[1].CommaToken);
    }

    [TestMethod]
    public void Parse_PreservesDynamicLocalArray()
    {
        var declaration = ParseLocal("Dim TempArray() As String");
        var declarator = declaration.Declarators.Single();

        Assert.IsTrue(declarator.IsArray);
        Assert.AreEqual(0, declarator.Dimensions.Length);
        Assert.AreEqual("String", declarator.TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_RecognizesModuleArrayDeclaration()
    {
        const string source = "Private values(1 To 10) As Long";
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        var declaration = (ModuleVariableDeclarationSyntax)result.Root.Members.Single();
        Assert.IsTrue(declaration.Declarators.Single().IsArray);
    }

    private static FunctionDeclarationSyntax ParseFunction(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        return (FunctionDeclarationSyntax)result.Root.Members.Single();
    }

    private static DimStatementSyntax ParseLocal(string declaration)
    {
        var source = $"Sub Main()\n    {declaration}\nEnd Sub";
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(0, result.Diagnostics.Length, FormatDiagnostics(result));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        return (DimStatementSyntax)procedure.Statements.Single();
    }

    private static string FormatDiagnostics(VB6.Parser.ParseResult result) =>
        string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}