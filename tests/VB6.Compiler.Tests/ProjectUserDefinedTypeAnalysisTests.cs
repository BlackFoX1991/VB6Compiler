namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectUserDefinedTypeAnalysisTests
{
    [TestMethod]
    public void Analyze_ExposesPublicAndPrivateUserDefinedTypeScopes()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Type Point
                    X As Long
                End Type
                """,
                """
                Private Type Container
                    Position As Point
                End Type
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            Assert.IsNotNull(analysis.UserDefinedTypes);
            Assert.AreEqual(1, analysis.UserDefinedTypes.PublicTypes.Count);
            Assert.AreEqual(2, analysis.UserDefinedTypes.Modules.Length);

            var point = analysis.UserDefinedTypes.PublicTypes["Point"];
            var container = analysis.UserDefinedTypes.Modules[1].Types["Container"];
            Assert.IsTrue(container.TryGetMember("Position", out var position));
            Assert.AreSame(point, position.Type);

            Assert.IsNotNull(analysis.Units[0].Analysis.UserDefinedTypes);
            Assert.AreSame(point, analysis.Units[0].Analysis.UserDefinedTypes!.Types["Point"]);
            Assert.IsNotNull(analysis.Units[1].Analysis.UserDefinedTypes);
            Assert.AreSame(container, analysis.Units[1].Analysis.UserDefinedTypes!.Types["Container"]);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicatePublicUserDefinedTypesAcrossModules()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Public Type Point
                    X As Long
                End Type
                """,
                """
                Public Type POINT
                    Y As Long
                End Type
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0041"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory, string firstSource, string secondSource)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "UdtProject.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="UdtProject"
            Module=First; First.bas
            Module=Second; Second.bas
            """);
        File.WriteAllText(Path.Combine(directory, "First.bas"), firstSource);
        File.WriteAllText(Path.Combine(directory, "Second.bas"), secondSource);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis)
    {
        var projectDiagnostics = analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString());
        var sourceDiagnostics = analysis.Diagnostics.Select(diagnostic => diagnostic.ToString());
        return string.Join(Environment.NewLine, projectDiagnostics.Concat(sourceDiagnostics));
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectUdtTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
