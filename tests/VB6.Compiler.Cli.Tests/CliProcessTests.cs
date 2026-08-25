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
            var packResult = RunDotNet(
                "pack",
                Path.Combine(repositoryRoot, "src", "VB6.Compiler.Sdk", "VB6.Compiler.Sdk.csproj"),
                "-c",
                "Release",
                "--no-build",
                "--no-restore",
                "--nologo",
                "-p:PackageVersion=1.0.0-vbg-binary-input-test",
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
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-vbg-binary-input-test" DefaultTargets="Build">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Configuration>Release</Configuration>
                    <OutputPath>bin\Release\</OutputPath>
                    <VB6ProjectGroup>{groupPath}</VB6ProjectGroup>
                    <VB6CompilerPath>{compilerPath}</VB6CompilerPath>
                    <VB6CompilerGroupOutputDirectory>{outputDirectory}</VB6CompilerGroupOutputDirectory>
                  </PropertyGroup>
                </Project>
                """);

            var firstBuild = RunMsBuild(projectPath, restore: true);
            Assert.AreEqual(0, firstBuild.ExitCode, firstBuild.StandardError + firstBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "VB6.Runtime.dll")));

            var stampPath = Directory
                .GetFiles(directory, "VB6GroupCompile.stamp", SearchOption.AllDirectories)
                .Single();
            var firstStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);

            var secondBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, secondBuild.ExitCode, secondBuild.StandardError + secondBuild.StandardOutput);
            Assert.AreEqual(firstStamp, File.GetLastWriteTimeUtc(stampPath));

            Thread.Sleep(1100);
            File.AppendAllText(localControlPath, "\nchanged");
            var controlReferenceBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, controlReferenceBuild.ExitCode, controlReferenceBuild.StandardError + controlReferenceBuild.StandardOutput);
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > firstStamp);

            Thread.Sleep(1100);
            File.Delete(Path.Combine(outputDirectory, "Second.exe"));
            var recoveryBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, recoveryBuild.ExitCode, recoveryBuild.StandardError + recoveryBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "Second.exe")));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > firstStamp);

            var recoveryStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);
            File.WriteAllText(
                Path.Combine(directory, "First.bas"),
                "Sub Main()\n    Debug.Print 2\nEnd Sub\n");
            var thirdBuild = RunMsBuild(projectPath);
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
            var designerBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, designerBuild.ExitCode, designerBuild.StandardError + designerBuild.StandardOutput);
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > thirdStamp);

            var designerStamp = File.GetLastWriteTimeUtc(stampPath);
            Thread.Sleep(1100);
            File.AppendAllText(
                Path.Combine(directory, "First.vbp"),
                "ExeName32=renamed\\FirstRenamed.exe\n");
            var renameBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, renameBuild.ExitCode, renameBuild.StandardError + renameBuild.StandardOutput);
            Assert.IsTrue(File.Exists(Path.Combine(outputDirectory, "FirstRenamed.exe")));
            Assert.IsFalse(File.Exists(Path.Combine(outputDirectory, "First.exe")));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > designerStamp);

            Thread.Sleep(1100);
            File.WriteAllText(
                groupPath,
                "Type=Group\nProject=First.vbp\n");
            var removalBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, removalBuild.ExitCode, removalBuild.StandardError + removalBuild.StandardOutput);
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
    public void MsBuildSdk_TracksSingleVbpIncrementallyAndRepairsMissingOutput()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(directory);
            WriteExecutableProject(directory, "SingleSdk");
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
                "-p:PackageVersion=1.0.0-single-output-reconciliation-test2",
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
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-single-output-reconciliation-test2" DefaultTargets="Build">
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

            var firstBuild = RunMsBuild(projectPath, restore: true);
            Assert.AreEqual(0, firstBuild.ExitCode, firstBuild.StandardError + firstBuild.StandardOutput);
            Assert.IsTrue(File.Exists(outputPath));
            var stampPath = Directory.GetFiles(directory, "VB6Compile.stamp", SearchOption.AllDirectories).Single();
            var firstStamp = File.GetLastWriteTimeUtc(stampPath);

            Thread.Sleep(1100);
            var secondBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, secondBuild.ExitCode, secondBuild.StandardError + secondBuild.StandardOutput);
            Assert.AreEqual(firstStamp, File.GetLastWriteTimeUtc(stampPath));

            Thread.Sleep(1100);
            File.Delete(outputPath);
            var recoveryBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, recoveryBuild.ExitCode, recoveryBuild.StandardError + recoveryBuild.StandardOutput);
            Assert.IsTrue(File.Exists(outputPath));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > firstStamp);

            var recoveryStamp = File.GetLastWriteTimeUtc(stampPath);
            var renamedOutputPath = Path.Combine(directory, "bin", "Release", "legacy", "SingleSdkRenamed.dll");
            Thread.Sleep(1100);
            File.WriteAllText(projectPath, $"""
                <Project Sdk="VB6.Compiler.Sdk/1.0.0-single-output-reconciliation-test2" DefaultTargets="Build">
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
            var renameBuild = RunMsBuild(projectPath);
            Assert.AreEqual(0, renameBuild.ExitCode, renameBuild.StandardError + renameBuild.StandardOutput);
            Assert.IsFalse(File.Exists(outputPath));
            Assert.IsTrue(File.Exists(renamedOutputPath));
            Assert.IsTrue(File.GetLastWriteTimeUtc(stampPath) > recoveryStamp);
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

    private static CliResult RunMsBuild(string projectPath, bool restore = false)
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
        startInfo.ArgumentList.Add("/t:Build");
        startInfo.ArgumentList.Add("/p:Configuration=Release");
        startInfo.ArgumentList.Add("/v:minimal");
        startInfo.ArgumentList.Add("/nologo");
        if (restore)
        {
            startInfo.ArgumentList.Add("/restore");
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
