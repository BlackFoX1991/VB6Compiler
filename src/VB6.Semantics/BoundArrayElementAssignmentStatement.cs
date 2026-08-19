using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// Assignment to an element of a bound VB6 array. The existing AssignmentStatement kind is
/// reused because lowering only needs to distinguish the target shape, not invent a new control-
/// flow category.
/// </summary>
public sealed record BoundArrayElementAssignmentStatement(
    VariableSymbol Array,
    ImmutableArray<BoundExpression> Indices,
    BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);
