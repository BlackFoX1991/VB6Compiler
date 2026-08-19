using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class LongExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesLongArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Long
                Dim i As Long
                value = 40000 + 20000

                For i = 1 To 3
                    value = value + 1
                Next i

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerLongTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "LongProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Empty
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
                ?? throw new InvalidOperationException("Failed to start the generated Long application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("60003", standardOutput.Trim());
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
