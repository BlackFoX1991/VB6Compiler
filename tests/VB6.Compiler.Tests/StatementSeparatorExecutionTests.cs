using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class StatementSeparatorExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesColonSeparatedStatements()
    {
        const string source = """
            Sub Main()
                Dim x As Integer: Dim y As Integer
                x = 1: y = 2
                If x = 1 Then x = x + 2: y = y + 3 Else x = 99
                Select Case y
                    Case 5: x = x + 4: y = y + 5
                End Select
                Debug.Print x: Debug.Print y
            End Sub
            """;

        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerStatementSeparatorTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "StatementSeparatorProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(d =>
                    $"{d.Id}: {d.Message}"));
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
                new[] { "7", "10" },
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
