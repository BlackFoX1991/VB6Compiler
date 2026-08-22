namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ScalarStringIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesSearchReplacementAndConversionIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print InStr("abc", "b")
                Debug.Print InStr(1, "abc", "B", vbTextCompare)
                Debug.Print InStrRev("abca", "A", -1, vbTextCompare)
                Debug.Print Replace("a-b-b", "b", "x")
                Debug.Print StrConv("aBc", vbUpperCase)
                Debug.Print Int(-1.2)
                Debug.Print UBound(Split("a,b,c", ","))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2", "2", "4", "a-x-x", "ABC", "-2", "2" }, VB6TestProgram.SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesBoundsAndElementsOfAnArrayHeldInVariant()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Dim values As Variant
                values = Split("a,b,c", ",")
                Debug.Print LBound(values)
                Debug.Print UBound(values)
                Debug.Print values(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "2", "b" }, VB6TestProgram.SplitLines(output), output);
    }
}
