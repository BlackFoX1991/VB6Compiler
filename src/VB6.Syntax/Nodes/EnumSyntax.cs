using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// A module-level VB6 Enum declaration. M2 preserves enum syntax so real projects can be
/// parsed further; enum type binding and code generation are added in a later milestone.
/// </summary>
public sealed record EnumDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken EnumKeyword,
    SyntaxToken Identifier,
    ImmutableArray<EnumMemberSyntax> Members,
    SyntaxToken EndKeyword,
    SyntaxToken EndEnumKeyword) : MemberSyntax(SyntaxKind.EnumDeclaration);

public sealed record EnumMemberSyntax(
    SyntaxToken Identifier,
    SyntaxToken? EqualsToken,
    ExpressionSyntax? Value) : SyntaxNode(SyntaxKind.EnumMember);
