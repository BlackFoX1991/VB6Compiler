using VB6.Compiler;
using VB6.Emit.Managed;
using VB6.Emit.Llvm;
using VB6.IR;
using VB6.ProjectSystem;

const string usage =
    "Usage: vb6c <source-file|project.vbp> [--emit-assembly <output-file> [--x86|--x64|--anycpu] [--com-host] | --emit-llvm <output-file> [--x86|--x64] | --dump-ir [output-file]]\n" +
    "       vb6c <project.vbp> --report\n" +
    "       vb6c <project.vbg> --report\n" +
    "       vb6c <project.vbg> --emit-assembly <output-directory> [--x86|--x64|--anycpu] [--com-host]";

if (args.Length == 0)
{
    Console.WriteLine("VB6Compiler");
    Console.WriteLine(usage);
    return 0;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Input file not found: {path}");
    return 1;
}

if (string.Equals(Path.GetExtension(path), ".vbp", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length == 1)
    {
        var loadResult = new VBProjectLoader().Load(path);
        foreach (var diagnostic in loadResult.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Code} line {diagnostic.Line}: {diagnostic.Message}");
        }

        var project = loadResult.Project;
        Console.WriteLine($"Loaded VB6 project: {project.Name ?? Path.GetFileNameWithoutExtension(project.FilePath)}");
        Console.WriteLine($"Type: {project.ProjectType ?? "Unknown"}");
        Console.WriteLine($"Startup: {project.StartupObject ?? "Not specified"}");
        Console.WriteLine(
            $"Items: {project.Items.Length} " +
            $"(modules: {project.Modules.Count()}, classes: {project.Classes.Count()}, forms: {project.Forms.Count()})");
        Console.WriteLine($"References: {project.References.Length}");
        foreach (var reference in project.References)
        {
            var metadata = reference.Metadata;
            var target = metadata.FilePath is null
                ? metadata.DisplayName ?? "unresolved"
                : metadata.GetFullPath(project.ProjectDirectory)!;
            Console.WriteLine($"  Reference [{metadata.Kind}]: {target}");
        }
        Console.WriteLine($"Components: {project.Objects.Length}");
        foreach (var component in project.Objects)
        {
            var metadata = component.Metadata;
            var target = metadata.FilePath is null
                ? metadata.DisplayName ?? "unresolved"
                : metadata.GetFullPath(project.ProjectDirectory)!;
            Console.WriteLine($"  Object: {target}");
        }
        return loadResult.Success ? 0 : 1;
    }

    var projectPlatform = ManagedPlatform.AnyCpu;
    var projectComHost = false;
    VBCompilationOptions? projectCompilationOptions = null;
    if (args.Length is >= 3 and <= 5 &&
        string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryParseManagedArguments(args, out projectPlatform, out projectComHost))
        {
            return 1;
        }

        projectCompilationOptions = CreateCompilationOptions(projectPlatform);
    }

    var projectCompilation = VBProjectCompilation.Create(path, projectCompilationOptions);

    if (args.Length == 2 && string.Equals(args[1], "--report", StringComparison.OrdinalIgnoreCase))
    {
        var projectAnalysis = projectCompilation.Analyze();
        var report = VBProjectParityReport.Create(projectAnalysis);
        Console.Write(report.Render());
        PrintProjectDiagnostics(projectAnalysis);
        return projectAnalysis.Success ? 0 : 1;
    }

    if (args.Length is 2 or 3 && string.Equals(args[1], "--dump-ir", StringComparison.OrdinalIgnoreCase))
    {
        var lowering = projectCompilation.Lower();
        PrintProjectDiagnostics(lowering.Analysis);

        if (!lowering.Success || lowering.Program is null)
        {
            return 1;
        }

        return WriteIr(IrDumper.Dump(lowering.Program), args.Length == 3 ? args[2] : null);
    }

    if (args.Length is 3 or 4 && string.Equals(args[1], "--emit-llvm", StringComparison.OrdinalIgnoreCase))
    {
        var lowering = projectCompilation.Lower();
        PrintProjectDiagnostics(lowering.Analysis);

        if (!lowering.Success || lowering.Program is null)
        {
            return 1;
        }

        return EmitLlvm(lowering.Program, args[2], args.Length == 4 ? args[3] : null);
    }

    if (args.Length is >= 3 and <= 5 && string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
    {
        var emitOptions = CreateManagedEmitOptions(args[2], projectPlatform, projectComHost);
        var emitResult = projectCompilation.EmitManagedApplication(args[2], emitOptions);
        PrintProjectDiagnostics(emitResult.Lowering.Analysis);
        PrintBackendDiagnostics(emitResult.BackendResult);

        if (!emitResult.Success)
        {
            return 1;
        }

        Console.WriteLine($"Generated managed project assembly: {emitResult.AssemblyPath}");
        PrintDebugInformation(emitResult.PdbPath);
        Console.WriteLine($"Runtime support: {emitResult.RuntimeAssemblyPath}");
        Console.WriteLine($"Runtime config: {emitResult.RuntimeConfigPath}");
        return 0;
    }

    Console.Error.WriteLine(usage);
    return 1;
}

if (string.Equals(Path.GetExtension(path), ".vbg", StringComparison.OrdinalIgnoreCase))
{
    return HandleProjectGroup(path, args);
}

var sourcePlatform = ManagedPlatform.AnyCpu;
var sourceComHost = false;
VBCompilationOptions? sourceCompilationOptions = null;
if (args.Length is >= 3 and <= 5 &&
    string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
{
    if (!TryParseManagedArguments(args, out sourcePlatform, out sourceComHost))
    {
        return 1;
    }

    sourceCompilationOptions = CreateCompilationOptions(sourcePlatform);
}

var compilation = VBCompilation.Create(
    VB6TextFile.ReadAllText(path),
    path,
    sourceCompilationOptions);

if (args.Length is 2 or 3 && string.Equals(args[1], "--dump-ir", StringComparison.OrdinalIgnoreCase))
{
    var lowering = compilation.Lower();
    foreach (var diagnostic in lowering.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    if (!lowering.Success || lowering.Program is null)
    {
        return 1;
    }

    return WriteIr(IrDumper.Dump(lowering.Program), args.Length == 3 ? args[2] : null);
}

if (args.Length is 3 or 4 && string.Equals(args[1], "--emit-llvm", StringComparison.OrdinalIgnoreCase))
{
    var lowering = compilation.Lower();
    foreach (var diagnostic in lowering.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    if (!lowering.Success || lowering.Program is null)
    {
        return 1;
    }

    return EmitLlvm(lowering.Program, args[2], args.Length == 4 ? args[3] : null);
}

if (args.Length is >= 3 and <= 5 && string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
{
    var emitOptions = CreateManagedEmitOptions(args[2], sourcePlatform, sourceComHost);
    var emitResult = compilation.EmitManagedApplication(args[2], emitOptions);
    foreach (var diagnostic in emitResult.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    PrintBackendDiagnostics(emitResult.BackendResult);

    if (!emitResult.Success)
    {
        return 1;
    }

    Console.WriteLine($"Generated managed assembly: {emitResult.AssemblyPath}");
    PrintDebugInformation(emitResult.PdbPath);
    Console.WriteLine($"Runtime support: {emitResult.RuntimeAssemblyPath}");
    Console.WriteLine($"Runtime config: {emitResult.RuntimeConfigPath}");
    return 0;
}

if (args.Length != 1)
{
    Console.Error.WriteLine(usage);
    return 1;
}

var analysis = compilation.Analyze();
foreach (var diagnostic in analysis.Diagnostics)
{
    Console.Error.WriteLine(diagnostic);
}

if (analysis.SemanticModel is not null)
{
    Console.WriteLine(
        $"Analyzed {path} ({compilation.Text.Length} chars, {compilation.Text.Lines.Length} lines, " +
        $"{analysis.ParseResult.Root.Members.Length} members, {analysis.SemanticModel.Procedures.Length} procedures)");
}

return analysis.Success ? 0 : 1;

static int WriteIr(string dump, string? outputPath)
{
    if (outputPath is null)
    {
        Console.Write(dump);
        return 0;
    }

    File.WriteAllText(outputPath, dump);
    Console.WriteLine($"Generated IR dump: {outputPath}");
    return 0;
}

static int HandleProjectGroup(string path, string[] args)
{
    var groupPlatform = ManagedPlatform.AnyCpu;
    var groupComHost = false;
    VBCompilationOptions? groupCompilationOptions = null;
    if (args.Length is >= 3 and <= 5 &&
        string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
    {
        if (!TryParseManagedArguments(args, out groupPlatform, out groupComHost))
        {
            return 1;
        }

        groupCompilationOptions = CreateCompilationOptions(groupPlatform);
    }

    var compilation = VBProjectGroupCompilation.Create(path, groupCompilationOptions);
    if (args.Length == 1)
    {
        var analysis = compilation.Analyze();
        PrintProjectGroupSummary(analysis);
        PrintProjectGroupDiagnostics(analysis);
        return analysis.Success ? 0 : 1;
    }

    if (args.Length == 2 && string.Equals(args[1], "--report", StringComparison.OrdinalIgnoreCase))
    {
        var analysis = compilation.Analyze();
        PrintProjectGroupSummary(analysis);
        PrintProjectGroupDiagnostics(analysis);
        foreach (var project in analysis.Projects.Where(project => project.Compilation is not null))
        {
            Console.WriteLine($"--- {project.FullPath} ---");
            Console.Write(VBProjectParityReport.Create(project.Compilation!).Render());
        }

        return analysis.Success ? 0 : 1;
    }

    if (args.Length is >= 3 and <= 5 && string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
    {
        var emitOptions = CreateManagedEmitOptions(args[2], groupPlatform, groupComHost);
        var result = compilation.EmitManagedApplications(args[2], emitOptions);
        PrintProjectGroupSummary(result.Analysis);
        PrintProjectGroupDiagnostics(result.Analysis);
        foreach (var project in result.Projects)
        {
            PrintProjectDiagnostics(project.Emit.Lowering.Analysis);
            PrintBackendDiagnostics(project.Emit.BackendResult);
            if (project.Success)
            {
                Console.WriteLine($"Generated managed project assembly: {project.OutputPath}");
                PrintDebugInformation(project.Emit.PdbPath);
                Console.WriteLine($"Runtime support: {project.Emit.RuntimeAssemblyPath}");
                Console.WriteLine($"Runtime config: {project.Emit.RuntimeConfigPath}");
            }
        }

        return result.Success ? 0 : 1;
    }

    Console.Error.WriteLine(usage);
    return 1;
}

static void PrintProjectGroupSummary(VBProjectGroupAnalysis analysis)
{
    Console.WriteLine($"Loaded VB6 project group: {Path.GetFileNameWithoutExtension(analysis.Group.FilePath)}");
    Console.WriteLine($"Type: {analysis.Group.GroupType ?? "Unknown"}");
    Console.WriteLine($"Startup project: {analysis.Group.StartupProject ?? "Not specified"}");
    Console.WriteLine($"Projects: {analysis.Group.Projects.Length}");
}

static void PrintProjectGroupDiagnostics(VBProjectGroupAnalysis analysis)
{
    foreach (var diagnostic in analysis.GroupDiagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    foreach (var project in analysis.Projects)
    {
        foreach (var diagnostic in project.Diagnostics)
        {
            Console.Error.WriteLine(diagnostic);
        }

        if (project.Compilation is not null)
        {
            PrintProjectDiagnostics(project.Compilation);
        }
    }
}

static int EmitLlvm(IrProgram program, string outputPath, string? architectureArgument)
{
    var architecture = architectureArgument?.ToLowerInvariant() switch
    {
        null or "--x64" => LlvmArchitecture.X64,
        "--x86" => LlvmArchitecture.X86,
        _ => (LlvmArchitecture?)null
    };

    if (architecture is null)
    {
        Console.Error.WriteLine($"Unknown LLVM architecture '{architectureArgument}'. Use --x86 or --x64.");
        return 1;
    }

    var moduleName = Path.GetFileNameWithoutExtension(outputPath);
    var result = new LlvmEmitter().Emit(
        program,
        new LlvmEmitOptions(architecture.Value, string.IsNullOrWhiteSpace(moduleName) ? "VB6Program" : moduleName));
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }

    if (!result.Success)
    {
        return 1;
    }

    File.WriteAllText(outputPath, result.ModuleText);
    Console.WriteLine($"Generated LLVM module: {outputPath}");
    Console.WriteLine($"LLVM target: {architecture.Value}");
    return 0;
}

static bool TryParseManagedArguments(
    string[] arguments,
    out ManagedPlatform platform,
    out bool enableComHosting)
{
    platform = ManagedPlatform.AnyCpu;
    enableComHosting = false;
    ManagedPlatform? selectedPlatform = null;
    foreach (var argument in arguments.Skip(3))
    {
        if (string.Equals(argument, "--com-host", StringComparison.OrdinalIgnoreCase))
        {
            enableComHosting = true;
            continue;
        }

        if (argument is "--x86" or "--x64" or "--anycpu")
        {
            if (!TryParseManagedPlatform(argument, out var parsedPlatform))
            {
                return false;
            }

            if (selectedPlatform is not null)
            {
                Console.Error.WriteLine("Managed architecture was specified more than once.");
                return false;
            }

            selectedPlatform = parsedPlatform;
            platform = parsedPlatform;
            continue;
        }

        Console.Error.WriteLine(
            $"Unknown managed option '{argument}'. Use --x86, --x64, --anycpu or --com-host.");
        return false;
    }

    return true;
}

static bool TryParseManagedPlatform(string? argument, out ManagedPlatform platform)
{
    platform = ManagedPlatform.AnyCpu;
    if (argument is null)
    {
        return true;
    }

    platform = argument.ToLowerInvariant() switch
    {
        "--x86" => ManagedPlatform.X86,
        "--x64" => ManagedPlatform.X64,
        "--anycpu" => ManagedPlatform.AnyCpu,
        _ => (ManagedPlatform)(-1)
    };

    if ((int)platform >= 0)
    {
        return true;
    }

    Console.Error.WriteLine($"Unknown managed architecture '{argument}'. Use --x86, --x64 or --anycpu.");
    return false;
}

static ManagedEmitOptions CreateManagedEmitOptions(
    string outputPath,
    ManagedPlatform platform,
    bool enableComHosting = false) =>
    new(
        Path.GetFileNameWithoutExtension(Path.GetFullPath(outputPath)),
        Platform: platform)
    {
        EnableComHosting = enableComHosting
    };

static VBCompilationOptions? CreateCompilationOptions(ManagedPlatform platform) => platform switch
{
    ManagedPlatform.X86 => new VBCompilationOptions(TargetIs64Bit: false),
    ManagedPlatform.X64 => new VBCompilationOptions(TargetIs64Bit: true),
    _ => null
};

static void PrintDebugInformation(string? pdbPath)
{
    if (pdbPath is not null)
    {
        Console.WriteLine($"Debug information: {pdbPath}");
    }
}

static void PrintProjectDiagnostics(VBProjectCompilationAnalysis analysis)
{
    foreach (var diagnostic in analysis.ProjectDiagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    foreach (var diagnostic in analysis.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }
}

static void PrintBackendDiagnostics(VB6.Emit.Managed.ManagedEmitResult? backendResult)
{
    if (backendResult is null)
    {
        return;
    }

    foreach (var diagnostic in backendResult.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
    }
}
