using VB6.Compiler;

namespace VB6.CodeGen.CSharp.Tests;

[TestClass]
public sealed class VariantFoundationCodeGenTests
{
    [TestMethod]
    public void Generate_EmitsExplicitVariantStorageParametersReturnsAndArrays()
    {
        var generation = VBCompilation.Create("""
            Sub Consume(ByVal value As Variant)
                Debug.Print value
            End Sub

            Function Echo(ByVal value As Variant) As Variant
                Echo = value
            End Function

            Sub Main()
                Dim value As Variant
                Dim values(1 To 2) As Variant
                value = 42
                values(1) = value
                Consume values(1)
                value = Echo("hello")
                Debug.Print value
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        StringAssert.Contains(generation.Source, "object? __vb6_arg_value");
        StringAssert.Contains(generation.Source, "object? __vb6_return = default;");
        StringAssert.Contains(generation.Source, "object? __vb6_value = default;");
        StringAssert.Contains(generation.Source, "VBArray<object?> __vb6_values = new VBArray<object?>");

        using var peStream = new MemoryStream();
        var emitResult = new CSharpAssemblyEmitter().Emit(generation.Source, "GeneratedVariantFoundationProgram", peStream);
        Assert.IsTrue(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}")));
    }

    [TestMethod]
    public void GenerateCSharp_BlocksVariantOperatorsUntilPromotionRulesExist()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                value = 1
                Debug.Print value + 1
            End Sub
            """, "test.bas").GenerateCSharp();

        Assert.IsFalse(generation.Success);
        Assert.IsNull(generation.Source);
        Assert.IsTrue(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
    }
}
