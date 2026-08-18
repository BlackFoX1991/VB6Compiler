using System.Collections.Immutable;
using VB6.Parser;
using VB6.Semantics;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

namespace VB6.Compiler;

public sealed class VBCompilation
{
    private VBCompilation(SourceText text)
    {
        Text = text;
    }

    public SourceText Text { get; }

    public static VBCompilation Create(string source, string? filePath = null) =>
        new(SourceText.From(source, filePath));

    public CompilationAnalysis Analyze()
    {
        var parseResult = new Parser.Parser(Text).ParseCompilationUnit();
        if (parseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new CompilationAnalysis(
                parseResult,
                null,
                parseResult.Diagnostics);
        }

        var semanticModel = new Binder(Text).BindCompilationUnit(parseResult.Root);
        var diagnostics = parseResult.Diagnostics.AddRange(semanticModel.Diagnostics);

        return new CompilationAnalysis(parseResult, semanticModel, diagnostics);
    }
}

public sealed record CompilationAnalysis(
    ParseResult ParseResult,
    SemanticModel? SemanticModel,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
