using VB6.Parser;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ContextualAliasParserTests
{
    [TestMethod]
    public void Parse_AllowsAliasAsOptionalParameterAndExpressionName()
    {
        const string source = """
            Function HasAlias(Optional Alias As String = "") As Boolean
                HasAlias = Alias <> ""
            End Function
            """;

        var result = new ParserType(SourceText.From(source, "Module1.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual("Alias", function.Parameters.Single().Identifier.Text);
        Assert.IsNotNull(function.Parameters.Single().OptionalKeyword);
        Assert.IsNotNull(function.Parameters.Single().DefaultValue);
    }

    [TestMethod]
    public void Parse_PreservesDeclareAliasClauseAcrossLineContinuations()
    {
        const string source = """
            Public Declare Function GetWindowLong _
                Lib "user32" _
                Alias "GetWindowLongA" _
                (ByVal hWnd As Long) As Long
            """;

        var result = new ParserType(SourceText.From(source, "Module1.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(Environment.NewLine, result.Diagnostics));
        var declaration = (DeclareDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual("Alias", declaration.AliasKeyword?.Text);
        Assert.AreEqual("GetWindowLongA", declaration.AliasName?.Value);
    }
}
