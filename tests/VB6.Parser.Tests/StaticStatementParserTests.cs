using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class StaticStatementParserTests
{
    [TestMethod]
    public void Parse_RecognizesStaticLocalDeclaratorList()
    {
        const string source = """
            Sub Main()
                Static count As Integer, total As Long
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var declaration = (StaticStatementSyntax)procedure.Statements.Single();
        Assert.AreEqual(SyntaxKind.StaticKeyword, declaration.StaticKeyword.Kind);
        Assert.AreEqual(2, declaration.Declarators.Length);
        Assert.AreEqual("count", declaration.Declarators[0].Identifier.Text);
        Assert.AreEqual("Integer", declaration.Declarators[0].TypeToken!.Text);
        Assert.AreEqual("total", declaration.Declarators[1].Identifier.Text);
        Assert.AreEqual("Long", declaration.Declarators[1].TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_PreservesUntypedStaticDeclaratorAsVariantShape()
    {
        const string source = """
            Function NextValue() As Long
                Static implicitVariant, count As Long
                NextValue = count
            End Function
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (FunctionDeclarationSyntax)result.Root.Members.Single();
        var declaration = (StaticStatementSyntax)procedure.Statements[0];
        Assert.AreEqual(2, declaration.Declarators.Length);
        Assert.IsNull(declaration.Declarators[0].AsKeyword);
        Assert.IsNull(declaration.Declarators[0].TypeToken);
        Assert.AreEqual("Long", declaration.Declarators[1].TypeToken!.Text);
    }

    [TestMethod]
    public void Parse_StaticParticipatesInColonSeparatedStatementLists()
    {
        const string source = """
            Sub Main()
                Static count As Integer: count = 1
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(2, procedure.Statements.Length);
        Assert.IsInstanceOfType<StaticStatementSyntax>(procedure.Statements[0]);
        Assert.IsInstanceOfType<AssignmentStatementSyntax>(procedure.Statements[1]);
    }
}