using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// The first piece of M6 that does not need the lowered representation: C# has labels and goto of
/// its own, so a jump to a label in the same procedure body maps directly. What it cannot express
/// is a jump *into* a block, which is exactly where the guard stays.
/// </summary>
[TestClass]
public sealed class GoToExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_JumpsForwardOverStatements()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim i As Long
                i = 1

                If i = 1 Then
                    GoTo Done
                End If

                Debug.Print 99
            Done:
                Debug.Print i
            End Sub
            """,
            "1");
    }

    [TestMethod]
    public void EmitManagedApplication_JumpsBackwardToFormALoop()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Public Sub Main()
                Dim i As Long
                i = 0
            Again:
                i = i + 1
                Debug.Print i
                If i < 3 Then
                    GoTo Again
                End If
            End Sub
            """,
            "1",
            "2",
            "3");
    }

    /// <summary>
    /// A label nested inside a block cannot be jumped to, because C# refuses a jump into a block.
    /// Reported rather than emitted as something that happens to compile but jumps elsewhere.
    /// </summary>
    [TestMethod]
    public void Analyze_ReportsAJumpIntoABlock()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim i As Long
                GoTo Inside
                If i = 0 Then
            Inside:
                    Debug.Print 1
                End If
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        Assert.IsTrue(analysis.Diagnostics.Any(d => d.Code == "VB6S0061"));
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerGoToTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "GoToProgram.dll");

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
