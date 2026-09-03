namespace VB6.Compiler.Tests;

/// <summary>
/// The same measurement one layer up: generated VB6 code talking to a COM server this project did
/// not build. This is the end-to-end proof the EXCEPINFO mapping was missing when it was written.
/// </summary>
[TestClass]
public sealed class ComInteropExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_LeavesAGapInAComArgumentList()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM interop measurement requires Windows.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6ComOptional", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "gap.txt").Replace("\\", "\\\\", StringComparison.Ordinal);

        try
        {
            // Eine Lücke mitten in der Argumentliste, nicht am Ende: VB6 überträgt sie als
            // VT_ERROR mit DISP_E_PARAMNOTFOUND, und nur daran erkennt der Server, dass das
            // Argument fehlt statt leer zu sein.
            var source = string.Join(
                Environment.NewLine,
                "Sub Main()",
                "    On Error Resume Next",
                "    Dim fso As Object",
                "    Set fso = CreateObject(\"Scripting.FileSystemObject\")",
                "    Dim writer As Object",
                "    Set writer = fso.CreateTextFile(\"" + file + "\")",
                "    writer.WriteLine \"hallo\"",
                "    writer.Close",
                "    Debug.Print Err.Number",
                "    Err.Clear",
                "    Dim reader As Object",
                "    Set reader = fso.OpenTextFile(\"" + file + "\", , False)",
                "    Debug.Print Err.Number",
                "    Debug.Print reader.ReadLine()",
                "    reader.Close",
                "End Sub");

            CollectionAssert.AreEqual(
                new[] { "0", "0", "hallo" },
                VB6TestProgram.RunLines(source));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_CarriesComServerErrorNumbersIntoErr()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM interop measurement requires Windows.");
            return;
        }

        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim d As Object
                Set d = CreateObject("Scripting.Dictionary")
                d.Add "a", 1
                d.Add "b", 2
                Debug.Print d.Count
                Debug.Print d.Item("a")

                d.Add "a", 3
                Debug.Print Err.Number
                Debug.Print Err.Description
                Err.Clear

                d.Remove "absent"
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "2",
                "1",
                "457",
                "This key is already associated with an element of this collection",
                "32811"
            },
            output);
    }
}
