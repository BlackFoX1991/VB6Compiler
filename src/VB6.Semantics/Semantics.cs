using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

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
    public static readonly TypeSymbol Date = new("Date");
    public static readonly TypeSymbol String = new("String");
    public static readonly TypeSymbol Boolean = new("Boolean");
    public static readonly TypeSymbol Double = new("Double");
    public static readonly TypeSymbol Currency = new("Currency");
    public static readonly TypeSymbol Variant = new("Variant");

    public static TypeSymbol? Lookup(string name) => name.ToUpperInvariant() switch
    {
        "BYTE" => Byte,
        "INTEGER" => Integer,
        "LONG" => Long,
        "LONGLONG" => LongLong,
        "INT64" => LongLong,
        "SINGLE" => Single,
        "DATE" => Date,
        "STRING" => String,
        "BOOLEAN" => Boolean,
        "DOUBLE" => Double,
        "CURRENCY" => Currency,
        "VARIANT" => Variant,
        "COLLECTION" => VBStandardTypes.Collection,
        "APP" => VBStandardTypes.App,
        "SCREEN" => VBStandardTypes.Screen,
        "AMBIENT" => VBStandardTypes.Ambient,
        "PROPERTYBAG" => VBStandardTypes.PropertyBag,
        "PICTURE" => VBStandardTypes.Picture,
        "STDPICTURE" => VBStandardTypes.Picture,
        "FONT" => VBStandardTypes.Font,
        "STDFONT" => VBStandardTypes.Font,
        "OBJECT" => VBStandardTypes.Object,
        "FORM" => VBStandardTypes.Form,
        "USERCONTROL" => VBStandardTypes.UserControl,
        "CONTROL" => VBStandardTypes.Control,
        "OLE_COLOR" => Long,
        _ => UserDefinedTypeLookupScope.Lookup(name)
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
    : VariableSymbol(Name, Type)
{
    public bool IsOptional { get; init; }
    public bool IsParamArray { get; init; }
    public bool IsAny { get; init; }
    public object? DefaultValue { get; init; }
}

public sealed record ReturnValueSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type);

public enum VBIntrinsicKind
{
    Len,
    Mid,
    Chr,
    Left,
    Right,
    UCase,
    LCase,
    Trim,
    LTrim,
    RTrim,
    Asc,
    IsNumeric,
    InStr,
    InStrRev,
    Replace,
    Space,
    Split,
    StrConv,
    Int,
    DoEvents,
    Kill,
    Dir,
    MsgBox,
    InputBox,
    FileLen,
    Now,
    Command,
    Load,
    Unload,
    VarPtr,
    ObjPtr,
    StrPtr,
    LSet,
    CreateObject,
    GetObject,
    Shell,
    ErrNumber,
    ErrDescription,
    ErrClear,
    ErrRaise,
    TypeName,
    Switch,
    IsEmpty,
    IsNull,
    IsMissing,
    VarType,
    Empty,
    Null,
    Nothing,
    Missing,
    FreeFile,
    LOF,
    EOF,
    Seek,
    CByte,
    CInt,
    CLng,
    CDec,
    CDate,
    CSng,
    CDbl,
    CBool,
    CStr,
    Abs,
    Sgn,
    Fix,
    Round,
    Sqr,
    IIf,
    RGB,
    GetSetting,
    SaveSetting,
    SendKeys,
    PopupMenu,
    LoadPicture,
    PropertyChanged
}

public sealed record ProcedureSymbol(
    string Name,
    ImmutableArray<ParameterSymbol> Parameters,
    TypeSymbol? ReturnType) : Symbol(Name)
{
    /// <summary>Backend-independent identity for a VB6 language intrinsic.</summary>
    public VBIntrinsicKind? IntrinsicKind { get; init; }

    /// <summary>
    /// Transitional compatibility field for the retiring C# backend. New lowering and emit code
    /// must use <see cref="IntrinsicKind"/> instead. Removed at the backend cutover.
    /// </summary>
    public string? IntrinsicTarget { get; init; }

    /// <summary>True for a VB6 Declare/PInvoke contract whose body lives in a native library.</summary>
    public bool IsExternal { get; init; }

    public string? ExternalLibrary { get; init; }
    public string? ExternalAlias { get; init; }

    public int? IntrinsicMinimumArguments { get; init; }

    /// <summary>Identifies a class property accessor that is bound as an internal procedure.</summary>
    public PropertyAccessorKind? PropertyAccessor { get; init; }

    /// <summary>True when the call is resolved by the runtime object dispatch contract.</summary>
    public bool IsLateBound { get; init; }

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

public enum PropertyAccessorKind
{
    Get,
    Let,
    Set
}

public sealed record PropertySymbol(
    string Name,
    PropertyAccessorKind Accessor,
    TypeSymbol Type,
    ImmutableArray<ParameterSymbol> Parameters) : Symbol(Name)
{
    /// <summary>True when the property is resolved by the runtime object dispatch contract.</summary>
    public bool IsLateBound { get; init; }
}

public sealed record EventSymbol(
    string Name,
    ImmutableArray<ParameterSymbol> Parameters) : Symbol(Name);

public enum BoundLoopKind
{
    For,
    Do
}

public enum BoundErrorHandlingMode
{
    Disable,
    ResumeNext,
    GoToLabel
}

public enum BoundNodeKind
{
    BlockStatement,
    VariableDeclarationStatement,
    ReDimStatement,
    EraseStatement,
    AssignmentStatement,
    NewExpression,
    ArrayElementAssignmentStatement,
    IfStatement,
    ForStatement,
    WhileStatement,
    DoStatement,
    ExitLoopStatement,
    ReturnStatement,
    SelectCaseStatement,
    DebugPrintStatement,
    FilePrintStatement,
    InvocationStatement,
    LabelStatement,
    GoToStatement,
    GoSubStatement,
    GoSubReturnStatement,
    OnGoToStatement,
    OnGoSubStatement,
    OnErrorStatement,
    ResumeStatement,
    OpenStatement,
    CloseStatement,
    SeekStatement,
    GetStatement,
    PutStatement,
    LineInputStatement,
    FileInputStatement,
    LiteralExpression,
    VariableExpression,
    PropertyAccessExpression,
    TypeOfExpression,
    ArrayAccessExpression,
    ArrayBoundExpression,
    InvocationExpression,
    UnaryExpression,
    BinaryExpression,
    ConversionExpression,
    ArrayLiteralExpression,
    ErrorExpression
}

/// <summary>
/// Where a bound node was written. The line/column range travels with the offsets because only
/// the binder still has the <see cref="SourceText"/> that can resolve one into the other, and
/// debug information is expressed in lines and columns.
/// </summary>
public sealed record SourceLocation(string? FilePath, TextSpan Span, LinePositionSpan Lines = default);

public abstract record BoundNode(BoundNodeKind Kind)
{
    public SourceLocation? SourceLocation { get; init; }
}

public abstract record BoundStatement(BoundNodeKind Kind) : BoundNode(Kind);
public abstract record BoundExpression(BoundNodeKind Kind, TypeSymbol Type) : BoundNode(Kind);

public sealed record BoundBlockStatement(ImmutableArray<BoundStatement> Statements)
    : BoundStatement(BoundNodeKind.BlockStatement);

public sealed record BoundArrayDimension(
    BoundExpression LowerBound,
    BoundExpression UpperBound);

public sealed record BoundVariableDeclarationStatement(
    LocalVariableSymbol Variable,
    ImmutableArray<BoundArrayDimension> ArrayDimensions,
    BoundExpression? Initializer = null)
    : BoundStatement(BoundNodeKind.VariableDeclarationStatement)
{
    public BoundVariableDeclarationStatement(LocalVariableSymbol variable)
        : this(variable, ImmutableArray<BoundArrayDimension>.Empty)
    {
    }
}

public sealed record BoundReDimStatement(
    BoundExpression Target,
    ImmutableArray<BoundArrayDimension> ArrayDimensions,
    bool Preserve)
    : BoundStatement(BoundNodeKind.ReDimStatement);

public sealed record BoundEraseStatement(
    VariableSymbol Array,
    bool Deallocate)
    : BoundStatement(BoundNodeKind.EraseStatement);

public sealed record BoundAssignmentStatement(VariableSymbol Variable, BoundExpression Expression)
    : BoundStatement(BoundNodeKind.AssignmentStatement);

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

public sealed record BoundFilePrintStatement(
    BoundExpression FileNumber,
    BoundExpression Expression)
    : BoundStatement(BoundNodeKind.FilePrintStatement);

public sealed record BoundLabelStatement(string Name) : BoundStatement(BoundNodeKind.LabelStatement);

public sealed record BoundGoToStatement(string Name) : BoundStatement(BoundNodeKind.GoToStatement);

public sealed record BoundGoSubStatement(string Name) : BoundStatement(BoundNodeKind.GoSubStatement);

public sealed record BoundGoSubReturnStatement()
    : BoundStatement(BoundNodeKind.GoSubReturnStatement);

public sealed record BoundOnGoToStatement(
    BoundExpression Expression,
    ImmutableArray<string> Labels)
    : BoundStatement(BoundNodeKind.OnGoToStatement);

public sealed record BoundOnGoSubStatement(
    BoundExpression Expression,
    ImmutableArray<string> Labels)
    : BoundStatement(BoundNodeKind.OnGoSubStatement);

public sealed record BoundOnErrorStatement(
    BoundErrorHandlingMode Mode,
    string? HandlerLabel = null)
    : BoundStatement(BoundNodeKind.OnErrorStatement);

public sealed record BoundResumeStatement(
    bool IsNext,
    string? TargetLabel = null)
    : BoundStatement(BoundNodeKind.ResumeStatement);

public enum BoundFileOpenMode
{
    Binary,
    Input,
    Output,
    Append,
    Random
}

public sealed record BoundOpenStatement(
    BoundExpression FileNumber,
    BoundExpression Path,
    BoundFileOpenMode Mode,
    BoundExpression? RecordLength = null) : BoundStatement(BoundNodeKind.OpenStatement);

public sealed record BoundCloseStatement(
    ImmutableArray<BoundExpression> FileNumbers) : BoundStatement(BoundNodeKind.CloseStatement);

public sealed record BoundSeekStatement(
    BoundExpression FileNumber,
    BoundExpression Position) : BoundStatement(BoundNodeKind.SeekStatement);

public sealed record BoundGetStatement(
    BoundExpression FileNumber,
    BoundExpression? Position,
    BoundExpression Target) : BoundStatement(BoundNodeKind.GetStatement);

public sealed record BoundPutStatement(
    BoundExpression FileNumber,
    BoundExpression? Position,
    BoundExpression Value) : BoundStatement(BoundNodeKind.PutStatement);

public sealed record BoundLineInputStatement(
    BoundExpression FileNumber,
    BoundExpression Target) : BoundStatement(BoundNodeKind.LineInputStatement);

public sealed record BoundFileInputStatement(
    BoundExpression FileNumber,
    ImmutableArray<BoundExpression> Targets) : BoundStatement(BoundNodeKind.FileInputStatement);

public sealed record BoundArgument(
    ParameterSymbol? Parameter,
    BoundExpression Expression)
{
    public bool RequiresByRefTemporary { get; init; }
    public bool IsByValAtCallSite { get; init; }
}

    public sealed record BoundInvocationStatement(
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

public sealed record BoundRaiseEventStatement(
    EventSymbol Event,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

public sealed record BoundLiteralExpression(object? Value, TypeSymbol LiteralType)
    : BoundExpression(BoundNodeKind.LiteralExpression, LiteralType);

public sealed record BoundNewExpression(ClassTypeSymbol ClassType)
    : BoundExpression(BoundNodeKind.NewExpression, ClassType);

public sealed record BoundPropertyAccessExpression(
    BoundExpression Receiver,
    PropertySymbol Property)
    : BoundExpression(BoundNodeKind.PropertyAccessExpression, Property.Type);

public sealed record BoundTypeOfExpression(
    BoundExpression Expression,
    ClassTypeSymbol TargetType)
    : BoundExpression(BoundNodeKind.TypeOfExpression, TypeSymbol.Boolean);

public sealed record BoundVariableExpression(VariableSymbol Variable)
    : BoundExpression(BoundNodeKind.VariableExpression, Variable.Type);

public sealed record BoundArrayAccessExpression(
    VariableSymbol Array,
    ImmutableArray<BoundExpression> Indices,
    TypeSymbol ElementType)
    : BoundExpression(BoundNodeKind.ArrayAccessExpression, ElementType);

public sealed record BoundArrayLiteralExpression(
    ArrayTypeSymbol ArrayType,
    ImmutableArray<BoundExpression> Elements)
    : BoundExpression(BoundNodeKind.ArrayLiteralExpression, ArrayType);

public sealed record BoundArrayBoundExpression(
    BoundExpression Array,
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
    : BoundExpression(BoundNodeKind.BinaryExpression, ResultType)
{
    /// <summary>True when a Like expression inherits <c>Option Compare Text</c>.</summary>
    public bool UseTextCompare { get; init; }
}

public sealed record BoundConversionExpression(TypeSymbol TargetType, BoundExpression Expression)
    : BoundExpression(BoundNodeKind.ConversionExpression, TargetType);

public sealed record BoundErrorExpression()
    : BoundExpression(BoundNodeKind.ErrorExpression, TypeSymbol.Error);

public sealed record BoundProcedure(
    ProcedureSymbol Symbol,
    ImmutableArray<LocalVariableSymbol> Locals,
    BoundBlockStatement Body);

public sealed record BoundModuleVariable(
    ModuleVariableSymbol Symbol,
    BoundExpression? Initializer,
    bool IsConstant,
    ImmutableArray<BoundArrayDimension> ArrayDimensions)
{
    public bool IsWithEvents { get; init; }

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
    public ImmutableArray<ProcedureSymbol> ExternalProcedures { get; init; } =
        ImmutableArray<ProcedureSymbol>.Empty;
    public ImmutableArray<ClassTypeSymbol> ClassTypes { get; init; } =
        ImmutableArray<ClassTypeSymbol>.Empty;
    public ImmutableArray<PropertySymbol> Properties { get; init; } =
        ImmutableArray<PropertySymbol>.Empty;
    public ImmutableArray<EventSymbol> Events { get; init; } =
        ImmutableArray<EventSymbol>.Empty;
    public ImmutableArray<BoundModuleVariable> ModuleVariables { get; init; } =
        ImmutableArray<BoundModuleVariable>.Empty;
    public ImmutableArray<BoundModuleVariable> StaticVariables { get; init; } =
        ImmutableArray<BoundModuleVariable>.Empty;
    /// <summary>Class fields declared by the current class/form module.</summary>
    public ImmutableArray<BoundModuleVariable> InstanceVariables { get; init; } =
        ImmutableArray<BoundModuleVariable>.Empty;
    public ClassTypeSymbol? ContainingClass { get; init; }
}
