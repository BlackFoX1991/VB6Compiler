namespace VB6.Compiler.Tests;

/// <summary>
/// Named arguments on a late-bound call. The names cannot be resolved when the call is compiled,
/// so both dispatch paths have to match them at run time: a COM target through GetIDsOfNames, a
/// managed one against its own parameter metadata.
/// </summary>
[TestClass]
public sealed class NamedArgumentExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_MatchesNamedArgumentsOnAComTarget()
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

                d.Add Key:="a", Item:=1
                Debug.Print d("a")

                ' Die Reihenfolge der Namen ist gleichgültig, und gemischt mit Positionen auch.
                d.Add Item:=2, Key:="b"
                Debug.Print d("b")
                d.Add "c", Item:=3
                Debug.Print d("c")
                Debug.Print d.Count

                d.Add Unbekannt:="x", Item:=4
                Debug.Print Err.Number
            End Sub
            """);

        // 448 ist die dokumentierte VB6-Antwort auf einen Namen, den das Ziel nicht kennt.
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "3", "448" }, output);
    }

    [TestMethod]
    public void Lower_CarriesLateBoundNamedArgumentsToTheDispatchLayer()
    {
        var program = VB6TestIr.Lower("""
            Sub Main()
                Dim o As Object
                o.Tue Wert:=1, Zweitens:=2
            End Sub
            """);

        var calls = VB6TestIr.RuntimeCalls(program).ToArray();
        Assert.AreEqual(
            2,
            calls.Count(method => method == VB6.IR.IrRuntimeMethod.NamedArgument),
            "Jedes benannte Argument reist mit seinem Namen zur Laufzeit.");
    }

    [TestMethod]
    public void Bind_StillResolvesNamedArgumentsOfADeclaredProcedure()
    {
        // Eine bekannte Signatur wird weiterhin zur Übersetzungszeit aufgelöst -- der neue Weg
        // gilt nur dort, wo es keine Signatur gibt.
        var matched = VB6TestProgram.RunLines("""
            Function Differenz(ByVal Links As Long, ByVal Rechts As Long) As Long
                Differenz = Links - Rechts
            End Function

            Sub Main()
                Debug.Print Differenz(Rechts:=3, Links:=10)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "7" }, matched);

        var result = VBCompilation.Create("""
            Function Differenz(ByVal Links As Long, ByVal Rechts As Long) As Long
                Differenz = Links - Rechts
            End Function

            Sub Main()
                Debug.Print Differenz(Falsch:=3, Links:=10)
            End Sub
            """).Analyze();

        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0069"),
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
    }

    [TestMethod]
    public void EmitManagedApplication_MatchesNamedArgumentsOnAClassTarget()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6NamedArguments", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Named.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Named"
                Class=Rechner; Rechner.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Rechner.cls"), """
                Option Explicit

                Public Function Differenz(ByVal Links As Long, ByVal Rechts As Long) As Long
                    Differenz = Links - Rechts
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    On Error Resume Next
                    Dim o As Object
                    Set o = New Rechner
                    Debug.Print o.Differenz(Links:=10, Rechts:=3)
                    Debug.Print o.Differenz(Rechts:=3, Links:=10)
                    Debug.Print o.Differenz(10, Rechts:=4)
                    Debug.Print o.Differenz(Falsch:=1, Rechts:=2)
                    Debug.Print Err.Number
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);

            // Die vierte Zeile schlägt fehl und druckt nichts -- Resume Next überspringt den Rest
            // der Anweisung, nicht nur den Ausdruck.
            CollectionAssert.AreEqual(
                new[] { "7", "7", "6", "448" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
