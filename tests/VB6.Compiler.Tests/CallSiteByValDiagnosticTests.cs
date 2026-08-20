namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CallSiteByValDiagnosticTests
{
    [TestMethod]
    public void Analyze_ReportsDedicatedCallSiteByValDiagnosticWithoutParserErrors()
    {
        const string source = """
            Sub CopyValue(ByRef value As Long)
            End Sub

            Sub Main()
                Dim source As Long
                CopyValue ByVal source
            End Sub
            """;

        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.AreEqual(1, analysis.Diagnostics.Count(diagnostic => diagnostic.Code == "VB6S0059"));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6L", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ProjectAnalyze_ReportsDedicatedCallSiteByValDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerCallSiteByValTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "ByVal.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ByVal"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub CopyValue(ByRef value As Long)
                End Sub

                Sub Main()
                    Dim source As Long
                    CopyValue ByVal source
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.AreEqual(1, analysis.Diagnostics.Count(diagnostic => diagnostic.Code == "VB6S0059"));
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
