using System.Diagnostics;
using System.Reflection.Metadata;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VBProjectGroupCompilationTests
{
    [TestMethod]
    public void AnalyzeAndEmit_CompilesEachVbpInDeclaredVbgOrder()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, """
                Type=Group
                Project=First.vbp
                Project=Second.vbp
                StartupProject=First.vbp
                """);
            WriteProject(directory, "First", "First.vbp", "First.bas", "1");
            WriteProject(directory, "Second", "Second.vbp", "Second.bas", "2");

            var compilation = VBProjectGroupCompilation.Create(groupPath);
            var analysis = compilation.Analyze();

            Assert.IsTrue(analysis.Success, FormatDiagnostics(analysis));
            CollectionAssert.AreEqual(
                new[] { "First.vbp", "Second.vbp" },
                analysis.Projects.Select(project => project.Project.RelativePath).ToArray());

            var outputDirectory = Path.Combine(directory, "bin");
            var emit = compilation.EmitManagedApplications(outputDirectory);

            Assert.IsTrue(emit.Success, FormatDiagnostics(emit.Analysis));
            Assert.AreEqual(2, emit.Projects.Length);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "First.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsMissingVbpWithItsResolvedPath()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "Missing.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=Missing.vbp\n");

            var analysis = VBProjectGroupCompilation.Create(groupPath).Analyze();

            Assert.IsFalse(analysis.Success);
            var diagnostic = analysis.Projects.Single().Diagnostics.Single();
            Assert.AreEqual("VB6VBG0006", diagnostic.Code);
            Assert.AreEqual(Path.Combine(directory, "Missing.vbp"), diagnostic.FilePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsStartupProjectThatIsNotDeclaredInTheGroup()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=Actual.vbp\nStartupProject=Missing.vbp\n");
            WriteProject(directory, "Actual", "Actual.vbp", "Actual.bas", "1");

            var analysis = VBProjectGroupCompilation.Create(groupPath).Analyze();

            Assert.IsFalse(analysis.Success);
            var diagnostic = analysis.GroupDiagnostics.Single(diagnostic => diagnostic.Code == "VB6VBG0007");
            StringAssert.Contains(diagnostic.Message, "Missing.vbp");
            Assert.AreEqual(groupPath, diagnostic.FilePath);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsProjectReferenceThatIsNotDeclaredInTheGroup()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=Consumer.vbp\n");
            File.WriteAllText(Path.Combine(directory, "Shared.vbp"), """
                Type=OleDll
                Name=Shared
                Class=Customer; Customer.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Customer.cls"), """
                Public Function Value() As Long
                    Value = 7
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Consumer.vbp"), """
                Type=Exe
                Startup="Sub Main"
                Name=Consumer
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Debug.Print 1
                End Sub
                """);

            var compilation = VBProjectGroupCompilation.Create(groupPath);
            var analysis = compilation.Analyze();

            Assert.IsFalse(analysis.Success);
            var diagnostic = analysis.Projects
                .Single(project => project.Project.RelativePath == "Consumer.vbp")
                .Diagnostics
                .Single(diagnostic => diagnostic.Code == "VB6VBG0008");
            StringAssert.Contains(diagnostic.Message, "Shared.vbp");

            var outputDirectory = Path.Combine(directory, "bin");
            var emit = compilation.EmitManagedApplications(outputDirectory);
            Assert.IsFalse(emit.Success);
            Assert.AreEqual(0, emit.Projects.Length);
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Consumer.exe")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ReportsDuplicateProjectEntries()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "Duplicate.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=App.vbp\nProject=App.vbp\n");
            WriteProject(directory, "App", "App.vbp", "App.bas", "1");

            var analysis = VBProjectGroupCompilation.Create(groupPath).Analyze();

            Assert.IsTrue(
                analysis.Projects.SelectMany(project => project.Diagnostics)
                    .Any(diagnostic => diagnostic.Code == "VB6VBG0005"));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplications_UsesExeName32ForExecutableProjects()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=InternalName.vbp\n");
            File.WriteAllText(Path.Combine(directory, "InternalName.vbp"), """
                Type=Exe
                Startup="Sub Main"
                Name="InternalName"
                ExeName32="bin\\LegacyOutput.exe"
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Debug.Print 1
                End Sub
                """);

            var outputDirectory = Path.Combine(directory, "out");
            var result = VBProjectGroupCompilation.Create(groupPath)
                .EmitManagedApplications(outputDirectory);

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Analysis));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "LegacyOutput.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "InternalName.exe")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitManagedApplications_EmitsReferencedLibrariesBeforeConsumers()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, """
                Type=Group
                Project=Consumer.vbp
                Project=Shared.vbp
                """);
            File.WriteAllText(Path.Combine(directory, "Shared.vbp"), """
                Type=OleDll
                Name=Shared
                Class=Customer; Customer.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Customer.cls"), """
                Public Function Value() As Long
                    Value = 7
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Consumer.vbp"), """
                Type=Exe
                Startup="Sub Main"
                Name=Consumer
                Reference=*\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Dim customer As Shared.Customer
                    Set customer = New Shared.Customer
                    Debug.Print customer.Value
                End Sub
                """);

            var result = VBProjectGroupCompilation.Create(groupPath)
                .EmitManagedApplications(Path.Combine(directory, "bin"));

            Assert.IsTrue(result.Success, FormatDiagnostics(result.Analysis));
            CollectionAssert.AreEqual(
                new[] { "Shared.vbp", "Consumer.vbp" },
                result.Projects.Select(project => project.Project.Project.RelativePath).ToArray());
            Assert.IsTrue(File.Exists(Path.Combine(directory, "bin", "Shared.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(directory, "bin", "Consumer.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(directory, "bin", "Consumer.dll")));

            using (var sharedStream = File.OpenRead(Path.Combine(directory, "bin", "Shared.dll")))
            using (var sharedPe = new System.Reflection.PortableExecutable.PEReader(sharedStream))
            {
                var metadata = sharedPe.GetMetadataReader();
                var customer = metadata.TypeDefinitions.SingleOrDefault(handle =>
                    metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "__vb6_class_Customer");
                Assert.IsTrue(
                    !customer.IsNil,
                    string.Join(", ", metadata.TypeDefinitions.Select(handle =>
                        metadata.GetString(metadata.GetTypeDefinition(handle).Name))));
                var value = metadata.GetTypeDefinition(customer).GetMethods().SingleOrDefault(handle =>
                    metadata.GetString(metadata.GetMethodDefinition(handle).Name) == "__vb6_Value");
                Assert.IsTrue(
                    !value.IsNil,
                    string.Join(", ", metadata.GetTypeDefinition(customer).GetMethods().Select(handle =>
                        metadata.GetString(metadata.GetMethodDefinition(handle).Name))));
                var method = metadata.GetMethodDefinition(value);
                Assert.AreEqual(
                    System.Reflection.MethodAttributes.Public,
                    method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask);
                Assert.IsFalse(method.Attributes.HasFlag(System.Reflection.MethodAttributes.Static));
            }

            var startInfo = new ProcessStartInfo(Path.Combine(directory, "bin", "Consumer.exe"))
            {
                WorkingDirectory = Path.Combine(directory, "bin"),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the emitted consumer project.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("7", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static void WriteProject(
        string directory,
        string name,
        string projectFileName,
        string moduleFileName,
        string value)
    {
        File.WriteAllText(
            Path.Combine(directory, projectFileName),
            $"""
            Type=Exe
            Startup="Sub Main"
            Name="{name}"
            Module={Path.GetFileNameWithoutExtension(moduleFileName)}; {moduleFileName}
            """);
        File.WriteAllText(
            Path.Combine(directory, moduleFileName),
            $"""
            Sub Main()
                Debug.Print {value}
            End Sub
            """);
    }

    private static string FormatDiagnostics(VBProjectGroupAnalysis analysis) =>
        string.Join(
            Environment.NewLine,
            analysis.GroupDiagnostics.Select(diagnostic => diagnostic.ToString())
                .Concat(analysis.Projects
                    .SelectMany(project => project.Diagnostics)
                    .Select(diagnostic => diagnostic.ToString()))
                .Concat(analysis.Projects
                    .Where(project => project.Compilation is not null)
                    .SelectMany(project => project.Compilation!.ProjectDiagnostics)
                    .Select(diagnostic => diagnostic.ToString())));

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerProjectGroupTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
