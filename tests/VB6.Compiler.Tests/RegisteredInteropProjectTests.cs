using VB6.Semantics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class RegisteredInteropProjectTests
{
    [TestMethod]
    public void Analyze_ResolvesRegisteredTypeLibraryWhenVbpStoresOnlyLegacyFileName()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Registered COM type-library resolution requires Windows.");
        }

        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "stdole2.tlb");
        if (!File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The registered Windows stdole2.tlb fixture is not available.");
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerRegisteredInteropTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "RegisteredStdole.vbp");
            File.WriteAllText(
                projectPath,
                "Type=Exe\n" +
                "Startup=\"Sub Main\"\n" +
                "Reference=*\\G{00020430-0000-0000-C000-000000000046}#2.0#0#stdole2.tlb#stdole\n" +
                "Module=Main; Main.bas\n");
            File.WriteAllText(
                Path.Combine(directory, "Main.bas"),
                "Sub Main()\n" +
                "    Dim picture As stdole.IPicture\n" +
                "    Set picture = Nothing\n" +
                "End Sub\n");

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            var main = analysis.Units
                .Single(unit => string.Equals(unit.Item.Name, "Main", StringComparison.OrdinalIgnoreCase))
                .Analysis
                .SemanticModel!
                .Procedures
                .Single(procedure => string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));
            var picture = main.Locals.Single(local =>
                string.Equals(local.Name, "picture", StringComparison.OrdinalIgnoreCase));
            Assert.IsInstanceOfType<ClassTypeSymbol>(picture.Type);
            Assert.AreEqual("stdole.IPicture", picture.Type.Name);
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
