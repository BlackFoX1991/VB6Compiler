using VB6.CodeGen.CSharp;
using VB6.Runtime;

namespace VB6.Compiler;

internal static class ManagedApplicationWriter
{
    public static ManagedApplicationArtifacts Emit(string source, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(outputDirectory);

        var assemblyName = Path.GetFileNameWithoutExtension(fullOutputPath);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = "VB6Program";
        }

        AssemblyEmitResult backendResult;
        using (var peStream = File.Create(fullOutputPath))
        {
            backendResult = new CSharpAssemblyEmitter().Emit(source, assemblyName, peStream);
        }

        if (!backendResult.Success)
        {
            File.Delete(fullOutputPath);
            return new ManagedApplicationArtifacts(backendResult, null, null, null);
        }

        var runtimeSourcePath = typeof(VBConversions).Assembly.Location;
        var runtimeOutputPath = Path.Combine(outputDirectory, "VB6.Runtime.dll");
        if (!string.Equals(
                Path.GetFullPath(runtimeSourcePath),
                Path.GetFullPath(runtimeOutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(runtimeSourcePath, runtimeOutputPath, overwrite: true);
        }

        var runtimeConfigPath = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(fullOutputPath) + ".runtimeconfig.json");
        File.WriteAllText(runtimeConfigPath, CreateRuntimeConfig());

        return new ManagedApplicationArtifacts(
            backendResult,
            fullOutputPath,
            runtimeOutputPath,
            runtimeConfigPath);
    }

    private static string CreateRuntimeConfig()
    {
        var targetFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
        var frameworkVersion = $"{Environment.Version.Major}.{Environment.Version.Minor}.0";

        return $$"""
            {
              "runtimeOptions": {
                "tfm": "{{targetFramework}}",
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "{{frameworkVersion}}"
                }
              }
            }
            """;
    }
}

internal sealed record ManagedApplicationArtifacts(
    AssemblyEmitResult BackendResult,
    string? AssemblyPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => BackendResult.Success && AssemblyPath is not null;
}
