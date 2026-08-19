using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantFoundationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantStorageAndScalarConversions()
    {
        var compilation = VBCompilation.Create("""
            Sub Consume(ByVal value As Variant)
                Debug.Print value
            End Sub

            Sub Main()
                Dim value As Variant
                Dim number As Long
                Dim values(1 To 2) As Variant

                Debug.Print value
                value = 42
                number = value
                Debug.Print number

                value = "hello"
                Debug.Print value

                values(1) = 7
                Debug.Print values(1)
                Consume 9
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerVariantFoundationTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "VariantFoundationProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated Variant foundation application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { string.Empty, "42", "hello", "7", "9" },
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
