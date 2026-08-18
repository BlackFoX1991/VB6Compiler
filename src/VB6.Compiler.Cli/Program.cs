using VB6.Compiler;

const string usage = "Usage: vb6c <source-file> [--emit-csharp <output-file> | --emit-assembly <output-file>]";

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

    if (emitResult.BackendResult is not null)
    {
        foreach (var diagnostic in emitResult.BackendResult.Diagnostics)
        {
            Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Id}: {diagnostic.Message}");
        }
    }

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
