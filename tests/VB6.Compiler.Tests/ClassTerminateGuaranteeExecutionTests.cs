namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Class_Terminate</c> runs. Before this card it did not — not on <c>Set x = Nothing</c>, not at
/// scope exit, not on reassignment, and not at program end either: the emitted class carried a
/// finalizer, and the CLR does not run pending finalizers when a process ends. A program whose
/// cleanup code lives in Terminate simply never ran it.
///
/// What is guaranteed here is that it runs, not when. VB6 counts references and terminates the
/// moment the last one goes; deriving that moment without a real reference count would fire
/// Terminate on a live object, which is worse than firing it late.
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
                    Set x = Nothing

                    Dim y As C
                    Set y = New C
                    y.Etikett = "zwei"

                    Debug.Print "Ende"
                End Sub
                """);

            var lines = VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath));

            // Beide Terminatoren laufen, und zwar nach dem Programmtext -- die Reihenfolge ist die
            // des Abbaus, nicht die von VB6. Genau das ist die Zusage: dass sie laufen.
            CollectionAssert.AreEqual(
                new[] { "Ende", "Terminate zwei", "Terminate eins" },
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
