using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// Omitted <c>Optional</c> arguments. In the conformance corpus this was the largest single cause
/// of argument-count errors: <c>AddSymbol</c> declares five parameters, two of them Optional, and
/// every call supplies four.
/// </summary>
[TestClass]
public sealed class OptionalArgumentExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesDeclaredDefaultsAndTypeDefaults()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Report(ByVal Name As String, Optional ByVal Level As Long = 3, Optional ByVal Loud As Boolean)
                Debug.Print Name & "/" & Level & "/" & Loud
            End Sub

            Public Sub Main()
                Report "a"
                Report "b", 7
                Report "c", 7, True
            End Sub
            """,
            "a/3/False",
            "b/7/False",
            "c/7/True");
    }

    /// <summary>An Optional String without a default gets the empty string, not a null reference.</summary>
    [TestMethod]
    public void EmitManagedApplication_DefaultsAnOptionalStringToEmpty()
    {
        Run("""
            Attribute VB_Name = "Module1"
            Option Explicit

            Private Sub Show(Optional ByVal Suffix As String)
                Debug.Print "[" & Suffix & "]"
            End Sub

            Public Sub Main()
                Show
                Show "x"
            End Sub
            """,
            "[]",
            "[x]");
    }

    [TestMethod]
    public void Analyze_StillReportsTooFewRequiredArguments()
    {
        var analysis = VBCompilation.Create("""
            Sub Report(ByVal Name As String, Optional ByVal Level As Long = 3)
                Debug.Print Name
            End Sub

            Sub Main()
                Report
            End Sub
            """, "Module1.bas").Analyze();

        var diagnostic = analysis.Diagnostics.Single(d => d.Code == "VB6S0006");
        StringAssert.Contains(diagnostic.Message, "1 to 2");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerOptionalArgumentTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "OptionalArgumentProgram.dll");

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
