using VB6.Compiler;
using VB6.ProjectSystem;

const string usage =
    "Usage: vb6c <source-file|project.vbp> [--emit-csharp <output-file> | --emit-assembly <output-file>]\n" +
    "       vb6c <project.vbp> --report";

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
        Console.WriteLine($"Components: {project.Objects.Length}");
        return loadResult.Success ? 0 : 1;
    }

    var projectCompilation = VBProjectCompilation.Create(path);

    if (args.Length == 2 && string.Equals(args[1], "--report", StringComparison.OrdinalIgnoreCase))
    {
        var report = VBProjectParityReport.Create(projectCompilation.Analyze());
        Console.Write(report.Render());
        return 0;
    }

    if (args.Length == 3 && string.Equals(args[1], "--emit-csharp", StringComparison.OrdinalIgnoreCase))
    {
        var generation = projectCompilation.GenerateCSharp();
        PrintProjectDiagnostics(generation.Analysis);

        if (!generation.Success || generation.Source is null)
        {
            return 1;
        }

        File.WriteAllText(args[2], generation.Source);
        Console.WriteLine($"Generated project C# source: {args[2]}");
        return 0;
    }

    if (args.Length == 3 && string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
    {
        var emitResult = projectCompilation.EmitManagedApplication(args[2]);
        PrintProjectDiagnostics(emitResult.Generation.Analysis);
        PrintBackendDiagnostics(emitResult.BackendResult);

        if (!emitResult.Success)
        {
            return 1;
        }

        Console.WriteLine($"Generated managed project assembly: {emitResult.AssemblyPath}");
        Console.WriteLine($"Runtime support: {emitResult.RuntimeAssemblyPath}");
        Console.WriteLine($"Runtime config: {emitResult.RuntimeConfigPath}");
        return 0;
    }

    Console.Error.WriteLine(usage);
    return 1;
}

var compilation = VBCompilation.Create(File.ReadAllText(path), path);

if (args.Length == 3 && string.Equals(args[1], "--emit-csharp", StringComparison.OrdinalIgnoreCase))
{
    var generation = compilation.GenerateCSharp();
    foreach (var diagnostic in generation.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }

    if (!generation.Success || generation.Source is null)
    {
        return 1;
    }

    File.WriteAllText(args[2], generation.Source);
    Console.WriteLine($"Generated C# source: {args[2]}");
    return 0;
}

if (args.Length == 3 && string.Equals(args[1], "--emit-assembly", StringComparison.OrdinalIgnoreCase))
{
    var emitResult = compilation.EmitManagedApplication(args[2]);
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

static void PrintBackendDiagnostics(VB6.CodeGen.CSharp.AssemblyEmitResult? backendResult)
{
    if (backendResult is null)
    {
        return;
    }

    foreach (var diagnostic in backendResult.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}");
    }
}
