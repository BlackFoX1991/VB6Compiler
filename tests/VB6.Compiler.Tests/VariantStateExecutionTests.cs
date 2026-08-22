namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantStateExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantStateIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant

                Debug.Print IsEmpty(Empty)
                Debug.Print IsNull(Empty)
                Debug.Print VarType(Empty)

                value = Null
                Debug.Print IsEmpty(value)
                Debug.Print IsNull(value)
                Debug.Print VarType(value)

                Debug.Print IsMissing(Missing)
                Debug.Print IsEmpty(Nothing)
                Debug.Print VarType(Nothing)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "False", "0", "False", "True", "1", "True", "False", "9" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_UserDeclarationShadowsVariantIntrinsic()
    {
        var output = VB6TestProgram.Run("""
            Function IsEmpty(ByVal value As Variant) As Boolean
                IsEmpty = False
            End Function

            Sub Main()
                Debug.Print IsEmpty(Empty)
            End Sub
            """);

        Assert.AreEqual("False", output.Trim());
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesCDecAsVariantDecimal()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CDec("1.25")

                Debug.Print VarType(value)
                Debug.Print value + 1
                Debug.Print value * 2
                Debug.Print value / 2
                Debug.Print value ^ 2
                Debug.Print value Mod 1
                Debug.Print value \ 1
                Debug.Print Not value
                Debug.Print value And 3
                Debug.Print value = CDec("1.2500000000000000000000000001")
                Debug.Print value + CDbl(1)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "14", "2.25", "2.5", "0.625", "1.5625", "0.25", "1", "-2", "1", "False", "2.25" },
            output);
    }
}
