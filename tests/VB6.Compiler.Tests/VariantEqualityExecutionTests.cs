using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantEqualityExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ComparesEmptyAndNumericStringVariantToInteger()
    {
        const string source = """
            Sub Main()
                Dim value As Variant
                Debug.Print value = 0

                value = "42"
                Debug.Print value = 42
                Debug.Print value = 41
            End Sub
            """;

        var output = EmitAndRun(source, "VariantEqualityProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "True", "True", "False" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComparesVariantFunctionReturnSlotToInteger()
    {
        const string source = """
            Function NumberExpression() As Variant
                If NumberExpression = 0 Then
                    NumberExpression = 42
                End If
            End Function

            Sub Main()
                Debug.Print NumberExpression()
            End Sub
            """;

        var output = EmitAndRun(source, "VariantReturnEqualityProgram.dll");

        CollectionAssert.AreEqual(
            new[] { "42" },
            SplitLines(output),
            output);
    }

    [TestMethod]
    public void GenerateCSharp_LowersVariantLeftIntegerEqualityThroughDoubleConversions()
    {
        var generation = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                If value = 0 Then
                    Debug.Print 1
                End If
            End Sub
            """, "Module1.bas").GenerateCSharp();

        Assert.IsTrue(
            generation.Success,
            string.Join(Environment.NewLine, generation.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        Assert.IsNotNull(generation.Source);
        Assert.IsFalse(generation.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0053"));
        StringAssert.Contains(generation.Source, "VBOperators.Equal(VBConversions.CDbl(__vb6_value), VBConversions.CDbl(");
    }

    [TestMethod]
    public void Analyze_KeepsNumericLeftVariantRightEqualityGuarded()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Debug.Print 0 = value
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0053");
    }

    [TestMethod]
    public void Analyze_KeepsVariantDoubleEqualityGuarded()
    {
        var analysis = VBCompilation.Create("""
            Sub Main()
                Dim value As Variant
                Dim target As Double
                target = 0
                Debug.Print value = target
            End Sub
            """, "Module1.bas").Analyze();

        Assert.IsFalse(analysis.Success);
        CollectionAssert.Contains(
            analysis.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray(),
            "VB6S0053");
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(Path.GetTempPath(), "VB6CompilerVariantEqualityTests", Guid.NewGuid().ToString("N"));
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
                ?? throw new InvalidOperationException("Failed to start the generated Variant equality application.");

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
