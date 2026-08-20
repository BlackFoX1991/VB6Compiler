using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Parser.Tests;

[TestClass]
public sealed class TypeOfParserTests
{
    [TestMethod]
    public void Parse_TypeOfIsConditionWithoutParserErrors()
    {
        const string source = """
            Sub Main()
                If TypeOf ctlControl Is CheckBox Then
                    Debug.Print 1
                End If
            End Sub
            """;

        var result = new ParserType(SourceText.From(source, "test.bas")).ParseCompilationUnit();

        Assert.AreEqual(
            0,
            result.Diagnostics.Length,
            string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        var procedure = (SubDeclarationSyntax)result.Root.Members.Single();
        var ifStatement = (IfStatementSyntax)procedure.Statements.Single();
        var typeOf = (TypeOfExpressionSyntax)ifStatement.Condition;
        Assert.AreEqual("TypeOf", typeOf.TypeOfToken.Text);
        Assert.AreEqual("ctlControl", ((NameExpressionSyntax)typeOf.Expression).IdentifierToken.Text);
        Assert.AreEqual("CheckBox", typeOf.TypeName.Text);
    }
}
