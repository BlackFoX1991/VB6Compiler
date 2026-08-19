using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class BracketedEnumExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesBracketedEnumMembers()
    {
        const string source = """
            Public Enum GradientDirection
                [GR_Fill_None] = -1
                [gr_Fill_Horizontal] = 0
                [GR_Fill_Vertical] = 1
            End Enum

            Sub Main()
                Debug.Print [GR_Fill_None]
                Debug.Print [GR_Fill_Vertical]
            End Sub
            """;

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerBracketedEnumTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "BracketedEnumProgram.dll");

        try
        {
            var result = VBCompilation.Create(source, "Module1.bas").EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()))
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
                ?? throw new InvalidOperationException("Failed to start generated bracketed Enum application.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                new[] { "-1", "1" },
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
