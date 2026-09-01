namespace VB6.Compiler.Tests;

/// <summary>
/// Three documented intrinsics of card <c>l1-02-k</c> that had no name to bind to. <c>AscB</c>
/// and <c>ChrB</c> complete the byte-string family <c>LeftB</c>, <c>RightB</c>, <c>MidB</c>,
/// <c>InStrB</c> and <c>LenB</c> already form, and <c>CLngLng</c> reaches the LongLong
/// conversion the runtime always had while no VB6 source could name it.
/// </summary>
[TestClass]
public sealed class ByteStringAndLongLongIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ReadsTheFirstByteWithAscB()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print AscB("A")
                Debug.Print Asc("A")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "65", "65" }, output);
    }

    /// <summary>
    /// ChrB produces exactly one byte of the active byte view, which the deterministic profile
    /// reads back as one UTF-16 character - so Len is 1 while LenB is 2.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ProducesASingleByteWithChrB()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Len(ChrB(65))
                Debug.Print LenB(ChrB(65))
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "1", "2" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ConvertsToLongLongWithCLngLng()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As LongLong
                value = CLngLng("42")
                Debug.Print value
                Debug.Print CLngLng(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "42", "1" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsInvalidArgumentsForTheByteIntrinsics()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Debug.Print AscB("")
                Debug.Print "asc=" & Err.Number
                Err.Clear
                Debug.Print ChrB(300)
                Debug.Print "chr=" & Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "asc=5", "chr=5" }, output);
    }
}
