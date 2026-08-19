namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantMultiplyProjectCompilationTests
{
    [TestMethod]
    public void GenerateCSharp_ProjectPathLowersVariantMultiply()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory, """
                Sub Main()
                    Dim value
                    value = 3
                    Debug.Print value * 4
                End Sub
                """);

            var generation = VBProjectCompilation.Create(projectPath).GenerateCSharp();

            Assert.IsTrue(
                generation.Success,
                FormatDiagnostics(generation.Analysis));
            Assert.IsNotNull(generation.Source);
            Assert.IsFalse(generation.Analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
            StringAssert.Contains(generation.Source, "VBOperators.MultiplyInteger(__vb6_value, VBConversions.CInt(4L))");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void GenerateCSharp_ProjectPathKeepsVariantPlusGuarded()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory, """
                Sub Main()
                    Dim value
                    value = 3
                    Debug.Print value + 1
                End Sub
                """);

            var generation = VBProjectCompilation.Create(projectPath).GenerateCSharp();

            Assert.IsFalse(generation.Success);
            Assert.IsNull(generation.Source);
            Assert.IsTrue(generation.Analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory, string moduleSource)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "VariantMultiply.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="VariantMultiply"
            Module=MainModule; MainModule.bas
            """);
        File.WriteAllText(Path.Combine(directory, "MainModule.bas"), moduleSource);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis) =>
        string.Join(
            Environment.NewLine,
            analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())
                .Concat(analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())));

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerVariantMultiplyProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
