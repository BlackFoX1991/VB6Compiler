using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ConstAndExitProcedureExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesModuleConstants()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Const Base As Long = 10
            Public Const Doubled As Long = Base * 2
            Const Untyped = 7

            Public Sub Main()
                Debug.Print Base
                Debug.Print Doubled
                Debug.Print Untyped
            End Sub
            """,
            "10", "20", "7");
    }

    [TestMethod]
    public void EmitManagedApplication_LeavesProcedureOnExitSub()
    {
        Run("""
            Sub Report(ByVal value As Integer)
                If value < 0 Then
                    Debug.Print 0
                    Exit Sub
                End If

                Debug.Print value
            End Sub

            Sub Main()
                Report -5
                Report 3
            End Sub
            """,
            "0", "3");
    }

    [TestMethod]
    public void EmitManagedApplication_ReturnsAssignedValueOnExitFunction()
    {
        Run("""
            Function Clamp(ByVal value As Integer) As Integer
                Clamp = 100
                If value > 100 Then
                    Exit Function
                End If

                Clamp = value
            End Function

            Sub Main()
                Debug.Print Clamp(150)
                Debug.Print Clamp(42)
            End Sub
            """,
            "100", "42");
    }

    [TestMethod]
    public void EmitManagedApplication_AcceptsContinuedLinesAndTypeSuffixes()
    {
        Run("""
            Sub Main()
                Dim total As Long
                total = 1 + _
                        2 + _
                        3
                Debug.Print total&
            End Sub
            """,
            "6");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerConstExitTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ConstExitProgram.dll");

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
