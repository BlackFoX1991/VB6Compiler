namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Class_Terminate</c> must follow the ownership of generated local storage rather than the
/// collector's schedule. The shutdown register remains a fallback for boundaries not yet counted,
/// but a normal Set-to-Nothing and a procedure return are observable at their actual boundaries.
/// </summary>
[TestClass]
public sealed class ClassTerminateGuaranteeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RunsEveryClassTerminate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateRuns", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Leben.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Leben"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Public Etikett As String

                Private Sub Class_Terminate()
                    Debug.Print "Terminate " & Etikett
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim x As C
                    Set x = New C
                    x.Etikett = "eins"

                    Dim alias As C
                    Set alias = x
                    Set x = Nothing
                    Debug.Print "Alias lebt"
                    Set alias = alias
                    Debug.Print "Selbst lebt"
                    Set alias = Nothing

                    Dim y As C
                    Set y = New C
                    y.Etikett = "zwei"

                    Debug.Print "Ende"
                End Sub
                """);

            var lines = VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath));

            // The alias keeps x alive after its first slot is cleared; self-assignment must also
            // retain before release. y remains alive until Main releases its local storage.
            CollectionAssert.AreEqual(
                new[] { "Alias lebt", "Selbst lebt", "Terminate eins", "Ende", "Terminate zwei" },
                lines);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_DoesNotTerminateAClassWhoseInitializerFailed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateInit", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Init.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Init"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);

            // In VB6 gilt ein Objekt, dessen Class_Initialize einen Fehler auslöst, als nie
            // erzeugt -- es bekommt kein Terminate. Deshalb meldet der Konstruktor die Instanz
            // erst an, nachdem der Initialisierer durch ist.
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Initialize()
                    Err.Raise 5
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    On Error Resume Next
                    Dim x As C
                    Set x = New C
                    Debug.Print Err.Number
                    Debug.Print "Ende"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "5", "Ende" },
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
