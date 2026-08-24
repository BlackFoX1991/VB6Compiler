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
    public void EmitManagedApplication_PromotesIntegerVariantDivisionToSingle()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CInt(5)

                Debug.Print TypeName(value / CInt(2))
                Debug.Print value / CInt(2)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Single", "2.5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PromotesCurrencyAndDoubleToDouble()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CCur(1)

                Debug.Print TypeName(value + 0.5)
                Debug.Print value + 0.5
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Double", "1.5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PromotesCurrencyBeforeDoubleForMultiplication()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CCur(1)

                Debug.Print TypeName(value * 0.5)
                Debug.Print value * 0.5
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Currency", "0.5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesErrorVariantStateIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Variant
                value = CVErr(11)

                Debug.Print value
                Debug.Print IsError(value)
                Debug.Print IsError(Empty)
                Debug.Print VarType(value)
                Debug.Print TypeName(value)
                Debug.Print TypeName(Null)
                Debug.Print TypeName(Nothing)
                Debug.Print IsNull(CVErr(Null))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Error 11", "True", "False", "10", "Error", "Null", "Nothing", "True" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_DistinguishesExplicitAndImplicitErrorConversions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim errorValue As Variant
                Dim number As Long
                errorValue = CVErr(2001)

                Debug.Print CInt(errorValue)
                Debug.Print CDbl(errorValue)

                On Error Resume Next
                number = errorValue
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2001", "2001", "13" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesErrorVariantOperatorContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim errorValue As Variant
                Dim sameError As Variant
                Dim laterError As Variant

                errorValue = CVErr(2001)
                sameError = CVErr(2001)
                laterError = CVErr(2002)

                Debug.Print errorValue = sameError
                Debug.Print errorValue < laterError

                On Error Resume Next
                Debug.Print errorValue + 1
                Debug.Print Err.Number
                Err.Clear
                Debug.Print errorValue & "value"
                Debug.Print Err.Number
                Err.Clear
                Debug.Print errorValue And 1
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "True", "True", "13", "13", "13" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesMissingVariantAsError448OutsideIsMissing()
    {
        var output = VB6TestProgram.RunLines("""
            Private Sub Inspect(Optional value)
                Debug.Print IsMissing(value)
                Debug.Print TypeName(value)
                Debug.Print CInt(value)

                On Error Resume Next
                Debug.Print value + 1
                Debug.Print Err.Number
                Err.Clear
                Debug.Print value & "x"
                Debug.Print Err.Number
                Err.Clear
                Debug.Print value = 1
                Debug.Print Err.Number
                Err.Clear
                Debug.Print CStr(value)
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub

            Sub Main()
                Inspect
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "Error", "448", "448", "448", "448", "448" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesArrayVariantTypeAndOperatorContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim values As Variant
                values = Array(1, 2)

                Debug.Print IsArray(values)
                Debug.Print TypeName(values)
                Debug.Print VarType(values)

                On Error Resume Next
                Debug.Print values + 1
                Debug.Print Err.Number
                Err.Clear
                Debug.Print values & "x"
                Debug.Print Err.Number
                Err.Clear
                Debug.Print values = values
                Debug.Print Err.Number
                On Error GoTo 0
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "True", "Variant()", "8204", "13", "13", "13" },
            output);
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
