using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A VB6 <c>With expression ... End With</c> block. The receiver remains an ordinary expression;
/// leading-dot member references inside the body use <see cref="WithReceiverExpressionSyntax"/>.
/// </summary>
public sealed record WithStatementSyntax(
    SyntaxToken WithKeyword,
    ExpressionSyntax Expression,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndWithKeyword) : StatementSyntax(SyntaxKind.WithStatement);

/// <summary>
/// Synthetic receiver for an implicit member selection such as <c>.X</c>. It has no source token:
/// the following <see cref="MemberAccessExpressionSyntax"/> owns the leading dot and member token.
/// Binding resolves this node against the innermost active With block.
/// </summary>
public sealed record WithReceiverExpressionSyntax()
    : ExpressionSyntax(SyntaxKind.WithReceiverExpression);
