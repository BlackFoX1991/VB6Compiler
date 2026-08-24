using System.Diagnostics;
using System.Security;
using VB6.Emit.Managed;

namespace VB6.Compiler;

/// <summary>
/// Produces the native .NET COM host for an already emitted VB6 library. The managed emitter owns
/// the COM metadata; the SDK owns the native loader and CLSID map format, so this bridge invokes
/// the installed SDK in an isolated temporary project instead of reimplementing that binary ABI.
/// </summary>
internal static class ManagedComHostWriter
{
    public static string Create(string managedAssemblyPath, ManagedPlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedAssemblyPath);
        if (!OperatingSystem.IsWindows())
        {
            throw new ManagedArtifactException("COM hosting is supported only on Windows.");
        }

        var assemblyPath = Path.GetFullPath(managedAssemblyPath);
        if (!File.Exists(assemblyPath))
        {
            throw new ManagedArtifactException(
                $"Cannot create a COM host because '{assemblyPath}' does not exist.");
        }

        var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var destination = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            assemblyName + ".comhost.dll");
        var root = Path.Combine(
            Path.GetTempPath(),
            "VB6Compiler-comhost-" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            var outputDirectory = Path.Combine(root, "out");
            var intermediateDirectory = Path.Combine(root, "obj");
            var projectPath = Path.Combine(root, assemblyName + ".csproj");
            var sourcePath = Path.Combine(root, "Placeholder.cs");
            File.WriteAllText(projectPath, CreateProjectFile(
                assemblyName,
                assemblyPath,
                outputDirectory,
                intermediateDirectory,
                GetRuntimeIdentifier(platform)));
            File.WriteAllText(sourcePath, "internal static class ComHostPlaceholder { }\n");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("build");
            startInfo.ArgumentList.Add(projectPath);
            startInfo.ArgumentList.Add("--configuration");
            startInfo.ArgumentList.Add("Release");
            startInfo.ArgumentList.Add("--nologo");

            using var process = Process.Start(startInfo)
                ?? throw new ManagedArtifactException("Could not start the .NET SDK for COM host generation.");
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new ManagedArtifactException(
                    "The .NET SDK could not generate the COM host." + Environment.NewLine +
                    TrimOutput(standardOutput + Environment.NewLine + standardError));
            }

            var generated = Directory
                .EnumerateFiles(root, assemblyName + ".comhost.dll", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (generated is null)
            {
                throw new ManagedArtifactException(
                    $"The .NET SDK completed without producing '{assemblyName}.comhost.dll'.");
            }

            File.Copy(generated, destination, overwrite: true);
            return destination;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
                // A locked SDK diagnostic file must not hide a successful COM host generation.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup only; the generated artifact is already usable.
            }
        }
    }

    private static string CreateProjectFile(
        string assemblyName,
        string assemblyPath,
        string outputDirectory,
        string intermediateDirectory,
        string runtimeIdentifier) =>
        $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net{{Environment.Version.Major}}.{{Environment.Version.Minor}}</TargetFramework>
            <EnableComHosting>true</EnableComHosting>
            <RuntimeIdentifier>{{Xml(runtimeIdentifier)}}</RuntimeIdentifier>
            <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
            <EnableDefaultNoneItems>false</EnableDefaultNoneItems>
            <AssemblyName>{{Xml(assemblyName)}}</AssemblyName>
            <OutputPath>{{Xml(outputDirectory + Path.DirectorySeparatorChar)}}</OutputPath>
            <IntermediateOutputPath>{{Xml(intermediateDirectory + Path.DirectorySeparatorChar)}}</IntermediateOutputPath>
            <InputAssembly>{{Xml(assemblyPath)}}</InputAssembly>
          </PropertyGroup>
          <ItemGroup>
            <Compile Include="Placeholder.cs" />
          </ItemGroup>
          <Target Name="PrepareEmittedAssembly" BeforeTargets="_GenerateClsidMap">
            <Copy SourceFiles="$(InputAssembly)" DestinationFiles="@(IntermediateAssembly)" />
          </Target>
        </Project>
        """;

    private static string GetRuntimeIdentifier(ManagedPlatform platform) =>
        platform switch
        {
            ManagedPlatform.X86 => "win-x86",
            ManagedPlatform.X64 => "win-x64",
            _ => Environment.Is64BitOperatingSystem ? "win-x64" : "win-x86"
        };

    private static string Xml(string value) => SecurityElement.Escape(value) ?? value;

    private static string TrimOutput(string output)
    {
        const int maxLength = 6000;
        return output.Length <= maxLength ? output : output[^maxLength..];
    }
}
