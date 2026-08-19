namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantEqualityProjectCompilationTests
{
    [TestMethod]
    public void GenerateCSharp_ProjectPathLowersVariantScalarEquality()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory, """
                Sub Main()
                    Dim value
                    value = 3
                    Debug.Print value = 3
                End Sub
                """);

            var generation = VBProjectCompilation.Create(projectPath).GenerateCSharp();

            Assert.IsTrue(generation.Success, FormatDiagnostics(generation.Analysis));
            Assert.IsNotNull(generation.Source);
            Assert.IsFalse(generation.Analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
            StringAssert.Contains(generation.Source, "VBOperators.Equal(__vb6_value, VBConversions.CInt(3L))");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void GenerateCSharp_ProjectPathKeepsVariantToVariantEqualityGuarded()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(directory, """
                Sub Main()
                    Dim leftValue
                    Dim rightValue
                    Debug.Print leftValue = rightValue
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
        var projectPath = Path.Combine(directory, "VariantEquality.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="VariantEquality"
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
        Path.Combine(Path.GetTempPath(), "VB6CompilerVariantEqualityProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
