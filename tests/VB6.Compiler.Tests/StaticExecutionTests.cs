using System.Diagnostics;
using VB6.Compiler;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class StaticExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PreservesStaticLocalBetweenCalls()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Debug.Print NextValue()
                Debug.Print NextValue()
            End Sub

            Function NextValue() As Long
                Static count As Long
                count = count + 1
                NextValue = count
            End Function
            """, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerStaticTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "StaticProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(d =>
                    $"{d.Id}: {d.Message}"));
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
                ?? throw new InvalidOperationException("Failed to start the generated static-local application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "1", "2" },
                standardOutput.Split(
                    new[] { "\r\n", "\n" },
                    StringSplitOptions.RemoveEmptyEntries));
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
