using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// Postfix indexing applied to an arbitrary expression, for example <c>record.Values(i)</c>,
/// <c>.Values(i)</c> inside a With block, or <c>record.Children(i).Value</c>. Identifier-only
/// calls remain <see cref="InvocationExpressionSyntax"/> so binding can continue to distinguish
/// procedures from ordinary array variables without changing the existing call model.
/// </summary>
public sealed record ElementAccessExpressionSyntax(
    ExpressionSyntax Receiver,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken CloseParenthesisToken)
    : ExpressionSyntax(SyntaxKind.ElementAccessExpression);
