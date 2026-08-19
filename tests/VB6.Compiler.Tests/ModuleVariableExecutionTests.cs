using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ModuleVariableExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_SharesModuleStateBetweenProcedures()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Counter As Long
            Private Label As String

            Private Sub Bump(ByVal amount As Long)
                Counter = Counter + amount
            End Sub

            Public Sub Main()
                Label = "total="
                Bump 10
                Bump 5
                Debug.Print Label & Counter
            End Sub
            """,
            "total=15");
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsALocalSeparateFromTheModuleVariableItShadows()
    {
        Run("""
            Public Value As Long

            Sub Hide()
                Dim Value As Integer
                Value = 7
                Debug.Print Value
            End Sub

            Sub Main()
                Value = 100
                Hide
                Debug.Print Value
            End Sub
            """,
            "7", "100");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerModuleVariableTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ModuleVariableProgram.dll");

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
                expectedLines,
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
