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
    /// Modules the loader finds in the VISIA project. Update when the corpus changes.
    /// </summary>
    private const int VisiaModuleCount = 27;

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

    /// <summary>
    /// Parser errors the corpus still produces.
    ///
    /// This is the ratchet that actually bites today. Clean files are stuck at zero and will
    /// stay there for a while, because binding is project-wide and a file only counts as clean
    /// once its whole dependency chain parses - so that baseline cannot catch a regression yet.
    /// Parser errors can, and every slice so far has lowered them: 3183 at M0, 1758 at the M2
    /// closeout, 1214 after the UDT type space, 480 after With and member access, 466 with untyped functions, 454 once ReDim recovery stopped a cascade, 218 with file numbers lexed and the binary file statements parsed, 122 once TypeOf parsed, 83 with call-site ByVal.
    ///
    /// Lower it whenever a slice lands. Raising it is not forbidden but must be deliberate: a
    /// slice can legitimately expose parser gaps deeper in a file that used to derail at line 10
    /// and never reach line 400. Raise it with a note saying which construct surfaced, the same
    /// way the total error count is explained rather than asserted.
    /// </summary>
    private const int VisiaParserErrorBaseline = 83;

    [TestMethod]
    public void Analyze_SurvivesTheVisiaProject()
    {
        var report = AnalyzeCorpusProject(VisiaProject);

        Assert.AreEqual(VisiaModuleCount, report.AnalyzedFileCount);
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

    [TestMethod]
    public void Analyze_DoesNotRegressOnVisiaParserErrors()
    {
        var report = AnalyzeCorpusProject(VisiaProject);
        var parserErrors = report.DiagnosticCodes
            .Where(code => code.Code.StartsWith("VB6P", StringComparison.Ordinal))
            .Sum(code => code.Occurrences);

        Assert.IsTrue(
            parserErrors <= VisiaParserErrorBaseline,
            $"Parity regressed: {parserErrors} parser errors, the baseline is {VisiaParserErrorBaseline}.");
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
