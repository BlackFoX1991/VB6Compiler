using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;

namespace VB6.Semantics;

public abstract record Symbol(string Name);

public sealed record TypeSymbol(string Name) : Symbol(Name)
{
    public static readonly TypeSymbol Error = new("<error>");
    public static readonly TypeSymbol Byte = new("Byte");
    public static readonly TypeSymbol Integer = new("Integer");
    public static readonly TypeSymbol Long = new("Long");
    public static readonly TypeSymbol LongLong = new("LongLong");
    public static readonly TypeSymbol Single = new("Single");
    public static readonly TypeSymbol String = new("String");
    public static readonly TypeSymbol Boolean = new("Boolean");
    public static readonly TypeSymbol Double = new("Double");
    public static readonly TypeSymbol Currency = new("Currency");

    public static TypeSymbol? Lookup(string name) => name.ToUpperInvariant() switch
    {
        "BYTE" => Byte,
        "INTEGER" => Integer,
        "LONG" => Long,
        "LONGLONG" => LongLong,
        "INT64" => LongLong,
        "SINGLE" => Single,
        "STRING" => String,
        "BOOLEAN" => Boolean,
        "DOUBLE" => Double,
        "CURRENCY" => Currency,
        _ => null
    };
}

public enum ParameterPassingMode
{
    ByRef,
    ByVal
}

public abstract record VariableSymbol(string Name, TypeSymbol Type) : Symbol(Name);

public sealed record LocalVariableSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type);

/// <summary>
/// A variable declared at module level. VB6 <c>Public</c> module variables are visible across
/// the whole project; <c>Private</c> ones are module-local. The binder currently makes both
/// visible everywhere, which accepts more than VB6 does but never miscompiles valid code.
/// </summary>
public sealed record ModuleVariableSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type);

public sealed record ParameterSymbol(
    string Name,
    TypeSymbol Type,
    ParameterPassingMode PassingMode)
    : VariableSymbol(Name, Type);

public sealed record ReturnValueSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type);

public sealed record ProcedureSymbol(
    string Name,
    ImmutableArray<ParameterSymbol> Parameters,
    TypeSymbol? ReturnType) : Symbol(Name)
{
    public ProcedureSymbol(string name)
        : this(name, ImmutableArray<ParameterSymbol>.Empty, null)
    {
    }

    public ProcedureSymbol(string name, ImmutableArray<ParameterSymbol> parameters)
        : this(name, parameters, null)
    {
    }

    public bool IsFunction => ReturnType is not null;
}

public enum BoundLoopKind
{
    For,
    Do
}

public enum BoundNodeKind
{
    BlockStatement,
    VariableDeclarationStatement,
    AssignmentStatement,
    IfStatement,
    ForStatement,
    WhileStatement,
    DoStatement,
    ExitLoopStatement,
    ReturnStatement,
    SelectCaseStatement,
    DebugPrintStatement,
    InvocationStatement,
    LiteralExpression,
    VariableExpression,
    InvocationExpression,
    UnaryExpression,
    BinaryExpression,
    ConversionExpression,
    ErrorExpression
}

public abstract record BoundNode(BoundNodeKind Kind);
public abstract record BoundStatement(BoundNodeKind Kind) : BoundNode(Kind);
public abstract record BoundExpression(BoundNodeKind Kind, TypeSymbol Type) : BoundNode(Kind);

public sealed record BoundBlockStatement(ImmutableArray<BoundStatement> Statements)
    : BoundStatement(BoundNodeKind.BlockStatement);

public sealed record BoundVariableDeclarationStatement(LocalVariableSymbol Variable)
    : BoundStatement(BoundNodeKind.VariableDeclarationStatement);

public sealed record BoundAssignmentStatement(VariableSymbol Variable, BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);

public sealed record BoundElseIfClause(
    BoundExpression Condition,
    BoundBlockStatement Body);

public sealed record BoundIfStatement(
    BoundExpression Condition,
    BoundBlockStatement Body,
    ImmutableArray<BoundElseIfClause> ElseIfClauses,
    BoundBlockStatement? ElseBody)
    : BoundStatement(BoundNodeKind.IfStatement);

public sealed record BoundForStatement(
    int LoopId,
    VariableSymbol ControlVariable,
    BoundExpression InitialValue,
    BoundExpression Limit,
    BoundExpression Step,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.ForStatement);

public sealed record BoundWhileStatement(
    BoundExpression Condition,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.WhileStatement);

public sealed record BoundDoStatement(
    int LoopId,
    BoundExpression? Condition,
    bool ConditionIsPostTest,
    bool IsUntil,
    BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.DoStatement);

public sealed record BoundExitLoopStatement(
    BoundLoopKind LoopKind,
    int TargetLoopId)
    : BoundStatement(BoundNodeKind.ExitLoopStatement);

/// <summary>
/// <c>Exit Sub</c> and <c>Exit Function</c>: leave the procedure, returning whatever has been
/// assigned to the function name so far.
/// </summary>
public sealed record BoundReturnStatement()
    : BoundStatement(BoundNodeKind.ReturnStatement);

public abstract record BoundCaseClause;

public sealed record BoundCaseValueClause(BoundExpression Value) : BoundCaseClause;

public sealed record BoundCaseRangeClause(
    BoundExpression LowerBound,
    BoundExpression UpperBound) : BoundCaseClause;

public sealed record BoundCaseRelationalClause(
    SyntaxKind OperatorKind,
    BoundExpression Value) : BoundCaseClause;

public sealed record BoundCaseElseClause : BoundCaseClause;

public sealed record BoundCaseBlock(
    ImmutableArray<BoundCaseClause> Clauses,
    BoundBlockStatement Body);

public sealed record BoundSelectCaseStatement(
    int SelectId,
    BoundExpression Expression,
    ImmutableArray<BoundCaseBlock> Cases)
    : BoundStatement(BoundNodeKind.SelectCaseStatement);

public sealed record BoundDebugPrintStatement(BoundExpression Expression)
    : BoundStatement(BoundNodeKind.DebugPrintStatement);

public sealed record BoundArgument(
    ParameterSymbol? Parameter,
    BoundExpression Expression);

public sealed record BoundInvocationStatement(
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

public sealed record BoundLiteralExpression(object? Value, TypeSymbol LiteralType)
    : BoundExpression(BoundNodeKind.LiteralExpression, LiteralType);

public sealed record BoundVariableExpression(VariableSymbol Variable)
    : BoundExpression(BoundNodeKind.VariableExpression, Variable.Type);

public sealed record BoundInvocationExpression(
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundExpression(BoundNodeKind.InvocationExpression, Procedure.ReturnType ?? TypeSymbol.Error);

public sealed record BoundUnaryExpression(SyntaxKind OperatorKind, BoundExpression Operand, TypeSymbol ResultType)
    : BoundExpression(BoundNodeKind.UnaryExpression, ResultType);

public sealed record BoundBinaryExpression(
    BoundExpression Left,
    SyntaxKind OperatorKind,
    BoundExpression Right,
    TypeSymbol ResultType)
    : BoundExpression(BoundNodeKind.BinaryExpression, ResultType);

public sealed record BoundConversionExpression(TypeSymbol TargetType, BoundExpression Expression)
    : BoundExpression(BoundNodeKind.ConversionExpression, TargetType);

public sealed record BoundErrorExpression()
    : BoundExpression(BoundNodeKind.ErrorExpression, TypeSymbol.Error);

public sealed record BoundProcedure(
    ProcedureSymbol Symbol,
    ImmutableArray<LocalVariableSymbol> Locals,
    BoundBlockStatement Body);

/// <summary>
/// A module-level variable together with its initial value. Plain declarations have none;
/// constants always do.
/// </summary>
public sealed record BoundModuleVariable(
    ModuleVariableSymbol Symbol,
    BoundExpression? Initializer,
    bool IsConstant);

public sealed record SemanticModel(
    ImmutableArray<BoundProcedure> Procedures,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public ImmutableArray<BoundModuleVariable> ModuleVariables { get; init; } =
        ImmutableArray<BoundModuleVariable>.Empty;
}
