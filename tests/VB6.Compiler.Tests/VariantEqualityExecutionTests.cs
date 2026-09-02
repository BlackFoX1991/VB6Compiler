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

    [TestMethod]
    public void EmitManagedApplication_OrdersVariantNumericValueBeforeNonNumericString()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = 1

                Debug.Print value < "abc"
                Debug.Print value = "abc"
                Debug.Print "abc" > value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False", "True" }, output);
    }

    [TestMethod]
    public void Lower_SelectsTypedAndVariantOperatorDispatchPaths()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim left As Long
                Dim right As Long
                Dim value As Variant

                If left < right Then
                    Debug.Print left + right
                End If

                Debug.Print value + right
            End Sub
            """);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                IrRuntimeMethod.Less,
                IrRuntimeMethod.AddLong,
                IrRuntimeMethod.AddVariant
            },
            VB6TestIr.RuntimeCalls(program).ToArray());
    }

    [TestMethod]
    public void EmitManagedApplication_MapsOperatorOverflowAndInvalidVariantArrayToVbErrors()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim number As Long
                Dim value As Variant

                On Error Resume Next
                number = 2147483647
                Debug.Print number + 1
                Debug.Print Err.Number
                Err.Clear

                value = Array(1, 2)
                Debug.Print value + 1
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "6", "13" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_MapsOperatorDivisionFailuresToVbErrors()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim numerator As Double
                Dim zero As Double
                Dim divisor As Long
                Dim quotient As Long

                numerator = 1
                zero = 0
                divisor = 0

                On Error Resume Next
                Debug.Print numerator / zero
                Debug.Print Err.Number
                Err.Clear
                Debug.Print zero / zero
                Debug.Print Err.Number
                Err.Clear
                quotient = numerator \ divisor
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "11", "6", "11" }, output);
    }


    [TestMethod]
    public void EmitManagedApplication_AppliesTheAbsorbingCasesOfTheNullTruthTable()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim n As Variant
                n = Null

                Debug.Print Describe(n And True)
                Debug.Print Describe(n And False)
                Debug.Print Describe(n Or True)
                Debug.Print Describe(n Or False)
                Debug.Print Describe(n Imp True)
                Debug.Print Describe(False Imp n)
                Debug.Print Describe(n Imp False)
                Debug.Print Describe(n Xor True)
                Debug.Print Describe(Not n)
                Debug.Print Describe(n And 0)
                Debug.Print Describe(n And 1)
                Debug.Print Describe(n Or -1)
                Debug.Print Describe(n Or 1)
            End Sub

            Function Describe(ByVal value As Variant) As String
                If IsNull(value) Then
                    Describe = "Null"
                Else
                    Describe = CStr(value)
                End If
            End Function
            """);

        // VB6 loest die dreiwertige Logik nicht mit "Null gewinnt" auf: And steht fest, sobald
        // eine Seite False ist, Or sobald eine Seite True ist, und Imp sobald der Vordersatz
        // False oder der Nachsatz True ist. Xor, Eqv und Not haben keinen solchen Fall.
        // Numerisch entscheiden nur 0 und der Wert mit allen gesetzten Bits.
        CollectionAssert.AreEqual(
            new[]
            {
                "Null", "False", "True", "Null",
                "True", "True", "Null",
                "Null", "Null",
                "0", "Null", "-1", "Null"
            },
            output);
    }
}
