using System.Diagnostics;
using System.Reflection.PortableExecutable;
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
    public void EmitAssembly_CompilesAndRunsReferencedVbgProjectsThroughTheCli()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(
                groupPath,
                "Type=Group\nProject=Consumer.vbp\nProject=Shared.vbp\n");
            File.WriteAllText(
                Path.Combine(directory, "Shared.vbp"),
                "Type=OleDll\nName=Shared\nClass=Customer; Customer.cls\n");
            File.WriteAllText(
                Path.Combine(directory, "Customer.cls"),
                "Public Function Value() As Long\n    Value = 7\nEnd Function\n");
            File.WriteAllText(
                Path.Combine(directory, "Consumer.vbp"),
                "Type=Exe\nStartup=\"Sub Main\"\nName=Consumer\n" +
                "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#Shared.vbp#Shared\n" +
                "Module=Main; Main.bas\n");
            File.WriteAllText(
                Path.Combine(directory, "Main.bas"),
                "Sub Main()\n" +
                "    Dim customer As Shared.Customer\n" +
                "    Set customer = New Shared.Customer\n" +
                "    Debug.Print customer.Value\n" +
                "End Sub\n");
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(groupPath, "--emit-assembly", outputDirectory);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Shared.dll")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Consumer.exe")));

            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(Path.Combine(outputDirectory, "Consumer.exe"));
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the emitted VBG consumer.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("7", standardOutput.Trim());
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

    [TestMethod]
    public void EmitAssembly_AcceptsX86ForLegacyVbpProjects()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "Legacy32");
            var outputPath = Path.Combine(directory, "bin", "Legacy32.exe");

            var result = RunCli(
                Path.Combine(directory, "Legacy32.vbp"),
                "--emit-assembly",
                outputPath,
                "--x86");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            using var stream = File.OpenRead(outputPath);
            using var peReader = new PEReader(stream);
            Assert.AreEqual(Machine.I386, peReader.PEHeaders.CoffHeader.Machine);
            Assert.IsTrue(peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_AppliesX64TargetToConditionalCompilation()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Width.bas");
            File.WriteAllText(sourcePath, """
                #If Win64 Then
                    Sub Main()
                        Debug.Print 64
                    End Sub
                #Else
                    Sub Main()
                        Debug.Print 32
                    End Sub
                #End If
                """);
            var outputPath = Path.Combine(directory, "bin", "Width.exe");

            var result = RunCli(sourcePath, "--emit-assembly", outputPath, "--x64");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = Path.GetDirectoryName(outputPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(outputPath);
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the x64 conditional-compilation output.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("64", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_CompilesLegacyDesignerVbpProjectsThroughTheCli()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "LegacyData.vbp"),
                "Type=OleDll\nName=LegacyData\nDesigner=MSDataEnvironment; DataEnvironment1.dsr\n");
            File.WriteAllText(
                Path.Combine(directory, "DataEnvironment1.dsr"),
                """
                VERSION 5.00
                Begin MSDataEnvironment DataEnvironment1
                End
                Attribute VB_Name = "DataEnvironment1"
                Public Function Value() As Long
                    Value = 3
                End Function
                """);
            var outputPath = Path.Combine(directory, "bin", "LegacyData.dll");

            var result = RunCli(
                Path.Combine(directory, "LegacyData.vbp"),
                "--emit-assembly",
                outputPath);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.IsTrue(File.Exists(outputPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_AcceptsX64ForSourceFiles()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Module1.bas");
            File.WriteAllText(sourcePath, "Sub Main()\n    Debug.Print 1\nEnd Sub\n");
            var outputPath = Path.Combine(directory, "bin", "Module1.exe");

            var result = RunCli(sourcePath, "--emit-assembly", outputPath, "--x64");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            using var stream = File.OpenRead(outputPath);
            using var peReader = new PEReader(stream);
            Assert.AreEqual(Machine.Amd64, peReader.PEHeaders.CoffHeader.Machine);
            Assert.IsFalse(peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
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
