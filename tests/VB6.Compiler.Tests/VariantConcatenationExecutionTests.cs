using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantConcatenationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ConcatenatesEmptyNumericAndStringVariants()
    {
        const string source = """
            Sub Main()
                Dim value As Variant
                Debug.Print value & "x"
                value = 42
                Debug.Print value & "x"
                value = "a"
                Debug.Print "x" & value
            End Sub
            """;

        var output = EmitAndRun(source, "VariantConcatProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "x", "42x", "xa" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void GenerateCSharp_AllowsOnlyBoundAmpersandStringPath()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Debug.Print value & "x"
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        StringAssert.Contains(generation.Source, "VBOperators.Concat(");
        StringAssert.Contains(generation.Source, "VBConversions.CStr(__vb6_value)");
    }

    [TestMethod]
    public void Analyze_KeepsVariantPlusGuarded()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Debug.Print value + 1
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0053");
    }

    [TestMethod]
    public void ProjectAnalysis_AllowsVariantAmpersandConcatenation()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantConcatProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Concat.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Concat"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim value As Variant
                    value = 42
                    Debug.Print "value=" & value
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"),
                string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
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
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerVariantConcatTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, assemblyName);

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()))
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
                ?? throw new InvalidOperationException("Failed to start generated Variant concatenation application.");
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

    private static string[] SplitLines(string output) =>
        output.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
}
