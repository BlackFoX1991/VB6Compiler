using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class EnumExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesEnumTypeAliasesAndMemberValues()
    {
        const string source = """
            Enum Fruit
                Apple = 3
                Banana
                Cherry = Apple + 5
            End Enum

            Sub Main()
                Dim value As Fruit
                value = Banana
                Debug.Print value
                Debug.Print Cherry
            End Sub
            """;

        var output = EmitAndRun(source, "EnumProgram.dll");

        CollectionAssert.AreEqual(new[] { "4", "8" }, SplitLines(output), output);
    }

    [TestMethod]
    public void Analyze_AllowsEnumTypeInsideUserDefinedType()
    {
        var analysis = VBCompilation.Create("""
            Enum SymbolKind
                SymbolNone = 0
                SymbolString = 9
            End Enum

            Type SymbolRecord
                Kind As SymbolKind
            End Type

            Sub Main()
                Dim record As SymbolRecord
                record.Kind = SymbolString
                Debug.Print record.Kind
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Diagnostics.Any(diagnostic =>
                diagnostic.Code is "VB6S0001" or "VB6S0003" or "VB6S0011"),
            string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
    }

    [TestMethod]
    public void EmitManagedApplication_SharesEnumAcrossVbpModules()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerEnumProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Enums.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Enums"
                Module=Enums; Enums.bas
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Enums.bas"), """
                Public Enum Status
                    Ready = 10
                    Done
                End Enum
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim state As Status
                    state = Done
                    Debug.Print state
                End Sub
                """);

            var outputDirectory = Path.Combine(directory, "bin");
            var assemblyPath = Path.Combine(outputDirectory, "Enums.dll");
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
                ?? throw new InvalidOperationException("Failed to start generated Enum project application.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(new[] { "11" }, SplitLines(standardOutput), standardOutput);
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
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerEnumTests", Guid.NewGuid().ToString("N"));
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
                ?? throw new InvalidOperationException("Failed to start generated Enum application.");
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
