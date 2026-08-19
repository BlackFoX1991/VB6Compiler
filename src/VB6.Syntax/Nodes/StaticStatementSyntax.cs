using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A procedure-local <c>Static</c> variable declaration. Its declarators use the same VB6
/// per-name typing rules as <c>Dim</c>, but the lifetime is intentionally represented by a
/// distinct syntax node so the binder cannot accidentally lower it as an ordinary local.
/// </summary>
public sealed record StaticStatementSyntax(
    SyntaxToken StaticKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators) : StatementSyntax(SyntaxKind.StaticStatement);