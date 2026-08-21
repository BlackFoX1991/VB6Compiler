using System.Collections.Immutable;
using VB6.Semantics;

namespace VB6.IR;

public enum IrArrayOperation
{
    Clear,
    LBound,
    UBound,
    Length,
    GetFlatValue,
    Clone
}

public sealed record IrArrayCallExpression(
    IrArrayOperation Operation,
    IrExpression Array,
    ImmutableArray<IrExpression> Arguments,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrNullExpression(TypeSymbol NullType)
    : IrExpression(NullType);
