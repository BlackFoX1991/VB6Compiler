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
                Debug.Print VarType(Split("a,b", ","))
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "False", "0", "False", "True", "1", "True", "False", "9", "8200" },
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

    [TestMethod]
    public void EmitManagedApplication_ExecutesErrorVariantStateIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CVErr(11)

                Debug.Print IsError(value)
                Debug.Print IsError(Empty)
                Debug.Print VarType(value)
                Debug.Print TypeName(value)
                Debug.Print TypeName(Null)
                Debug.Print TypeName(Nothing)
                Debug.Print IsNull(CVErr(Null))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "False", "10", "Error", "Null", "Nothing", "True" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesDateSubtypeInsideVariant()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim typedDate As Date
                Dim value As Variant

                typedDate = CDate(43832)
                value = typedDate

                Debug.Print VarType(value)
                Debug.Print TypeName(value)
                Debug.Print CDbl(value)
                Debug.Print CDbl(CDate(value))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "7", "Date", "43832", "43832" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesDateSubtypeThroughVariantArithmetic()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CDate(43832)

                Debug.Print TypeName(value + 1)
                Debug.Print Format$(value + 1, "yyyy-mm-dd")
                Debug.Print TypeName(value - value)
                Debug.Print value - value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Date", "2020-01-03", "Double", "0" }, output);
    }
}
