using System.Diagnostics;
using VB6.Emit.Managed;
using VB6.Syntax.Diagnostics;

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
    private const int ExpectedFailureExitTimeoutMilliseconds = 2_000;

    /// <summary>
    /// Selects the .NET host matching the test process. An x86 test build can reference an x86
    /// runtime support assembly, which a 64-bit host cannot load even though the resulting error
    /// is reported as a missing managed dependency.
    /// </summary>
    internal static string DotnetHostPath
    {
        get
        {
            var rootVariable = Environment.Is64BitProcess
                ? "DOTNET_ROOT_X64"
                : "DOTNET_ROOT_X86";
            var roots = new[]
            {
                Environment.GetEnvironmentVariable(rootVariable),
                Environment.Is64BitProcess
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                    : Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Environment.GetEnvironmentVariable("DOTNET_ROOT")
            };

            foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var candidate = Path.Combine(root!, "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                candidate = Path.Combine(root!, "dotnet", "dotnet.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "dotnet";
        }
    }

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
    public static string RunEmitted(Func<string, string> emit) =>
        RunEmitted(emit, expectSuccess: true).StandardOutput;

    /// <summary>
    /// Runs a program that is expected to fail and returns what it wrote to standard error.
    /// </summary>
    /// <remarks>
    /// Some VB6 contracts end the process: an untrapped runtime error is a real, observable
    /// outcome and needs a test like any other. Without this the only way to cover one is to
    /// rebuild the process handling in the test file, which is exactly what this class exists to
    /// prevent.
    /// </remarks>
    public static string RunExpectingFailure(string source, string fileName = "Module1.bas")
    {
        var compilation = VBCompilation.Create(source, fileName);
        var result = RunEmitted(directory => Emit(compilation, directory), expectSuccess: false);
        Assert.AreNotEqual(
            0,
            result.ExitCode,
            "Das Programm sollte fehlschlagen, lief aber durch: " + result.StandardOutput);
        return result.StandardError;
    }

    private static (string StandardOutput, string StandardError, int ExitCode) RunEmitted(
        Func<string, string> emit,
        bool expectSuccess)
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
            var startInfo = new ProcessStartInfo(DotnetHostPath)
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
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            if (expectSuccess)
            {
                process.WaitForExit();
            }
            else if (!process.WaitForExit(ExpectedFailureExitTimeoutMilliseconds))
            {
                // The program has already reported its unhandled VB6 error by this point. Some
                // Windows compatibility diagnostics keep that failing child alive afterwards,
                // which would otherwise deadlock the test host while it waits for closed pipes.
                // A successful program never takes this path.
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            var standardOutput = standardOutputTask.GetAwaiter().GetResult();
            var standardError = standardErrorTask.GetAwaiter().GetResult();

            if (expectSuccess)
            {
                Assert.AreEqual(0, process.ExitCode, standardError);
            }

            return (standardOutput, standardError, process.ExitCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string Emit(VBCompilation compilation, string directory) =>
        AssemblyOf(compilation.EmitManagedApplication(Path.Combine(directory, "Program.dll")));

    private static string EmitDirect(VBCompilation compilation, string directory) =>
        AssemblyOf(DirectManagedCompilation.EmitManaged(compilation, Path.Combine(directory, "Program.dll")));

    private static string AssemblyOf(ManagedApplicationEmitResult result)
    {
        Assert.IsTrue(result.Success, Join(Front(result.Diagnostics), Backend(result.BackendResult)));
        Assert.IsNotNull(result.AssemblyPath);
        return result.AssemblyPath!;
    }

    private static string Emit(VBProjectCompilation compilation, string directory)
    {
        var result = compilation.EmitManagedApplication(Path.Combine(directory, "Program.dll"));
        Assert.IsTrue(
            result.Success,
            Join(
                result.Lowering.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString()),
                Front(result.Lowering.Analysis.Diagnostics),
                Backend(result.BackendResult)));
        Assert.IsNotNull(result.AssemblyPath);
        return result.AssemblyPath!;
    }

    /// <summary>
    /// Renders whatever stopped the emit as the assertion message. Front-end and backend
    /// diagnostics are separate lists, and a failure shows up in exactly one of them.
    /// </summary>
    private static IEnumerable<string> Front(IEnumerable<Diagnostic> diagnostics) =>
        diagnostics.Select(diagnostic => diagnostic.ToString());

    private static IEnumerable<string> Backend(ManagedEmitResult? result) =>
        result?.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}") ?? [];
    private static string Join(params IEnumerable<string>[] parts) =>
        string.Join(Environment.NewLine, parts.SelectMany(part => part));
}
