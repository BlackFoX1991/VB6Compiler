namespace VB6.Compiler.Tests;

[TestClass]
public sealed class StandardLibraryIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesNumericStringIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Val("  -12.5 tail")
                Debug.Print Hex(255)
                Debug.Print Oct(459)
                Debug.Print "[" & Str(459) & "]"
                Debug.Print "[" & Str(-459.65) & "]"
                Debug.Print IsNull(Oct(Null))
                Debug.Print VarType(CVar(CDate(43832)))
                Debug.Print String(3, "x")
                Debug.Print String(3, 65)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "-12.5", "FF", "713", "[ 459]", "[-459.65]", "True", "7", "xxx", "AAA" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_FormatsErrorVariantWithCStr()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print CStr(CVErr(11))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Error 11" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesQBColor()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print QBColor(12)
                On Error Resume Next
                Debug.Print QBColor(16)
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "255", "5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesCallByNameThroughDynamicDispatch()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim values As Collection
                Dim ignored As Variant

                Set values = New Collection
                values.Add "one"
                Debug.Print CallByName(values, "Count", vbGet)
                Debug.Print CallByName(values, "Item", vbGet, 1)
                ignored = CallByName(values, "Add", vbMethod, "two")
                Debug.Print CallByName(values, "Count", vbGet)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "one", "2" }, output);
    }
}
