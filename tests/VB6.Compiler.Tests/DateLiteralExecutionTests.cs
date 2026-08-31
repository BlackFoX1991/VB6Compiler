namespace VB6.Compiler.Tests;

/// <summary>
/// #...# date literals are core VB6 syntax and were not parsed at all, so every legacy source
/// carrying one failed at the lexer. The literal resolves to an OLE automation date at lex time
/// and travels on as an ordinary Date constant.
/// </summary>
[TestClass]
public sealed class DateLiteralExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesADateLiteralInUsOrder()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = #1/2/2000#
                Debug.Print Year(value); Month(value); Day(value)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2000 1 2" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesADateLiteralWithATimePart()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = #1/2/2000 3:04:05 PM#
                Debug.Print Hour(value); Minute(value); Second(value)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "15 4 5" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesATimeOnlyLiteral()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = #12:30:00#
                Debug.Print Hour(value); Minute(value)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "12 30" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_StillTreatsAHashAsAFileNumber()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim handle As Integer, line1 As String
                handle = FreeFile
                Open "datelit.txt" For Output As #handle
                Print #handle, "a#b#c"
                Close #handle
                handle = FreeFile
                Open "datelit.txt" For Input As #handle
                Line Input #handle, line1
                Close #handle
                Kill "datelit.txt"
                Debug.Print line1
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "a#b#c" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LeavesDoubleTypeSuffixesAlone()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print 5# - 2#
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "3" }, output);
    }
}
