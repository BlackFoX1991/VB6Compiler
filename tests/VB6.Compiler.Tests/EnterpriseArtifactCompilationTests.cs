namespace VB6.Compiler.Tests;

/// <summary>
/// PropertyPage and UserDocument are drawn in a designer just like a form, so their code has to
/// see the controls on them. They are not forms, though: neither owns a global instance named
/// after itself, and a project that addresses one that way must still be rejected.
/// </summary>
[TestClass]
public sealed class EnterpriseArtifactCompilationTests
{
    [TestMethod]
    public void Compile_ResolvesDesignerControlsOnPropertyPagesAndUserDocuments()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6Artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Enterprise.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name="Enterprise"
                PropertyPage=Seite.pag
                UserDocument=Dok.dob
                """);
            File.WriteAllText(Path.Combine(directory, "Seite.pag"), """
                VERSION 5.00
                Begin VB.PropertyPage Seite
                   Caption = "Seite"
                   Begin VB.CommandButton cmdOk
                      Caption = "OK"
                   End
                End
                Attribute VB_Name = "Seite"
                Option Explicit

                Private Sub PropertyPage_ApplyChanges()
                    cmdOk.Caption = "Fertig"
                End Sub

                Public Function Beschriftung() As String
                    Beschriftung = cmdOk.Caption
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Dok.dob"), """
                VERSION 5.00
                Begin VB.UserDocument Dok
                   ScaleMode = 1
                   Begin VB.TextBox txtWert
                      Text = "leer"
                   End
                End
                Attribute VB_Name = "Dok"
                Option Explicit

                Private Sub UserDocument_Initialize()
                    txtWert.Text = "bereit"
                End Sub

                Public Function Wert() As String
                    Wert = txtWert.Text
                End Function
                """);

            var analysis = VBProjectCompilation.Create(projectPath).AnalyzeForEmission();

            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics.Select(d => d.ToString()))
                        .Concat(analysis.ProjectDiagnostics.Select(d => d.ToString()))));
            Assert.AreEqual(2, analysis.Units.Length);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void Compile_LeavesAPropertyPageWithoutAGlobalInstance()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6ArtifactsGlobal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Enterprise.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Enterprise"
                PropertyPage=Seite.pag
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Seite.pag"), """
                VERSION 5.00
                Begin VB.PropertyPage Seite
                   Caption = "Seite"
                End
                Attribute VB_Name = "Seite"
                Option Explicit

                Public Function Wert() As Long
                    Wert = 7
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Debug.Print Seite.Wert
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).AnalyzeForEmission();

            // Eine Form wäre hier ansprechbar, eine PropertyPage ist es nicht.
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

    [TestMethod]
    public void EmitManagedApplication_RunsAPropertyPageAndAUserDocumentArtifact()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6ArtifactRun", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Lauf.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Lauf"
                PropertyPage=Seite.pag
                UserDocument=Dok.dob
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Seite.pag"), """
                VERSION 5.00
                Begin VB.PropertyPage Seite
                   Caption = "Seite"
                   Begin VB.TextBox txtName
                      Text = "aus dem Designer"
                   End
                End
                Attribute VB_Name = "Seite"
                Option Explicit

                Public Function Kennung() As String
                    Kennung = "Seite:" & CStr(txtName Is Nothing)
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Dok.dob"), """
                VERSION 5.00
                Begin VB.UserDocument Dok
                   Begin VB.Label lblTitel
                      Caption = "Titel"
                   End
                End
                Attribute VB_Name = "Dok"
                Option Explicit

                Public Function Kennung() As String
                    Kennung = "Dok:" & CStr(lblTitel Is Nothing)
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim s As Seite
                    Set s = New Seite
                    Debug.Print s.Kennung

                    Dim d As Dok
                    Set d = New Dok
                    Debug.Print d.Kennung
                End Sub
                """);

            // Analysiert wurden diese Artefakte laengst; dass sie auch laufen, stand nirgends.
            // Geprueft wird, was ohne UI-Host wahr ist: die Klasse ist erzeugbar, ihre Prozedur
            // laeuft, und die Designer-Huelle hat ihre Controls angelegt. Die Designer-*Werte*
            // gehen headless bewusst verloren -- ohne Host verwirft VBInteraction.SetMember sie,
            // und das gilt fuer eine Form genauso. Das gehoert in einen Hosttest, nicht hierher.
            CollectionAssert.AreEqual(
                new[] { "Seite:False", "Dok:False" },
                VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
