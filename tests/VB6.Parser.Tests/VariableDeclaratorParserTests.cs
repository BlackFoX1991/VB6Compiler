using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class VariableDeclaratorParserTests
{
    [TestMethod]
    public void Parse_RecognizesTypedLocalDeclaratorList()
    {
        const string source = """
            Sub Main()
                Dim small As Integer, wide As Long, label As String
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var declaration = (DimStatementSyntax)((SubDeclarationSyntax)result.Root.Members.Single()).Statements.Single();
        Assert.AreEqual(3, declaration.Declarators.Length);
        Assert.AreEqual("small", declaration.Declarators[0].Identifier.Text);
        Assert.AreEqual("Integer", declaration.Declarators[0].TypeToken!.Text);
        Assert.AreEqual(SyntaxKind.CommaToken, declaration.Declarators[0].CommaToken!.Kind);
        Assert.AreEqual("wide", declaration.Declarators[1].Identifier.Text);
        Assert.AreEqual("Long", declaration.Declarators[1].TypeToken!.Text);
        Assert.AreEqual("label", declaration.Declarators[2].Identifier.Text);
        Assert.AreEqual("String", declaration.Declarators[2].TypeToken!.Text);
        Assert.IsNull(declaration.Declarators[2].CommaToken);
    }

    [TestMethod]
    public void Parse_DoesNotFlowTrailingTypeToEarlierDeclarator()
    {
        const string source = """
            Sub Main()
                Dim implicitVariant, typed As Integer
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var declaration = (DimStatementSyntax)((SubDeclarationSyntax)result.Root.Members.Single()).Statements.Single();
        Assert.AreEqual(2, declaration.Declarators.Length);
        Assert.AreEqual("implicitVariant", declaration.Declarators[0].Identifier.Text);
        Assert.IsNull(declaration.Declarators[0].AsKeyword);
        Assert.IsNull(declaration.Declarators[0].TypeToken);
        Assert.AreEqual("typed", declaration.Declarators[1].Identifier.Text);
        Assert.AreEqual("Integer", declaration.Declarators[1].TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_RecognizesModuleDeclaratorLists()
    {
        const string source = """
            Public Left As Integer, Right As Long
            Dim implicitVariant, Count As Long
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var declarations = result.Root.Members.OfType<ModuleVariableDeclarationSyntax>().ToArray();
        Assert.AreEqual(2, declarations.Length);
        Assert.AreEqual(2, declarations[0].Declarators.Length);
        Assert.AreEqual("Left", declarations[0].Declarators[0].Identifier.Text);
        Assert.AreEqual("Right", declarations[0].Declarators[1].Identifier.Text);
        Assert.AreEqual("Long", declarations[0].Declarators[1].TypeToken!.Text);
        Assert.AreEqual(2, declarations[1].Declarators.Length);
        Assert.IsNull(declarations[1].Declarators[0].TypeToken);
        Assert.AreEqual("Long", declarations[1].Declarators[1].TypeToken!.Text);
    }
}
