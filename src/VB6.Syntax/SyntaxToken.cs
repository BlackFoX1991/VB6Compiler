using System.Collections.Immutable;
using VB6.Syntax.Text;

namespace VB6.Syntax;

public sealed record SyntaxToken(
    SyntaxKind Kind,
    TextSpan Span,
    string Text,
    object? Value,
    ImmutableArray<SyntaxTrivia> LeadingTrivia);
