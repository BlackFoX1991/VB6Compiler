using VB6.Compiler;

if (args.Length == 0)
{
    Console.WriteLine("VB6Compiler");
    Console.WriteLine("Usage: vb6c <source-file>");
    return 0;
}

var path = args[0];
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Input file not found: {path}");
    return 1;
}

var compilation = VBCompilation.Create(File.ReadAllText(path), path);
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
