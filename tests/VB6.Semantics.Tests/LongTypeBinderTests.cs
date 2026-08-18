using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class LongTypeBinderTests
{
    [TestMethod]
    public void Bind_PromotesIntegerAndLongArithmeticToLong()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Long
                value = 40000 + 1
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.Long, add.Type);
        Assert.AreEqual(TypeSymbol.Long, add.Left.Type);
        Assert.AreEqual(TypeSymbol.Long, add.Right.Type);
    }

    [TestMethod]
    public void Bind_AllowsLongForControlVariableAndLongFunctionReturn()
    {
        var model = BindSource("""
            Function CountUp(ByVal limit As Long) As Long
                Dim i As Long
                For i = 1 To limit
                    CountUp = i
                Next i
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.Long, procedure.Symbol.ReturnType);
        Assert.AreEqual(TypeSymbol.Long, procedure.Symbol.Parameters.Single().Type);
        var loop = procedure.Body.Statements.OfType<BoundForStatement>().Single();
        Assert.AreEqual(TypeSymbol.Long, loop.ControlVariable.Type);
        Assert.AreEqual(TypeSymbol.Long, loop.Step.Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
