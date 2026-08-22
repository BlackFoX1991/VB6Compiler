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
    public static LoweringResult Lower(VBCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        var analysis = compilation.Analyze();
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new LoweringResult(analysis, null);
        }

        var sourcePath = compilation.Text.FilePath;
        var moduleName = sourcePath is null
            ? "Module1"
            : Path.GetFileNameWithoutExtension(sourcePath);
        var program = IrLowerer.Lower(new[]
        {
            new IrModuleInput(moduleName, sourcePath, analysis.SemanticModel)
        }, analysis.SemanticModel.StaticVariables);
        return new LoweringResult(analysis, program);
    }

    public static VBProjectLoweringResult Lower(VBProjectCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        // Success already accounts for the project diagnostics, and the entry-point check adds
        // to exactly that list, so nothing beyond it has to be inspected here.
        var analysis = VBProjectCompilation.ValidateEntryPoint(compilation.Analyze());
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new VBProjectLoweringResult(analysis, null);
        }

        var modules = analysis.Units
            .Where(unit => unit.Analysis.SemanticModel is not null)
            .Select(unit => new IrModuleInput(
                Path.GetFileNameWithoutExtension(unit.FilePath),
                unit.FilePath,
                unit.Analysis.SemanticModel!))
            .ToImmutableArray();
        var program = IrLowerer.Lower(
            modules,
            analysis.SemanticModel.ModuleVariables.Concat(analysis.SemanticModel.StaticVariables));
        return new VBProjectLoweringResult(analysis, program);
    }

    public static ManagedApplicationEmitResult EmitManaged(
        VBCompilation compilation,
        string outputPath,
        ManagedEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var lowering = Lower(compilation);
        if (!lowering.Success || lowering.Program is null)
        {
            return new ManagedApplicationEmitResult(lowering, null, null, null, null, null);
        }

        var documents = ImmutableArray.Create(CreateSourceDocument(
            compilation.Text.FilePath ?? "Module1.bas",
            Encoding.UTF8.GetBytes(compilation.Text.ToString())));
        return WriteArtifacts(lowering, lowering.Program, outputPath, options, documents);
    }

    public static VBProjectManagedApplicationEmitResult EmitManaged(
        VBProjectCompilation compilation,
        string outputPath,
        ManagedEmitOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var lowering = Lower(compilation);
        if (!lowering.Success || lowering.Program is null)
        {
            return new VBProjectManagedApplicationEmitResult(lowering, null, null, null, null, null);
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
            return new VBProjectManagedApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath);
        return new VBProjectManagedApplicationEmitResult(
            lowering,
            backend,
            artifacts.AssemblyPath,
            artifacts.PdbPath,
            artifacts.RuntimeAssemblyPath,
            artifacts.RuntimeConfigPath);
    }

    private static ManagedApplicationEmitResult WriteArtifacts(
        LoweringResult lowering,
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
            return new ManagedApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath);
        return new ManagedApplicationEmitResult(
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
            var pdbImage = PortablePdbEmitter.Emit(program, backend.PeImage, options, backend.SequencePoints);
            return backend with { PdbImage = pdbImage };
        }
        catch (Exception exception)
        {
            // Debug information has no partial form: either the PDB matches the emitted assembly
            // or there is none. The origin travels with the diagnostic because every failure here
            // is a defect rather than an unsupported construct.
            return new ManagedEmitResult(
                false,
                backend.Diagnostics.Add(new ManagedEmitDiagnostic(
                    "VB6E0002",
                    $"Portable PDB emission failed: {exception}")),
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

}

public sealed record LoweringResult(
    CompilationAnalysis Analysis,
    IrProgram? Program)
{
    public bool Success => Analysis.Success && Program is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Analysis.Diagnostics;
}

public sealed record VBProjectLoweringResult(
    VBProjectCompilationAnalysis Analysis,
    IrProgram? Program)
{
    /// <summary>
    /// Problems with the project itself rather than with a source file - a missing module, a
    /// startup object that is not <c>Sub Main</c>. They are reported separately because they have
    /// no source span to point at.
    /// </summary>
    public ImmutableArray<VBProjectCompilationDiagnostic> ProjectDiagnostics => Analysis.ProjectDiagnostics;

    public bool Success => Analysis.Success && Program is not null;
}

public sealed record ManagedApplicationEmitResult(
    LoweringResult Lowering,
    ManagedEmitResult? BackendResult,
    string? AssemblyPath,
    string? PdbPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => Lowering.Success && BackendResult?.Success == true && AssemblyPath is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Lowering.Diagnostics;
}

public sealed record VBProjectManagedApplicationEmitResult(
    VBProjectLoweringResult Lowering,
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
