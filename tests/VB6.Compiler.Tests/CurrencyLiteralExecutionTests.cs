using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class CurrencyLiteralExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesCurrencyLiteralArithmetic()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim amount As Currency
                amount = 1.2345@
                amount = amount * 1.2345@

                If amount = 1.524@ Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerCurrencyLiteralTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "CurrencyLiteralProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated Currency literal application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("1", standardOutput.Trim());
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
