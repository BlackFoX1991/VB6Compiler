using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 accepts a literal, an expression, or a function result where a ByRef parameter is declared.
/// It passes a temporary and discards the write-back. This was the single largest semantic blocker
/// in the conformance corpus at 409 occurrences.
/// </summary>
[TestClass]
public sealed class ByRefTemporaryExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PassesNonVariableArgumentsByRefThroughTemporaries()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Private Function Twice(ByVal N As Long) As Long
                Twice = N * 2
            End Function

            Public Sub Main()
                Dim keep As Long
                keep = 10

                Bump keep
                Debug.Print keep

                Bump 5
                Bump Twice(3)
                Bump keep + 1
                Debug.Print keep
            End Sub
            """,
            "11",
            "11");
    }

    /// <summary>
    /// Parentheses force an argument to be evaluated to a value, so the callee cannot write back.
    /// Only a Call statement has a parenthesized argument list, which is what separates
    /// <c>Call Bump(keep)</c> from <c>Bump (keep)</c> and <c>Call Bump((keep))</c>.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_TreatsParenthesizedArgumentsAsByValue()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Public Sub Main()
                Dim keep As Long

                keep = 0
                Call Bump(keep)
                Debug.Print keep

                keep = 0
                Bump (keep)
                Debug.Print keep

                keep = 0
                Call Bump((keep))
                Debug.Print keep
            End Sub
            """,
            "1",
            "0",
            "0");
    }

    [TestMethod]
    public void GenerateCSharp_UsesATemporaryOnlyForNonVariableArguments()
    {
        var generation = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim keep As Long
                Bump keep
                Bump 5
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        StringAssert.Contains(generation.Source, "__vb6_Bump(ref __vb6_keep);");
        StringAssert.Contains(generation.Source, "__vb6_Bump(ref VBByRef.Temp<int>(");
    }

    [TestMethod]
    public void Analyze_KeepsByRefTypeMismatchAnError()
    {
        var analysis = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim small As Integer
                Bump small
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success, "VB6 reports a ByRef argument type mismatch for a variable of the wrong type.");
        Assert.IsTrue(analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0008"));
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerByRefTemporaryTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "ByRefTemporaryProgram.dll");

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
