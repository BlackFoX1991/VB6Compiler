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
    ImmutableArray<VariableDeclaratorSyntax> Declarators,
    SyntaxToken? WithEventsKeyword = null) : MemberSyntax(SyntaxKind.ModuleVariableDeclaration)
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

public sealed record ParameterSyntax(
    SyntaxToken? PassingModeKeyword,
    SyntaxToken Identifier,
    SyntaxToken? AsKeyword,
    SyntaxToken? TypeToken,
    SyntaxToken? OptionalKeyword = null,
    SyntaxToken? EqualsToken = null,
    ExpressionSyntax? DefaultValue = null,
    SyntaxToken? OpenParenthesisToken = null,
    ImmutableArray<ArrayDimensionSyntax> Dimensions = default,
    SyntaxToken? CloseParenthesisToken = null,
    SyntaxToken? ParamArrayKeyword = null) : SyntaxNode(SyntaxKind.Parameter)
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
    // A VB6 Function may omit its As clause, in which case it returns Variant. Both tokens are
    // absent then, the same way DeclareDeclarationSyntax already models it.
    SyntaxToken? AsKeyword,
    SyntaxToken? ReturnTypeToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndFunctionKeyword,
    SyntaxToken? VisibilityKeyword = null) : MemberSyntax(SyntaxKind.FunctionDeclaration);

/// <summary>
/// A VB6 class property procedure. The accessor remains a token because <c>Get</c>, <c>Let</c>
/// and <c>Set</c> are contextual words in VB6 and are not globally reserved identifiers.
/// </summary>
public sealed record PropertyDeclarationSyntax(
    SyntaxToken PropertyKeyword,
    SyntaxToken AccessorKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken? AsKeyword,
    SyntaxToken? ReturnTypeToken,
    ImmutableArray<StatementSyntax> Statements,
    SyntaxToken EndKeyword,
    SyntaxToken EndPropertyKeyword,
    SyntaxToken? VisibilityKeyword = null) : MemberSyntax(SyntaxKind.PropertyDeclaration)
{
    public bool IsGet => string.Equals(AccessorKeyword.Text, "Get", StringComparison.OrdinalIgnoreCase);
    public bool IsLet => string.Equals(AccessorKeyword.Text, "Let", StringComparison.OrdinalIgnoreCase);
    public bool IsSet => string.Equals(AccessorKeyword.Text, "Set", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A class event declaration such as <c>Public Event Changed(ByVal Value As Long)</c>.</summary>
public sealed record EventDeclarationSyntax(
    SyntaxToken EventKeyword,
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ParameterSyntax> Parameters,
    SyntaxToken CloseParenthesisToken,
    SyntaxToken? VisibilityKeyword = null) : MemberSyntax(SyntaxKind.EventDeclaration);

/// <summary>A class contract declaration such as <c>Implements IFormatter</c>.</summary>
public sealed record ImplementsStatementSyntax(
    SyntaxToken ImplementsKeyword,
    SyntaxToken TypeToken) : MemberSyntax(SyntaxKind.ImplementsStatement);

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
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.AssignmentStatement);

/// <summary>A VB6 object reference assignment such as <c>Set target = New Widget</c>.</summary>
public sealed record SetAssignmentStatementSyntax(
    SyntaxToken SetKeyword,
    ExpressionSyntax Target,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.SetAssignmentStatement);

/// <summary>
/// Assignment through an addressable member/postfix chain, for example <c>point.X = 1</c>,
/// <c>record.Values(i) = 2</c> or <c>outer.Children(i).Value = 3</c>. Binding validates the
/// resulting expression target using the same addressability model as ByRef and With.
/// </summary>
public sealed record MemberAssignmentStatementSyntax(
    ExpressionSyntax Target,
    SyntaxToken EqualsToken,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.MemberAssignmentStatement);

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

public sealed record FilePrintStatementSyntax(
    SyntaxToken PrintKeyword,
    FileNumberSyntax FileNumber,
    ExpressionSyntax Expression) : StatementSyntax(SyntaxKind.FilePrintStatement);

public sealed record InvocationStatementSyntax(
    SyntaxToken? CallKeyword,
    SyntaxToken Identifier,
    SyntaxToken? OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken? CloseParenthesisToken) : StatementSyntax(SyntaxKind.InvocationStatement);

public sealed record SkippedStatementSyntax(SyntaxToken Token) : StatementSyntax(SyntaxKind.SkippedStatement);

/// <summary>
/// A method call on an object, as in <c>frmMain.SelectObjectObject "Frames"</c>. The receiver
/// keeps its full member chain.
/// </summary>
public sealed record QualifiedInvocationStatementSyntax(
    ExpressionSyntax Target,
    ImmutableArray<ExpressionSyntax> Arguments) : StatementSyntax(SyntaxKind.QualifiedInvocationStatement);

/// <summary>
/// An argument left out of a call, as in <c>List.Add , , "General"</c>. It keeps its place so the
/// arguments after it stay at the position VB6 gives them.
/// </summary>
public sealed record OmittedArgumentExpressionSyntax()
    : ExpressionSyntax(SyntaxKind.OmittedArgumentExpression);

public sealed record OnGoToStatementSyntax(
    ExpressionSyntax Expression,
    SyntaxToken GoToKeyword,
    ImmutableArray<SyntaxToken> LabelTokens) : StatementSyntax(SyntaxKind.OnGoToStatement);

public sealed record OnGoSubStatementSyntax(
    ExpressionSyntax Expression,
    SyntaxToken GoSubKeyword,
    ImmutableArray<SyntaxToken> LabelTokens) : StatementSyntax(SyntaxKind.OnGoSubStatement);

/// <summary>
/// <c>On Error GoTo Handler</c>, <c>On Error GoTo 0</c> and <c>On Error Resume Next</c>. The
/// action keyword tells the three apart and the target carries the label, the zero, or Next.
/// </summary>
public sealed record OnErrorStatementSyntax(
    SyntaxToken OnKeyword,
    SyntaxToken ErrorKeyword,
    SyntaxToken ActionKeyword,
    SyntaxToken TargetToken) : StatementSyntax(SyntaxKind.OnErrorStatement);

/// <summary><c>Resume</c>, <c>Resume Next</c> and <c>Resume Handler</c> in an error handler.</summary>
public sealed record ResumeStatementSyntax(
    SyntaxToken ResumeKeyword,
    SyntaxToken? TargetToken) : StatementSyntax(SyntaxKind.ResumeStatement);

public sealed record GoToStatementSyntax(
    SyntaxToken GoToKeyword,
    SyntaxToken LabelToken) : StatementSyntax(SyntaxKind.GoToStatement);

public sealed record GoSubStatementSyntax(
    SyntaxToken GoSubKeyword,
    SyntaxToken LabelToken) : StatementSyntax(SyntaxKind.GoSubStatement);

public sealed record GoSubReturnStatementSyntax(
    SyntaxToken ReturnKeyword) : StatementSyntax(SyntaxKind.GoSubReturnStatement);

/// <summary>
/// A jump target such as <c>LinkFail:</c>. Only a label that stands alone on its line is
/// recognized, because an identifier followed by a colon is otherwise a parameterless call
/// followed by the statement separator - <c>Foo: Bar</c> is two statements in VB6.
/// </summary>
public sealed record LabelStatementSyntax(
    SyntaxToken Identifier,
    SyntaxToken? ColonToken) : StatementSyntax(SyntaxKind.LabelStatement);

/// <summary>
/// A VB6 file number, written <c>#1</c> or <c>#FileNum</c>. The hash is optional in VB6 for some
/// statements, so it is kept separately rather than folded into the expression.
/// </summary>
public sealed record FileNumberSyntax(
    SyntaxToken? HashToken,
    ExpressionSyntax Expression) : SyntaxNode(SyntaxKind.FileNumber);

public sealed record OpenStatementSyntax(
    SyntaxToken OpenKeyword,
    ExpressionSyntax PathExpression,
    SyntaxToken ForKeyword,
    SyntaxToken ModeToken,
    SyntaxToken AsKeyword,
    FileNumberSyntax FileNumber,
    SyntaxToken? LenKeyword = null,
    SyntaxToken? LenEqualsToken = null,
    ExpressionSyntax? RecordLength = null) : StatementSyntax(SyntaxKind.OpenStatement);

public sealed record CloseStatementSyntax(
    SyntaxToken CloseKeyword,
    ImmutableArray<FileNumberSyntax> FileNumbers) : StatementSyntax(SyntaxKind.CloseStatement);

/// <summary>
/// <c>Get #1, position, target</c> and <c>Put #1, position, target</c> share their shape. VB6
/// allows the record position to be omitted, as in <c>Get #1, , target</c>, which then continues
/// from the current file position.
/// </summary>
public sealed record GetStatementSyntax(
    SyntaxToken GetKeyword,
    FileNumberSyntax FileNumber,
    ExpressionSyntax? RecordPosition,
    ExpressionSyntax Target) : StatementSyntax(SyntaxKind.GetStatement);

public sealed record PutStatementSyntax(
    SyntaxToken PutKeyword,
    FileNumberSyntax FileNumber,
    ExpressionSyntax? RecordPosition,
    ExpressionSyntax Target) : StatementSyntax(SyntaxKind.PutStatement);

public sealed record SeekStatementSyntax(
    SyntaxToken SeekKeyword,
    FileNumberSyntax FileNumber,
    ExpressionSyntax Position) : StatementSyntax(SyntaxKind.SeekStatement);

public sealed record LineInputStatementSyntax(
    SyntaxToken LineKeyword,
    SyntaxToken InputKeyword,
    FileNumberSyntax FileNumber,
    ExpressionSyntax Target) : StatementSyntax(SyntaxKind.LineInputStatement);

public sealed record FileInputStatementSyntax(
    SyntaxToken InputKeyword,
    FileNumberSyntax FileNumber,
    ImmutableArray<ExpressionSyntax> Targets) : StatementSyntax(SyntaxKind.FileInputStatement);
public sealed record LiteralExpressionSyntax(SyntaxToken LiteralToken) : ExpressionSyntax(SyntaxKind.LiteralExpression);
public sealed record NameExpressionSyntax(SyntaxToken IdentifierToken) : ExpressionSyntax(SyntaxKind.NameExpression);
public sealed record NewExpressionSyntax(
    SyntaxToken NewKeyword,
    SyntaxToken TypeToken) : ExpressionSyntax(SyntaxKind.NewExpression);
public sealed record InvocationExpressionSyntax(
    SyntaxToken Identifier,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.InvocationExpression);

public sealed record MemberInvocationExpressionSyntax(
    MemberAccessExpressionSyntax Target,
    SyntaxToken OpenParenthesisToken,
    ImmutableArray<ExpressionSyntax> Arguments,
    SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.MemberInvocationExpression);

/// <summary>
/// Explicit VB member selection. Chains are represented recursively, so <c>a.B.C</c> is a
/// MemberAccess whose receiver is another MemberAccess. Member names may be VB keywords, matching
/// the language's legal UDT member-name rules.
/// </summary>
public sealed record MemberAccessExpressionSyntax(
    ExpressionSyntax Receiver,
    SyntaxToken DotToken,
    SyntaxToken MemberToken) : ExpressionSyntax(SyntaxKind.MemberAccessExpression);

public sealed record UnaryExpressionSyntax(SyntaxToken OperatorToken, ExpressionSyntax Operand) : ExpressionSyntax(SyntaxKind.UnaryExpression);
public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, SyntaxToken OperatorToken, ExpressionSyntax Right) : ExpressionSyntax(SyntaxKind.BinaryExpression);
public sealed record ParenthesizedExpressionSyntax(SyntaxToken OpenParenthesisToken, ExpressionSyntax Expression, SyntaxToken CloseParenthesisToken) : ExpressionSyntax(SyntaxKind.ParenthesizedExpression);

/// <summary>
/// An argument written <c>ByVal expr</c> or <c>ByRef expr</c> at the call site. VB6 uses this to
/// override how a parameter would be passed, most often against a Declare with an As Any
/// parameter, as in <c>CopyMemory dst, ByVal VarPtr(src), 4</c>.
/// </summary>
public sealed record ArgumentPassingModeExpressionSyntax(
    SyntaxToken PassingModeKeyword,
    ExpressionSyntax Expression) : ExpressionSyntax(SyntaxKind.ArgumentPassingModeExpression);

/// <summary>
/// <c>TypeOf ctlControl Is CheckBox</c>. The type name is a plain token because resolving it
/// needs the object model, which does not exist yet.
/// </summary>
public sealed record TypeOfExpressionSyntax(
    SyntaxToken TypeOfKeyword,
    ExpressionSyntax Expression,
    SyntaxToken IsKeyword,
    SyntaxToken TypeToken) : ExpressionSyntax(SyntaxKind.TypeOfExpression);
