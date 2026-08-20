namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FileIoDiagnosticTests
{
    [TestMethod]
    public void Analyze_ReportsDedicatedDiagnosticForNestedFileIo()
    {
        const string source = """
            Sub Main()
                If True Then
                    Open "data.bin" For Binary As #1
                    Put #1, 1, 42
                    Close #1
                End If
            End Sub
            """;

        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.AreEqual(
            new[] { "VB6S0057", "VB6S0057", "VB6S0057" },
            analysis.Diagnostics
                .Where(diagnostic => diagnostic.Code == "VB6S0057")
                .Select(diagnostic => diagnostic.Code)
                .ToArray());
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6L", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ProjectAnalyze_ReportsDedicatedFileIoDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerFileIoTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "FileIo.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="FileIo"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Open "data.bin" For Binary As #1
                    Close #1
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.AreEqual(2, analysis.Diagnostics.Count(diagnostic => diagnostic.Code == "VB6S0057"));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)));
            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6L", StringComparison.Ordinal)));
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
