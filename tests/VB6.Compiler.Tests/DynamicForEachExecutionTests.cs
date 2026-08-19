using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DynamicForEachExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesDynamicArrayForEachInRuntimeOrder()
    {
        const string source = """
            Sub Main()
                Dim item As Variant
                Dim values() As Long
                ReDim values(2 To 4)
                values(2) = 20
                values(3) = 30
                values(4) = 40

                For Each item In values
                    Debug.Print item
                Next item
            End Sub
            """;

        var output = EmitAndRun(source, "DynamicForEachProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "20", "30", "40" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ExecutesForEachOverArrayParameterAndExitFor()
    {
        const string source = """
            Sub PrintFirst(values() As Long)
                Dim item As Variant
                For Each item In values
                    Debug.Print item
                    Exit For
                Next item
            End Sub

            Sub Main()
                Dim values() As Long
                ReDim values(5 To 7)
                values(5) = 50
                values(6) = 60
                values(7) = 70
                Call PrintFirst(values)
            End Sub
            """;

        var output = EmitAndRun(source, "ArrayParameterForEachProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "50" },
            SplitLines(output),
            output);
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerDynamicForEachTests", Guid.NewGuid().ToString("N"));
        var assemblyPath = Path.Combine(directory, assemblyName);

        try
        {
            var result = compilation.EmitManagedApplication(assemblyPath);
            var diagnostics = result.BackendResult is null
                ? string.Join(Environment.NewLine, result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}: {diagnostic.Message}"))
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
                ?? throw new InvalidOperationException("Failed to start the generated dynamic For Each application.");

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
