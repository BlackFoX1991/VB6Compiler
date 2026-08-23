using System.Collections.Immutable;
using VB6.Syntax.Text;

namespace VB6.Syntax;

public sealed record SyntaxToken(
    SyntaxKind Kind,
    TextSpan Span,
    string Text,
    object? Value,
    ImmutableArray<SyntaxTrivia> LeadingTrivia)
{
    /// <summary>The VB6 identifier type character consumed into this token, if present.</summary>
    public char? TypeSuffix { get; init; }
}
