using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ForEachArrayExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesMultidimensionalForEachExitAndValueControlVariable()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim item As Variant
                Dim count As Long
                Dim values(1 To 2, 5 To 6) As Long

                values(1, 5) = 15
                values(1, 6) = 16
                values(2, 5) = 25
                values(2, 6) = 26

                For Each item In values
                    count = count + 1
                    Debug.Print item
                    If count = 3 Then Exit For
                Next item

                Debug.Print count
                item = 99
                Debug.Print values(2, 5)
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerForEachArrayTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ForEachArrayProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated For Each array application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { "15", "16", "25", "3", "25" },
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
