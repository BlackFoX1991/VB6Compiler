using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class Int64TypeBinderTests
{
    [TestMethod]
    public void Bind_RecognizesLongLongAndInt64Aliases()
    {
        var model = BindSource("""
            Function AddOne(ByVal value As Int64) As LongLong
                AddOne = value + 1
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        Assert.AreEqual(TypeSymbol.LongLong, procedure.Symbol.ReturnType);
        Assert.AreEqual(TypeSymbol.LongLong, procedure.Symbol.Parameters.Single().Type);

        var assignment = (BoundAssignmentStatement)procedure.Body.Statements.Single();
        var add = (BoundBinaryExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.LongLong, add.Type);
        Assert.AreEqual(TypeSymbol.LongLong, add.Left.Type);
        Assert.AreEqual(TypeSymbol.LongLong, add.Right.Type);
    }

    [TestMethod]
    public void Bind_InfersInt64ForIntegerLiteralBeyondInt32()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Int64
                value = 3000000000
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var assignment = (BoundAssignmentStatement)model.Procedures.Single().Body.Statements[1];
        var literal = (BoundLiteralExpression)assignment.Expression;
        Assert.AreEqual(TypeSymbol.LongLong, literal.Type);
        Assert.AreEqual(3000000000L, literal.Value);
    }

    [TestMethod]
    public void Bind_AllowsInt64ForControlVariable()
    {
        var model = BindSource("""
            Sub Main()
                Dim i As Int64
                For i = 3000000000 To 3000000002
                    Debug.Print i
                Next i
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var loop = model.Procedures.Single().Body.Statements.OfType<BoundForStatement>().Single();
        Assert.AreEqual(TypeSymbol.LongLong, loop.ControlVariable.Type);
        Assert.AreEqual(TypeSymbol.LongLong, loop.InitialValue.Type);
        Assert.AreEqual(TypeSymbol.LongLong, loop.Limit.Type);
        Assert.AreEqual(TypeSymbol.LongLong, loop.Step.Type);
    }

    [TestMethod]
    public void Bind_RecognizesLongPtrAsNativeIntegerType()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As LongPtr
                value = value + 1
                For value = 1 To 2
                Next value
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);
        var procedure = model.Procedures.Single();
        var value = procedure.Body.Statements
            .OfType<BoundVariableDeclarationStatement>()
            .Single()
            .Variable;
        Assert.AreEqual(TypeSymbol.LongPtr, value.Type);
        var assignment = procedure.Body.Statements.OfType<BoundAssignmentStatement>().Single();
        Assert.AreEqual(TypeSymbol.LongPtr, assignment.Expression.Type);
        var loop = procedure.Body.Statements.OfType<BoundForStatement>().Single();
        Assert.AreEqual(TypeSymbol.LongPtr, loop.ControlVariable.Type);
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(0, parseResult.Diagnostics.Length);
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
