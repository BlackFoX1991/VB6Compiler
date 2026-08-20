using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class OptionalParameterParserTests
{
    [TestMethod]
    public void Parse_RecognizesVisiaStyleOptionalParameters()
    {
        const string source = """
            Public Function AddOfficeBorders(frmForm As Form, _
                                             Optional blnNoBorderStyle As Boolean, _
                                             Optional strMsgBoxTitle As String) _
                                            As Long
            End Function
            """;

        var result = new ParserType(SourceText.From(source, "envBorders.bas")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var function = (FunctionDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(3, function.Parameters.Length);
        Assert.IsNull(function.Parameters[0].OptionalKeyword);
        Assert.AreEqual(SyntaxKind.OptionalKeyword, function.Parameters[1].OptionalKeyword!.Kind);
        Assert.IsNull(function.Parameters[1].PassingModeKeyword);
        Assert.AreEqual("Boolean", function.Parameters[1].TypeToken!.Text);
        Assert.AreEqual(SyntaxKind.OptionalKeyword, function.Parameters[2].OptionalKeyword!.Kind);
        Assert.IsNull(function.Parameters[2].DefaultValue);
    }

    [TestMethod]
    public void Parse_PreservesOptionalPassingModeAndDefaultValue()
    {
        const string source = """
            Sub Configure(Optional ByVal retries As Long = -1, Optional ByRef caption As String = "VISIA")
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        Assert.AreEqual(2, sub.Parameters.Length);

        var retries = sub.Parameters[0];
        Assert.AreEqual(SyntaxKind.OptionalKeyword, retries.OptionalKeyword!.Kind);
        Assert.AreEqual(SyntaxKind.ByValKeyword, retries.PassingModeKeyword!.Kind);
        Assert.IsNotNull(retries.EqualsToken);
        Assert.IsInstanceOfType<UnaryExpressionSyntax>(retries.DefaultValue);

        var caption = sub.Parameters[1];
        Assert.AreEqual(SyntaxKind.OptionalKeyword, caption.OptionalKeyword!.Kind);
        Assert.AreEqual(SyntaxKind.ByRefKeyword, caption.PassingModeKeyword!.Kind);
        Assert.IsNotNull(caption.EqualsToken);
        Assert.IsInstanceOfType<LiteralExpressionSyntax>(caption.DefaultValue);
        Assert.AreEqual("VISIA", ((LiteralExpressionSyntax)caption.DefaultValue!).LiteralToken.Value);
    }

    [TestMethod]
    public void Parse_AllowsUntypedOptionalParameterAsVariantShape()
    {
        const string source = """
            Sub Configure(Optional value)
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length);
        var sub = (SubDeclarationSyntax)result.Root.Members.Single();
        var parameter = sub.Parameters.Single();
        Assert.AreEqual(SyntaxKind.OptionalKeyword, parameter.OptionalKeyword!.Kind);
        Assert.AreEqual("value", parameter.Identifier.Text);
        Assert.IsNull(parameter.AsKeyword);
        Assert.IsNull(parameter.TypeToken);
    }
}
