namespace VB6.Compiler.Tests;

[TestClass]
public sealed class UserControlHostIntrinsicAnalysisTests
{
    [TestMethod]
    public void Analyze_ResolvesImplicitUserControlHostProcedures()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerUserControlHostTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "HostProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="HostProject"
                Module=Main; Main.bas
                UserControl=Widget.ctl
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Widget.ctl"), """
                Private Function Measure(ByVal value As String) As Single
                    Measure = TextWidth(value)
                    Measure = ScaleX(Measure)
                    Measure = ScaleY(Measure)
                End Function

                Private Sub Paint()
                    Height = ScaleHeight + ScaleWidth
                    CurrentX = 0
                    CurrentY = 0
                    FillStyle = vbSolid
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0005"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis) =>
        string.Join(
            Environment.NewLine,
            analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
}
