using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;

namespace VB6.Semantics;

public abstract record Symbol(string Name);

public record TypeSymbol(string Name) : Symbol(Name)
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

/// <summary>
/// A VB6 array type. Bounds are properties of an array instance/declaration, not of its type.
/// Fixed arrays have a known rank; dynamic arrays and array parameters use an unknown rank because
/// VB6 allows a later ReDim (or caller) to determine the actual number of dimensions.
/// </summary>
public sealed record ArrayTypeSymbol : TypeSymbol
{
    public ArrayTypeSymbol(TypeSymbol elementType)
        : base(BuildName(elementType, null))
    {
        ArgumentNullException.ThrowIfNull(elementType);
        ElementType = elementType;
        Rank = null;
    }

    public ArrayTypeSymbol(TypeSymbol elementType, int rank)
        : base(BuildName(elementType, rank))
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (rank <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Array rank must be positive.");
        }

        ElementType = elementType;
        Rank = rank;
    }

    public TypeSymbol ElementType { get; }
    public int? Rank { get; }
    public bool HasKnownRank => Rank.HasValue;

    private static string BuildName(TypeSymbol elementType, int? rank)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (rank is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Array rank must be positive when specified.");
        }

        return rank is null
            ? $"{elementType.Name}()"
            : $"{elementType.Name}({new string(',', rank.Value - 1)})";
    }
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
    ReDimStatement,
    EraseStatement,
    AssignmentStatement,
    ArrayElementAssignmentStatement,
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
    ArrayAccessExpression,
    ArrayBoundExpression,
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

/// <summary>
/// One bound VB6 array dimension. Bounds are inclusive and normalized to VB6 Long so the
/// runtime and later code generation can preserve non-zero lower bounds exactly.
/// </summary>
public sealed record BoundArrayDimension(
    BoundExpression LowerBound,
    BoundExpression UpperBound);

public sealed record BoundVariableDeclarationStatement(
    LocalVariableSymbol Variable,
    ImmutableArray<BoundArrayDimension> ArrayDimensions)
    : BoundStatement(BoundNodeKind.VariableDeclarationStatement)
{
    public BoundVariableDeclarationStatement(LocalVariableSymbol variable)
        : this(variable, ImmutableArray<BoundArrayDimension>.Empty)
    {
    }
}

/// <summary>
/// A bound resize of a dynamic VB6 array. ReDim without Preserve replaces storage; Preserve uses
/// the runtime's VB6-compatible last-dimension preservation rules.
/// </summary>
public sealed record BoundReDimStatement(
    VariableSymbol Array,
    ImmutableArray<BoundArrayDimension> ArrayDimensions,
    bool Preserve)
    : BoundStatement(BoundNodeKind.ReDimStatement);

/// <summary>
/// VB6 Erase either reinitializes a fixed array while preserving its bounds or deallocates a
/// dynamic array. The binder records which operation applies so code generation never guesses.
/// </summary>
public sealed record BoundEraseStatement(
    VariableSymbol Array,
    bool Deallocate)
    : BoundStatement(BoundNodeKind.EraseStatement);

public sealed record BoundAssignmentStatement(VariableSymbol Variable, BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);

/// <summary>
/// Assignment to one element of a VB6 array. Indices are normalized to VB6 Long by the binder;
/// the runtime remains responsible for lower/upper-bound checks.
/// </summary>
public sealed record BoundArrayElementAssignmentStatement(
    VariableSymbol Array,
    ImmutableArray<BoundExpression> Indices,
    BoundExpression Expression)
    : BoundStatement(BoundNodeKind.ArrayElementAssignmentStatement);

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

/// <summary>
/// Read of one VB6 array element. The expression type is the array's element type, not the array
/// type itself, which keeps later conversion and operator binding identical to scalar values.
/// </summary>
public sealed record BoundArrayAccessExpression(
    VariableSymbol Array,
    ImmutableArray<BoundExpression> Indices,
    TypeSymbol ElementType)
    : BoundExpression(BoundNodeKind.ArrayAccessExpression, ElementType);

/// <summary>
/// Bound LBound/UBound access. The optional dimension is normalized to VB6 Long and defaults to
/// one in the binder. Runtime range validation stays centralized in VBArray&lt;T&gt;.
/// </summary>
public sealed record BoundArrayBoundExpression(
    VariableSymbol Array,
    BoundExpression Dimension,
    bool IsUpperBound)
    : BoundExpression(BoundNodeKind.ArrayBoundExpression, TypeSymbol.Long);

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
/// constants always do. Fixed arrays carry their declaration bounds; dynamic arrays have an
/// array type but an empty bound list until ReDim allocates them.
/// </summary>
public sealed record BoundModuleVariable(
    ModuleVariableSymbol Symbol,
    BoundExpression? Initializer,
    bool IsConstant,
    ImmutableArray<BoundArrayDimension> ArrayDimensions)
{
    public BoundModuleVariable(
        ModuleVariableSymbol Symbol,
        BoundExpression? Initializer,
        bool IsConstant)
        : this(Symbol, Initializer, IsConstant, ImmutableArray<BoundArrayDimension>.Empty)
    {
    }
}

public sealed record SemanticModel(
    ImmutableArray<BoundProcedure> Procedures,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public ImmutableArray<BoundModuleVariable> ModuleVariables { get; init; } =
        ImmutableArray<BoundModuleVariable>.Empty;
}
