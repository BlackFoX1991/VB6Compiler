using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
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
    public void Report_ReturnsNonZeroForExecutableProjectWithoutEntryPoint()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "NoMain.vbp");
            File.WriteAllText(
                projectPath,
                "Type=Exe\nStartup=\"Sub Main\"\nName=NoMain\nModule=Only; Only.bas\n");
            File.WriteAllText(
                Path.Combine(directory, "Only.bas"),
                "Sub Helper()\n    Debug.Print 1\nEnd Sub\n");

            var result = RunCli(projectPath, "--report");

            Assert.AreNotEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "VB6PRJ0005");
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
    public void EmitAssembly_StartsAFormProjectThroughTheGeneratedAppHost()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClosingForm.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="MainForm"
                Name="ClosingForm"
                Form=MainForm.frm
                """);
            File.WriteAllText(Path.Combine(directory, "MainForm.frm"), """
                VERSION 5.00
                Begin VB.Form MainForm
                   Caption = "Closes during load"
                End
                Attribute VB_Name = "MainForm"
                Attribute VB_PredeclaredId = True

                Private Sub Form_Load()
                    Unload Me
                End Sub
                """);
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(projectPath, "--emit-assembly", outputDirectory, "--x86");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var appHostPath = Path.Combine(outputDirectory, "ClosingForm.exe");
            Assert.IsTrue(File.Exists(appHostPath));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.WinForms.dll")));

            using var process = Process.Start(new ProcessStartInfo(appHostPath)
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start the generated Form apphost.");
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("The generated Form apphost did not exit after Unload Me.");
            }
            var standardError = process.StandardError.ReadToEnd();
            Assert.AreEqual(0, process.ExitCode, standardError);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_CompilesSingleVbpIntoAnOutputDirectoryAndRunsIt()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "SingleProject");
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(
                Path.Combine(directory, "SingleProject.vbp"),
                "--emit-assembly",
                outputDirectory);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var outputPath = Path.Combine(outputDirectory, "SingleProject.exe");
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(IsNativeWindowsAppHost(outputPath));

            var run = RunProcess(outputPath, outputDirectory);
            Assert.AreEqual(0, run.ExitCode, run.StandardError);
            Assert.AreEqual("1", run.StandardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_PassesProcessArgumentsToCommandIntrinsic()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "CommandLine.bas");
            File.WriteAllText(sourcePath, "Sub Main()\n    Debug.Print Command$\nEnd Sub\n");
            var outputPath = Path.Combine(directory, "bin", "CommandLine.exe");

            var result = RunCli(sourcePath, "--emit-assembly", outputPath);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var startInfo = new ProcessStartInfo(outputPath)
            {
                WorkingDirectory = Path.GetDirectoryName(outputPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("first");
            startInfo.ArgumentList.Add("two words");
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the generated Command apphost.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("first \"two words\"", standardOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_CompilesSingleLibraryVbpIntoAnOutputDirectory()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "SingleLibrary.vbp"),
                "Type=OleDll\nName=SingleLibrary\nClass=Widget; Widget.cls\n");
            File.WriteAllText(
                Path.Combine(directory, "Widget.cls"),
                "VERSION 1.0 CLASS\nAttribute VB_Name = \"Widget\"\nPublic Function Value() As Long\n    Value = 7\nEnd Function\n");
            var outputDirectory = Path.Combine(directory, "bin");
            Directory.CreateDirectory(outputDirectory);

            var result = RunCli(
                Path.Combine(directory, "SingleLibrary.vbp"),
                "--emit-assembly",
                outputDirectory);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "SingleLibrary.dll")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void WriteInputManifest_ContainsOnlyDeclaredProjectDependencies()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Manifest.vbp");
            File.WriteAllText(
                projectPath,
                "Type=Exe\nName=Manifest\nModule=Main; Main.bas\nForm=Main.frm\n" +
                "Reference=*\\G{00025E01-0000-0000-C000-000000000046}#1.0#0#References\\Legacy.tlb#Legacy Library\n" +
                "Object={831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0; Controls\\Legacy.ocx\n" +
                "ResFile32=Resources\\Legacy.res\n");
            File.WriteAllText(Path.Combine(directory, "Main.bas"), "Sub Main()\nEnd Sub\n");
            File.WriteAllText(
                Path.Combine(directory, "Main.frm"),
                "VERSION 5.00\nBegin VB.Form Main\nEnd\n");
            File.WriteAllBytes(Path.Combine(directory, "Main.frx"), new byte[] { 1, 2, 3 });
            var referencesDirectory = Path.Combine(directory, "References");
            var controlsDirectory = Path.Combine(directory, "Controls");
            var resourcesDirectory = Path.Combine(directory, "Resources");
            Directory.CreateDirectory(referencesDirectory);
            Directory.CreateDirectory(controlsDirectory);
            Directory.CreateDirectory(resourcesDirectory);
            File.WriteAllBytes(Path.Combine(referencesDirectory, "Legacy.tlb"), new byte[] { 1 });
            File.WriteAllBytes(Path.Combine(controlsDirectory, "Legacy.ocx"), new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(resourcesDirectory, "Legacy.res"), new byte[] { 3 });
            var unrelatedDirectory = Path.Combine(directory, "unrelated");
            Directory.CreateDirectory(unrelatedDirectory);
            File.WriteAllText(Path.Combine(unrelatedDirectory, "Ignored.bas"), "Sub Ignored()\nEnd Sub\n");
            var manifestPath = Path.Combine(directory, "obj", "Manifest.inputs");

            var result = RunCli(projectPath, "--write-input-manifest", manifestPath);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var lines = File.ReadAllLines(manifestPath);
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(directory, "Main.bas"));
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(directory, "Main.frm"));
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(directory, "Main.frx"));
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(referencesDirectory, "Legacy.tlb"));
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(controlsDirectory, "Legacy.ocx"));
            StringAssert.Contains(string.Join(Environment.NewLine, lines), Path.Combine(resourcesDirectory, "Legacy.res"));
            Assert.IsFalse(lines.Any(line => line.Contains("Ignored.bas", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(lines.All(line => line.Contains('\t')));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void MsBuildSdk_CompilesAndTracksVbgProjectGroupsIncrementally()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacySuite.vbg");
            File.WriteAllText(
                groupPath,
                "Type=Group\nProject=First.vbp\nProject=Second.vbp\n");
            WriteExecutableProject(directory, "First");
            WriteExecutableProject(directory, "Second");
            var localControlDirectory = Path.Combine(directory, "References");
            Directory.CreateDirectory(localControlDirectory);
            var localControlPath = Path.Combine(localControlDirectory, "LegacyControl.ocx");
            File.WriteAllBytes(localControlPath, new byte[] { 0x4C, 0x65, 0x67, 0x61, 0x63, 0x79 });
            File.AppendAllText(
                Path.Combine(directory, "First.vbp"),
                "Designer=MSDataEnvironment; First.dsr\n");
            File.WriteAllText(
                Path.Combine(directory, "First.dsr"),
                "VERSION 5.00\n" +
                "Begin MSDataEnvironment DataEnvironment1\n" +
                "End\n" +
                "Attribute VB_Name = \"DataEnvironment1\"\n" +
                "Public Function Value() As Long\n" +
                "    Value = 1\n" +
                "End Function\n");

            var repositoryRoot = FindRepositoryRoot();
            var packageDirectory = Path.Combine(directory, "packages");
            var packageCache = Path.Combine(directory, "nuget");
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-vbg-binary-input-platform-test",
                "-p:PackageOutputPath=" + packageDirectory);
            Assert.AreEqual(0, packResult.ExitCode, packResult.StandardError + packResult.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);

            var projectPath = Path.Combine(directory, "LegacySuite.csproj");
            var outputDirectory = Path.Combine(directory, "bin", "Release", "legacy");
            var compilerPath = Path.Combine(AppContext.BaseDirectory, "vb6c.exe");
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-vbg-binary-input-platform-test" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <OutputPath>bin\Release\</OutputPath>
                    <VB6ProjectGroup>{groupPath}</VB6ProjectGroup>
                    <VB6CompilerPath>{compilerPath}</VB6CompilerPath>
                    <VB6CompilerGroupOutputDirectory>{outputDirectory}</VB6CompilerGroupOutputDirectory>
                    <VB6TargetPlatform>x64</VB6TargetPlatform>
                  </PropertyGroup>
                </Project>
                """);

            var firstBuild = RunMsBuild(projectPath, restore: true, nugetPackages: packageCache);
            Assert.AreEqual(0, firstBuild.ExitCode, firstBuild.StandardError + firstBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));
            AssertPeTarget(Path.Combine(outputDirectory, "First.dll"), Machine.Amd64, requires32Bit: false);

            var stampPath = Directory
                .GetFiles(directory, "VB6GroupCompile.stamp", SearchOption.AllDirectories)
                .Single();
            var firstStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);

            var secondBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, secondBuild.ExitCode, secondBuild.StandardError + secondBuild.StandardOutput);
            Assert.AreEqual(firstStamp, File.GetLastWriteTimeUtc(stampPath));

            Thread.Sleep(1100);
            File.AppendAllText(localControlPath, "\nchanged");
            var controlReferenceBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, controlReferenceBuild.ExitCode, controlReferenceBuild.StandardError + controlReferenceBuild.StandardOutput);
            Assert.AreEqual(
                firstStamp,
                File.GetLastWriteTimeUtc(stampPath),
                "Undeclared files in the project directory must not invalidate an exact input manifest.");

            Thread.Sleep(1100);
            File.Delete(Path.Combine(outputDirectory, "Second.exe"));
            var recoveryBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, recoveryBuild.ExitCode, recoveryBuild.StandardError + recoveryBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > firstStamp);

            var recoveryStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);
            File.WriteAllText(
                Path.Combine(directory, "First.bas"),
                "Sub Main()\n    Debug.Print 2\nEnd Sub\n");
            var thirdBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, thirdBuild.ExitCode, thirdBuild.StandardError + thirdBuild.StandardOutput);
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > recoveryStamp);

            var thirdStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);
            File.WriteAllText(
                Path.Combine(directory, "First.dsr"),
                "VERSION 5.00\n" +
                "Begin MSDataEnvironment DataEnvironment1\n" +
                "End\n" +
                "Attribute VB_Name = \"DataEnvironment1\"\n" +
                "Public Function Value() As Long\n" +
                "    Value = 2\n" +
                "End Function\n");
            var designerBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, designerBuild.ExitCode, designerBuild.StandardError + designerBuild.StandardOutput);
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > thirdStamp);

            var designerStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);
            File.AppendAllText(
                Path.Combine(directory, "First.vbp"),
                "ExeName32=renamed\\FirstRenamed.exe\n");
            var renameBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, renameBuild.ExitCode, renameBuild.StandardError + renameBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "FirstRenamed.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > designerStamp);

            Thread.Sleep(1100);
            File.WriteAllText(
                groupPath,
                "Type=Group\nProject=First.vbp\n");
            var removalBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, removalBuild.ExitCode, removalBuild.StandardError + removalBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "FirstRenamed.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Second.exe")));

            var clean = RunMsBuild(projectPath, target: "Clean", nugetPackages: packageCache);
            Assert.AreEqual(0, clean.ExitCode, clean.StandardError + clean.StandardOutput);
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "FirstRenamed.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));
            Assert.IsFalse(File.Exists(stampPath));

            var rebuild = RunMsBuild(projectPath, target: "Rebuild", nugetPackages: packageCache);
            Assert.AreEqual(0, rebuild.ExitCode, rebuild.StandardError + rebuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "FirstRenamed.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void MsBuildSdk_FailsWhenConfiguredVbpIsMissing()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "MissingLegacy.csproj");
            var packageDirectory = Path.Combine(directory, "packages");
            var repositoryRoot = FindRepositoryRoot();
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-missing-vbp-test",
                "-p:PackageOutputPath=" + packageDirectory);
            Assert.AreEqual(0, packResult.ExitCode, packResult.StandardError + packResult.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-missing-vbp-test" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <VB6Project>{Path.Combine(directory, "MissingLegacy.vbp")}</VB6Project>
                    <VB6CompilerPath>{Path.Combine(AppContext.BaseDirectory, "vb6c.exe")}</VB6CompilerPath>
                  </PropertyGroup>
                </Project>
                """);

            var result = RunMsBuild(projectPath, restore: true);

            Assert.AreNotEqual(0, result.ExitCode, result.StandardOutput);
            StringAssert.Contains(result.StandardError + result.StandardOutput, "VB6 project file was not found");
            Assert.IsFalse(File.Exists(Path.Combine(directory, "bin", "Release", "MissingLegacy.dll")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void MsBuildSdk_DefaultsToX86AndSupportsExplicitX64AndAnyCpu()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "PlatformSdk");
            var packageDirectory = Path.Combine(directory, "packages");
            var packageCache = Path.Combine(directory, "nuget");
            var repositoryRoot = FindRepositoryRoot();
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-platform-target-test",
                "-p:PackageOutputPath=" + packageDirectory);
            Assert.AreEqual(0, packResult.ExitCode, packResult.StandardError + packResult.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);

            var projectPath = Path.Combine(directory, "PlatformSdk.csproj");
            var outputPath = Path.Combine(directory, "bin", "Release", "legacy", "PlatformSdk.dll");
            WriteSdkProject(projectPath, directory, outputPath, null, "1.0.0-platform-target-test");

            var defaultBuild = RunMsBuild(projectPath, restore: true, nugetPackages: packageCache);
            Assert.AreEqual(0, defaultBuild.ExitCode, defaultBuild.StandardError + defaultBuild.StandardOutput);
            AssertPeTarget(outputPath, Machine.I386, requires32Bit: true);

            Thread.Sleep(1100);
            WriteSdkProject(projectPath, directory, outputPath, "x64", "1.0.0-platform-target-test");
            var x64Build = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, x64Build.ExitCode, x64Build.StandardError + x64Build.StandardOutput);
            AssertPeTarget(outputPath, Machine.Amd64, requires32Bit: false);

            Thread.Sleep(1100);
            WriteSdkProject(projectPath, directory, outputPath, "AnyCPU", "1.0.0-platform-target-test");
            var anyCpuBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, anyCpuBuild.ExitCode, anyCpuBuild.StandardError + anyCpuBuild.StandardOutput);
            AssertPeTarget(outputPath, Machine.I386, requires32Bit: false);

            Thread.Sleep(1100);
            WriteSdkProject(
                projectPath,
                directory,
                outputPath,
                "x86",
                "1.0.0-platform-target-test",
                "vb6-sp6");
            var strictBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, strictBuild.ExitCode, strictBuild.StandardError + strictBuild.StandardOutput);
            AssertPeTarget(outputPath, Machine.I386, requires32Bit: true);

            Thread.Sleep(1100);
            WriteSdkProject(
                projectPath,
                directory,
                outputPath,
                "x64",
                "1.0.0-platform-target-test",
                "vb6-sp6");
            var strictInvalidBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreNotEqual(0, strictInvalidBuild.ExitCode, strictInvalidBuild.StandardOutput);
            StringAssert.Contains(
                strictInvalidBuild.StandardError + strictInvalidBuild.StandardOutput,
                "VB6CompatibilityProfile vb6-sp6 supports x86 targets only");

            Thread.Sleep(1100);
            WriteSdkProject(projectPath, directory, outputPath, "arm64", "1.0.0-platform-target-test");
            var invalidBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreNotEqual(0, invalidBuild.ExitCode, invalidBuild.StandardOutput);
            StringAssert.Contains(
                invalidBuild.StandardError + invalidBuild.StandardOutput,
                "VB6TargetPlatform must be x86, x64 or anycpu");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void MsBuildSdk_TracksSingleVbpIncrementallyAndRepairsMissingOutput()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "SingleSdk");
            var packageDirectory = Path.Combine(directory, "packages");
            var packageCache = Path.Combine(directory, "nuget");
            var repositoryRoot = FindRepositoryRoot();
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-single-output-reconciliation-test3",
                "-p:PackageOutputPath=" + packageDirectory);
            Assert.AreEqual(0, packResult.ExitCode, packResult.StandardError + packResult.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);

            var projectPath = Path.Combine(directory, "SingleSdk.csproj");
            var outputPath = Path.Combine(directory, "bin", "Release", "legacy", "SingleSdk.dll");
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-single-output-reconciliation-test3" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <OutputPath>bin\Release\</OutputPath>
                    <VB6Project>{Path.Combine(directory, "SingleSdk.vbp")}</VB6Project>
                    <VB6CompilerPath>{Path.Combine(AppContext.BaseDirectory, "vb6c.exe")}</VB6CompilerPath>
                    <VB6CompilerOutput>{outputPath}</VB6CompilerOutput>
                  </PropertyGroup>
                </Project>
                """);

            var designTimeBuild = RunMsBuild(projectPath, restore: true, nugetPackages: packageCache, properties: ["DesignTimeBuild=true"]);
            Assert.AreEqual(0, designTimeBuild.ExitCode, designTimeBuild.StandardError + designTimeBuild.StandardOutput);
            Assert.IsFalse(File.Exists(outputPath));
            Assert.IsTrue(Directory.GetFiles(directory, "VB6Compile.stamp.inputs", SearchOption.AllDirectories).Length == 1);

            var firstBuild = RunMsBuild(projectPath, restore: true, nugetPackages: packageCache);
            Assert.AreEqual(0, firstBuild.ExitCode, firstBuild.StandardError + firstBuild.StandardOutput);
            Assert.IsTrue(File.Exists(outputPath));
            var stampPath = Directory.GetFiles(directory, "VB6Compile.stamp", SearchOption.AllDirectories).Single();
            var firstStamp = File.GetLastWriteTimeUtc(stampPath);

            Thread.Sleep(1100);
            var secondBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, secondBuild.ExitCode, secondBuild.StandardError + secondBuild.StandardOutput);
            Assert.AreEqual(firstStamp, File.GetLastWriteTimeUtc(stampPath));

            Thread.Sleep(1100);
            File.Delete(outputPath);
            var recoveryBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, recoveryBuild.ExitCode, recoveryBuild.StandardError + recoveryBuild.StandardOutput);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > firstStamp);

            var recoveryStamp = File.GetLastWriteTimeUtc(stampPath);
            var renamedOutputPath = Path.Combine(directory, "bin", "Release", "legacy", "SingleSdkRenamed.dll");
            Thread.Sleep(1100);
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-single-output-reconciliation-test3" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <OutputPath>bin\Release\</OutputPath>
                    <VB6Project>{Path.Combine(directory, "SingleSdk.vbp")}</VB6Project>
                    <VB6CompilerPath>{Path.Combine(AppContext.BaseDirectory, "vb6c.exe")}</VB6CompilerPath>
                    <VB6CompilerOutput>{renamedOutputPath}</VB6CompilerOutput>
                  </PropertyGroup>
                </Project>
                """);
            var renameBuild = RunMsBuild(projectPath, nugetPackages: packageCache);
            Assert.AreEqual(0, renameBuild.ExitCode, renameBuild.StandardError + renameBuild.StandardOutput);
            Assert.IsFalse(File.Exists(outputPath));
            Assert.IsTrue(File.Exists(renamedOutputPath));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > recoveryStamp);

            var clean = RunMsBuild(projectPath, target: "Clean", nugetPackages: packageCache);
            Assert.AreEqual(0, clean.ExitCode, clean.StandardError + clean.StandardOutput);
            Assert.IsFalse(File.Exists(renamedOutputPath));
            Assert.IsFalse(File.Exists(stampPath));
            Assert.IsFalse(File.Exists(Path.ChangeExtension(renamedOutputPath, ".pdb")));

            var rebuild = RunMsBuild(projectPath, target: "Rebuild", nugetPackages: packageCache);
            Assert.AreEqual(0, rebuild.ExitCode, rebuild.StandardError + rebuild.StandardOutput);
            Assert.IsTrue(File.Exists(renamedOutputPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_CompilesVbgProjectWithNativeOcxDesignerThroughTheCli()
    {
        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWow64",
            "RICHTX32.OCX");
        if (!OperatingSystem.IsWindows() || !File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The registered RichTextBox type library fixture is not available.");
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "NativeOcxGroup.vbg"),
                "Type=Group\nProject=NativeOcx.vbp\n");
            File.WriteAllText(
                Path.Combine(directory, "NativeOcx.vbp"),
                $"Type=Exe\nStartup=\"Main\"\nName=NativeOcx\n" +
                "Object={3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0; RICHTX32.OCX\n" +
                $"Reference=*\\G{{3B7C8863-D78F-101B-B9B5-04021C009402}}#1.2#0#{typeLibraryPath}#RichTextLib\n" +
                "Form=Main.frm\n");
            File.WriteAllText(
                Path.Combine(directory, "Main.frm"),
                "VERSION 5.00\n" +
                "Begin VB.Form Main\n" +
                "   Begin RichTextLib.RichTextBox editor\n" +
                "   End\n" +
                "End\n" +
                "Attribute VB_Name = \"Main\"\n" +
                "Attribute VB_PredeclaredId = True\n" +
                "Private Sub Editor_KeyPress(KeyAscii As Integer)\n" +
                "    KeyAscii = Asc(\"y\")\n" +
                "End Sub\n");
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(
                Path.Combine(directory, "NativeOcxGroup.vbg"),
                "--emit-assembly",
                outputDirectory,
                "--x86");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var assemblyPath = Path.Combine(outputDirectory, "NativeOcx.dll");
            var appHostPath = Path.Combine(outputDirectory, "NativeOcx.exe");
            Assert.IsTrue(File.Exists(assemblyPath));
            Assert.IsTrue(File.Exists(appHostPath));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);
            Assert.AreEqual(Machine.I386, peReader.PEHeaders.CoffHeader.Machine);
            Assert.IsTrue(peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
            Assert.IsTrue(IsNativeWindowsAppHost(appHostPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_StartsAndClosesAnX86VbgProjectWithNativeRichTextOcx()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Native OCX activation requires Windows.");
        }

        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWow64",
            "RICHTX32.OCX");
        if (!File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The registered RichTextBox type library fixture is not available.");
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "NativeOcxStartup.vbg"),
                "Type=Group\nProject=NativeOcxStartup.vbp\n");
            File.WriteAllText(
                Path.Combine(directory, "NativeOcxStartup.vbp"),
                $"Type=Exe\nStartup=\"Main\"\nName=NativeOcxStartup\n" +
                "Object={3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0; RICHTX32.OCX\n" +
                $"Reference=*\\G{{3B7C8863-D78F-101B-B9B5-04021C009402}}#1.2#0#{typeLibraryPath}#RichTextLib\n" +
                "Form=Main.frm\n");
            File.WriteAllText(
                Path.Combine(directory, "Main.frm"),
                "VERSION 5.00\n" +
                "Begin VB.Form Main\n" +
                "   Caption = \"Native OCX startup\"\n" +
                "   Begin RichTextLib.RichTextBox editor\n" +
                "   End\n" +
                "End\n" +
                "Attribute VB_Name = \"Main\"\n" +
                "Attribute VB_PredeclaredId = True\n" +
                "Private Sub Form_Load()\n" +
                "    Unload Me\n" +
                "End Sub\n");
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(
                Path.Combine(directory, "NativeOcxStartup.vbg"),
                "--emit-assembly",
                outputDirectory,
                "--x86");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var appHostPath = Path.Combine(outputDirectory, "NativeOcxStartup.exe");
            Assert.IsTrue(File.Exists(appHostPath));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.WinForms.dll")));

            var startInfo = new ProcessStartInfo(appHostPath)
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.Environment["VB6_REQUIRE_NATIVE_OCX"] = "1";
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the native OCX Form apphost.");
            if (!process.WaitForExit(15000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("The native OCX Form apphost did not exit after Unload Me.");
            }

            var standardError = process.StandardError.ReadToEnd();
            Assert.AreEqual(0, process.ExitCode, standardError);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_ProducesAnActivatableComHostForAnOleDllProject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM host activation requires Windows.");
        }

        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ComSample.vbp");
            File.WriteAllText(projectPath, "Type=OleDll\nName=ComSample\nClass=Widget; Widget.cls\n");
            File.WriteAllText(
                Path.Combine(directory, "Widget.cls"),
                "Attribute VB_Name = \"Widget\"\n" +
                "Public Function Add(ByVal left As Long, ByVal right As Long) As Long\n" +
                "    Add = left + right\n" +
                "End Function\n" +
                "Public Sub Increment(ByRef value As Long)\n" +
                "    value = value + 1\n" +
                "End Sub\n" +
                "Public Sub MutateVariantArray(ByRef value As Variant)\n" +
                "    value(1, 4) = 99\n" +
                "    value(2, 3) = 123\n" +
                "End Sub\n");
            var outputPath = Path.Combine(directory, "bin", "ComSample.dll");

            var result = RunCli(
                projectPath,
                "--emit-assembly",
                outputPath,
                "--com-host",
                "--com-manifest",
                "--x64");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var comHostPath = Path.Combine(directory, "bin", "ComSample.comhost.dll");
            Assert.IsTrue(File.Exists(comHostPath));
            var manifestPath = Path.Combine(directory, "bin", "ComSample.manifest");
            Assert.IsTrue(File.Exists(manifestPath));
            var manifest = XDocument.Load(manifestPath);
            XNamespace manifestNamespace = "urn:schemas-microsoft-com:asm.v1";
            var assemblyIdentity = manifest.Root?.Element(manifestNamespace + "assemblyIdentity");
            Assert.IsNotNull(assemblyIdentity);
            Assert.AreEqual("ComSample", assemblyIdentity!.Attribute("name")?.Value);
            var comClass = manifest.Root?
                .Element(manifestNamespace + "file")?
                .Element(manifestNamespace + "comClass");
            Assert.IsNotNull(comClass);
            Assert.AreEqual(CreateComClassId("ComSample", "Widget").ToString("B").ToUpperInvariant(),
                comClass!.Attribute("clsid")?.Value);

            var clsid = CreateComClassId("ComSample", "Widget");

            var probePath = Path.Combine(AppContext.BaseDirectory, "VB6.ComActivationProbe.dll");
            Assert.IsTrue(File.Exists(probePath));
            var probeStartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            probeStartInfo.ArgumentList.Add(probePath);
            probeStartInfo.ArgumentList.Add(comHostPath);
            probeStartInfo.ArgumentList.Add(clsid.ToString("D"));
            using var probe = Process.Start(probeStartInfo)
                ?? throw new InvalidOperationException("Could not start the COM activation probe.");
            var probeOutput = probe.StandardOutput.ReadToEnd();
            var probeError = probe.StandardError.ReadToEnd();
            probe.WaitForExit();
            Assert.AreEqual(0, probe.ExitCode, probeError);
            Assert.AreEqual("7|42|99|123", probeOutput.Trim());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static Guid CreateComClassId(string assemblyName, string className)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(assemblyName + "\0class\0" + className))
            .AsSpan(0, 16)
            .ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
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
            Assert.IsTrue(IsNativeWindowsAppHost(Path.Combine(outputDirectory, "Consumer.exe")));

            var startInfo = new ProcessStartInfo(Path.Combine(outputDirectory, "Consumer.exe"))
            {
                WorkingDirectory = outputDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
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
    public void Report_ReturnsNonZeroForVbgProjectReferenceOutsideTheGroup()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=Consumer.vbp\n");
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
                "Sub Main()\n    Debug.Print 1\nEnd Sub\n");

            var result = RunCli(groupPath, "--report");

            Assert.AreNotEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "VB6VBG0008");

            var outputDirectory = Path.Combine(directory, "bin");
            var emit = RunCli(groupPath, "--emit-assembly", outputDirectory);
            Assert.AreNotEqual(0, emit.ExitCode, emit.StandardError);
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "Consumer.exe")));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Report_ReturnsNonZeroForVbgProjectWithoutEntryPoint()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var groupPath = Path.Combine(directory, "LegacyGroup.vbg");
            File.WriteAllText(groupPath, "Type=Group\nProject=NoMain.vbp\n");
            File.WriteAllText(
                Path.Combine(directory, "NoMain.vbp"),
                "Type=Exe\nStartup=\"Sub Main\"\nName=NoMain\nModule=Only; Only.bas\n");
            File.WriteAllText(
                Path.Combine(directory, "Only.bas"),
                "Sub Helper()\n    Debug.Print 1\nEnd Sub\n");

            var result = RunCli(groupPath, "--report");

            Assert.AreNotEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardError, "VB6PRJ0005");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_DefaultsLegacyVbpProjectsToX86()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "DefaultBitness");
            var outputPath = Path.Combine(directory, "bin", "DefaultBitness.exe");

            // No architecture switch: a legacy VB6 project is 32-bit by definition.
            var result = RunCli(
                Path.Combine(directory, "DefaultBitness.vbp"),
                "--emit-assembly",
                outputPath);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            using var stream = File.OpenRead(Path.ChangeExtension(outputPath, ".dll"));
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
    public void EmitAssembly_LeavesWin64FalseForLegacyVbpProjectsByDefault()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "Bitness.vbp"),
                "Type=Exe\nStartup=\"Sub Main\"\nName=\"Bitness\"\nModule=Main; Bitness.bas\n");
            File.WriteAllText(Path.Combine(directory, "Bitness.bas"), """
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
            var outputPath = Path.Combine(directory, "bin", "Bitness.exe");

            // Without the x86 project default this followed the bitness of the compiler process,
            // so a legacy project saw Win64 as true on a 64-bit machine.
            var result = RunCli(
                Path.Combine(directory, "Bitness.vbp"),
                "--emit-assembly",
                outputPath);

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            var startInfo = new ProcessStartInfo(outputPath)
            {
                WorkingDirectory = Path.GetDirectoryName(outputPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the default-bitness output.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.AreEqual("32", standardOutput.Trim());
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
            using var stream = File.OpenRead(Path.ChangeExtension(outputPath, ".dll"));
            using var peReader = new PEReader(stream);
            Assert.AreEqual(Machine.I386, peReader.PEHeaders.CoffHeader.Machine);
            Assert.IsTrue(peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
            Assert.IsTrue(IsNativeWindowsAppHost(outputPath));

            var startInfo = new ProcessStartInfo(outputPath)
            {
                WorkingDirectory = Path.GetDirectoryName(outputPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start the x86 apphost.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Assert.AreEqual(0, process.ExitCode, standardError);
            Assert.IsFalse(standardError.Contains("System.Private.CoreLib", StringComparison.Ordinal));
            Assert.AreEqual("1", standardOutput.Trim());
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
            var startInfo = new ProcessStartInfo(outputPath)
            {
                WorkingDirectory = Path.GetDirectoryName(outputPath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
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
            using var stream = File.OpenRead(Path.ChangeExtension(outputPath, ".dll"));
            using var peReader = new PEReader(stream);
            Assert.AreEqual(Machine.Amd64, peReader.PEHeaders.CoffHeader.Machine);
            Assert.IsFalse(peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void EmitAssembly_AppliesVB6Sp6CompatibilityProfileAndDefaultsToX86()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Profile.bas");
            File.WriteAllText(sourcePath, "Sub Main()\n    Debug.Print 1\nEnd Sub\n");
            var outputPath = Path.Combine(directory, "bin", "Profile.dll");

            var result = RunCli(
                sourcePath,
                "--emit-assembly",
                outputPath,
                "--compatibility",
                "vb6-sp6");

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
    public void EmitAssembly_RejectsVB6Sp6CompatibilityProfileForX64()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Profile.bas");
            File.WriteAllText(sourcePath, "Sub Main()\nEnd Sub\n");
            var outputPath = Path.Combine(directory, "bin", "Profile.dll");

            var result = RunCli(
                sourcePath,
                "--emit-assembly",
                outputPath,
                "--x64",
                "--compatibility",
                "vb6-sp6");

            Assert.AreNotEqual(0, result.ExitCode);
            StringAssert.Contains(result.StandardError, "supports x86 targets only");
            Assert.IsFalse(File.Exists(outputPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void DumpIr_AnnotatesSelectedCompatibilityProfile()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Profile.bas");
            File.WriteAllText(sourcePath, "Sub Main()\nEnd Sub\n");

            var result = RunCli(
                sourcePath,
                "--dump-ir",
                "--compatibility",
                "vb6-sp6");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            StringAssert.StartsWith(result.StandardOutput, "profile VB6Sp6");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Analyze_AcceptsCompatibilityProfileOption()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Profile.bas");
            File.WriteAllText(sourcePath, "Sub Main()\nEnd Sub\n");

            var result = RunCli(sourcePath, "--compatibility", "vb6-sp6");

            Assert.AreEqual(0, result.ExitCode, result.StandardError);
            StringAssert.Contains(result.StandardOutput, "Analyzed");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void Options_ParseTheSameWayForEveryInputKind()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            var sourcePath = Path.Combine(directory, "Profile.bas");
            File.WriteAllText(sourcePath, "Sub Main()\nEnd Sub\n");

            var projectPath = Path.Combine(directory, "Profile.vbp");
            File.WriteAllText(
                projectPath,
                "Type=Exe\r\nModule=Module1; Profile.bas\r\nStartup=\"Sub Main\"\r\nName=\"Profile\"\r\n");

            // Dieselbe Option, dieselbe Stelle, beide Eingabearten -- vorher war die Grammatik
            // je Zweig eigenständig geschrieben und konnte auseinanderlaufen.
            var sourceDump = RunCli(sourcePath, "--dump-ir", "--compatibility", "vb6-sp6");
            Assert.AreEqual(0, sourceDump.ExitCode, sourceDump.StandardError);
            StringAssert.StartsWith(sourceDump.StandardOutput, "profile VB6Sp6");

            var projectDump = RunCli(projectPath, "--dump-ir", "--compatibility", "vb6-sp6");
            Assert.AreEqual(0, projectDump.ExitCode, projectDump.StandardError);
            StringAssert.StartsWith(projectDump.StandardOutput, "profile VB6Sp6");

            // Der optionale Ausgabepfad von --dump-ir gilt ebenfalls für beide.
            var dumpPath = Path.Combine(directory, "dump.ir");
            var written = RunCli(projectPath, "--dump-ir", dumpPath);
            Assert.AreEqual(0, written.ExitCode, written.StandardError);
            Assert.IsTrue(File.Exists(dumpPath));

            // Eine unbekannte Option wird beim Namen genannt, statt nur die Nutzung zu zeigen.
            var unknown = RunCli(projectPath, "--report", "--nonsense");
            Assert.AreEqual(1, unknown.ExitCode);
            StringAssert.Contains(unknown.StandardError, "--nonsense");
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

    [TestMethod]
    public void MsBuildSdk_ResolvesInputsWithThePackedTaskAndFallsBackToTheCli()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "TaskSdk");
            var packageDirectory = Path.Combine(directory, "packages");
            var packageCache = Path.Combine(directory, "nuget");
            var repositoryRoot = FindRepositoryRoot();
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-resolver-task-test",
                "-p:PackageOutputPath=" + packageDirectory);
            Assert.AreEqual(0, packResult.ExitCode, packResult.StandardError + packResult.StandardOutput);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packageDirectory}" />
                  </packageSources>
                </configuration>
                """);

            // Der Compilerpfad zeigt bewusst ins Leere. Läuft die Eingabeauflösung trotzdem durch,
            // hat sie kein Programm gestartet -- der Task hat sie im MSBuild-Prozess erledigt.
            var projectPath = Path.Combine(directory, "TaskSdk.csproj");
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-resolver-task-test" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <OutputPath>bin\Release\</OutputPath>
                    <VB6Project>{Path.Combine(directory, "TaskSdk.vbp")}</VB6Project>
                    <VB6CompilerPath>{Path.Combine(directory, "kein-compiler-hier.exe")}</VB6CompilerPath>
                  </PropertyGroup>
                </Project>
                """);

            var withTask = RunMsBuild(
                projectPath,
                restore: true,
                nugetPackages: packageCache,
                target: "ResolveVB6Project");
            Assert.AreEqual(0, withTask.ExitCode, withTask.StandardError + withTask.StandardOutput);
            var manifests = Directory.GetFiles(directory, "VB6Compile.stamp.inputs", SearchOption.AllDirectories);
            Assert.AreEqual(1, manifests.Length);
            Assert.IsTrue(File.ReadAllLines(manifests[0]).Length > 0);

            // Ohne den Task bleibt der CLI-Aufruf -- und der scheitert an genau diesem Pfad. Das
            // ist der Nachweis, dass die beiden Wege wirklich getrennt sind.
            var withoutTask = RunMsBuild(
                projectPath,
                nugetPackages: packageCache,
                target: "ResolveVB6Project",
                properties: "VB6UseResolverTask=false");
            Assert.AreNotEqual(0, withoutTask.ExitCode, withoutTask.StandardOutput);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static CliResult RunMsBuild(
        string projectPath,
        bool restore = false,
        string? nugetPackages = null,
        string target = "Build",
        params string[] properties)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("/t:" + target);
        startInfo.ArgumentList.Add("/p:Configuration=Release");
        startInfo.ArgumentList.Add("/v:minimal");
        startInfo.ArgumentList.Add("/nologo");
        if (!string.IsNullOrWhiteSpace(nugetPackages))
        {
            startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;
        }
        if (restore)
        {
            startInfo.ArgumentList.Add("/restore");
        }
        foreach (var property in properties)
        {
            startInfo.ArgumentList.Add("/p:" + property);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the MSBuild process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static CliResult RunDotNet(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the dotnet process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static CliResult RunProcess(string fileName, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{fileName}'.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, standardOutput, standardError);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "VB6.Compiler.Sdk", "Sdk", "Sdk.targets")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the VB6Compiler repository root.");
    }

    private static bool IsNativeWindowsAppHost(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        return peReader.PEHeaders.CorHeader is null;
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

    private static void WriteSdkProject(
        string projectPath,
        string directory,
        string outputPath,
        string? targetPlatform,
        string packageVersion,
        string? compatibilityProfile = null)
    {
        var platformProperty = targetPlatform is null
            ? string.Empty
            : $"\n    <VB6TargetPlatform>{targetPlatform}</VB6TargetPlatform>";
        var compatibilityProperty = compatibilityProfile is null
            ? string.Empty
            : $"\n    <VB6CompatibilityProfile>{compatibilityProfile}</VB6CompatibilityProfile>";
        File.WriteAllText(projectPath, $"""
            <Project Sdk="VB6.Compiler.Sdk/{packageVersion}" DefaultTargets="Build">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Configuration>Release</Configuration>
                <OutputPath>bin\Release\</OutputPath>
                <VB6Project>{Path.Combine(directory, "PlatformSdk.vbp")}</VB6Project>
                <VB6CompilerPath>{Path.Combine(AppContext.BaseDirectory, "vb6c.exe")}</VB6CompilerPath>
                <VB6CompilerOutput>{outputPath}</VB6CompilerOutput>{platformProperty}{compatibilityProperty}
              </PropertyGroup>
            </Project>
            """);
    }

    private static void AssertPeTarget(string path, Machine machine, bool requires32Bit)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        Assert.AreEqual(machine, peReader.PEHeaders.CoffHeader.Machine);
        Assert.AreEqual(
            requires32Bit,
            peReader.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
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

    /// <summary>
    /// The WinForms companion that ends up beside an emitted forms project has to be the one this
    /// compiler was built with. The resolver used to sort every candidate under
    /// src/VB6.Runtime.WinForms/bin by target framework alone, which demoted the copy next to the
    /// compiler - its folder is net10.0, not net10.0-windows - and let a stale Debug build from an
    /// earlier day win. A host change then looked as if it had no effect at all.
    /// </summary>
    [TestMethod]
    public void EmitAssembly_CopiesTheWinFormsCompanionOfThisBuild()
    {
        var expected = FindCompilerWinFormsCompanion();
        Assert.IsNotNull(expected, "Der Referenzstand der Companion-DLL wurde nicht gefunden.");

        var directory = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Companion.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="MainForm"
                Name="Companion"
                Form=MainForm.frm
                """);
            File.WriteAllText(Path.Combine(directory, "MainForm.frm"), """
                VERSION 5.00
                Begin VB.Form MainForm
                   Caption = "Companion"
                End
                Attribute VB_Name = "MainForm"
                Attribute VB_PredeclaredId = True

                Private Sub Form_Load()
                    Unload Me
                End Sub
                """);
            var outputDirectory = Path.Combine(directory, "bin");

            var result = RunCli(projectPath, "--emit-assembly", outputDirectory, "--x86");
            Assert.AreEqual(0, result.ExitCode, result.StandardError);

            var emitted = Path.Combine(outputDirectory, "VB6.Runtime.WinForms.dll");
            Assert.IsTrue(File.Exists(emitted), "Es wurde keine WinForms-Companion-DLL kopiert.");
            CollectionAssert.AreEqual(
                File.ReadAllBytes(expected!),
                File.ReadAllBytes(emitted),
                "Die kopierte Companion-DLL stammt nicht aus diesem Build.");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// The companion beside the compiler binaries of the configuration these tests were built in.
    /// </summary>
    private static string? FindCompilerWinFormsCompanion()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        var configuration = current.Parent?.Name;
        if (string.IsNullOrWhiteSpace(configuration))
        {
            return null;
        }

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "VB6.Compiler.Cli",
                "bin",
                configuration,
                "net10.0",
                "VB6.Runtime.WinForms.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
