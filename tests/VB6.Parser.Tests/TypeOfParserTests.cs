using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

/// <summary>
/// <c>TypeOf x Is T</c> asks whether an object reference has a given class. The semantics need the
/// object model, but the syntax is preserved so a single occurrence no longer derails the file -
/// it accounted for 72 parser errors in one conformance module.
/// </summary>
[TestClass]
public sealed class TypeOfParserTests
{
    [TestMethod]
    public void Parse_TypeOfInsideASingleLineIf()
    {
        const string source = """
            Sub Apply(ctlControl As Object)
                If TypeOf ctlControl Is CheckBox Then Debug.Print 1
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var ifStatement = (IfStatementSyntax)procedure.Statements.Single();
        var typeOf = (TypeOfExpressionSyntax)ifStatement.Condition;

        Assert.AreEqual("CheckBox", typeOf.TypeToken.Text);
        Assert.IsInstanceOfType<NameExpressionSyntax>(typeOf.Expression);
    }

    [TestMethod]
    public void Parse_NegatedTypeOf()
    {
        const string source = """
            Sub Apply(ctlControl As Object)
                If Not TypeOf ctlControl Is Frame Then Debug.Print 1
            End Sub
            """;

        var result = new ParserType(SourceText.From(source)).ParseCompilationUnit();

        Assert.AreEqual(0, result.Diagnostics.Length, string.Join(", ", result.Diagnostics.Select(d => d.Message)));
    }
}
