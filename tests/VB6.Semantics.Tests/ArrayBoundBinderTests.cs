using VB6.Parser;
using VB6.Syntax;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Semantics.Tests;

[TestClass]
public sealed class ArrayBoundBinderTests
{
    [TestMethod]
    public void Bind_OptionBaseAppliesOnlyToImplicitLowerBounds()
    {
        var model = BindSource("""
            Option Base 1
            Sub Main()
                Dim implicitBase(10) As Long
                Dim explicitBase(0 To 10) As Long
            End Sub
            """);

        var declarations = model.Procedures.Single().Body.Statements
            .OfType<BoundVariableDeclarationStatement>()
            .ToArray();

        Assert.AreEqual(2, declarations.Length);
        AssertBound(declarations[0].ArrayDimensions.Single(), 1, 10);
        AssertBound(declarations[1].ArrayDimensions.Single(), 0, 10);
    }

    [TestMethod]
    public void Bind_DefaultOptionBaseIsZero()
    {
        var model = BindSource("""
            Sub Main()
                Dim values(5) As Integer
            End Sub
            """);

        var declaration = model.Procedures.Single().Body.Statements
            .OfType<BoundVariableDeclarationStatement>()
            .Single();

        AssertBound(declaration.ArrayDimensions.Single(), 0, 5);
    }

    [TestMethod]
    public void Bind_MultidimensionalArrayPreservesIndependentBounds()
    {
        var model = BindSource("""
            Option Base 1
            Sub Main()
                Dim grid(4, -2 To 7, 0 To 3) As Long
            End Sub
            """);

        var declaration = model.Procedures.Single().Body.Statements
            .OfType<BoundVariableDeclarationStatement>()
            .Single();

        Assert.AreEqual(3, declaration.ArrayDimensions.Length);
        AssertBound(declaration.ArrayDimensions[0], 1, 4);
        AssertBound(declaration.ArrayDimensions[1], -2, 7);
        AssertBound(declaration.ArrayDimensions[2], 0, 3);
    }

    [TestMethod]
    public void Bind_ModuleArrayCarriesResolvedBounds()
    {
        var model = BindSource("""
            Option Base 1
            Private values(8) As Long
            Sub Main()
            End Sub
            """);

        var moduleVariable = model.ModuleVariables.Single();
        AssertBound(moduleVariable.ArrayDimensions.Single(), 1, 8);
    }

    [TestMethod]
    public void Bind_DynamicArrayHasNoAllocationBounds()
    {
        var model = BindSource("""
            Option Base 1
            Sub Main()
                Dim values() As Long
            End Sub
            """);

        var declaration = model.Procedures.Single().Body.Statements
            .OfType<BoundVariableDeclarationStatement>()
            .Single();

        Assert.AreEqual(0, declaration.ArrayDimensions.Length);
        Assert.IsInstanceOfType<ArrayTypeSymbol>(declaration.Variable.Type);
    }

    private static void AssertBound(BoundArrayDimension dimension, long expectedLower, long expectedUpper)
    {
        Assert.AreEqual(expectedLower, GetIntegerValue(dimension.LowerBound));
        Assert.AreEqual(expectedUpper, GetIntegerValue(dimension.UpperBound));
        Assert.AreEqual(TypeSymbol.Long, dimension.LowerBound.Type);
        Assert.AreEqual(TypeSymbol.Long, dimension.UpperBound.Type);
    }

    private static long GetIntegerValue(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression { Value: long value } => value,
        BoundConversionExpression conversion => GetIntegerValue(conversion.Expression),
        BoundUnaryExpression { OperatorKind: SyntaxKind.MinusToken } unary => -GetIntegerValue(unary.Operand),
        _ => throw new AssertFailedException($"Expected a bound integer expression, got {expression.GetType().Name}.")
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
}
