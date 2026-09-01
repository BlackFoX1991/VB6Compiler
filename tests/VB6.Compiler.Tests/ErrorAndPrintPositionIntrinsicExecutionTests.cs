namespace VB6.Compiler.Tests;

/// <summary>
/// The remaining three names of card <c>l1-02-k</c>. <c>Error</c> is a keyword because
/// <c>On Error</c> needs it, so its statement form and its function form each get their own path
/// through the parser. <c>Tab</c> and <c>Spc</c> position the next output item instead of
/// producing a value, so they travel through a print list as a marker every print path resolves.
/// </summary>
[TestClass]
public sealed class ErrorAndPrintPositionIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ReturnsTheDocumentedMessageForAnErrorNumber()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Error(5)
                Debug.Print Error$(53)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "Invalid procedure call or argument", "File not found" },
            output);
    }

    /// <summary>VB6 answers a number it does not document with one generic message.</summary>
    [TestMethod]
    public void EmitManagedApplication_FallsBackToTheGenericMessageForAnUndocumentedNumber()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Error(99999)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "Application-defined or object-defined error" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RaisesWithTheErrorStatement()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Error 53
                Debug.Print Err.Number & "|" & Err.Description
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "53|File not found" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RoutesTheErrorStatementIntoAHandler()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo Failed
                Error 6
                Exit Sub
            Failed:
                Debug.Print Err.Number & "|" & Err.Description
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "6|Overflow" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PositionsAnOutputItemWithTab()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print "a"; Tab(10); "b"
            End Sub
            """);

        Assert.AreEqual('a', output[0]);
        Assert.AreEqual('b', output[9]);
    }

    [TestMethod]
    public void EmitManagedApplication_InsertsSpacesWithSpc()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print "a"; Spc(3); "b"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "a   b" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PositionsAnOutputItemInAFileWithTab()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim handle As Integer, line1 As String
                handle = FreeFile
                Open "tabspc.txt" For Output As #handle
                Print #handle, "a"; Tab(6); "b"
                Close #handle
                handle = FreeFile
                Open "tabspc.txt" For Input As #handle
                Line Input #handle, line1
                Close #handle
                Kill "tabspc.txt"
                Debug.Print Len(line1) & "|" & line1
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "6|a    b" }, output);
    }
}
