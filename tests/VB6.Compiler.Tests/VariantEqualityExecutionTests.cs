using VB6.IR;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantEqualityExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ComparesEmptyAndNumericStringVariantToInteger()
    {
        const string source = """
            Sub Main()
                Dim value As Variant
                Debug.Print value = 0

                value = "42"
                Debug.Print value = 42
                Debug.Print value = 41
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "True", "True", "False" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesVariantFunctionReturnSlotToInteger()
    {
        const string source = """
            Function NumberExpression() As Variant
                If NumberExpression = 0 Then
                    NumberExpression = 42
                End If
            End Function

            Sub Main()
                Debug.Print NumberExpression()
            End Sub
            """;

        var output = VB6TestProgram.Run(source);

        CollectionAssert.AreEqual(
            new[] { "42" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void Lower_UsesVariantComparisonForVariantLeftIntegerEquality()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim value As Variant
                If value = 0 Then
                    Debug.Print 1
                End If
            End Sub
            """);

        // Variant comparison is selected by the binder. Scalar operands remain scalar in the IR;
        // the managed emitter boxes them when calling the object-based runtime method.
        var equality = VB6TestIr.Expressions(program)
            .OfType<IrRuntimeCallExpression>()
            .Single(call => call.Method == IrRuntimeMethod.VariantEqual);
        Assert.AreEqual(TypeSymbol.Variant, equality.Arguments[0].Expression.Type);
        Assert.AreEqual(TypeSymbol.Integer, equality.Arguments[1].Expression.Type);
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesNumericLeftToVariant()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim value As Variant
                Debug.Print 0 = value
            End Sub
            """, "Module1.bas");

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesVariantToDouble()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim value As Variant
                Dim target As Double
                target = 0
                Debug.Print value = target
            End Sub
            """, "Module1.bas");

        Assert.AreEqual("True", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesVariantStringsAndRelationalOperators()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = "42"
                Debug.Print value = 42
                Debug.Print value > 41
                Debug.Print 41 < value
                Debug.Print value <> 41
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "True", "True" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesDecimalVariantAgainstDoubleAtDecimalPrecision()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print CDec("0.100000000000000005") > 0.1
                Debug.Print CDec("0.100000000000000005") = 0.1
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesCurrencyAndSingleVariantsAtTheirPrecision()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim currencyValue As Variant
                Dim singleValue As Variant

                currencyValue = CCur(1)
                singleValue = CSng(0.1)

                Debug.Print currencyValue = 1.00004
                Debug.Print currencyValue = 1.00006
                Debug.Print singleValue = 0.1
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False", "True" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PropagatesNullThroughComparisonsAndIf()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = Null
                Debug.Print value = 0
                Debug.Print value <> 0
                If value = 0 Then
                    Debug.Print "true"
                Else
                    Debug.Print "false"
                End If
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Null", "Null", "false" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesStaticStringComparisonWithVariant()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = 2

                Debug.Print "10" = value
                Debug.Print "10" < value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "False", "True" }, output);
    }

}
