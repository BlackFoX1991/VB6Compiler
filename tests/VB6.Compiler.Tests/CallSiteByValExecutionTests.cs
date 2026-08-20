using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// A call site may override how an argument is passed. VB6 code does this against Declare
/// parameters typed As Any, as in <c>CopyMemory dst, ByVal VarPtr(src), 4</c>, which is listed in
/// the roadmap blocker table and appears 13 times in one conformance module.
/// </summary>
[TestClass]
public sealed class CallSiteByValExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ByValAtTheCallSiteOverridesAByRefParameter()
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
                Bump keep
                Debug.Print keep

                keep = 0
                Bump ByVal keep
                Debug.Print keep
            End Sub
            """,
            "1",
            "0");
    }

    [TestMethod]
    public void GenerateCSharp_ByRefAtTheCallSiteKeepsTheReference()
    {
        var generation = VBCompilation.Create("""
            Sub Bump(Value As Long)
                Value = Value + 1
            End Sub

            Sub Main()
                Dim keep As Long
                Bump ByRef keep
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        StringAssert.Contains(generation.Source, "__vb6_Bump(ref __vb6_keep);");
    }

    private static void Run(string source, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerCallSiteByValTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, "CallSiteByValProgram.dll");

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
