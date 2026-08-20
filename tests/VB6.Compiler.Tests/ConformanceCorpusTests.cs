namespace VB6.Compiler.Tests;

/// <summary>
/// Runs the compiler over the real VB6 projects in <c>conformance/</c>.
///
/// These are not pass/fail tests of a finished compiler — the corpus is far ahead of what the
/// compiler supports. They serve two purposes: the compiler must survive real-world input
/// without throwing, and the parity numbers must never move backwards.
/// </summary>
[TestClass]
public sealed class ConformanceCorpusTests
{
    private const string VisiaProject = "VISIA/4.8.7.1/prjVisia.vbp";

    /// <summary>
    /// Standard modules and class modules the project analysis currently reads from VISIA.
    /// Update when the corpus changes or another item kind becomes analyzed.
    /// </summary>
    private const int VisiaAnalyzedSourceCount = 30;

    /// <summary>
    /// Modules that currently analyze without a single error.
    ///
    /// This is the parity ratchet: raise it whenever a milestone lands. Only ever upwards.
    ///
    /// The total error count is deliberately not asserted. It is not monotonic — teaching the
    /// parser a construct lets it reach further into a file and surface errors that the earlier
    /// cascade hid, so a real improvement can raise the total. Cleanly analyzed files can only
    /// grow, which makes them the honest progress metric.
    /// </summary>
    private const int VisiaCleanModuleBaseline = 0;

    [TestMethod]
    public void Analyze_SurvivesTheVisiaProject()
    {
        var report = AnalyzeCorpusProject(VisiaProject);

        Assert.AreEqual(VisiaAnalyzedSourceCount, report.AnalyzedFileCount);
        Assert.AreEqual(40, report.TotalItemCount);
    }

    [TestMethod]
    public void Analyze_DoesNotRegressOnTheVisiaProject()
    {
        var report = AnalyzeCorpusProject(VisiaProject);

        Assert.IsTrue(
            report.CleanFileCount >= VisiaCleanModuleBaseline,
            $"Parity regressed: {report.CleanFileCount} modules analyze cleanly, " +
            $"the baseline is {VisiaCleanModuleBaseline}.");
    }

    [TestMethod]
    public void Report_RendersWithoutThrowing()
    {
        var rendered = AnalyzeCorpusProject(VisiaProject).Render();

        StringAssert.Contains(rendered, "VB6 parity report for");
        StringAssert.Contains(rendered, "project items");
    }

    private static VBProjectParityReport AnalyzeCorpusProject(string relativePath)
    {
        var projectPath = Path.Combine(FindCorpusDirectory(), relativePath);
        Assert.IsTrue(File.Exists(projectPath), $"Corpus project not found: {projectPath}");

        return VBProjectParityReport.Create(VBProjectCompilation.Create(projectPath).Analyze());
    }

    private static string FindCorpusDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "conformance");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No 'conformance' directory found above {AppContext.BaseDirectory}.");
    }
}
