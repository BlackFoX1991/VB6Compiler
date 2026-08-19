using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// Assignment to one element of a VB6 array, for example <c>values(i, j) = 42</c>.
/// Kept distinct from a procedure call because VB6 uses the same parenthesized surface syntax
/// for both calls and array subscripts.
/// </summary>
public sealed record ArrayElementAssignmentStatementSyntax(
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression)
    : StatementSyntax(SyntaxKind.ArrayElementAssignmentStatement);
