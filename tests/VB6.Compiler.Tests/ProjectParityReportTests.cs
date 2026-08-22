using VB6.ProjectSystem;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectParityReportTests
{
    [TestMethod]
    public void Create_CountsAnalyzedAndUnanalyzedItemKinds()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Type=Exe
                Name="Sample"
                Module=modClean; modClean.bas
                Class=cThing; cThing.cls
                Form=frmMain.frm
                """);
            File.WriteAllText(Path.Combine(directory, "modClean.bas"), CleanModule);
            File.WriteAllText(Path.Combine(directory, "cThing.cls"), "' not read yet");
            File.WriteAllText(Path.Combine(directory, "frmMain.frm"), "' not read yet");

            var report = VBProjectParityReport.Create(VBProjectCompilation.Create(projectPath).Analyze());

            Assert.AreEqual(3, report.TotalItemCount);
            Assert.AreEqual(3, report.AnalyzedFileCount);

            var module = report.ItemKinds.Single(kind => kind.Kind == VBProjectItemKind.Module);
            var @class = report.ItemKinds.Single(kind => kind.Kind == VBProjectItemKind.Class);
            Assert.IsTrue(module.IsAnalyzed);
            Assert.IsTrue(@class.IsAnalyzed);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Create_ReportsCleanFilesAndNoGaps()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Type=Exe
                Name="Sample"
                Module=modClean; modClean.bas
                """);
            File.WriteAllText(Path.Combine(directory, "modClean.bas"), CleanModule);

            var report = VBProjectParityReport.Create(VBProjectCompilation.Create(projectPath).Analyze());

            Assert.AreEqual(1, report.AnalyzedFileCount);
            Assert.AreEqual(1, report.CleanFileCount);
            Assert.AreEqual(0, report.TotalDiagnosticCount);
            Assert.AreEqual(0, report.Gaps.Length);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Create_GroupsGapsByMessageAndCountsAffectedFiles()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Type=Exe
                Name="Sample"
                Module=modClean; modClean.bas
                Module=modBrokenA; modBrokenA.bas
                Module=modBrokenB; modBrokenB.bas
                """);
            File.WriteAllText(Path.Combine(directory, "modClean.bas"), CleanModule);

            // Both modules open with the same unsupported construct, so the gap must be
            // reported once with two affected files rather than twice.
            File.WriteAllText(Path.Combine(directory, "modBrokenA.bas"), UnsupportedModule);
            File.WriteAllText(Path.Combine(directory, "modBrokenB.bas"), UnsupportedModule);

            var report = VBProjectParityReport.Create(VBProjectCompilation.Create(projectPath).Analyze());

            Assert.AreEqual(3, report.AnalyzedFileCount);
            Assert.AreEqual(1, report.CleanFileCount);

            var topGap = report.Gaps[0];
            Assert.AreEqual(2, topGap.FileCount);
            Assert.IsTrue(topGap.Occurrences >= 2);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Create_PicksTheEarliestErrorInTheFileAsTheFirstError()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Type=Exe
                Name="Sample"
                Module=modMixed; modMixed.bas
                """);

            // 'Then' on its own is a parser error early in the file; the '?' is a lexer error
            // later on. Lexer diagnostics are collected before parser diagnostics, so only
            // ordering by span yields the construct that actually derailed the file.
            File.WriteAllText(Path.Combine(directory, "modMixed.bas"), """
                Sub Main()
                    Then
                    Debug.Print 1 ?
                End Sub
                """);

            var report = VBProjectParityReport.Create(VBProjectCompilation.Create(projectPath).Analyze());

            var file = report.Files.Single();
            Assert.IsNotNull(file.FirstError);
            Assert.AreEqual("VB6P0001", file.FirstError!.Code);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Render_ProducesTheHeadlineNumbers()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Type=Exe
                Name="Sample"
                Module=modClean; modClean.bas
                Class=cThing; cThing.cls
                """);
            File.WriteAllText(Path.Combine(directory, "modClean.bas"), CleanModule);
            File.WriteAllText(Path.Combine(directory, "cThing.cls"), "' not read yet");

            var rendered = VBProjectParityReport
                .Create(VBProjectCompilation.Create(projectPath).Analyze())
                .Render();

            StringAssert.Contains(rendered, "VB6 parity report for Sample");
            StringAssert.Contains(rendered, "Analyzed 2 of 2 project items.");
            StringAssert.Contains(rendered, "2 of 2 analyze without errors.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReadsClassModuleSourceAfterDesignerMetadata()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClassProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Name="ClassProject"
                Module=Main; Main.bas
                Class=cThing; cThing.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "cThing.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "cThing"
                Option Explicit

                Private mValue As Long

                Public Sub SetValue(ByVal value As Long)
                    mValue = value
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
            Assert.AreEqual(2, analysis.Units.Length);
            var classUnit = analysis.Units.Single(unit => unit.Item.Kind == VBProjectItemKind.Class);
            Assert.IsNotNull(classUnit.Analysis.SemanticModel);
            Assert.IsTrue(classUnit.Analysis.SemanticModel!.Procedures.Any(
                procedure => procedure.Symbol.Name == "SetValue"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private const string CleanModule = """
        Sub Main()
            Dim value As Integer
            value = 1
            Debug.Print value
        End Sub
        """;

    // '?' is never valid VB6, so this module stays unsupported no matter how far the
    // compiler progresses.
    private const string UnsupportedModule = """
        Sub Main()
            Debug.Print 1 ?
        End Sub
        """;

    private static string WriteProject(string directory, string content)
    {
        var projectPath = Path.Combine(directory, "Sample.vbp");
        File.WriteAllText(projectPath, content);
        return projectPath;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerParityReportTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
