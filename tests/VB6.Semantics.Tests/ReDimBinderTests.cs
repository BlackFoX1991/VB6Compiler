using VB6.Parser;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ReDimBinderTests
{
    [TestMethod]
    public void Bind_ReDimAllocatesDynamicArrayBounds()
    {
        var model = BindSource("""
            Option Base 1
            Sub Main()
                Dim values() As Long
                ReDim values(3)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var reDim = model.Procedures.Single().Body.Statements.OfType<BoundReDimStatement>().Single();
        Assert.IsFalse(reDim.Preserve);
        Assert.AreEqual(1, reDim.ArrayDimensions.Length);
        AssertBound(reDim.ArrayDimensions[0], 1, 3);
        Assert.IsNull(((ArrayTypeSymbol)reDim.Target.Type).Rank);
    }

    [TestMethod]
    public void Bind_ReDimPreserveKeepsPreserveFlagAndExplicitBounds()
    {
        var model = BindSource("""
            Sub Main()
                Dim values() As Long
                ReDim values(0 To 2)
                ReDim Preserve values(0 To 4)
            End Sub
            """);

        Assert.AreEqual(0, model.Diagnostics.Length, FormatDiagnostics(model));
        var reDims = model.Procedures.Single().Body.Statements.OfType<BoundReDimStatement>().ToArray();
        Assert.AreEqual(2, reDims.Length);
        Assert.IsFalse(reDims[0].Preserve);
        Assert.IsTrue(reDims[1].Preserve);
        AssertBound(reDims[1].ArrayDimensions.Single(), 0, 4);
    }

    [TestMethod]
    public void Bind_ReDimRejectsFixedArray()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(2) As Long
                ReDim values(4)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0029" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_ReDimRejectsScalarAndMissingBounds()
    {
        var scalar = BindSource("""
            Sub Main()
                Dim value As Long
                ReDim value(4)
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0029" },
            scalar.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var missingBounds = BindSource("""
            Sub Main()
                Dim values() As Long
                ReDim values
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0030" },
            missingBounds.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_ReDimRejectsElementTypeChange()
    {
        var model = BindSource("""
            Sub Main()
                Dim values() As Long
                ReDim values(4) As Integer
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "VB6S0031" },
            model.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    [TestMethod]
    public void Bind_ArrayParameterMustBeByRefAndCannotDeclareBounds()
    {
        var byVal = BindSource("""
            Sub Sort(ByVal values() As Long)
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0028" },
            byVal.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());

        var fixedRank = BindSource("""
            Sub Sort(values(10) As Long)
            End Sub
            """);
        CollectionAssert.AreEqual(
            new[] { "VB6S0032" },
            fixedRank.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray());
    }

    private static void AssertBound(BoundArrayDimension dimension, long expectedLower, long expectedUpper)
    {
        Assert.AreEqual(expectedLower, GetIntegerValue(dimension.LowerBound));
        Assert.AreEqual(expectedUpper, GetIntegerValue(dimension.UpperBound));
    }

    private static long GetIntegerValue(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression { Value: long value } => value,
        BoundConversionExpression conversion => GetIntegerValue(conversion.Expression),
        BoundUnaryExpression { OperatorKind: VB6.Syntax.SyntaxKind.MinusToken } unary => -GetIntegerValue(unary.Operand),
        _ => throw new AssertFailedException($"Expected integer bound, got {expression.GetType().Name}.")
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
