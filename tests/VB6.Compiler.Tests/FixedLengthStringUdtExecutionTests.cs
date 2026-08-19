using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FixedLengthStringUdtExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesFixedLengthStringMembers()
    {
        var compilation = VBCompilation.Create("""
            Type Record
                Name As String * 5
            End Type

            Sub Main()
                Dim value As Record
                Dim copied As Record
                Dim values(1 To 1) As Record

                Debug.Print "[" & value.Name & "]"
                value.Name = "Hi"
                copied = value
                value.Name = "ABCDEFG"
                values(1).Name = "X"

                Debug.Print "[" & value.Name & "]"
                Debug.Print "[" & copied.Name & "]"
                Debug.Print "[" & values(1).Name & "]"
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerFixedStringUdtTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "FixedStringUdtProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated fixed-String UDT application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            var lines = standardOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            CollectionAssert.AreEqual(
                new[] { "[     ]", "[ABCDE]", "[Hi   ]", "[X    ]" },
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