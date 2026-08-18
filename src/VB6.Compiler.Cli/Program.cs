using VB6.Lexer;
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
var result = new Lexer(text).Lex();

foreach (var diagnostic in result.Diagnostics)
{
    Console.Error.WriteLine(diagnostic);
}

Console.WriteLine($"Loaded {path} ({text.Length} chars, {text.Lines.Length} lines, {result.Tokens.Length} tokens)");
return result.Diagnostics.Length == 0 ? 0 : 1;
