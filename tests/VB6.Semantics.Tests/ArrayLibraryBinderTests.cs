using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayLibraryBinderTests
{
    [TestMethod]
    public void Bind_EraseDistinguishesFixedAndDynamicArrays()
    {
        var model = BindSource("""
            Sub Main()
                Dim fixedValues(1 To 2) As Long
                Dim dynamicValues() As Long
                Erase fixedValues, dynamicValues
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var erases = model.Procedures.Single().Body.Statements.OfType<BoundEraseStatement>().ToArray();
        Assert.AreEqual(2, erases.Length);
        Assert.IsFalse(erases[0].Deallocate);
        Assert.IsTrue(erases[1].Deallocate);
    }

    [TestMethod]
    public void Bind_EraseRejectsScalarButAllowsArrayParameter()
    {
        var scalar = BindSource("""
            Sub Main()
                Dim value As Long
                Erase value
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0033" },
            scalar.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var parameter = BindSource("""
            Sub ClearValues(values() As Long)
                Erase values
            End Sub
            """);
        Assert.AreEqual(0, parameter.Diagnostics.Length, FormatDiagnostics(parameter));
        var erase = parameter.Procedures.Single().Body.Statements.OfType<BoundEraseStatement>().Single();
        Assert.IsTrue(erase.Deallocate);
    }

    [TestMethod]
    public void Bind_LBoundDefaultsToFirstDimensionAndReturnsLong()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(3 To 5) As Long
                Debug.Print LBound(values)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var print = model.Procedures.Single().Body.Statements.OfType<BoundDebugPrintStatement>().Single();
        var bound = print.Expression as BoundArrayBoundExpression;
        Assert.IsNotNull(bound);
        Assert.IsFalse(bound.IsUpperBound);
        Assert.AreEqual(TypeSymbol.Long, bound.Type);
        Assert.AreEqual(1L, GetIntegerValue(bound.Dimension));
    }

    [TestMethod]
    public void Bind_UBoundConvertsExplicitDimensionToLong()
    {
        var model = BindSource("""
            Sub Main()
                Dim dimension As Integer
                Dim grid(1 To 2, 4 To 6) As Long
                Debug.Print UBound(grid, dimension)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var print = model.Procedures.Single().Body.Statements.OfType<BoundDebugPrintStatement>().Single();
        var bound = print.Expression as BoundArrayBoundExpression;
        Assert.IsNotNull(bound);
        Assert.IsTrue(bound.IsUpperBound);
        Assert.AreEqual(TypeSymbol.Long, bound.Dimension.Type);
    }

    [TestMethod]
    public void Bind_UBoundAcceptsArrayNameWithEmptyParentheses()
    {
        var model = BindSource("""
            Sub Main()
                Dim values() As Long
                Debug.Print UBound(values())
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var print = model.Procedures.Single().Body.Statements.OfType<BoundDebugPrintStatement>().Single();
        var bound = print.Expression as BoundArrayBoundExpression;
        Assert.IsNotNull(bound);
        Assert.IsInstanceOfType<BoundVariableExpression>(bound.Array);
        Assert.IsInstanceOfType<ArrayTypeSymbol>(bound.Array.Type);
    }

    [TestMethod]
    public void Bind_ArrayBoundsRejectScalarAndWrongArity()
    {
        var scalar = BindSource("""
            Sub Main()
                Dim value As Long
                Debug.Print LBound(value)
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0035" },
            scalar.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var wrongArity = BindSource("""
            Sub Main()
                Dim values(1 To 2) As Long
                Debug.Print UBound(values, 1, 2)
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0034" },
            wrongArity.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    private static long GetIntegerValue(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression { Value: long value } => value,
        BoundConversionExpression conversion => GetIntegerValue(conversion.Expression),
        _ => throw new AssertFailedException($"Expected integer expression, got {expression.GetType().Name}.")
    };

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

    private static string FormatDiagnostics(SemanticModel model) =>
        string.Join(Environment.NewLine, model.Diagnostics.Select(diagnostic => diagnostic.ToString()));
}
