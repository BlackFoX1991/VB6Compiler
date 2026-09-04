namespace VB6.Compiler.Tests;

/// <summary>
/// <c>For Each</c> over something the compiler cannot classify at bind time: a Variant, or an
/// imported COM object. VB6 does not classify it either — it asks the value at run time and answers
/// 438 when the value has no enumerator. The binder used to refuse the whole loop with
/// <c>VB6S0055</c>, so <c>For Each k In dict.Keys</c> did not compile.
/// </summary>
[TestClass]
public sealed class ForEachObjectExecutionTests
{
    private const string ScriptingLibraryId = "{420B2830-E718-11CF-893D-00A0C9054228}";

    [TestMethod]
    public void EmitManagedApplication_IteratesAVariantHoldingAnArrayOrACollection()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
            Dim v As Variant
            v = Array(10, 20, 30)
            Dim e As Variant
            For Each e In v
                Debug.Print e
            Next

            Dim c As Collection
            Set c = New Collection
            c.Add "x"
            c.Add "y"
            Dim o As Variant
            Set o = c
            For Each e In o
                Debug.Print e
            Next
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "10", "20", "30", "x", "y" },
            output,
            string.Join(" | ", output));
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsThatAValueHasNoEnumerator()
    {
        // Nothing similar, nothing silent: a value with no enumerator is 438, and Nothing is 91.
        // The loop header carries its own protected region, so the number reaches the handler in
        // the same procedure -- and the loop is skipped, which is where the other loop headers
        // continue too. A good source afterwards still runs.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next
                Dim e As Variant
                Dim n As Variant

                n = 5
                Err.Clear
                For Each e In n
                    Debug.Print "nie"
                Next
                Debug.Print Err.Number

                Set n = Nothing
                Err.Clear
                For Each e In n
                    Debug.Print "nie"
                Next
                Debug.Print Err.Number

                Err.Clear
                n = Array(7)
                For Each e In n
                    Debug.Print e
                Next
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "438", "91", "7", "0" },
            output,
            string.Join(" | ", output));
    }

    [TestMethod]
    public void EmitManagedApplication_RoutesAForEachSourceErrorToAnOnErrorGoToHandler()
    {
        // Die GoTo-Form desselben Vertrags: der Fehler aus dem Schleifenkopf erreicht den Handler,
        // statt das Programm zu beenden, waehrend der Handler danebensteht.
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error GoTo Handler
                Dim e As Variant
                Dim n As Variant
                n = 5
                For Each e In n
                    Debug.Print "nie"
                Next
                Debug.Print "nicht erreicht"
                Exit Sub
            Handler:
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "438" }, output, string.Join(" | ", output));
    }

    [TestMethod]
    public void EmitManagedApplication_IteratesAComCollection()
    {
        if (!OperatingSystem.IsWindows() ||
            Type.GetTypeFromProgID("Scripting.Dictionary", throwOnError: false) is null)
        {
            Assert.Inconclusive("The registered Windows Scripting Runtime fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ForEachCom",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Schleife.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Schleife"
                Reference=*\G{ScriptingLibraryId}#1.0#0#scrrun.dll#Scripting
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim d As Scripting.Dictionary
                    Set d = New Scripting.Dictionary
                    d.Add "a", 1
                    d.Add "b", 2

                    Dim k As Variant
                    For Each k In d.Keys
                        Debug.Print k
                    Next

                    ' Das Dictionary selbst hat _NewEnum und wird direkt aufgezaehlt.
                    For Each k In d
                        Debug.Print k
                    Next
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "a", "b", "a", "b" },
                VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
