using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class Int64ExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesInt64ArithmeticAndForLoop()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value As Int64
                Dim i As LongLong
                value = 3000000000

                For i = 1 To 3
                    value = value + 1000000000
                Next i

                Debug.Print value
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerInt64Tests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "Int64Program.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated Int64 application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("6000000000", standardOutput.Trim());
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
