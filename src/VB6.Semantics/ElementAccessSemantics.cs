using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// Indexing applied to an arbitrary bound array expression. Unlike <see cref="BoundArrayAccessExpression"/>,
/// which retains a direct variable identity for the established identifier-array path, this node can
/// represent UDT members, With receivers and future expression-backed arrays while preserving the
/// same element type and VB6 Long index semantics.
/// </summary>
public sealed record BoundElementAccessExpression(
    BoundExpression Receiver,
    ImmutableArray<BoundExpression> Indices,
    TypeSymbol ElementType)
    : BoundExpression(BoundNodeKind.ArrayAccessExpression, ElementType);
