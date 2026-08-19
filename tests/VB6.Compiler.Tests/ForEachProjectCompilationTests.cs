using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachProjectCompilationTests
{
    [TestMethod]
    public void EmitManagedApplication_LowersFixedArrayForEachInsideVbpProject()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim item As Variant
                    Dim values(1 To 2, 5 To 6) As Long
                    values(1, 5) = 10
                    values(1, 6) = 11
                    values(2, 5) = 20
                    values(2, 6) = 21

                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);
            var outputDirectory = Path.Combine(directory, "bin");
            var assemblyPath = Path.Combine(outputDirectory, "ProjectForEach.dll");

            var result = VBProjectCompilation.Create(projectPath).EmitManagedApplication(assemblyPath);
            Assert.IsTrue(result.Success, FormatDiagnostics(result));
            Assert.IsNotNull(result.AssemblyPath);

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(result.AssemblyPath!);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the generated project For Each application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "10", "11", "20", "21" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ProjectForEachPreservesControlVariableGuard()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim item As Long
                    Dim values(1 To 2) As Long
                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            CollectionAssert.Contains(
                analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "VB6S0054");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_ProjectPathAppliesVariantOperatorGuard()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var projectPath = WriteProject(
                directory,
                """
                Sub Main()
                    Dim value As Variant
                    Debug.Print value + 1
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Success);
            CollectionAssert.Contains(
                analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
                "VB6S0053");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string WriteProject(string directory, string moduleSource)
    {
        Directory.CreateDirectory(directory);
        var projectPath = Path.Combine(directory, "ForEachProject.vbp");
        File.WriteAllText(projectPath, """
            Type=Exe
            Startup="Sub Main"
            Name="ForEachProject"
            Module=MainModule; MainModule.bas
            """);
        File.WriteAllText(Path.Combine(directory, "MainModule.bas"), moduleSource);
        return projectPath;
    }

    private static string FormatDiagnostics(VBProjectManagedApplicationEmitResult result)
    {
        var diagnostics = result.Generation.Analysis.Diagnostics
            .Select(diagnostic => diagnostic.ToString())
            .ToList();
        diagnostics.AddRange(result.Generation.Analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString()));

        if (result.BackendResult is not null)
        {
            diagnostics.AddRange(result.BackendResult.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}"));
        }

        return string.Join(Environment.NewLine, diagnostics);
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerForEachProjectTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
