using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class BitwiseExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesBitwiseExpressions()
    {
        // 12 And 10 = 8, 12 Or 10 = 14, 12 Xor 10 = 6, Not 0 = -1,
        // &HFF And &H0F = 15, and the flag test mirrors the &H masks in real VB6 code.
        Run("""
            Sub Main()
                Dim value As Integer
                Dim flags As Long
                value = 12 And 10
                Debug.Print value
                value = 12 Or 10
                Debug.Print value
                value = 12 Xor 10
                Debug.Print value
                value = Not 0
                Debug.Print value
                value = &HFF And &H0F
                Debug.Print value
                flags = &H10&
                If (flags And &H10&) <> 0 Then
                    Debug.Print 1
                Else
                    Debug.Print 0
                End If
            End Sub
            """,
            "8", "14", "6", "-1", "15", "1");
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesRadixLiteralsWithVb6Wrapping()
    {
        Run("""
            Sub Main()
                Dim small As Integer
                Dim wide As Long
                small = &HFFFF
                Debug.Print small
                small = &H7FFF
                Debug.Print small
                wide = &HFFFF&
                Debug.Print wide
                wide = &H10000
                Debug.Print wide
                small = &O17
                Debug.Print small
            End Sub
            """,
            "-1", "32767", "65535", "65536", "15");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerBitwiseTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "BitwiseProgram.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(d =>
                    $"{d.Id}: {d.Message}"));
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
                ?? throw new InvalidOperationException("Failed to start the generated bitwise application.");

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
