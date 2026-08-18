using System.Collections.Immutable;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;

namespace VB6.Parser;

public sealed record ParseResult(
    CompilationUnitSyntax Root,
    ImmutableArray<Diagnostic> Diagnostics);
