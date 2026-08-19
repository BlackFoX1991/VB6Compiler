using VB6.Syntax;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ModuleMemberParserTests
{
    private static CompilationUnitSyntax Parse(string source)
    {
        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        return result.Root;
    }

    [TestMethod]
    public void Parse_SkipsAttributeLines()
    {
        var root = Parse("""
            Attribute VB_Name = "modMain"
            Sub Main()
                Debug.Print 1
            End Sub
            """);

        Assert.AreEqual(2, root.Members.Length);
        Assert.IsInstanceOfType<AttributeSyntax>(root.Members[0]);
        Assert.IsInstanceOfType<SubDeclarationSyntax>(root.Members[1]);
    }

    [TestMethod]
    public void Parse_KeepsAttributeUsableAsAnIdentifier()
    {
        // 'Attribute' is not reserved in VB6, so it must still work as a variable name.
        var root = Parse("""
            Sub Main()
                Dim Attribute As Integer
                Attribute = 1
            End Sub
            """);

        Assert.AreEqual(1, root.Members.Length);
        Assert.IsInstanceOfType<SubDeclarationSyntax>(root.Members[0]);
    }

    [TestMethod]
    public void Parse_AcceptsVisibilityModifiersOnProcedures()
    {
        var root = Parse("""
            Public Sub Main()
                Debug.Print 1
            End Sub

            Private Function Twice(ByVal value As Integer) As Integer
                Twice = value
            End Function
            """);

        var sub = (SubDeclarationSyntax)root.Members[0];
        var function = (FunctionDeclarationSyntax)root.Members[1];
        Assert.AreEqual("Public", sub.VisibilityKeyword!.Text);
        Assert.AreEqual("Private", function.VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_ReadsModuleVariableDeclarations()
    {
        var root = Parse("""
            Public Source As String
            Private Position As Long
            Dim Counter As Integer

            Sub Main()
                Debug.Print Counter
            End Sub
            """);

        Assert.AreEqual(4, root.Members.Length);
        var declarations = root.Members.OfType<ModuleVariableDeclarationSyntax>().ToArray();
        Assert.AreEqual(3, declarations.Length);
        Assert.AreEqual("Source", declarations[0].Identifier.Text);
        Assert.AreEqual("Public", declarations[0].VisibilityKeyword!.Text);
        Assert.AreEqual("String", declarations[0].TypeToken.Text);
        Assert.AreEqual("Dim", declarations[2].VisibilityKeyword!.Text);
    }

    [TestMethod]
    public void Parse_DoesNotTreatDeclareAsModuleVariable()
    {
        var root = Parse("""
            Private Declare Function GetTickCount Lib "kernel32" () As Long
            """);

        Assert.AreEqual(0, root.Members.OfType<ModuleVariableDeclarationSyntax>().Count());
        Assert.AreEqual(1, root.Members.OfType<DeclareDeclarationSyntax>().Count());
    }
}
