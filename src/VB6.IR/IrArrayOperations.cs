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

public enum IrVariantArrayOperation
{
    LBound,
    UBound,
    GetElement
}

public sealed record IrVariantArrayCallExpression(
    IrVariantArrayOperation Operation,
    IrExpression Array,
    ImmutableArray<IrExpression> Arguments,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrNullExpression(TypeSymbol NullType)
    : IrExpression(NullType);
