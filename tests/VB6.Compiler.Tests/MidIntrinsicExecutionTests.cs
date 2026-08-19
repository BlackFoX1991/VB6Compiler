using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class MidIntrinsicExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesThreeArgumentMidAndMidDollar()
    {
        const string source = """
            Sub Main()
                Debug.Print Mid("abcdef", 2, 3)
                Debug.Print Mid$("abcdef", 5, 20)
            End Sub
            """;

        var output = EmitAndRun(source, "MidIntrinsicProgram.dll");

        CollectionAssert.AreEqual(new[] { "bcd", "ef" }, SplitLines(output), output);
    }

    [TestMethod]
    public void GenerateCSharp_RewritesBuiltInMidButPreservesUserFunction()
    {
        var builtIn = VBCompilation.Create("""
            Sub Main()
                Debug.Print Mid("abc", 2, 1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(builtIn.Success, string.Join(Environment.NewLine, builtIn.Diagnostics));
        Assert.IsNotNull(builtIn.Source);
        StringAssert.Contains(builtIn.Source, "VBStrings.Mid(\"abc\"");

        var userDefined = VBCompilation.Create("""
            Function Mid(ByVal value As String, ByVal start As Long, ByVal length As Long) As String
                Mid = "custom"
            End Function

            Sub Main()
                Debug.Print Mid("abc", 1, 1)
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(userDefined.Success, string.Join(Environment.NewLine, userDefined.Diagnostics));
        Assert.IsNotNull(userDefined.Source);
        StringAssert.Contains(userDefined.Source, "__vb6_Mid(");
        Assert.IsFalse(userDefined.Source.Contains("VBStrings.Mid(", StringComparison.Ordinal));
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerMidTests", Guid.NewGuid().ToString("N"));
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
                ?? throw new InvalidOperationException("Failed to start generated Mid application.");
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
