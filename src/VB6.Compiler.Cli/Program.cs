using VB6.Compiler;

if (args.Length == 0)
{
    Console.WriteLine("VB6Compiler");
    Console.WriteLine("Usage: vb6c <source-file> [--emit-csharp <output-file>]");
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

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: vb6c <source-file> [--emit-csharp <output-file>]");
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
