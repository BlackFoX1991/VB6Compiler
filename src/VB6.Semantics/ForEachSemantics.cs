namespace VB6.Semantics;

/// <summary>
/// Bound VB6 For Each loop over an array, the standard Collection object, or a host-provided
/// control collection. Collection iteration uses a Variant array snapshot during IR lowering so
/// the control-flow shape remains identical to ordinary array enumeration.
/// </summary>
public sealed record BoundForEachStatement(
    int LoopId,
    VariableSymbol ControlVariable,
    BoundExpression Collection,
    ArrayTypeSymbol ArrayType,
    bool IsCollection,
    bool IsHostCollection,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.ForStatement);
