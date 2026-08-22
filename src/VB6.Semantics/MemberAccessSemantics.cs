using System.Collections.Immutable;

namespace VB6.Semantics;

/// <summary>
/// A bound read through a VB6 user-defined type field. The receiver may itself be another member
/// access, which preserves nested UDT chains without flattening names or losing type identity.
/// </summary>
public sealed record BoundMemberAccessExpression(
    BoundExpression Receiver,
    UserDefinedTypeMemberSymbol Member)
    : BoundExpression(BoundNodeKind.VariableExpression, Member.Type);

/// <summary>
/// Assignment through a bound UDT member target. Error targets are preserved as bound expressions
/// so analysis can report the real member diagnostic without inventing a fake field symbol.
/// </summary>
public sealed record BoundMemberAssignmentStatement(
    BoundExpression Target,
    BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);

public sealed record BoundMemberInvocationStatement(
    BoundExpression Receiver,
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

public sealed record BoundMemberInvocationExpression(
    BoundExpression Receiver,
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundExpression(BoundNodeKind.InvocationExpression, Procedure.ReturnType ?? TypeSymbol.Error);

/// <summary>
/// An indexed property access. It stays distinct from a method call because an assignment to the
/// same syntax must retain the index arguments while invoking the matching Let/Set accessor.
/// </summary>
public sealed record BoundPropertyInvocationExpression(
    BoundExpression Receiver,
    PropertySymbol Property,
    ImmutableArray<BoundArgument> Arguments)
    : BoundExpression(BoundNodeKind.PropertyAccessExpression, Property.Type);
