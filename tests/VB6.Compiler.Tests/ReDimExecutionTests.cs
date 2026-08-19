using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ReDimExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesReDimAndPreserve()
    {
        const string source = """
            Option Base 1

            Sub Main()
                Dim values() As Long
                ReDim values(2)
                values(1) = 10
                values(2) = 20
                ReDim Preserve values(4)
                Debug.Print values(1)
                Debug.Print values(2)
                Debug.Print values(3)
                values(4) = 40
                Debug.Print values(4)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "10", "20", "0", "40" },
            CompileAndRun(source));
    }

    [TestMethod]
    public void EmitManagedApplication_ReDimWithoutPreserveClearsOldValues()
    {
        const string source = """
            Sub Main()
                Dim values() As Long
                ReDim values(0 To 1)
                values(0) = 99
                ReDim values(0 To 2)
                Debug.Print values(0)
                Debug.Print values(2)
            End Sub
            """;

        CollectionAssert.AreEqual(
            new[] { "0", "0" },
            CompileAndRun(source));
    }

    private static string[] CompileAndRun(string source)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerReDimTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ReDimProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated ReDim application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
