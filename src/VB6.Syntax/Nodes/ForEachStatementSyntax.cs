using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// VB6 <c>For Each name In expression ... Next [name]</c> loop syntax. Binding decides which
/// collection kinds are supported; the parser preserves the complete source shape independently.
/// </summary>
public sealed record ForEachStatementSyntax(
    SyntaxToken ForKeyword,
    SyntaxToken EachKeyword,
    SyntaxToken Identifier,
    SyntaxToken InKeyword,
    ExpressionSyntax Collection,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken NextKeyword,
    SyntaxToken? NextIdentifier) : StatementSyntax(SyntaxKind.ForEachStatement);
