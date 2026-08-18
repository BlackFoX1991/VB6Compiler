using VB6.Parser;
using VB6.Semantics;
using VB6.Syntax.Text;

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

var text = SourceText.From(File.ReadAllText(path), path);
var parseResult = new Parser(text).ParseCompilationUnit();

foreach (var diagnostic in parseResult.Diagnostics)
{
    Console.Error.WriteLine(diagnostic);
}

if (parseResult.Diagnostics.Length != 0)
{
    return 1;
}

var semanticModel = new Binder(text).BindCompilationUnit(parseResult.Root);
foreach (var diagnostic in semanticModel.Diagnostics)
{
    Console.Error.WriteLine(diagnostic);
}

Console.WriteLine(
    $"Bound {path} ({text.Length} chars, {text.Lines.Length} lines, " +
    $"{parseResult.Root.Members.Length} members, {semanticModel.Procedures.Length} procedures)");

return semanticModel.Diagnostics.Length == 0 ? 0 : 1;
