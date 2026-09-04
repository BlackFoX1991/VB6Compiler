namespace VB6.Compiler.Tests;

/// <summary>
/// <c>frmMain.Show</c> is how nearly every VB6 program opens its second window: a Form carries
/// <c>VB_PredeclaredId</c>, so its own name is a default instance that VB6 creates on first use.
/// Only <c>.cls</c> classes got that treatment here, so the global for a Form stayed Nothing and
/// the call died with "Object member access requires a non-empty object reference".
/// </summary>
[TestClass]
public sealed class FormDefaultInstanceExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_CreatesTheDefaultInstanceOfASecondForm()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6FormDefaultInstance",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Zwei.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Zwei"
                Form=frmB.frm
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "frmB.frm"), """
                VERSION 5.00
                Begin VB.Form frmB
                   Caption         =   "B"
                End
                Attribute VB_Name = "frmB"
                Attribute VB_PredeclaredId = True
                Option Explicit

                Public Function Kennung() As String
                    Kennung = "B"
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    ' Kein New, kein Set: der Name selbst ist die Default-Instanz.
                    Debug.Print (frmB Is Nothing)
                    Debug.Print frmB.Kennung

                    ' Und sie ist bei jedem Zugriff dieselbe.
                    Dim a As frmB
                    Set a = frmB
                    Debug.Print (a Is frmB)
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "False", "B", "True" },
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
