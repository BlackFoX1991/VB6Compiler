namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DateTimeIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesDatePartIntrinsics()
    {
        var output = VB6TestProgram.Run("""
            Sub Main()
                Debug.Print Year(CDate(43832))
                Debug.Print Month(CDate(43832))
                Debug.Print Day(CDate(43832))
                Debug.Print Hour(CDate(0.5))
                Debug.Print Minute(CDate(0.5))
                Debug.Print Second(CDate(0.5))
                Debug.Print Format$(DateSerial(2020, 1, 2), "yyyy-mm-dd")
                Debug.Print Format$(TimeSerial(12, 30, 45), "hh:nn:ss")
                Debug.Print Format$(DateAdd("m", 1, CDate(43832)), "yyyy-mm-dd")
                Debug.Print DateDiff("d", CDate(43832), CDate(43833))
                Debug.Print Format$(DateAdd("ww", 1, CDate(43832)), "yyyy-mm-dd")
                Debug.Print DateDiff("w", CDate(43832), CDate(43839))
                Debug.Print DateDiff("ww", CDate(43832), CDate(43835))
                Debug.Print DateDiff("ww", CDate(43832), CDate(43835), 2)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "2020", "1", "2", "12", "0", "0", "2020-01-02", "12:30:45", "2020-02-02", "1", "2020-01-09", "1", "1", "0" },
            VB6TestProgram.SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesDateValueAndTimeValue()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Format$(DateValue(CDate(43832.75)), "yyyy-mm-dd")
                Debug.Print Format$(TimeValue(CDate(43832.75)), "hh:nn:ss")
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "2020-01-02", "18:00:00" }, output);
    }
}
