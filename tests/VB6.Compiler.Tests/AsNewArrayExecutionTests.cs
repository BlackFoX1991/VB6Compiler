namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Dim a(1 To 3) As New C</c> — an array of objects, each created when it is first touched. The
/// binder refused the declaration outright with <c>VB6S0063</c> because it checked <c>As New</c>
/// against the array type instead of the element type, so an array of forms or classes could not be
/// declared at all.
/// </summary>
[TestClass]
public sealed class AsNewArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_CreatesEachAsNewArrayElementOnFirstUse()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6AsNewArray",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Felder.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Felder"
                Class=Zaehler; Zaehler.cls
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Zaehler.cls"), """
                VERSION 1.0 CLASS
                Attribute VB_Name = "Zaehler"
                Option Explicit

                Public Wert As Long
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Public g(1 To 2) As New Zaehler

                Sub Main()
                    ' Ein Element entsteht beim ersten Zugriff, nicht bei der Deklaration.
                    Debug.Print (g(1) Is Nothing)

                    ' Und jedes Element ist ein eigenes Objekt, nicht dreimal dasselbe.
                    Debug.Print (g(1) Is g(2))
                    g(1).Wert = 7
                    g(2).Wert = 9
                    Debug.Print g(1).Wert
                    Debug.Print g(2).Wert

                    Dim f(1 To 2) As New Zaehler
                    Debug.Print (f(1) Is f(2))

                    ' Set auf Nothing loescht das Element; der naechste Zugriff legt es neu an --
                    ' dieselbe Regel, die fuer eine skalare As-New-Variable gilt.
                    f(1).Wert = 3
                    Set f(1) = Nothing
                    Debug.Print (f(1) Is Nothing)
                    Debug.Print f(1).Wert
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "False", "False", "7", "9", "False", "False", "0" },
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
