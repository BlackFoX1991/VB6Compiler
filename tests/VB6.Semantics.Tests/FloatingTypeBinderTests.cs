using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class FloatingTypeBinderTests
{
    [TestMethod]
    public void Bind_ConvertsUnsuffixedFloatingLiteralToSingleOnAssignment()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Single
                value = 1.5
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var conversion = (BoundConversionExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Single, conversion.TargetType);
        Assert.AreEqual(TypeSymbol.Double, conversion.Expression.Type);
    }

    [TestMethod]
    public void Bind_PromotesSingleAndIntegerToSingle()
    {
        var model = BindSource("""
            Sub Main()
                Dim left As Single
                Dim result As Single
                left = 1.5
                result = left + 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Single, add.Type);
        Assert.AreEqual(TypeSymbol.Single, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Single, add.Right.Type);
    }

    [TestMethod]
    public void Bind_PromotesSingleAndLongToDouble()
    {
        var model = BindSource("""
            Sub Main()
                Dim left As Single
                Dim right As Long
                Dim result As Double
                result = left + right
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[3];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Double, add.Type);
        Assert.AreEqual(TypeSymbol.Double, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Double, add.Right.Type);
    }

    [TestMethod]
    public void Bind_IntegerFloatingDivisionProducesSingle()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Single
                value = 1 / 2
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var divide = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Single, divide.Type);
        Assert.AreEqual(TypeSymbol.Single, divide.Left.Type);
        Assert.AreEqual(TypeSymbol.Single, divide.Right.Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
