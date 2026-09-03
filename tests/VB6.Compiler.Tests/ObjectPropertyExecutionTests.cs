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
