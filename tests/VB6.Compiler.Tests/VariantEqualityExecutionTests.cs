using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantEqualityExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesEmptyNumericStringAndBooleanVariantEquality()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim value

                Debug.Print value = 0
                Debug.Print value = ""

                value = 3
                Debug.Print value = 3
                Debug.Print 3 = value
                Debug.Print value = "3"

                value = "abc"
                Debug.Print value = "abc"
                Debug.Print value = 0

                value = True
                Debug.Print value = -1
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerVariantEqualityTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "VariantEqualityProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated Variant equality application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { "True", "True", "True", "True", "False", "True", "False", "True" },
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
