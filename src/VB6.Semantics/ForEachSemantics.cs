namespace VB6.Semantics;

/// <summary>
/// Bound VB6 For Each loop over an array. The control variable is required to be Variant for the
/// currently supported array form; Collection retains its array type so code generation can emit
/// the correct typed runtime enumeration and box each element into the Variant control variable.
/// </summary>
public sealed record BoundForEachStatement(
    int LoopId,
    VariableSymbol ControlVariable,
    BoundExpression Collection,
    ArrayTypeSymbol ArrayType,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.ForStatement);
