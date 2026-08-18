using VB6.Syntax.Text;

if (args.Length == 0)
{
    Console.WriteLine("VB6Compiler bootstrap");
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
Console.WriteLine($"Loaded {path} ({text.Length} chars, {text.Lines.Length} lines)");
return 0;
