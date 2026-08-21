using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DirectManagedExecutionTests
{
    [TestMethod]
    public void DirectManagedBackend_ExecutesScalarProgramWithoutCSharp()
    {
        Run("""
            Sub AddOne(ByRef Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim value As Long
                value = 40 + 1
                AddOne value
                Debug.Print value
            End Sub
            """, "42");
    }

    [TestMethod]
    public void DirectManagedBackend_DiscardsByRefTemporaryWriteBack()
    {
        Run("""
            Sub Bump(ByRef Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim value As Long
                value = 10
                Bump value
                Bump value + 10
                Debug.Print value
            End Sub
            """, "11");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDirectManagedTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var assemblyPath = Path.Combine(directory, "Program.exe");

        try
        {
            var result = DirectManagedCompilation.EmitManaged(
                VBCompilation.Create(source, "Module1.bas"),
                assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"));
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
                ?? throw new InvalidOperationException("Failed to start direct managed output.");
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
