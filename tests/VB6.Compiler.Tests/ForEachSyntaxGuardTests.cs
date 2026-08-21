using VB6.IR;

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
    public void Lower_LowersFixedArrayForEachThroughExistingNumericLoopBackend()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim item As Variant
                Dim values(1 To 2) As Long
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas");

        var arrayCalls = VB6TestIr.ArrayCalls(program);

        // The declared bounds are known, so the loop counts from LBound to UBound and indexes the
        // array; nothing enumerates it.
        CollectionAssert.IsSubsetOf(
            new[] { IrArrayOperation.LBound, IrArrayOperation.UBound },
            arrayCalls.ToArray());
        CollectionAssert.DoesNotContain(arrayCalls.ToArray(), IrArrayOperation.GetFlatValue);
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
    public void Lower_BindsUnknownRankArrayForEachToRuntimeEnumeration()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim item As Variant
                Dim values() As Long
                ReDim values(2 To 4)
                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "test.bas");

        // Bounds a ReDim decides at runtime cannot drive a counted loop, so the array is walked in
        // storage order instead - which is also the order VB6 enumerates in.
        CollectionAssert.IsSubsetOf(
            new[] { IrArrayOperation.Length, IrArrayOperation.GetFlatValue },
            VB6TestIr.ArrayCalls(program).ToArray());
    }

    /// <summary>
    /// VB6 rejects this too. For Each needs a Variant control variable, and a Type declared in a
    /// standard module cannot be coerced into one - only public types in public object modules can.
    /// The diagnostic therefore has to survive; it is not a placeholder for missing support.
    /// </summary>
    [TestMethod]
    public void Analyze_RejectsForEachOverUserDefinedTypeArray()
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
        var diagnostic = analysis.Diagnostics.SingleOrDefault(item => item.Code == "VB6S0056");
        Assert.IsNotNull(diagnostic, "Expected VB6S0056 for a For Each over a user-defined type array.");
        StringAssert.Contains(diagnostic.Message, "Record");
        StringAssert.Contains(diagnostic.Message, "public object modules");
    }
}
