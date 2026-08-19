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
