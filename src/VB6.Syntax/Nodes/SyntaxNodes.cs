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
/// A module-level <c>Option Base 0</c> or <c>Option Base 1</c> directive. M2 preserves the
/// directive syntax; array lower-bound semantics are applied when arrays are implemented.
/// </summary>
public sealed record OptionBaseSyntax(
    SyntaxToken OptionKeyword,
    SyntaxToken BaseIdentifier,
    SyntaxToken ValueToken) : MemberSyntax(SyntaxKind.OptionBaseStatement);

/// <summary>
/// A module-level <c>Option Compare Text</c> or <c>Option Compare Binary</c> directive. M2
/// preserves the selected mode; comparison semantics are applied by the later string layer.
/// </summary>
public sealed record OptionCompareSyntax(
    SyntaxToken OptionKeyword,
    SyntaxToken CompareIdentifier,
    SyntaxToken ModeToken) : MemberSyntax(SyntaxKind.OptionCompareStatement);

/// <summary>
/// A VB6 <c>Attribute</c> line such as <c>Attribute VB_Name = "modMain"</c>. These carry IDE
/// metadata, not program semantics, so the tokens are kept for round-tripping and ignored
/// by the binder.
/// </summary>
public sealed record AttributeSyntax(
    SyntaxToken AttributeKeyword,
    ImmutableArray<SyntaxToken> Tokens) : MemberSyntax(SyntaxKind.AttributeStatement);

/// <summary>
/// VB6 class files start with non-code designer metadata such as <c>VERSION 1.0 CLASS</c>
/// and a <c>BEGIN</c>/<c>END</c> property block. The compiler preserves and ignores it.
/// </summary>
public sealed record ClassMetadataSyntax(
    ImmutableArray<SyntaxToken> Tokens) : MemberSyntax(SyntaxKind.ClassMetadataStatement);

/// <summary>
/// One dimension inside a VB6 array rank specifier. <c>x(10)</c> has only an upper bound;
/// <c>x(1 To 10)</c> preserves both explicit bounds. The trailing comma belongs to this
/// dimension so multidimensional source can be round-tripped without inventing separators.
/// </summary>
public sealed record ArrayDimensionSyntax(
    ExpressionSyntax? LowerBound,
    SyntaxToken? ToKeyword,
    ExpressionSyntax UpperBound,
    SyntaxToken? CommaToken) : SyntaxNode(SyntaxKind.ArrayDimension);

/// <summary>
/// One variable inside a comma-separated declaration. VB6 applies <c>As Type</c> only to the
/// declarator it follows. Array rank/bounds also belong to the individual declarator.
/// </summary>
public sealed record VariableDeclaratorSyntax(
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ArrayDimensionSyntax> Dimensions,
    SyntaxToken? CloseParenthesisToken,
    SyntaxToken? AsKeyword,
    SyntaxToken? TypeToken,
    SyntaxToken? FixedStringStarToken,
    ExpressionSyntax? FixedStringLength,
    SyntaxToken? CommaToken) : SyntaxNode(SyntaxKind.VariableDeclarator)
{
    public VariableDeclaratorSyntax(
        SyntaxToken identifier,
        SyntaxToken? asKeyword,
        SyntaxToken? typeToken,
        SyntaxToken? commaToken)
        : this(
            identifier,
            null,
            ImmutableArray<ArrayDimensionSyntax>.Empty,
            null,
            asKeyword,
            typeToken,
            null,
            null,
            commaToken)
    {
    }

    public bool IsArray => OpenParenthesisToken is not null;
}

/// <summary>
/// Variables declared at module level, such as <c>Public Source As String, Position As Long</c>
/// or <c>Dim Counter As Integer</c> outside a procedure.
/// </summary>
public sealed record ModuleVariableDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators) : MemberSyntax(SyntaxKind.ModuleVariableDeclaration)
{
    public ModuleVariableDeclarationSyntax(
        SyntaxToken? visibilityKeyword,
        SyntaxToken identifier,
        SyntaxToken asKeyword,
        SyntaxToken typeToken)
        : this(
            visibilityKeyword,
            ImmutableArray.Create(new VariableDeclaratorSyntax(identifier, asKeyword, typeToken, null)))
    {
    }

    public VariableDeclaratorSyntax FirstDeclarator => Declarators[0];
    public SyntaxToken Identifier => FirstDeclarator.Identifier;
    public SyntaxToken AsKeyword => FirstDeclarator.AsKeyword!;
    public SyntaxToken TypeToken => FirstDeclarator.TypeToken!;
}

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

public sealed record TypeDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken TypeKeyword,
    SyntaxToken Identifier,
    ImmutableArray<VariableDeclaratorSyntax> Fields,
    SyntaxToken EndKeyword,
    SyntaxToken EndTypeKeyword) : MemberSyntax(SyntaxKind.TypeDeclaration);

public sealed record EventDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken EventKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken) : MemberSyntax(SyntaxKind.EventDeclaration);

public sealed record ParameterSyntax(
    SyntaxToken? PassingModeKeyword,
    SyntaxToken Identifier,
    SyntaxToken? AsKeyword,
    SyntaxToken? TypeToken,
    SyntaxToken? ParamArrayKeyword = null,
    SyntaxToken? OptionalKeyword = null,
    SyntaxToken? EqualsToken = null,
    ExpressionSyntax? DefaultValue = null,
    SyntaxToken? OpenParenthesisToken = null,
    ImmutableArray<ArrayDimensionSyntax> Dimensions = default,
    SyntaxToken? CloseParenthesisToken = null) : SyntaxNode(SyntaxKind.Parameter)
{
    public bool IsArray => OpenParenthesisToken is not null;
    public bool IsParamArray => ParamArrayKeyword is not null;
}

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

public sealed record PropertyDeclarationSyntax(
    SyntaxToken? VisibilityKeyword,
    SyntaxToken PropertyKeyword,
    SyntaxToken AccessorKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken? AsKeyword,
    SyntaxToken? TypeToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndPropertyKeyword) : MemberSyntax(SyntaxKind.PropertyDeclaration)
{
    public bool IsGet => string.Equals(AccessorKeyword.Text, "Get", StringComparison.OrdinalIgnoreCase);
}

public sealed record DimStatementSyntax(
    SyntaxToken DimKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators) : StatementSyntax(SyntaxKind.DimStatement)
{
    public DimStatementSyntax(
        SyntaxToken dimKeyword,
        SyntaxToken identifier,
        SyntaxToken asKeyword,
        SyntaxToken typeToken)
        : this(
            dimKeyword,
            ImmutableArray.Create(new VariableDeclaratorSyntax(identifier, asKeyword, typeToken, null)))
    {
    }

    public VariableDeclaratorSyntax FirstDeclarator => Declarators[0];
    public SyntaxToken Identifier => FirstDeclarator.Identifier;
    public SyntaxToken AsKeyword => FirstDeclarator.AsKeyword!;
    public SyntaxToken TypeToken => FirstDeclarator.TypeToken!;
}

public sealed record AssignmentStatementSyntax(
    SyntaxToken Identifier,
    SyntaxToken? DotToken,
    SyntaxToken? MemberIdentifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken? CloseParenthesisToken,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression,
    ExpressionSyntax? Target = null) : StatementSyntax(SyntaxKind.AssignmentStatement)
{
    public AssignmentStatementSyntax(
        SyntaxToken identifier,
        SyntaxToken equalsToken,
        ExpressionSyntax expression)
        : this(
            identifier,
            null,
            null,
            null,
            ImmutableArray<ExpressionSyntax>.Empty,
            null,
            equalsToken,
            expression,
            null)
    {
    }

    public bool IsIndexed => OpenParenthesisToken is not null;
    public bool IsMember => DotToken is not null;
}

public sealed record ReDimStatementSyntax(
    SyntaxToken ReDimKeyword,
    SyntaxToken? PreserveKeyword,
    ImmutableArray<VariableDeclaratorSyntax> Declarators) : StatementSyntax(SyntaxKind.ReDimStatement);

public sealed record EraseStatementSyntax(
    SyntaxToken EraseKeyword,
    ImmutableArray<SyntaxToken> Identifiers) : StatementSyntax(SyntaxKind.EraseStatement);

public sealed record ImplicitMemberAssignmentStatementSyntax(
    SyntaxToken DotToken,
    SyntaxToken MemberIdentifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken? CloseParenthesisToken,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.ImplicitMemberAssignmentStatement)
{
    public bool IsIndexed => OpenParenthesisToken is not null;
}

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

public sealed record ForEachStatementSyntax(
    SyntaxToken ForKeyword,
    SyntaxToken EachKeyword,
    SyntaxToken Identifier,
    SyntaxToken InKeyword,
    ExpressionSyntax Collection,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken NextKeyword,
    SyntaxToken? NextIdentifier) : StatementSyntax(SyntaxKind.ForEachStatement);

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

public sealed record WithStatementSyntax(
    SyntaxToken WithKeyword,
    ExpressionSyntax Target,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndWithKeyword) : StatementSyntax(SyntaxKind.WithStatement);

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

public sealed record RaiseEventStatementSyntax(
    SyntaxToken RaiseEventKeyword,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken? CloseParenthesisToken) : StatementSyntax(SyntaxKind.RaiseEventStatement);

public sealed record InvocationStatementSyntax(
    SyntaxToken? CallKeyword,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken? CloseParenthesisToken) : StatementSyntax(SyntaxKind.InvocationStatement);

public sealed record SkippedStatementSyntax(SyntaxToken Token) : StatementSyntax(SyntaxKind.SkippedStatement);
public sealed record LiteralExpressionSyntax(SyntaxToken LiteralToken) : ExpressionSyntax(SyntaxKind.LiteralExpression);
public sealed record NameExpressionSyntax(SyntaxToken IdentifierToken) : ExpressionSyntax(SyntaxKind.NameExpression);
public sealed record MemberAccessExpressionSyntax(
    ExpressionSyntax Target,
    SyntaxToken DotToken,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken? CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.MemberAccessExpression)
{
    public MemberAccessExpressionSyntax(
        ExpressionSyntax target,
        SyntaxToken dotToken,
        SyntaxToken identifier)
        : this(
            target,
            dotToken,
            identifier,
            null,
            ImmutableArray<ExpressionSyntax>.Empty,
            null)
    {
    }

    public bool IsIndexed => OpenParenthesisToken is not null;
}
public sealed record ImplicitMemberAccessExpressionSyntax(
    SyntaxToken DotToken,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Indices,
    SyntaxToken? CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.ImplicitMemberAccessExpression)
{
    public bool IsIndexed => OpenParenthesisToken is not null;
}
public sealed record InvocationExpressionSyntax(
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.InvocationExpression);
public sealed record CallSiteByValExpressionSyntax(SyntaxToken ByValKeyword, ExpressionSyntax Expression)
    : ExpressionSyntax(SyntaxKind.CallSiteByValExpression);
public sealed record UnaryExpressionSyntax(SyntaxToken OperatorToken, ExpressionSyntax Operand) : ExpressionSyntax(SyntaxKind.UnaryExpression);
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, SyntaxToken OperatorToken, ExpressionSyntax Right) : ExpressionSyntax(SyntaxKind.BinaryExpression);
public sealed record ParenthesizedExpressionSyntax(SyntaxToken OpenParenthesisToken, ExpressionSyntax Expression, SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.ParenthesizedExpression);
