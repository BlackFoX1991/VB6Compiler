namespace VB6.Compiler.Tests;

/// <summary>
/// <c>As New</c> beyond locals: module variables, class fields, and the global default instance a
/// class gets from <c>Attribute VB_PredeclaredId = True</c>. All three defer creation to the first
/// read, which is what separates them from an ordinary object initializer.
/// </summary>
[TestClass]
public sealed class AsNewStorageExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_DefersAsNewForModuleVariablesAndClassFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6AsNewStorage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "AsNew.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNew"
                Class=Zaehler; Zaehler.cls
                Class=Halter; Halter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Zaehler.cls"), """
                Option Explicit

                Private m_wert As Long

                Private Sub Class_Initialize()
                    Debug.Print "init"
                End Sub

                Public Sub Erhoehe()
                    m_wert = m_wert + 1
                End Sub

                Public Property Get Wert() As Long
                    Wert = m_wert
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "Halter.cls"), """
                Option Explicit

                Private inner As New Zaehler

                Public Sub Tick()
                    inner.Erhoehe
                End Sub

                Public Property Get Wert() As Long
                    Wert = inner.Wert
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private g As New Zaehler

                Sub Main()
                    Debug.Print "vorher"
                    g.Erhoehe
                    g.Erhoehe
                    Debug.Print g.Wert

                    Dim h As Halter
                    Set h = New Halter
                    h.Tick
                    Debug.Print h.Wert

                    ' Nach Nothing entsteht beim nächsten Lesen ein frisches Objekt.
                    Set g = Nothing
                    Debug.Print g.Wert
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);

            CollectionAssert.AreEqual(
                new[] { "vorher", "init", "2", "init", "1", "init", "0" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void EmitManagedApplication_GivesAPredeclaredClassAGlobalInstance()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Predeclared", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Predeclared.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Predeclared"
                Class=Zaehler; Zaehler.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Zaehler.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Zaehler"
                Attribute VB_Creatable = False
                Attribute VB_PredeclaredId = True
                Attribute VB_Exposed = False
                Option Explicit

                Private m_wert As Long

                Private Sub Class_Initialize()
                    Debug.Print "init"
                End Sub

                Public Sub Erhoehe()
                    m_wert = m_wert + 1
                End Sub

                Public Property Get Wert() As Long
                    Wert = m_wert
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Debug.Print "vorher"
                    Zaehler.Erhoehe
                    Zaehler.Erhoehe
                    Debug.Print Zaehler.Wert
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);

            // Die Instanz entsteht beim ersten Zugriff, nicht beim Laden des Moduls, und bleibt
            // über Anweisungsgrenzen hinweg dieselbe.
            CollectionAssert.AreEqual(
                new[] { "vorher", "init", "2" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Compile_LeavesAClassWithoutThePredeclaredAttributeUndeclared()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6NotPredeclared", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Plain.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Plain"
                Class=Gewoehnlich; Gewoehnlich.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Gewoehnlich.cls"), """
                Attribute VB_Name = "Gewoehnlich"
                Attribute VB_PredeclaredId = False
                Option Explicit

                Public Sub Tue()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Gewoehnlich.Tue
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).AnalyzeForEmission();

            // Ohne das Attribut ist der Klassenname kein Wert -- VB6 kennt dort keine Instanz.
            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(
                analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics)
                    .Any(diagnostic => diagnostic.Code == "VB6S0001"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
