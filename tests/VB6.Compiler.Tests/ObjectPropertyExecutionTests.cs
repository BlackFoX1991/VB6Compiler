namespace VB6.Compiler.Tests;

/// <summary>
/// A class property that holds an object needs a Get and a Set of the same name — that pair is the
/// normal form, not an edge case. It used to break: the Set stored correctly, the Get answered
/// Nothing.
/// </summary>
[TestClass]
public sealed class ObjectPropertyExecutionTests
{
    [TestMethod]
    public void Compile_RejectsAccessToAPrivateClassField()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6PrivateField", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Sicht.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Sicht"
                Class=Halter; Halter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Halter.cls"), """
                Option Explicit

                Private m_geheim As Long
                Public Offen As Long

                Public Sub Setze()
                    m_geheim = 5
                    Offen = 6
                End Sub

                Public Function Eigen() As Long
                    Eigen = Me.m_geheim
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim h As Halter
                    Set h = New Halter
                    h.Setze
                    Debug.Print h.Offen
                    Debug.Print h.m_geheim
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).AnalyzeForEmission();
            var diagnostics = analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics).ToArray();

            // Von außen ist das Feld kein Mitglied. Vorher übersetzte der Zugriff und scheiterte
            // erst zur Laufzeit an der CLR-Sichtbarkeit -- ohne Zeile, ohne Bezug zur Deklaration.
            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(
                diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0074"),
                string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code)));

            // Die Klasse selbst erreicht es weiterhin über Me -- sonst wäre die Meldung zu breit.
            Assert.IsFalse(
                diagnostics.Any(diagnostic =>
                    diagnostic.Code == "VB6S0074" && diagnostic.ToString().Contains("Halter.cls", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_IndexesTheResultOfAnArrayReturningProperty()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6ArrayProperty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Arrays.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Arrays"
                Class=Halter; Halter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Halter.cls"), """
                Option Explicit

                Private m_zahlen(1 To 3) As Long
                Private m_namen(0 To 1) As String

                Public Sub Fuelle()
                    m_zahlen(1) = 11
                    m_zahlen(3) = 33
                    m_namen(0) = "eins"
                End Sub

                Public Property Get Nums() As Long()
                    Nums = m_zahlen
                End Property

                Public Property Get Namen() As String()
                    Namen = m_namen
                End Property
                """);

            // Die Property wird gerufen und ihr Ergebnis indiziert; h.Nums(1) ist kein Aufruf mit
            // einem Argument, und genau das hat der Binder vorher daraus gemacht.
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim h As Halter
                    Set h = New Halter
                    h.Fuelle
                    Debug.Print h.Nums(1)
                    Debug.Print h.Nums(3)
                    Debug.Print h.Namen(0)
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);

            CollectionAssert.AreEqual(
                new[] { "11", "33", "eins" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_ReadsBackAnObjectPropertyThatAlsoHasASetter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6ObjectProperty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Objekte.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Objekte"
                Class=Halter; Halter.cls
                Class=Inhalt; Inhalt.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Inhalt.cls"), """
                Option Explicit

                Public Function Kennung() As String
                    Kennung = "inhalt"
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Halter.cls"), """
                Option Explicit

                Private m_obj As Inhalt

                Public Property Get Obj() As Inhalt
                    Set Obj = m_obj
                End Property

                Public Property Set Obj(ByVal wert As Inhalt)
                    Set m_obj = wert
                End Property

                Public Property Get Zahl() As Long
                    Zahl = 42
                End Property

                Public Property Let Zahl(ByVal wert As Long)
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim h As Halter
                    Set h = New Halter
                    Dim i As Inhalt
                    Set i = New Inhalt
                    Set h.Obj = i

                    ' Innerhalb des Get ist der eigene Name der Rückgabewert, nicht die
                    ' gleichnamige Property Set der Klasse.
                    Debug.Print TypeName(h.Obj)
                    Debug.Print (h.Obj Is Nothing)
                    Debug.Print h.Obj.Kennung

                    Dim o As Inhalt
                    Set o = h.Obj
                    Debug.Print (o Is i)

                    ' Dasselbe für das Let/Get-Paar, das denselben Weg nimmt.
                    Debug.Print h.Zahl
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);

            CollectionAssert.AreEqual(
                new[] { "Inhalt", "False", "inhalt", "True", "42" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
