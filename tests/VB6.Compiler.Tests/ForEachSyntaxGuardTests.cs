namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachSyntaxGuardTests
{
    [TestMethod]
    public void Analyze_LowersFixedArrayForEachWithVariantControlVariable()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim item As Variant
                Dim values(1 To 2) As Long
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas").Analyze();

        Assert.IsNotNull(analysis.SemanticModel);
        Assert.IsTrue(
            analysis.Success,
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsFalse(analysis.ParseResult.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6P0001"));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0052"));
    }

    [TestMethod]
    public void GenerateCSharp_LowersFixedArrayForEachThroughExistingNumericLoopBackend()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim item As Variant
                Dim values(1 To 2) As Long
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "__vb6_for_each_index_0_1");
        StringAssert.Contains(generation.Source, ".LBound(");
        StringAssert.Contains(generation.Source, ".UBound(");
        StringAssert.Contains(generation.Source, "__vb6_item = __vb6_values[");
    }

    [TestMethod]
    public void Analyze_RequiresVariantControlVariableForArrayForEach()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim item As Long
                Dim values(1 To 2) As Long
                For Each item In values
                Next item
            End Sub
            """, "test.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0054"));
    }

    [TestMethod]
    public void GenerateCSharp_BindsUnknownRankArrayForEachToRuntimeEnumeration()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim item As Variant
                Dim values() As Long
                ReDim values(2 To 4)
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0055"));
        StringAssert.Contains(generation.Source, "foreach (var __vb6_for_each_item_");
        StringAssert.Contains(generation.Source, "__vb6_values.EnumerateValues()");
    }

    [TestMethod]
    public void Analyze_KeepsUdtElementArrayForEachGuarded()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Value As Long
            End Type

            Sub Main()
                Dim item As Variant
                Dim values(1 To 2) As Record
                For Each item In values
                Next item
            End Sub
            """, "test.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0056"));
    }
}
