namespace VB6.Compiler.Tests;

[TestClass]
public sealed class PartialModuleSymbolRecoveryTests
{
    [TestMethod]
    public void Analyze_RecoversProcedureDeclarationFromModuleWithParserErrors()
    {
        AnalyzeProject(
            """
            Public Sub Helper()
            End Sub

            .
            """,
            """
            Sub Main()
                Call Helper
            End Sub
            """,
            analysis =>
            {
                AssertProviderHasParserErrorAndNoSemanticBody(analysis);
                Assert.IsFalse(
                    analysis.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == "VB6S0005" &&
                        diagnostic.Message.Contains("Helper", StringComparison.OrdinalIgnoreCase)),
                    FormatDiagnostics(analysis));
            });
    }

    [TestMethod]
    public void Analyze_RecoversModuleVariableDeclarationFromModuleWithParserErrors()
    {
        AnalyzeProject(
            """
            Public SharedValue As Long

            .
            """,
            """
            Sub Main()
                Debug.Print SharedValue
            End Sub
            """,
            analysis =>
            {
                AssertProviderHasParserErrorAndNoSemanticBody(analysis);
                Assert.IsFalse(
                    analysis.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == "VB6S0001" &&
                        diagnostic.Message.Contains("SharedValue", StringComparison.OrdinalIgnoreCase)),
                    FormatDiagnostics(analysis));
            });
    }

    [TestMethod]
    public void Analyze_RecoversEnumTypeDeclarationFromModuleWithParserErrors()
    {
        AnalyzeProject(
            """
            Public Enum WorkState
                Ready = 1
            End Enum

            .
            """,
            """
            Public CurrentState As WorkState

            Sub Main()
            End Sub
            """,
            analysis =>
            {
                AssertProviderHasParserErrorAndNoSemanticBody(analysis);
                Assert.IsFalse(
                    analysis.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == "VB6S0003" &&
                        diagnostic.Message.Contains("WorkState", StringComparison.OrdinalIgnoreCase)),
                    FormatDiagnostics(analysis));
            });
    }

    [TestMethod]
    public void Analyze_RecoversUserDefinedTypeDeclarationFromModuleWithParserErrors()
    {
        AnalyzeProject(
            """
            Public Type WorkItem
                Value As Long
            End Type

            .
            """,
            """
            Public CurrentItem As WorkItem

            Sub Main()
            End Sub
            """,
            analysis =>
            {
                AssertProviderHasParserErrorAndNoSemanticBody(analysis);
                Assert.IsFalse(
                    analysis.Diagnostics.Any(diagnostic =>
                        diagnostic.Code == "VB6S0003" &&
                        diagnostic.Message.Contains("WorkItem", StringComparison.OrdinalIgnoreCase)),
                    FormatDiagnostics(analysis));
            });
    }

    private static void AnalyzeProject(
        string providerSource,
        string consumerSource,
        Action<VBProjectCompilationAnalysis> assertAnalysis)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerPartialModuleSymbolTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "PartialSymbols.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="PartialSymbols"
                Module=Provider; Provider.bas
                Module=Consumer; Consumer.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Provider.bas"), providerSource);
            File.WriteAllText(Path.Combine(directory, "Consumer.bas"), consumerSource);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            assertAnalysis(analysis);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void AssertProviderHasParserErrorAndNoSemanticBody(VBProjectCompilationAnalysis analysis)
    {
        var provider = analysis.Units.Single(unit =>
            string.Equals(Path.GetFileName(unit.FilePath), "Provider.bas", StringComparison.OrdinalIgnoreCase));

        Assert.IsTrue(
            provider.Analysis.ParseResult.Diagnostics.Any(diagnostic =>
                diagnostic.Code.StartsWith("VB6P", StringComparison.Ordinal)),
            FormatDiagnostics(analysis));
        Assert.IsNull(provider.Analysis.SemanticModel);
    }

    private static string FormatDiagnostics(VBProjectCompilationAnalysis analysis)
    {
        var projectDiagnostics = analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString());
        var sourceDiagnostics = analysis.Diagnostics.Select(diagnostic => diagnostic.ToString());
        return string.Join(Environment.NewLine, projectDiagnostics.Concat(sourceDiagnostics));
    }
}
