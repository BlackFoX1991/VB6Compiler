using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DynamicForEachProjectCompilationTests
{
    [TestMethod]
    public void EmitManagedApplication_LowersDynamicArrayForEachInsideVbpProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDynamicForEachProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DynamicForEachProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="DynamicForEachProject"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim item
                    Dim values() As Long
                    ReDim values(-1 To 1)
                    values(-1) = 7
                    values(0) = 8
                    values(1) = 9

                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);

            var outputDirectory = Path.Combine(directory, "bin");
            var assemblyPath = Path.Combine(outputDirectory, "DynamicForEachProject.dll");
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
                ?? throw new InvalidOperationException("Failed to start the generated dynamic project For Each application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "7", "8", "9" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
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
}
