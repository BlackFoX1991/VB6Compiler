using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;

namespace VB6.Lexer;

public sealed record LexResult(
    ImmutableArray<SyntaxToken> Tokens,
    ImmutableArray<Diagnostic> Diagnostics);
