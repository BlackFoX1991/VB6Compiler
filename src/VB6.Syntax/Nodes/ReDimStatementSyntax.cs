using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A <c>ReDim</c> target that reaches through a user-defined type, as in
/// <c>ReDim Section(0).Bytes(0)</c>. The receiver is an ordinary expression; only the final
/// parenthesized list belongs to the ReDim, which is what distinguishes the bounds from the
/// indices that select the element being reached into.
/// </summary>
public sealed record ReDimQualifiedTargetSyntax(
    ExpressionSyntax Target,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ArrayDimensionSyntax> Dimensions,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken? AsKeyword = null,
    SyntaxToken? TypeToken = null,
    SyntaxToken? CommaToken = null) : SyntaxNode(SyntaxKind.ReDimQualifiedTarget);

/// <summary>
/// A VB6 <c>ReDim</c> statement. The optional <c>Preserve</c> keyword applies to every
/// declarator in the statement. Declarators reuse the ordinary array declarator shape so
/// explicit lower bounds, rank and optional <c>As Type</c> clauses remain lossless.
///
/// A target that reaches through a user-defined type cannot be a declarator - there is no name to
/// declare - so those are kept separately.
/// </summary>
public sealed record ReDimStatementSyntax(
    SyntaxToken ReDimKeyword,
    SyntaxToken? PreserveKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators,
    ImmutableArray<ReDimQualifiedTargetSyntax> QualifiedTargets = default)
    : StatementSyntax(SyntaxKind.ReDimStatement)
{
    public ImmutableArray<ReDimQualifiedTargetSyntax> QualifiedTargets { get; init; } =
        QualifiedTargets.IsDefault ? ImmutableArray<ReDimQualifiedTargetSyntax>.Empty : QualifiedTargets;
}
