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
    // Test-only seam: allows the compiler tests to exercise the diagnostic path around PDB
    // emission without manufacturing an invalid PE image or depending on platform behaviour.
    internal static Func<
        IrProgram,
        byte[],
        ManagedEmitOptions,
        IReadOnlyDictionary<IrProcedure, ImmutableArray<ManagedSequencePoint>>?,
        byte[]>? PortablePdbEmitterOverride { get; set; }

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
        },
        analysis.SemanticModel.StaticVariables,
        compilation.CompilationOptions.CompatibilityProfile);
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
            analysis.SemanticModel.ModuleVariables.Concat(analysis.SemanticModel.StaticVariables),
            compilation.CompilationOptions.CompatibilityProfile);
        if (VBProjectCompilation.TryGetStartupForm(analysis, out var startupForm))
        {
            program = AddStartupFormEntryPoint(program, startupForm!);
        }

        return new VBProjectLoweringResult(analysis, program);
    }

    private static IrProgram AddStartupFormEntryPoint(IrProgram program, ClassTypeSymbol startupForm)
    {
        // Keep the generated form instance alive through Load/Show so a UI host can attach its
        // native window and control tree before the application returns to the message pump.
        var startupLocal = new IrLocal(0, "startupForm", startupForm, IsCompilerGenerated: true);
        var entryPoint = new IrProcedure(
            null,
            "Main",
            null,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray.Create(startupLocal),
            ImmutableArray.Create(
                new IrBasicBlock(
                    0,
                    "startup_form_entry",
                    ImmutableArray.Create<IrInstruction>(
                        new IrStoreInstruction(
                            new IrLocalPlace(startupLocal),
                            new IrNewClassExpression(startupForm)),
                        new IrEvaluateInstruction(new IrRuntimeCallExpression(
                            IrRuntimeMethod.InteractionLoad,
                            ImmutableArray.Create(new IrCallArgument(
                                new IrLoadExpression(new IrLocalPlace(startupLocal)))),
                            TypeSymbol.Error)),
                        new IrEvaluateInstruction(new IrRuntimeCallExpression(
                            IrRuntimeMethod.InteractionShow,
                            ImmutableArray.Create(new IrCallArgument(
                                new IrLoadExpression(new IrLocalPlace(startupLocal)))),
                            TypeSymbol.Error))),
                    new IrReturnTerminator(null))),
            IsStatic: true,
            IsCompilerGenerated: true);
        var startupModule = new IrModule(
            "__VB6Startup",
            null,
            ImmutableArray<IrGlobal>.Empty,
            ImmutableArray.Create(entryPoint));
        return program with
        {
            Modules = program.Modules.Add(startupModule),
            EntryPoint = entryPoint
        };
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

        var outputKind = VBProjectCompilation.IsLibraryProjectType(lowering.Analysis.Project.ProjectType)
            ? ManagedOutputKind.Library
            : ManagedOutputKind.Application;
        var projectOptions = options is null
            ? null
            : options with { OutputKind = outputKind };
        var actualOptions = PrepareOptions(
            projectOptions,
            assemblyName,
            CreateProjectSourceDocuments(lowering.Analysis),
            outputKind);
        var program = lowering.Program;
        if (actualOptions.OutputKind == ManagedOutputKind.Application)
        {
            program = AddCommandLineInitialization(program);
        }

        var hasStartupForm = VBProjectCompilation.TryGetStartupForm(lowering.Analysis, out _);
        if (actualOptions.EnableWinFormsHost && hasStartupForm)
        {
            program = AddWinFormsStartupLifecycle(program);
        }
        else if (actualOptions.EnableWinFormsHost)
        {
            // The optional WindowsDesktop framework belongs only to an actual Form startup. Do
            // not burden Sub Main or COM-library artifacts with a UI runtime dependency.
            actualOptions = actualOptions with { EnableWinFormsHost = false };
        }

        var backend = EmitBackend(program, actualOptions);
        if (!backend.Success || backend.PeImage is null)
        {
            return new VBProjectManagedApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        try
        {
            var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath, actualOptions);
            return new VBProjectManagedApplicationEmitResult(
                lowering,
                backend,
                artifacts.AssemblyPath,
                artifacts.PdbPath,
                artifacts.RuntimeAssemblyPath,
                artifacts.RuntimeConfigPath)
            {
                ManagedAssemblyPath = artifacts.ManagedAssemblyPath,
                WinFormsRuntimeAssemblyPath = artifacts.WinFormsRuntimeAssemblyPath,
                ComManifestPath = artifacts.ComManifestPath,
                TypeLibraryPath = artifacts.TypeLibraryPath
            };
        }
        catch (ManagedArtifactException exception)
        {
            return new VBProjectManagedApplicationEmitResult(
                lowering,
                backend with
                {
                    Success = false,
                    Diagnostics = backend.Diagnostics.Add(
                        new ManagedEmitDiagnostic("VB6E0003", exception.Message))
                },
                null,
                null,
                null,
                null);
        }
    }

    private static IrProgram AddWinFormsStartupLifecycle(IrProgram program)
    {
        var entryPoint = program.EntryPoint ??
            throw new InvalidOperationException("A Form startup project has no generated entry point.");
        if (entryPoint.Blocks.Any(block => block.Instructions
                .OfType<IrEvaluateInstruction>()
                .Select(instruction => instruction.Expression)
                .OfType<IrRuntimeCallExpression>()
                .Any(call => call.Method == IrRuntimeMethod.InteractionStartWinForms)))
        {
            return program;
        }

        var startHost = new IrEvaluateInstruction(new IrRuntimeCallExpression(
            IrRuntimeMethod.InteractionStartWinForms,
            ImmutableArray<IrCallArgument>.Empty,
            TypeSymbol.Error));
        var runMessageLoop = new IrEvaluateInstruction(new IrRuntimeCallExpression(
            IrRuntimeMethod.InteractionRunWinFormsMessageLoop,
            ImmutableArray<IrCallArgument>.Empty,
            TypeSymbol.Integer));
        var updatedEntryPoint = entryPoint with
        {
            Blocks = entryPoint.Blocks
                .Select((block, index) => index == 0
                    ? block with
                    {
                        Instructions = block.Instructions
                            .Insert(0, startHost)
                            .Add(runMessageLoop)
                    }
                    : block)
                .ToImmutableArray()
        };
        var updatedModules = program.Modules
            .Select(module => module with
            {
                Procedures = module.Procedures
                    .Select(procedure => ReferenceEquals(procedure, entryPoint)
                        ? updatedEntryPoint
                        : procedure)
                    .ToImmutableArray()
            })
            .ToImmutableArray();
        return program with
        {
            Modules = updatedModules,
            EntryPoint = updatedEntryPoint
        };
    }

    private static IrProgram AddCommandLineInitialization(IrProgram program)
    {
        var entryPoint = program.EntryPoint ??
            throw new InvalidOperationException("Managed application output requires an IR entry point.");
        if (entryPoint.Blocks.Any(block => block.Instructions
                .OfType<IrEvaluateInstruction>()
                .Select(instruction => instruction.Expression)
                .OfType<IrRuntimeCallExpression>()
                .Any(call => call.Method == IrRuntimeMethod.InteractionInitializeCommandLine)))
        {
            return program;
        }

        var initializeCommandLine = new IrEvaluateInstruction(new IrRuntimeCallExpression(
            IrRuntimeMethod.InteractionInitializeCommandLine,
            ImmutableArray<IrCallArgument>.Empty,
            TypeSymbol.Error));
        var updatedEntryPoint = entryPoint with
        {
            Blocks = entryPoint.Blocks
                .Select((block, index) => index == 0
                    ? block with { Instructions = block.Instructions.Insert(0, initializeCommandLine) }
                    : block)
                .ToImmutableArray()
        };
        var updatedModules = program.Modules
            .Select(module => module with
            {
                Procedures = module.Procedures
                    .Select(procedure => ReferenceEquals(procedure, entryPoint)
                        ? updatedEntryPoint
                        : procedure)
                    .ToImmutableArray()
            })
            .ToImmutableArray();
        return program with
        {
            Modules = updatedModules,
            EntryPoint = updatedEntryPoint
        };
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
        if (actualOptions.OutputKind == ManagedOutputKind.Application)
        {
            program = AddCommandLineInitialization(program);
        }

        var backend = EmitBackend(program, actualOptions);
        if (!backend.Success || backend.PeImage is null)
        {
            return new ManagedApplicationEmitResult(lowering, backend, null, null, null, null);
        }

        try
        {
            var artifacts = ManagedArtifactWriter.Write(backend, fullOutputPath, actualOptions);
            return new ManagedApplicationEmitResult(
                lowering,
                backend,
                artifacts.AssemblyPath,
                artifacts.PdbPath,
                artifacts.RuntimeAssemblyPath,
                artifacts.RuntimeConfigPath)
            {
                ManagedAssemblyPath = artifacts.ManagedAssemblyPath,
                WinFormsRuntimeAssemblyPath = artifacts.WinFormsRuntimeAssemblyPath,
                ComManifestPath = artifacts.ComManifestPath,
                TypeLibraryPath = artifacts.TypeLibraryPath
            };
        }
        catch (ManagedArtifactException exception)
        {
            return new ManagedApplicationEmitResult(
                lowering,
                backend with
                {
                    Success = false,
                    Diagnostics = backend.Diagnostics.Add(
                        new ManagedEmitDiagnostic("VB6E0003", exception.Message))
                },
                null,
                null,
                null,
                null);
        }
    }

    private static ManagedEmitOptions PrepareOptions(
        ManagedEmitOptions? options,
        string assemblyName,
        ImmutableArray<ManagedSourceDocument> sourceDocuments,
        ManagedOutputKind defaultOutputKind = ManagedOutputKind.Application)
    {
        var actualOptions = options ?? new ManagedEmitOptions(
            assemblyName,
            OutputKind: defaultOutputKind,
            EmitPortablePdb: true);
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
            var pdbEmitter = PortablePdbEmitterOverride ?? PortablePdbEmitter.Emit;
            var pdbImage = pdbEmitter(program, backend.PeImage, options, backend.SequencePoints);
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
    /// <summary>
    /// The actual managed assembly when <see cref="AssemblyPath"/> is a native Windows apphost.
    /// For DLL output, both paths are identical.
    /// </summary>
    public string? ManagedAssemblyPath { get; init; }

    /// <summary>The optional WinForms runtime companion copied for a Form startup application.</summary>
    public string? WinFormsRuntimeAssemblyPath { get; init; }

    /// <summary>The side-by-side activation manifest when COM hosting was requested.</summary>
    public string? ComManifestPath { get; init; }

    /// <summary>The generated .tlb when COM hosting was requested.</summary>
    public string? TypeLibraryPath { get; init; }

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
    /// <summary>The managed companion assembly when the requested output is an apphost.</summary>
    public string? ManagedAssemblyPath { get; init; }

    /// <summary>The optional WinForms runtime companion copied for a Form startup application.</summary>
    public string? WinFormsRuntimeAssemblyPath { get; init; }

    /// <summary>The side-by-side activation manifest when COM hosting was requested.</summary>
    public string? ComManifestPath { get; init; }

    /// <summary>The generated .tlb when COM hosting was requested.</summary>
    public string? TypeLibraryPath { get; init; }

    public bool Success => Lowering.Success && BackendResult?.Success == true && AssemblyPath is not null;
}

internal static class ManagedArtifactWriter
{
    public static ManagedArtifactPaths Write(
        ManagedEmitResult result,
        string outputPath,
        ManagedEmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(options);
        if (!result.Success || result.PeImage is null)
        {
            throw new InvalidOperationException("Cannot write unsuccessful managed emit result.");
        }
        if (options.EnableComHosting && options.OutputKind != ManagedOutputKind.Library)
        {
            throw new ManagedArtifactException(
                "COM hosting requires ManagedOutputKind.Library output.");
        }
        if (options.EnableComManifest && !options.EnableComHosting)
        {
            throw new ManagedArtifactException(
                "COM manifests require COM hosting to be enabled.");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(outputDirectory);
        var managedAssemblyPath = ManagedAppHostWriter.ShouldCreateAppHost(fullOutputPath, options)
            ? Path.ChangeExtension(fullOutputPath, ".dll")
            : fullOutputPath;
        File.WriteAllBytes(managedAssemblyPath, result.PeImage);

        string? pdbPath = null;
        if (result.PdbImage is not null)
        {
            pdbPath = Path.ChangeExtension(managedAssemblyPath, ".pdb");
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

        string? winFormsRuntimeOutputPath = null;
        if (options.EnableWinFormsHost)
        {
            var winFormsSourcePath = FindWinFormsRuntimeAssembly();
            if (winFormsSourcePath is null)
            {
                throw new ManagedArtifactException(
                    "Form startup emission requires VB6.Runtime.WinForms.dll. Build or install the optional WinForms runtime companion.");
            }

            winFormsRuntimeOutputPath = Path.Combine(outputDirectory, "VB6.Runtime.WinForms.dll");
            if (!string.Equals(
                    Path.GetFullPath(winFormsSourcePath),
                    Path.GetFullPath(winFormsRuntimeOutputPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(winFormsSourcePath, winFormsRuntimeOutputPath, overwrite: true);
            }
        }

        var runtimeConfigPath = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(managedAssemblyPath) + ".runtimeconfig.json");
        File.WriteAllText(runtimeConfigPath, CreateRuntimeConfig(options.EnableWinFormsHost));

        if (!string.Equals(managedAssemblyPath, fullOutputPath, StringComparison.OrdinalIgnoreCase) &&
            !ManagedAppHostWriter.TryCreate(managedAssemblyPath, fullOutputPath, options.Platform))
        {
            // A managed DLL renamed to .exe is not a Windows .NET executable and can produce the
            // misleading System.Private.CoreLib load error. Fail the emission instead of leaving
            // behind an artifact that looks executable but cannot be launched directly.
            if (File.Exists(fullOutputPath))
            {
                File.Delete(fullOutputPath);
            }

            throw new ManagedArtifactException(
                $"Could not create a native .NET apphost for '{fullOutputPath}'. " +
                "Install the matching Microsoft.NETCore.App.Host.win-x86/win-x64 pack or emit a .dll output.");
        }

        string? comManifestPath = null;
        string? typeLibraryPath = null;
        if (options.EnableComHosting)
        {
            var comHostPath = ManagedComHostWriter.Create(managedAssemblyPath, options.Platform);

            // A late-bound client needs no type library; an early-bound one -- VB6, VBA, C++ --
            // cannot see the classes without it, so it is written whenever COM hosting is on.
            // COM hosting itself is already Windows-only, so this cannot be reached elsewhere.
            if (OperatingSystem.IsWindows())
            {
                typeLibraryPath = ManagedTypeLibraryWriter.Create(managedAssemblyPath, options.Platform);
            }
            if (options.EnableComManifest)
            {
                comManifestPath = ManagedComManifestWriter.Create(
                    managedAssemblyPath,
                    comHostPath,
                    options.Platform);
            }
        }

        return new ManagedArtifactPaths(
            fullOutputPath,
            managedAssemblyPath,
            pdbPath,
            runtimeOutputPath,
            runtimeConfigPath,
            comManifestPath,
            winFormsRuntimeOutputPath,
            typeLibraryPath);
    }

    /// <summary>
    /// Locates the WinForms runtime companion that is copied next to an emitted forms project.
    /// The assembly beside the loaded VB6.Runtime wins, because those two have to match; only if
    /// there is none does the search fall back to the repository build tree.
    ///
    /// That fallback used to take whichever file the directory walk produced first, which put a
    /// stale Debug build ahead of a fresh Release one. A host change then appeared to have no
    /// effect even though it was compiled, so the ordering is now explicit: the configuration of
    /// the loaded runtime first, then the target framework, then the most recent build.
    /// </summary>
    private static string? FindWinFormsRuntimeAssembly()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(VBConversions).Assembly.Location);
        foreach (var directory in new[] { runtimeDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var beside = Path.Combine(directory, "VB6.Runtime.WinForms.dll");
            if (File.Exists(beside))
            {
                return beside;
            }
        }

        // The build configuration the compiler itself was loaded from, e.g. "Release" in
        // src/VB6.Compiler.Cli/bin/Release/net10.0.
        var configuration = Path.GetFileName(Path.GetDirectoryName(runtimeDirectory));
        var candidates = new List<string>();
        foreach (var root in EnumerateAncestors(runtimeDirectory).Concat(EnumerateAncestors(Environment.CurrentDirectory)))
        {
            var binDirectory = Path.Combine(root, "src", "VB6.Runtime.WinForms", "bin");
            if (!Directory.Exists(binDirectory))
            {
                continue;
            }

            try
            {
                candidates.AddRange(Directory.EnumerateFiles(
                    binDirectory,
                    "VB6.Runtime.WinForms.dll",
                    SearchOption.AllDirectories));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return candidates
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => !string.IsNullOrEmpty(configuration) && string.Equals(
                Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))),
                configuration,
                StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(path => string.Equals(
                Path.GetFileName(Path.GetDirectoryName(path)),
                "net10.0-windows",
                StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        static IEnumerable<string> EnumerateAncestors(string? start)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                yield break;
            }

            var current = new DirectoryInfo(Path.GetFullPath(start));
            while (current is not null)
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }

    private static string CreateRuntimeConfig(bool enableWinFormsHost)
    {
        var targetFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
        var frameworkVersion = $"{Environment.Version.Major}.{Environment.Version.Minor}.0";
        var frameworkSection = enableWinFormsHost
            ? $$"""
                "frameworks": [
                  {
                    "name": "Microsoft.NETCore.App",
                    "version": "{{frameworkVersion}}"
                  },
                  {
                    "name": "Microsoft.WindowsDesktop.App",
                    "version": "{{frameworkVersion}}"
                  }
                ]
                """
            : $$"""
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "{{frameworkVersion}}"
                }
                """;
        return $$"""
            {
              "runtimeOptions": {
                "tfm": "{{targetFramework}}",
                {{frameworkSection}}
              }
            }
            """;
    }
}

internal sealed record ManagedArtifactPaths(
    string AssemblyPath,
    string ManagedAssemblyPath,
    string? PdbPath,
    string RuntimeAssemblyPath,
    string RuntimeConfigPath,
    string? ComManifestPath,
    string? WinFormsRuntimeAssemblyPath,
    string? TypeLibraryPath = null);

internal sealed class ManagedArtifactException : Exception
{
    public ManagedArtifactException(string message)
        : base(message)
    {
    }
}
