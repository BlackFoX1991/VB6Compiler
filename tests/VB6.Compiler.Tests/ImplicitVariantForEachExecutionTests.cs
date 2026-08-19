using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ImplicitVariantForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesImplicitVariantForArrayForEachControlVariable()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim item
                Dim values(1 To 2) As Long
                values(1) = 4
                values(2) = 5

                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerImplicitVariantTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ImplicitVariantProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated implicit Variant application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(new[] { "4", "5" }, lines);
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
