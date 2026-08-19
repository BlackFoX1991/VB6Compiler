namespace VB6.Semantics;

/// <summary>
/// A bound VB6 With block. The target is an addressable UDT expression and is aliased exactly
/// once by the backend so implicit member reads/writes preserve VB6 mutation semantics for managed
/// value types and do not evaluate array/member receiver chains repeatedly.
/// </summary>
public sealed record BoundWithStatement(
    int WithId,
    BoundExpression Target,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.BlockStatement);

/// <summary>
/// Synthetic expression standing for the ref alias of the innermost active With block.
/// </summary>
public sealed record BoundWithReceiverExpression(
    int WithId,
    TypeSymbol ReceiverType)
    : BoundExpression(BoundNodeKind.VariableExpression, ReceiverType);
