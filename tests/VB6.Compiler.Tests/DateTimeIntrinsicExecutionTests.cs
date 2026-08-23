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
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "2020", "1", "2", "12", "0", "0" },
            VB6TestProgram.SplitLines(output),
            output);
    }
}
