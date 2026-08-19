using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

public abstract record SyntaxNode(SyntaxKind Kind);
public abstract record MemberSyntax(SyntaxKind Kind) : SyntaxNode(Kind);
public abstract record StatementSyntax(SyntaxKind Kind) : SyntaxNode(Kind);
public abstract record ExpressionSyntax(SyntaxKind Kind) : SyntaxNode(Kind);
public abstract record CaseClauseSyntax(SyntaxKind Kind) : SyntaxNode(Kind);

public sealed record CompilationUnitSyntax(
    ImmutableArray<MemberSyntax> Members,
    SyntaxToken EndOfFileToken) : SyntaxNode(SyntaxKind.CompilationUnit);

public sealed record OptionExplicitSyntax(
    SyntaxToken OptionKeyword,
    SyntaxToken ExplicitKeyword) : MemberSyntax(SyntaxKind.OptionExplicitStatement);

/// <summary>
/// A VB6 <c>Attribute</c> line such as <c>Attribute VB_Name = "modMain"</c>. These carry IDE
/// metadata, not program semantics, so the tokens are kept for round-tripping and ignored
/// by the binder.
/// </summary>
public sealed record AttributeSyntax(
    SyntaxToken AttributeKeyword,
    ImmutableArray<SyntaxToken> Tokens) : MemberSyntax(SyntaxKind.AttributeStatement);

/// <summary>
/// A variable declared at module level, such as <c>Public Source As String</c> or
/// <c>Dim Position As Long</c> outside a procedure.
/// </summary>
public sealed record ModuleVariableDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken Identifier,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken) : MemberSyntax(SyntaxKind.ModuleVariableDeclaration);

/// <summary>
/// A module-level constant, such as <c>Private Const Limit As Long = 10</c>. The type is
/// optional in VB6; without it the type follows from the value.
/// </summary>
public sealed record ConstDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken ConstKeyword,
    SyntaxToken Identifier,
    SyntaxToken? AsKeyword,
    SyntaxToken? TypeToken,
    SyntaxToken EqualsToken,
    ExpressionSyntax Value) : MemberSyntax(SyntaxKind.ConstDeclaration);

/// <summary>
/// A native procedure declaration such as
/// <c>Private Declare Function GetTickCount Lib "kernel32" () As Long</c>. M2 preserves the
/// complete declaration syntax; binding and P/Invoke emission are added later by the interop
/// milestone.
/// </summary>
public sealed record DeclareDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken DeclareKeyword,
    SyntaxToken ProcedureKindKeyword,
    SyntaxToken Identifier,
    SyntaxToken LibKeyword,
    SyntaxToken LibraryName,
    SyntaxToken? AliasKeyword,
    SyntaxToken? AliasName,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken? AsKeyword,
    SyntaxToken? ReturnTypeToken) : MemberSyntax(SyntaxKind.DeclareDeclaration);

public sealed record ParameterSyntax(
    SyntaxToken? PassingModeKeyword,
    SyntaxToken Identifier,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken,
    SyntaxToken? OptionalKeyword = null,
    SyntaxToken? EqualsToken = null,
    ExpressionSyntax? DefaultValue = null) : SyntaxNode(SyntaxKind.Parameter);

public sealed record SubDeclarationSyntax(
    SyntaxToken SubKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndSubKeyword,
    SyntaxToken? VisibilityKeyword = null) : MemberSyntax(SyntaxKind.SubDeclaration);

public sealed record FunctionDeclarationSyntax(
    SyntaxToken FunctionKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken AsKeyword,
    SyntaxToken ReturnTypeToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndFunctionKeyword,
    SyntaxToken? VisibilityKeyword = null) : MemberSyntax(SyntaxKind.FunctionDeclaration);

public sealed record DimStatementSyntax(
    SyntaxToken DimKeyword,
    SyntaxToken Identifier,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken) : StatementSyntax(SyntaxKind.DimStatement);

public sealed record AssignmentStatementSyntax(
    SyntaxToken Identifier,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.AssignmentStatement);

public sealed record ElseIfClauseSyntax(
    SyntaxToken ElseIfKeyword,
    ExpressionSyntax Condition,
    SyntaxToken ThenKeyword,
    ImmutableArray<StatementSyntax> Statements) : SyntaxNode(SyntaxKind.ElseIfClause);

public sealed record IfStatementSyntax(
    SyntaxToken IfKeyword,
    ExpressionSyntax Condition,
    SyntaxToken ThenKeyword,
    ImmutableArray<StatementSyntax> Statements,
    ImmutableArray<ElseIfClauseSyntax> ElseIfClauses,
    SyntaxToken? ElseKeyword,
    ImmutableArray<StatementSyntax> ElseStatements,
    SyntaxToken? EndKeyword,
    SyntaxToken? IfEndKeyword,
    bool IsSingleLine) : StatementSyntax(SyntaxKind.IfStatement);

public sealed record ForStatementSyntax(
    SyntaxToken ForKeyword,
    SyntaxToken Identifier,
    SyntaxToken EqualsToken,
    ExpressionSyntax InitialValue,
    SyntaxToken ToKeyword,
    ExpressionSyntax Limit,
    SyntaxToken? StepKeyword,
    ExpressionSyntax? Step,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken NextKeyword,
    SyntaxToken? NextIdentifier) : StatementSyntax(SyntaxKind.ForStatement);

public sealed record WhileStatementSyntax(
    SyntaxToken WhileKeyword,
    ExpressionSyntax Condition,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken WendKeyword) : StatementSyntax(SyntaxKind.WhileStatement);

public sealed record DoStatementSyntax(
    SyntaxToken DoKeyword,
    SyntaxToken? PreConditionKeyword,
    ExpressionSyntax? PreCondition,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken LoopKeyword,
    SyntaxToken? PostConditionKeyword,
    ExpressionSyntax? PostCondition) : StatementSyntax(SyntaxKind.DoStatement);

public sealed record ExitStatementSyntax(
    SyntaxToken ExitKeyword,
    SyntaxToken TargetKeyword) : StatementSyntax(SyntaxKind.ExitStatement);

public sealed record SelectCaseStatementSyntax(
    SyntaxToken SelectKeyword,
    SyntaxToken CaseKeyword,
    ExpressionSyntax Expression,
    ImmutableArray<CaseBlockSyntax> Cases,
    SyntaxToken EndKeyword,
    SyntaxToken EndSelectKeyword) : StatementSyntax(SyntaxKind.SelectCaseStatement);

public sealed record CaseBlockSyntax(
    SyntaxToken CaseKeyword,
    ImmutableArray<CaseClauseSyntax> Clauses,
    ImmutableArray<StatementSyntax> Statements) : SyntaxNode(SyntaxKind.CaseBlock);

public sealed record CaseValueClauseSyntax(ExpressionSyntax Value)
    : CaseClauseSyntax(SyntaxKind.CaseValueClause);

public sealed record CaseRangeClauseSyntax(
    ExpressionSyntax LowerBound,
    SyntaxToken ToKeyword,
    ExpressionSyntax UpperBound) : CaseClauseSyntax(SyntaxKind.CaseRangeClause);

public sealed record CaseRelationalClauseSyntax(
    SyntaxToken IsKeyword,
    SyntaxToken OperatorToken,
    ExpressionSyntax Value) : CaseClauseSyntax(SyntaxKind.CaseRelationalClause);

public sealed record CaseElseClauseSyntax(SyntaxToken ElseKeyword)
    : CaseClauseSyntax(SyntaxKind.CaseElseClause);

public sealed record DebugPrintStatementSyntax(
    SyntaxToken DebugKeyword,
    SyntaxToken DotToken,
    SyntaxToken PrintKeyword,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.DebugPrintStatement);

public sealed record InvocationStatementSyntax(
    SyntaxToken? CallKeyword,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken? CloseParenthesisToken) : StatementSyntax(SyntaxKind.InvocationStatement);

public sealed record SkippedStatementSyntax(SyntaxToken Token) : StatementSyntax(SyntaxKind.SkippedStatement);
public sealed record LiteralExpressionSyntax(SyntaxToken LiteralToken) : ExpressionSyntax(SyntaxKind.LiteralExpression);
public sealed record NameExpressionSyntax(SyntaxToken IdentifierToken) : ExpressionSyntax(SyntaxKind.NameExpression);
public sealed record InvocationExpressionSyntax(
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.InvocationExpression);
public sealed record UnaryExpressionSyntax(SyntaxToken OperatorToken, ExpressionSyntax Operand) : ExpressionSyntax(SyntaxKind.UnaryExpression);
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, SyntaxToken OperatorToken, ExpressionSyntax Right) : ExpressionSyntax(SyntaxKind.BinaryExpression);
public sealed record ParenthesizedExpressionSyntax(SyntaxToken OpenParenthesisToken, ExpressionSyntax Expression, SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.ParenthesizedExpression);
