using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using VB6.Emit.Managed;
using VB6.IR;
using VB6.Runtime;
using VB6.Semantics;
using VB6.Syntax.Diagnostics;

namespace VB6.Compiler;

/// <summary>
/// Transitional orchestration entry point for the direct managed backend. Keeping it separate from
/// the public compilation types lets execution tests move to IR/CIL one slice at a time; after the
/// parity cutover these methods become VBCompilation.Lower/EmitManaged and the old C# path is removed.
/// </summary>
public static class DirectManagedCompilation
{
    public static DirectManagedLoweringResult Lower(VBCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        var analysis = compilation.Analyze();
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new DirectManagedLoweringResult(analysis, null);
        }

        var sourcePath = compilation.Text.FilePath;
        var moduleName = sourcePath is null
            ? "Module1"
            : Path.GetFileNameWithoutExtension(sourcePath);
        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput(moduleName, sourcePath, analysis.SemanticModel)
        });
        return new DirectManagedLoweringResult(analysis, program);
    }

    public static DirectManagedProjectLoweringResult Lower(VBProjectCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        var analysis = compilation.Analyze();
        var projectDiagnostics = ValidateProjectEntryPoint(analysis);
        if (!analysis.Success ||
            projectDiagnostics.Any(diagnostic => diagnostic.Code is "VB6PRJ0004" or "VB6PRJ0005") ||
            analysis.SemanticModel is null)
        {
            return new DirectManagedProjectLoweringResult(analysis, projectDiagnostics, null);
        }

        var modules = analysis.Units
            .Where(unit => unit.Analysis.SemanticModel is not null)
            .Select(unit => new IrModuleInput(
                Path.GetFileNameWithoutExtension(unit.FilePath),
                unit.FilePath,
                unit.Analysis.SemanticModel!))
            .ToImmutableArray();
        var program = IrLowerer.Lower(modules, analysis.SemanticModel.ModuleVariables);
        return new DirectManagedProjectLoweringResult(analysis, projectDiagnostics, program);
    }

    public static DirectManagedApplicationEmitResult EmitManaged(
        VBCompilation compilation,
        string outputPath,
        ManagedEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var lowering = Lower(compilation);
        if (!lowering.Success || lowering.Program is null)
        {
            return new DirectManagedApplicationEmitResult(lowering, null, null, null, null, null);
        }

        var documents = ImmutableArray.Create(CreateSourceDocument(
            compilation.Text.FilePath ?? "Module1.bas",
            Encoding.UTF8.GetBytes(compilation.Text.ToString())));
        return WriteArtifacts(lowering, lowering.Program, outputPath, options, documents);
    }

    public static DirectManagedProjectApplicationEmitResult EmitManaged(
        VBProjectCompilation compilation,
        string outputPath,
        ManagedEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var lowering = Lower(compilation);
        if (!lowering.Success || lowering.Program is null)
        {
            return new DirectManagedProjectApplicationEmitResult(lowering, null, null, null, null, null);
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var assemblyName = Path.GetFileNameWithoutExtension(fullOutputPath);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = "VB6Program";
        }

        var actualOptions = PrepareOptions(
            options,
            assemblyName,
            CreateProjectSourceDocuments(lowering.Analysis));
        var backend = EmitBackend(lowering.Program, actualOptions);
        if (!backend.Success || backend.PeImage is null)
        {
            return new DirectManagedProjectApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath);
        return new DirectManagedProjectApplicationEmitResult(
            lowering,
            backend,
            artifacts.AssemblyPath,
            artifacts.PdbPath,
            artifacts.RuntimeAssemblyPath,
            artifacts.RuntimeConfigPath);
    }

    private static DirectManagedApplicationEmitResult WriteArtifacts(
        DirectManagedLoweringResult lowering,
        IrProgram program,
        string outputPath,
        ManagedEmitOptions? options,
        ImmutableArray<ManagedSourceDocument> sourceDocuments)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var assemblyName = Path.GetFileNameWithoutExtension(fullOutputPath);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = "VB6Program";
        }

        var actualOptions = PrepareOptions(options, assemblyName, sourceDocuments);
        var backend = EmitBackend(program, actualOptions);
        if (!backend.Success || backend.PeImage is null)
        {
            return new DirectManagedApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath);
        return new DirectManagedApplicationEmitResult(
            lowering,
            backend,
            artifacts.AssemblyPath,
            artifacts.PdbPath,
            artifacts.RuntimeAssemblyPath,
            artifacts.RuntimeConfigPath);
    }

    private static ManagedEmitOptions PrepareOptions(
        ManagedEmitOptions? options,
        string assemblyName,
        ImmutableArray<ManagedSourceDocument> sourceDocuments)
    {
        var actualOptions = options ?? new ManagedEmitOptions(assemblyName, EmitPortablePdb: true);
        if (!string.Equals(actualOptions.AssemblyName, assemblyName, StringComparison.Ordinal))
        {
            actualOptions = actualOptions with { AssemblyName = assemblyName };
        }

        if (actualOptions.SourceDocuments.IsDefaultOrEmpty)
        {
            actualOptions = actualOptions with { SourceDocuments = sourceDocuments };
        }

        return actualOptions;
    }

    private static ManagedEmitResult EmitBackend(IrProgram program, ManagedEmitOptions options)
    {
        var backend = new ManagedEmitter().Emit(program, options);
        if (!backend.Success || backend.PeImage is null || !options.EmitPortablePdb)
        {
            return backend;
        }

        try
        {
            var pdbImage = PortablePdbEmitter.Emit(program, backend.PeImage, options);
            return backend with { PdbImage = pdbImage };
        }
        catch (Exception exception)
        {
            return new ManagedEmitResult(
                false,
                backend.Diagnostics.Add(new ManagedEmitDiagnostic("VB6E0002", exception.Message)),
                backend.PeImage,
                null);
        }
    }

    private static ManagedSourceDocument CreateSourceDocument(string filePath, byte[] bytes) =>
        new(
            filePath,
            ImmutableArray.CreateRange(SHA256.HashData(bytes)));

    private static ImmutableArray<ManagedSourceDocument> CreateProjectSourceDocuments(
        VBProjectCompilationAnalysis analysis)
    {
        var documents = ImmutableArray.CreateBuilder<ManagedSourceDocument>();
        foreach (var unit in analysis.Units)
        {
            if (!File.Exists(unit.FilePath))
            {
                continue;
            }

            documents.Add(CreateSourceDocument(unit.FilePath, File.ReadAllBytes(unit.FilePath)));
        }

        return documents.ToImmutable();
    }

    private static ImmutableArray<VBProjectCompilationDiagnostic> ValidateProjectEntryPoint(
        VBProjectCompilationAnalysis analysis)
    {
        var diagnostics = analysis.ProjectDiagnostics.ToBuilder();
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return diagnostics.ToImmutable();
        }

        var startupObject = analysis.Project.StartupObject;
        if (!string.IsNullOrWhiteSpace(startupObject) &&
            !string.Equals(startupObject, "Sub Main", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0004",
                $"Startup object '{startupObject}' is not supported by project emission yet. Only 'Sub Main' is supported.",
                analysis.Project.FilePath));
            return diagnostics.ToImmutable();
        }

        var mainCount = analysis.SemanticModel.Procedures.Count(procedure =>
            !procedure.Symbol.IsFunction &&
            string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));
        if (mainCount != 1)
        {
            diagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0005",
                mainCount == 0
                    ? "Project emission requires a Sub Main entry point."
                    : "Project emission found more than one Sub Main entry point.",
                analysis.Project.FilePath));
        }

        return diagnostics.ToImmutable();
    }
}

public sealed record DirectManagedLoweringResult(
    CompilationAnalysis Analysis,
    IrProgram? Program)
{
    public bool Success => Analysis.Success && Program is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Analysis.Diagnostics;
}

public sealed record DirectManagedProjectLoweringResult(
    VBProjectCompilationAnalysis Analysis,
    ImmutableArray<VBProjectCompilationDiagnostic> ProjectDiagnostics,
    IrProgram? Program)
{
    public bool Success =>
        Analysis.Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error) &&
        ProjectDiagnostics.Length == 0 &&
        Program is not null;
}

public sealed record DirectManagedApplicationEmitResult(
    DirectManagedLoweringResult Lowering,
    ManagedEmitResult? BackendResult,
    string? AssemblyPath,
    string? PdbPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => Lowering.Success && BackendResult?.Success == true && AssemblyPath is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Lowering.Diagnostics;
}

public sealed record DirectManagedProjectApplicationEmitResult(
    DirectManagedProjectLoweringResult Lowering,
    ManagedEmitResult? BackendResult,
    string? AssemblyPath,
    string? PdbPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => Lowering.Success && BackendResult?.Success == true && AssemblyPath is not null;
}

internal static class ManagedArtifactWriter
{
    public static ManagedArtifactPaths Write(ManagedEmitResult result, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (!result.Success || result.PeImage is null)
        {
            throw new InvalidOperationException("Cannot write unsuccessful managed emit result.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllBytes(fullOutputPath, result.PeImage);

        string? pdbPath = null;
        if (result.PdbImage is not null)
        {
            pdbPath = Path.ChangeExtension(fullOutputPath, ".pdb");
            File.WriteAllBytes(pdbPath, result.PdbImage);
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

        return new ManagedArtifactPaths(fullOutputPath, pdbPath, runtimeOutputPath, runtimeConfigPath);
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

internal sealed record ManagedArtifactPaths(
    string AssemblyPath,
    string? PdbPath,
    string RuntimeAssemblyPath,
    string RuntimeConfigPath);
