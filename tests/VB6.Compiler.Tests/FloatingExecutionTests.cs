using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FloatingExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesSingleDivisionAndSingleLongPromotion()
    {
        var compilation = VBCompilation.Create("""
            Sub Main()
                Dim singleValue As Single
                Dim longValue As Long
                Dim doubleValue As Double

                singleValue = 1 / 2
                longValue = 40000
                doubleValue = singleValue + longValue

                If doubleValue = 40000.5 Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerFloatingTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "FloatingProgram.dll");

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
                ?? throw new InvalidOperationException("Failed to start the generated floating-point application.");

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
