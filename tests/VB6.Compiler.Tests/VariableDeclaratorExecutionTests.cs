using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariableDeclaratorExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesTypedLocalAndModuleDeclaratorLists()
    {
        Run("""
            Public LeftValue As Integer, RightValue As Long

            Sub Main()
                Dim small As Integer, wide As Long
                LeftValue = 3
                RightValue = 40000
                small = 4
                wide = 5
                Debug.Print LeftValue + small
                Debug.Print RightValue + wide
            End Sub
            """,
            "7", "40005");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariableDeclaratorTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "VariableDeclaratorProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Id}: {diagnostic.Message}"));
            Assert.IsTrue(result.Success, diagnostics);

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
                ?? throw new InvalidOperationException("Failed to start the generated application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                expectedLines,
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
}
