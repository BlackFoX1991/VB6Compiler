using System.Diagnostics;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class BuiltInStringConstantExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_UsesVbStringConstants()
    {
        const string source = """
            Sub Main()
                Debug.Print Len(vbCrLf)
                Debug.Print Len(vbTab)
                Debug.Print vbNullString & "x"
            End Sub
            """;

        var output = EmitAndRun(source, "BuiltInStringConstants.dll");

        CollectionAssert.AreEqual(new[] { "2", "1", "x" }, SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_UserDeclarationShadowsBuiltInConstant()
    {
        const string source = """
            Private Const vbCrLf As String = "custom"

            Sub Main()
                Debug.Print vbCrLf
            End Sub
            """;

        var output = EmitAndRun(source, "ShadowBuiltInStringConstant.dll");

        CollectionAssert.AreEqual(new[] { "custom" }, SplitLines(output), output);
    }

    [TestMethod]
    public void EmitManagedApplication_ComposesBuiltInConstantsWithBracketedEnumSymbols()
    {
        const string source = """
            Enum SeparatorLength
                [CrLfLength] = 2
            End Enum

            Sub Main()
                Debug.Print Len(vbCrLf) = [CrLfLength]
            End Sub
            """;

        var output = EmitAndRun(source, "BuiltInConstantBracketedEnum.dll");

        CollectionAssert.AreEqual(new[] { "True" }, SplitLines(output), output);
    }

    [TestMethod]
    public void ProjectAnalysis_ResolvesBuiltInConstantsAcrossModules()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerBuiltInConstantProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Constants.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Constants"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Debug.Print vbCrLf & vbTab
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsFalse(analysis.Diagnostics.Any(diagnostic =>
                    diagnostic.Code == "VB6S0001" &&
                    (diagnostic.Message.Contains("vbCrLf", StringComparison.OrdinalIgnoreCase) ||
                     diagnostic.Message.Contains("vbTab", StringComparison.OrdinalIgnoreCase))),
                string.Join(Environment.NewLine, analysis.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static string EmitAndRun(string source, string assemblyName)
    {
        var compilation = VBCompilation.Create(source, "Module1.bas");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerBuiltInStringConstantTests",
            Guid.NewGuid().ToString("N"));
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
                ?? throw new InvalidOperationException("Failed to start generated built-in constant application.");
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
