using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ChrIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesReachableAsciiChr()
    {
        const string source = """
            Sub Main()
                Debug.Print Chr(34)
                Debug.Print Chr(65)
            End Sub
            """;

        var output = EmitAndRun(source, "ChrIntrinsicProgram.dll");

        CollectionAssert.AreEqual(new[] { "\"", "A" }, SplitLines(output), output);
    }

    [TestMethod]
    public void GenerateCSharp_RewritesBuiltInChrButPreservesUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Chr(34)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(builtIn.Success, string.Join(Environment.NewLine, builtIn.Diagnostics));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Chr(");
        Assert.IsFalse(builtIn.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "VB6S0005" && diagnostic.Message.Contains("Chr", StringComparison.OrdinalIgnoreCase)));

        var userDefined = VBCompilation.Create("""
            Function Chr(ByVal value As Long) As String
                Chr = "custom"
            End Function

            Sub Main()
                Debug.Print Chr(34)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(userDefined.Success, string.Join(Environment.NewLine, userDefined.Diagnostics));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Chr(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Chr(", StringComparison.Ordinal));
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerChrTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, assemblyName);

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic => diagnostic.ToString()))
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
                ?? throw new InvalidOperationException("Failed to start generated Chr application.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            return standardOutput;
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string[] SplitLines(string output) =>
        output.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray();
}
