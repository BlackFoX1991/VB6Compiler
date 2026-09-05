using VB6.Compiler;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ProjectDiagnosticCoverageTests
{
    [TestMethod]
    public void Analyze_ReportsDuplicateClassModules()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0008",
            "Type=OleDll\nClass=Same; First.cls\nClass=Same; Second.cls\n",
            ("First.cls", "Public Sub First()\nEnd Sub"),
            ("Second.cls", "Public Sub Second()\nEnd Sub"));
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateClassMembers()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0009",
            "Type=OleDll\nClass=Customer; Customer.cls\n",
            ("Customer.cls", "Public Sub Touch()\nEnd Sub\nPublic Sub Touch()\nEnd Sub"));
    }

    [TestMethod]
    public void Analyze_ReportsDuplicatePublicModulePropertiesAcrossModules()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0003",
            "Type=Exe\nStartup=\"Sub Main\"\n" +
            "Module=First; First.bas\nModule=Second; Second.bas\nModule=Main; Main.bas\n",
            ("First.bas", "Public Property Get State() As Long\nState = 1\nEnd Property"),
            ("Second.bas", "Public Property Get State() As Long\nState = 2\nEnd Property"),
            ("Main.bas", "Sub Main()\nDebug.Print State\nEnd Sub"));
    }

    [TestMethod]
    [DataRow("VB6PRJ0010", "Implements Missing")]
    [DataRow("VB6PRJ0011", "Implements Self")]
    public void Analyze_ReportsInvalidClassImplementation(string code, string declaration)
    {
        AssertProjectDiagnostic(
            code,
            "Type=OleDll\nClass=Self; Self.cls\n",
            ("Self.cls", declaration));
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateProjectReferences()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0014",
            "Type=Exe\nStartup=\"Sub Main\"\nModule=Main; Main.bas\n" +
            "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared\n" +
            "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared\n",
            ("Main.bas", "Sub Main()\nEnd Sub"));
    }

    [TestMethod]
    public void Analyze_ReportsSelfProjectReference()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0015",
            "Type=OleDll\nName=Self\n" +
            "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Test.vbp#Test\n");
    }

    [TestMethod]
    public void Analyze_ReportsReferencedProjectCompilationFailure()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0018",
            "Type=Exe\nStartup=\"Sub Main\"\nModule=Main; Main.bas\n" +
            "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Broken.vbp#Broken\n",
            ("Main.bas", "Sub Main()\nEnd Sub"),
            ("Broken.vbp", "Type=OleDll\nModule=Missing; Missing.bas\n"));
    }

    [TestMethod]
    public void AnalyzeForEmission_ReportsUnsupportedStartupObject()
    {
        AssertProjectDiagnostic(
            "VB6PRJ0004",
            "Type=Exe\nStartup=\"Unknown\"\nModule=Main; Main.bas\n",
            analyzeForEmission: true,
            ("Main.bas", "Sub Main()\nEnd Sub"));
    }

    [TestMethod]
    public void Analyze_ReportsInstantiationOfAnInterfaceContract()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Contracts.vbp");
            File.WriteAllText(projectPath, "Type=Exe\nStartup=\"Sub Main\"\n" +
                "Class=I; I.cls\nClass=C; C.cls\nModule=Main; Main.bas\n");
            File.WriteAllText(Path.Combine(directory, "I.cls"), "Public Sub Touch()\nEnd Sub");
            File.WriteAllText(Path.Combine(directory, "C.cls"), "Implements I\n");
            File.WriteAllText(Path.Combine(directory, "Main.bas"),
                "Sub Main()\nDim value As I\nSet value = New I\nEnd Sub");

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(
                analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics)
                    .Any(diagnostic => diagnostic.Code == "VB6S0068"),
                "Expected VB6S0068 in project analysis.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static void AssertProjectDiagnostic(
        string code,
        string projectText,
        params (string Name, string Content)[] files) =>
        AssertProjectDiagnostic(code, projectText, false, files);

    private static void AssertProjectDiagnostic(
        string code,
        string projectText,
        bool analyzeForEmission,
        params (string Name, string Content)[] files)
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Test.vbp");
            File.WriteAllText(projectPath, projectText);
            foreach (var file in files)
            {
                File.WriteAllText(Path.Combine(directory, file.Name), file.Content);
            }

            var analysis = analyzeForEmission
                ? VBProjectCompilation.Create(projectPath).AnalyzeForEmission()
                : VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == code),
                $"Expected {code}, got: {string.Join(", ", analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.Code))}");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectDiagnostics", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
