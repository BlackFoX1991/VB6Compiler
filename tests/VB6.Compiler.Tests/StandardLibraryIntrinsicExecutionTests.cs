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
}
