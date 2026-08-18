using System.Collections.Immutable;

namespace VB6.Syntax.Nodes;

public abstract record SyntaxNode(SyntaxKind Kind);
public abstract record MemberSyntax(SyntaxKind Kind) : SyntaxNode(Kind);
public abstract record StatementSyntax(SyntaxKind Kind) : SyntaxNode(Kind);
public abstract record ExpressionSyntax(SyntaxKind Kind) : SyntaxNode(Kind);

public sealed record CompilationUnitSyntax(
    ImmutableArray<MemberSyntax> Members,
    SyntaxToken EndOfFileToken) : SyntaxNode(SyntaxKind.CompilationUnit);

public sealed record OptionExplicitSyntax(
    SyntaxToken OptionKeyword,
    SyntaxToken ExplicitKeyword) : MemberSyntax(SyntaxKind.OptionExplicitStatement);

public sealed record ParameterSyntax(
    SyntaxToken? PassingModeKeyword,
    SyntaxToken Identifier,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken) : SyntaxNode(SyntaxKind.Parameter);

public sealed record SubDeclarationSyntax(
    SyntaxToken SubKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndSubKeyword) : MemberSyntax(SyntaxKind.SubDeclaration);

public sealed record DimStatementSyntax(
    SyntaxToken DimKeyword,
    SyntaxToken Identifier,
    SyntaxToken AsKeyword,
    SyntaxToken TypeToken) : StatementSyntax(SyntaxKind.DimStatement);

public sealed record AssignmentStatementSyntax(
    SyntaxToken Identifier,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.AssignmentStatement);

public sealed record IfStatementSyntax(
    SyntaxToken IfKeyword,
    ExpressionSyntax Condition,
    SyntaxToken ThenKeyword,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken IfEndKeyword) : StatementSyntax(SyntaxKind.IfStatement);

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
public sealed record UnaryExpressionSyntax(SyntaxToken OperatorToken, ExpressionSyntax Operand) : ExpressionSyntax(SyntaxKind.UnaryExpression);
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, SyntaxToken OperatorToken, ExpressionSyntax Right) : ExpressionSyntax(SyntaxKind.BinaryExpression);
public sealed record ParenthesizedExpressionSyntax(SyntaxToken OpenParenthesisToken, ExpressionSyntax Expression, SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.ParenthesizedExpression);
