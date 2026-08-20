namespace VB6.Compiler.Tests;

[TestClass]
public sealed class TypeOfDiagnosticTests
{
    [TestMethod]
    public void Analyze_ReportsDedicatedTypeOfDiagnosticWithoutParserErrors()
    {
        const string source = """
            Sub Main()
                Dim ctlControl As Long
                If TypeOf ctlControl Is CheckBox Then
                    Debug.Print 1
                End If
            End Sub
            """;

        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.AreEqual(1, analysis.Diagnostics.Count(diagnostic => diagnostic.Code == "VB6S0058"));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)));
        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("VB6L", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ProjectAnalyze_ReportsDedicatedTypeOfDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerTypeOfTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "TypeOf.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="TypeOf"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim ctlControl As Long
                    If TypeOf ctlControl Is CheckBox Then
                        Debug.Print 1
                    End If
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            Assert.AreEqual(1, analysis.Diagnostics.Count(diagnostic => diagnostic.Code == "VB6S0058"));
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
