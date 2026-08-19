using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class LenIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesLenForStringEmptyAndIntegerVariant()
    {
        const string source = """
            Sub Main()
                Dim value
                Debug.Print Len("Hello")
                Debug.Print Len(value)
                value = 42
                Debug.Print Len(value)
            End Sub
            """;

        var output = EmitAndRun(source, "LenIntrinsicProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "5", "0", "2" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void GenerateCSharp_RewritesBuiltInLenToRuntimeWithoutTouchingUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Len("abc")
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            builtIn.Success,
            string.Join(Environment.NewLine, builtIn.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Len(\"abc\")");
        Assert.IsFalse(builtIn.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "VB6S0005" && diagnostic.Message.Contains("Len", StringComparison.OrdinalIgnoreCase)));

        var userDefined = VBCompilation.Create("""
            Function Len(ByVal value As Long) As Long
                Len = 99
            End Function

            Sub Main()
                Debug.Print Len(1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            userDefined.Success,
            string.Join(Environment.NewLine, userDefined.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Len(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Len(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesLenInsideVbpProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerLenProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "LenProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="LenProject"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim value
                    Debug.Print Len("project")
                    Debug.Print Len(value)
                End Sub
                """);

            var outputDirectory = Path.Combine(directory, "bin");
            var assemblyPath = Path.Combine(outputDirectory, "LenProject.dll");
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
                ?? throw new InvalidOperationException("Failed to start the generated Len project application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "7", "0" },
                SplitLines(standardOutput),
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

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerLenTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, assemblyName);

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.Message}"));
            Assert.IsTrue(result.Success, diagnostics);
            Assert.IsNotNull(result.AssemblyPath);

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(result.AssemblyPath!);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the generated Len application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput;
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

    private static string[] SplitLines(string output) =>
        output.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
}
