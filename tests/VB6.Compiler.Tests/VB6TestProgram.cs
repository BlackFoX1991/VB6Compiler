using System.Diagnostics;
using VB6.CodeGen.CSharp;

namespace VB6.Compiler.Tests;

/// <summary>
/// Compiles a VB6 program, runs it and returns what it printed.
///
/// Every language feature is required to have an end-to-end test that executes real output, so
/// this sequence - emit, report emit diagnostics as the failure message, start the assembly,
/// compare stdout, delete the temporary directory - is the single most repeated block in the
/// suite. Keeping it here means a change to the emit API is one edit rather than one per test.
///
/// The program runs with its own output directory as the working directory: file I/O tests use
/// relative paths, and the emitted assembly needs its runtime dependencies beside it.
/// </summary>
internal static class VB6TestProgram
{
    /// <summary>Runs one source file and returns its standard output verbatim.</summary>
    public static string Run(string source, string fileName = "Module1.bas") =>
        Run(VBCompilation.Create(source, fileName));

    /// <summary>Runs an already-created compilation and returns its standard output verbatim.</summary>
    public static string Run(VBCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        return RunEmitted(directory => Emit(compilation, directory));
    }

    /// <summary>Runs one source file and returns its standard output as trimmed lines.</summary>
    public static string[] RunLines(string source, string fileName = "Module1.bas") =>
        SplitLines(Run(source, fileName));

    /// <summary>
    /// Runs one source file through the direct managed backend, bypassing the compatibility
    /// facade. Both paths emit the same assembly once the cutover is complete; until then this
    /// keeps the backend covered on its own API.
    /// </summary>
    public static string[] RunDirectLines(string source, string fileName = "Module1.bas") =>
        SplitLines(RunEmitted(directory => EmitDirect(VBCompilation.Create(source, fileName), directory)));

    /// <summary>Runs a project and returns its standard output verbatim.</summary>
    public static string RunProject(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        return RunEmitted(directory => Emit(VBProjectCompilation.Create(projectPath), directory));
    }

    /// <summary>Runs a project and returns its standard output as trimmed lines.</summary>
    public static string[] RunProjectLines(string projectPath) => SplitLines(RunProject(projectPath));

    /// <summary>
    /// Splits standard output the way the execution tests compare it: one entry per printed line,
    /// each trimmed, so a trailing newline or platform line ending never decides a test.
    /// </summary>
    public static string[] SplitLines(string standardOutput)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        var trimmed = standardOutput.Trim();
        return trimmed.Length == 0
            ? []
            : trimmed
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n')
                .Select(line => line.Trim())
                .ToArray();
    }

    /// <summary>
    /// Runs a program emitted by the caller. <paramref name="emit"/> receives the temporary output
    /// directory and returns the assembly to start, which lets a test assert on the emit result
    /// itself - artifact paths, diagnostics - while the process handling and cleanup stay here.
    /// </summary>
    public static string RunEmitted(Func<string, string> emit)
    {
        ArgumentNullException.ThrowIfNull(emit);
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerExecutionTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var assemblyPath = emit(directory);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(assemblyPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start '{assemblyPath}'.");
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

    private static string Emit(VBCompilation compilation, string directory)
    {
        var result = compilation.EmitManagedApplication(Path.Combine(directory, "Program.dll"));
        Assert.IsTrue(
            result.Success,
            Join(
                result.Diagnostics.Select(diagnostic => diagnostic.ToString()),
                Backend(result.BackendResult)));
        Assert.IsNotNull(result.AssemblyPath);
        return result.AssemblyPath!;
    }

    private static string EmitDirect(VBCompilation compilation, string directory)
    {
        var result = DirectManagedCompilation.EmitManaged(compilation, Path.Combine(directory, "Program.dll"));
        Assert.IsTrue(
            result.Success,
            Join(
                result.Diagnostics.Select(diagnostic => diagnostic.ToString()),
                result.BackendResult?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}") ?? []));
        Assert.IsNotNull(result.AssemblyPath);
        return result.AssemblyPath!;
    }

    private static string Emit(VBProjectCompilation compilation, string directory)
    {
        var result = compilation.EmitManagedApplication(Path.Combine(directory, "Program.dll"));
        Assert.IsTrue(
            result.Success,
            Join(
                result.Generation.Analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString()),
                result.Generation.Analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()),
                Backend(result.BackendResult)));
        Assert.IsNotNull(result.AssemblyPath);
        return result.AssemblyPath!;
    }

    /// <summary>
    /// Renders whatever stopped the emit as the assertion message. Front-end and backend
    /// diagnostics are separate lists, and a failure shows up in exactly one of them.
    /// </summary>
    private static IEnumerable<string> Backend(AssemblyEmitResult? result) =>
        result?.Diagnostics.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.Message}") ?? [];

    private static string Join(params IEnumerable<string>[] parts) =>
        string.Join(Environment.NewLine, parts.SelectMany(part => part));
}
