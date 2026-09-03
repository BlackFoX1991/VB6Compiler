namespace VB6.Compiler.Tests;

/// <summary>
/// The last two declarations missing from the documented VB6 function set. Both are small, and
/// both answer rather than refuse: a program that merely asks must not fall over.
/// </summary>
[TestClass]
public sealed class FileAttrExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ReportsTheModeOfAnOpenChannel()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6FileAttr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "daten.txt").Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            var source = string.Join(
                Environment.NewLine,
                "Sub Main()",
                "    On Error Resume Next",
                "    Dim f As Integer",
                "    f = FreeFile",
                "",
                "    ' 2 ist Output, 1 ist Input, 32 ist Binary -- dieselben Bits, die Open benutzt.",
                "    Open \"" + file + "\" For Output As #f",
                "    Debug.Print FileAttr(f)",
                "    Close #f",
                "    Open \"" + file + "\" For Input As #f",
                "    Debug.Print FileAttr(f)",
                "    Close #f",
                "    Open \"" + file + "\" For Binary As #f",
                "    Debug.Print FileAttr(f)",
                "",
                "    ' Ein DOS-Handle gibt es in 32-Bit-VB6 nicht; die Antwort ist 5.",
                "    Debug.Print FileAttr(f, 2)",
                "    Debug.Print Err.Number",
                "    Close #f",
                "    Err.Clear",
                "",
                "    ' Ein geschlossener Kanal meldet 52.",
                "    Debug.Print FileAttr(f)",
                "    Debug.Print Err.Number",
                "End Sub");

            var output = VB6TestProgram.RunLines(source);

            CollectionAssert.AreEqual(new[] { "2", "1", "32", "5", "52" }, output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_AnswersIMEStatusWithoutAnInputMethod()
    {
        // vbIMEModeNoControl. Dieser Host installiert nie eine Eingabemethode, und das ist die
        // Antwort, die VB6 auf einem System ohne eine solche gibt.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print IMEStatus()
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0" }, output);
    }
}
