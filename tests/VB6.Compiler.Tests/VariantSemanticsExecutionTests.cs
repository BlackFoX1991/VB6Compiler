using System.Diagnostics;

namespace VB6.Compiler.Tests;

/// <summary>
/// Variant behaviour that only shows up when the generated program actually runs: whether '+'
/// adds or concatenates, and whether IsNumeric accepts a numeric string.
/// </summary>
[TestClass]
public sealed class VariantSemanticsExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_AddsWhenOneVariantOperandIsANumber()
    {
        Run(
            """
            Sub Main()
                Dim total
                Dim text
                total = 40
                text = "2"
                Debug.Print total + text
                Debug.Print text + text
                Debug.Print total & text
            End Sub
            """,
            "AddProgram",
            "42", "22", "402");
    }

    [TestMethod]
    public void EmitManagedApplication_TreatsNumericStringsAsNumeric()
    {
        Run(
            """
            Sub Main()
                Dim value
                value = "123"
                Debug.Print IsNumeric(value)
                value = "abc"
                Debug.Print IsNumeric(value)
            End Sub
            """,
            "IsNumericProgram",
            "True", "False");
    }

    private static void Run(string source, string assemblyName, params string[] expectedLines)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantSemanticsTests",
            Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, $"{assemblyName}.dll");

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(d => $"{d.Code}: {d.Message}"))
                : string.Join(Environment.NewLine, result.BackendResult.Diagnostics.Select(d => $"{d.Id}: {d.Message}"));
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
                ?? throw new InvalidOperationException("Failed to start the generated Variant application.");

            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            CollectionAssert.AreEqual(
                expectedLines,
                standardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToArray());
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
