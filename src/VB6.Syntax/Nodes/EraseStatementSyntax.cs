using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A VB6 <c>Erase</c> statement over one or more comma-separated array variables.
/// </summary>
public sealed record EraseStatementSyntax(
    SyntaxToken EraseKeyword,
    ImmutableArray<SyntaxToken> Identifiers,
    SyntaxToken? MemberDotToken = null)
    : StatementSyntax(SyntaxKind.EraseStatement);
