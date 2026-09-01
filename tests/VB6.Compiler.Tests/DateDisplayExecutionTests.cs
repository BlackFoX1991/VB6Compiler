namespace VB6.Compiler.Tests;

/// <summary>
/// A Date shares its representation with an OLE automation Double, and printing used to show
/// that raw serial number instead of a date. These cases state the display contract by name so
/// it stops being an incidental readout inside tests about other subjects.
///
/// The rendering is the documented General Date form - date only while the value carries no time
/// of day, date and time otherwise - and it stays invariant in the deterministic profile like the
/// rest of the runtime.
/// </summary>
[TestClass]
public sealed class DateDisplayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PrintsATypedDateAsAGeneralDate()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = CDate(43832)
                Debug.Print value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2020-01-02" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PrintsTheTimeOfDayWhenTheDateCarriesOne()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = CDate(43832.75)
                Debug.Print value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2020-01-02 18:00:00" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PrintsADateSubtypeVariantAsADate()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim typed As Date
                Dim value As Variant
                typed = CDate(43832)
                value = typed
                Debug.Print value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2020-01-02" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_PrintsADateInsideAnOutputList()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim value As Date
                value = CDate(43832)
                Debug.Print "on "; value
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "on 2020-01-02" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LeavesPlainNumbersUnchanged()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim serial As Double
                serial = 43832
                Debug.Print serial
                Debug.Print 42
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "43832", "42" }, output);
    }
}
