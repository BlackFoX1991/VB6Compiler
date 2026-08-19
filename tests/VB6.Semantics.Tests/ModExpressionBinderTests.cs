using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ModExpressionBinderTests
{
    [TestMethod]
    public void Bind_BindsIntegerModExpression()
    {
        const string source = """
            Sub Main()
                Dim value As Integer
                value = 17 Mod 5
            End Sub
            """;

        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);

        var model = new Binder(text).BindCompilationUnit(parseResult.Root);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var mod = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(SyntaxKind.ModKeyword, mod.OperatorKind);
        Assert.AreEqual(TypeSymbol.Integer, mod.Type);
        Assert.AreEqual(TypeSymbol.Integer, mod.Left.Type);
        Assert.AreEqual(TypeSymbol.Integer, mod.Right.Type);
    }
}
