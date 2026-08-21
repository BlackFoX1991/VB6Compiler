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
    public void GenerateCSharp_LowersVariantLeftIntegerEqualityThroughDoubleConversions()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                If value = 0 Then
                    Debug.Print 1
                End If
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        StringAssert.Contains(generation.Source, "VBOperators.Equal(VBConversions.CDbl(__vb6_value), VBConversions.CDbl(");
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
