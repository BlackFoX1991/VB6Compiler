using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A VB6 <c>ReDim</c> statement. The optional <c>Preserve</c> keyword applies to every
/// declarator in the statement. Declarators reuse the ordinary array declarator shape so
/// explicit lower bounds, rank and optional <c>As Type</c> clauses remain lossless.
/// </summary>
public sealed record ReDimStatementSyntax(
    SyntaxToken ReDimKeyword,
    SyntaxToken? PreserveKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators)
    : StatementSyntax(SyntaxKind.ReDimStatement);
