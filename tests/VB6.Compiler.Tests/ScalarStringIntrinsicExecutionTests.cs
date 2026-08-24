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
    public void EmitManagedApplication_ExecutesUnicodeStringIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print AscW(ChrW(&H20AC))
                Debug.Print AscW("A")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "8364", "65" }, output);
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

    [TestMethod]
    public void EmitManagedApplication_ExecutesFormatStringAndNumericMasks()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Format$(5459.4, "##,##0.00")
                Debug.Print Format$(5, "0.00%")
                Debug.Print Format$("HELLO", "<")
                Debug.Print Format$("hello", ">")
                Debug.Print Format$("AB", "@@@")
                Debug.Print Format$("AB", "!@@@")
                Debug.Print Format$("AB", "&&&")
                Debug.Print Format$("", "@@;empty")
                Debug.Print Format$(CDate(43832), "yyyy-mm-dd")
                Debug.Print Format$(CDate(0.5), "hh:nn:ss")
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "5,459.40", "500.00%", "hello", "HELLO", "AB", "AB", "AB", "empty", "2020-01-02", "12:00:00" },
            VB6TestProgram.SplitLines(output),
            output);
    }
}
