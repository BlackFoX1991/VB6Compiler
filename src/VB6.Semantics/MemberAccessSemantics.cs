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
/// Assignment through a bound UDT member target. The target expression is kept intact so the C#
/// backend can emit the same l-value chain that is used for reads.
/// </summary>
public sealed record BoundMemberAssignmentStatement(
    BoundMemberAccessExpression Target,
    BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);
