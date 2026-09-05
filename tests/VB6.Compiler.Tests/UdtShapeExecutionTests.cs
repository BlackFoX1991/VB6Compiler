namespace VB6.Compiler.Tests;

/// <summary>
/// The UDT shape inventory keeps scalar Variant fields separate from dynamic Variant arrays:
/// the former are supported value storage, while the latter do not have a managed array layout.
/// </summary>
[TestClass]
public sealed class UdtShapeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesScalarVariantUdtFields()
    {
        var output = VB6TestProgram.RunLines("""
            Type Record
                Value As Variant
            End Type

            Sub Main()
                Dim record As Record

                record.Value = "allowed"
                Debug.Print record.Value
                record.Value = 42
                Debug.Print record.Value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "allowed", "42" }, output);
    }

    [TestMethod]
    public void Analyze_RejectsDynamicVariantArrayUdtMembers()
    {
        var analysis = VBCompilation.Create("""
            Type Record
                Values() As Variant
            End Type

            Sub Main()
                Dim record As Record
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsTrue(
            analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0046"),
            string.Join(Environment.NewLine, analysis.Diagnostics));
    }
}
