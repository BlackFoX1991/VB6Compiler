using VB6.IR;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantMultiplyProjectCompilationTests
{
    [TestMethod]
    public void Lower_ProjectPathLowersVariantMultiply()
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

            var program = VB6TestIr.LowerProject(projectPath);

            // Multiplying a Variant is a defined runtime operation rather than a reported gap. It
            // stays a Variant multiply: which numeric width applies depends on what the Variant
            // holds, and VB6 widens on overflow, so the decision belongs to run time.
            CollectionAssert.Contains(
                VB6TestIr.RuntimeCalls(program).ToArray(),
                IrRuntimeMethod.MultiplyVariant);
            Assert.AreEqual("12", VB6TestProgram.RunProject(projectPath).Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Lower_ProjectPathLowersVariantPlus()
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

            var program = VB6TestIr.LowerProject(projectPath);

            CollectionAssert.Contains(
                VB6TestIr.RuntimeCalls(program).ToArray(),
                IrRuntimeMethod.AddVariant);
            Assert.AreEqual("4", VB6TestProgram.RunProject(projectPath).Trim());
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
