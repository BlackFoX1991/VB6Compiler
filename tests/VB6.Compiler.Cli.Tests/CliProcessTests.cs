using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VB6.Compiler.Cli.Tests;

[TestClass]
public sealed class CliProcessTests
{
    [TestMethod]
    public void Report_ReturnsNonZeroForInvalidProject()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Broken.vbp");
            File.WriteAllText(projectPath, "Type=Exe\nModule=Main; Missing.bas\n");

            var result = RunCli(projectPath, "--report");

            Assert.AreNotEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "VB6PRJ0001");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_CompilesVbgProjectsThroughTheCli()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "LegacyGroup.vbg"),
                "Type=Group\nProject=First.vbp\nProject=Second.vbp\n");
            WriteExecutableProject(directory, "First");
            WriteExecutableProject(directory, "Second");
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(
                Path.Combine(directory, "LegacyGroup.vbg"),
                "--emit-assembly",
                outputDirectory);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Report_ReturnsNonZeroForUndeclaredVbgStartupProject()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(
                groupPath,
                "Type=Group\nProject=Actual.vbp\nStartupProject=Missing.vbp\n");
            WriteExecutableProject(directory, "Actual");

            var result = RunCli(groupPath, "--report");

            Assert.AreNotEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "VB6VBG0007");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static CliResult RunCli(string inputPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(inputPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "vb6c.dll"));
        startInfo.ArgumentList.Add(inputPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the vb6c process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static void WriteExecutableProject(string directory, string name)
    {
        File.WriteAllText(
            Path.Combine(directory, name + ".vbp"),
            $"Type=Exe\nStartup=\"Sub Main\"\nName=\"{name}\"\nModule=Main; {name}.bas\n");
        File.WriteAllText(
            Path.Combine(directory, name + ".bas"),
            "Sub Main()\n    Debug.Print 1\nEnd Sub\n");
    }

    private static string CreateTemporaryDirectory() =>
        Path.Combine(Path.GetTempPath(), "VB6CompilerCliTests", Guid.NewGuid().ToString("N"));

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
