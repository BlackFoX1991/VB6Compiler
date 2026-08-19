using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class DeclareParserTests
{
    private static DeclareDeclarationSyntax ParseDeclare(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        Assert.AreEqual(1, result.Root.Members.Length);
        Assert.IsInstanceOfType<DeclareDeclarationSyntax>(result.Root.Members[0]);
        return (DeclareDeclarationSyntax)result.Root.Members[0];
    }

    [TestMethod]
    public void Parse_DeclareFunctionWithAliasAndByValParameters()
    {
        var declaration = ParseDeclare(
            "Private Declare Function GetProp Lib \"user32\" Alias \"GetPropA\" (ByVal hwnd As Long, ByVal lpString As String) As Long");

        Assert.AreEqual("Private", declaration.VisibilityKeyword!.Text);
        Assert.AreEqual(SyntaxKind.FunctionKeyword, declaration.ProcedureKindKeyword.Kind);
        Assert.AreEqual("GetProp", declaration.Identifier.Text);
        Assert.AreEqual("\"user32\"", declaration.LibraryName.Text);
        Assert.AreEqual("\"GetPropA\"", declaration.AliasName!.Text);
        Assert.AreEqual(2, declaration.Parameters.Length);
        Assert.AreEqual(SyntaxKind.ByValKeyword, declaration.Parameters[0].PassingModeKeyword!.Kind);
        Assert.AreEqual("Long", declaration.Parameters[0].TypeToken.Text);
        Assert.AreEqual("String", declaration.Parameters[1].TypeToken.Text);
        Assert.AreEqual("Long", declaration.ReturnTypeToken!.Text);
    }

    [TestMethod]
    public void Parse_DeclareSubAcceptsImplicitByRefAndAsAny()
    {
        var declaration = ParseDeclare(
            "Private Declare Sub CopyMemory Lib \"kernel32\" Alias \"RtlMoveMemory\" (pDest As Any, pSource As Any, ByVal ByteLen As Long)");

        Assert.AreEqual(SyntaxKind.SubKeyword, declaration.ProcedureKindKeyword.Kind);
        Assert.AreEqual(3, declaration.Parameters.Length);
        Assert.IsNull(declaration.Parameters[0].PassingModeKeyword);
        Assert.AreEqual("Any", declaration.Parameters[0].TypeToken.Text);
        Assert.AreEqual("Any", declaration.Parameters[1].TypeToken.Text);
        Assert.AreEqual(SyntaxKind.ByValKeyword, declaration.Parameters[2].PassingModeKeyword!.Kind);
        Assert.IsNull(declaration.AsKeyword);
        Assert.IsNull(declaration.ReturnTypeToken);
    }

    [TestMethod]
    public void Parse_DeclareFunctionAllowsNoAlias()
    {
        var declaration = ParseDeclare(
            "Public Declare Function SetParent Lib \"user32\" (ByVal hWndChild As Long, ByVal hWndNewParent As Long) As Long");

        Assert.AreEqual("Public", declaration.VisibilityKeyword!.Text);
        Assert.IsNull(declaration.AliasKeyword);
        Assert.IsNull(declaration.AliasName);
        Assert.AreEqual(2, declaration.Parameters.Length);
    }
}
