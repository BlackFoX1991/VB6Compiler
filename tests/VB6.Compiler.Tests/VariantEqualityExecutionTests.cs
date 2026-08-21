using VB6.IR;

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
    public void Lower_LowersVariantLeftIntegerEqualityThroughDoubleConversions()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim value As Variant
                If value = 0 Then
                    Debug.Print 1
                End If
            End Sub
            """);

        // Both sides go through Double so the comparison has one defined numeric meaning rather
        // than depending on what the Variant currently holds.
        var equality = VB6TestIr.Expressions(program)
            .OfType<IrRuntimeCallExpression>()
            .Single(call => call.Method == IrRuntimeMethod.Equal);
        Assert.IsTrue(equality.Arguments.All(argument =>
            argument.Expression is IrRuntimeCallExpression { Method: IrRuntimeMethod.CDbl }));
    }

    [TestMethod]
    public void Analyze_KeepsNumericLeftVariantRightEqualityGuarded()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Debug.Print 0 = value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0053");
    }

    [TestMethod]
    public void Analyze_KeepsVariantDoubleEqualityGuarded()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Dim target As Double
                target = 0
                Debug.Print value = target
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0053");
    }

}
