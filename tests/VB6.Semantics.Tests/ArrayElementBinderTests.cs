using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayElementBinderTests
{
    [TestMethod]
    public void Bind_ArrayElementReadAndWriteUseElementTypeAndLongIndices()
    {
        var model = BindSource("""
            Sub Main()
                Dim i As Integer
                Dim values(1 To 3) As Long
                values(i) = 42
                Debug.Print values(i)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var statements = model.Procedures.Single().Body.Statements;
        var assignment = statements.OfType<BoundArrayElementAssignmentStatement>().Single();
        Assert.AreEqual(TypeSymbol.Long, ((ArrayTypeSymbol)assignment.Array.Type).ElementType);
        Assert.AreEqual(TypeSymbol.Long, assignment.Indices.Single().Type);
        Assert.AreEqual(TypeSymbol.Long, assignment.Expression.Type);

        var print = statements.OfType<BoundDebugPrintStatement>().Single();
        var access = print.Expression as BoundArrayAccessExpression;
        Assert.IsNotNull(access);
        Assert.AreEqual(TypeSymbol.Long, access.Type);
        Assert.AreEqual(TypeSymbol.Long, access.Indices.Single().Type);
    }

    [TestMethod]
    public void Bind_ArrayParameterReadResolvesAsArrayInsteadOfFunctionCall()
    {
        var model = BindSource("""
            Function First(values() As Long) As Long
                First = values(1)
            End Function
            """);

        Assert.AreEqual(0, model.Diagnostics.Length);

        var assignment = model.Procedures.Single().Body.Statements
            .OfType<BoundAssignmentStatement>()
            .Single();
        Assert.IsInstanceOfType<BoundArrayAccessExpression>(assignment.Expression);
        Assert.AreEqual(TypeSymbol.Long, assignment.Expression.Type);
    }

    [TestMethod]
    public void Bind_ArrayAccessReportsRankMismatch()
    {
        var model = BindSource("""
            Sub Main()
                Dim grid(1 To 2, 1 To 2) As Long
                Debug.Print grid(1)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0027" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_ArrayElementAssignmentRejectsScalarTarget()
    {
        var model = BindSource("""
            Sub Main()
                Dim value As Long
                value(1) = 2
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0026" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
        Assert.IsInstanceOfType<BoundArrayElementAssignmentStatement>(
            model.Procedures.Single().Body.Statements.Last());
    }

    private static SemanticModel BindSource(string source)
    {
        var text = SourceText.From(source, "test.bas");
        var parseResult = new ParserType(text).ParseCompilationUnit();
        Assert.AreEqual(
            0,
            parseResult.Diagnostics.Length,
            string.Join(Environment.NewLine, parseResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        return new Binder(text).BindCompilationUnit(parseResult.Root);
    }
}
