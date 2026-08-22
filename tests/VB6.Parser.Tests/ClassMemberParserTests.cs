using VB6.Parser;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class ClassMemberParserTests
{
    [TestMethod]
    public void Parse_PreservesPropertyAccessorsAndEvents()
    {
        var result = new Parser(SourceText.From("""
            Public Event Changed(ByVal value As Long)

            Property Get Value() As Long
                Value = 1
            End Property

            Property Let Value(ByVal newValue As Long)
            End Property

            Property Set Child(ByVal value As Variant)
            End Property
            """, "ClassMembers.cls")).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Count(diagnostic => diagnostic.Severity == VB6.Syntax.Diagnostics.DiagnosticSeverity.Error));
        Assert.IsInstanceOfType<EventDeclarationSyntax>(result.Root.Members[0]);
        var get = (PropertyDeclarationSyntax)result.Root.Members[1];
        var let = (PropertyDeclarationSyntax)result.Root.Members[2];
        var set = (PropertyDeclarationSyntax)result.Root.Members[3];
        Assert.IsTrue(get.IsGet);
        Assert.IsTrue(let.IsLet);
        Assert.IsTrue(set.IsSet);
        Assert.AreEqual("Value", get.Identifier.Text);
        Assert.AreEqual("Long", get.ReturnTypeToken!.Text);
        Assert.AreEqual(1, get.Statements.Length);
    }

}
