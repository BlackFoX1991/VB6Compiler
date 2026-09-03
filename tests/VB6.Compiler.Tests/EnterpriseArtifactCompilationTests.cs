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
}
