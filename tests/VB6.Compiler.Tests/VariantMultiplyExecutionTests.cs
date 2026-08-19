using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantMultiplyExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantMultiplyWithEmptyStringsAndNumericTargets()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value
                Dim number As Long

                Debug.Print value * 7

                value = 3
                Debug.Print value * 4
                number = value * 5
                Debug.Print number

                value = "2.5"
                Debug.Print value * 2

                value = True
                Debug.Print value * 2
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerVariantMultiplyTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "VariantMultiplyProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                : string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString())) +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic =>
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
                ?? throw new InvalidOperationException("Failed to start the generated Variant multiply application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { "0", "12", "15", "5", "-2" },
                lines);
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
