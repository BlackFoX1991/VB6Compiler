using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

/// <summary>
/// One field in a VB6 user-defined <c>Type</c>. Array bounds are preserved through the same
/// dimension nodes used by ordinary variables. Fixed-length strings preserve the trailing
/// <c>* length</c> expression separately from the declared String type.
/// </summary>
public sealed record TypeMemberSyntax(
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ArrayDimensionSyntax> Dimensions,
    SyntaxToken? CloseParenthesisToken,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken,
    SyntaxToken? StarToken,
    ExpressionSyntax? FixedStringLength)
    : SyntaxNode(SyntaxKind.TypeMember)
{
    public bool IsArray => OpenParenthesisToken is not null;
    public bool IsFixedLengthString => StarToken is not null;
}

/// <summary>
/// A module-level VB6 user-defined type declaration such as
/// <c>Public Type Point ... End Type</c>.
/// </summary>
public sealed record TypeDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken TypeKeyword,
    SyntaxToken Identifier,
    ImmutableArray<TypeMemberSyntax> Members,
    SyntaxToken EndKeyword,
    SyntaxToken EndTypeKeyword)
    : MemberSyntax(SyntaxKind.TypeDeclaration);
