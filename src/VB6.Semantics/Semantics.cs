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
    public static readonly TypeSymbol LongPtr = new("LongPtr");
    public static readonly TypeSymbol UShort = new("UShort");
    public static readonly TypeSymbol UInteger = new("UInteger");
    public static readonly TypeSymbol ULong = new("ULong");
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
        "LONGPTR" => LongPtr,
        "USHORT" => UShort,
        "UINT16" => UShort,
        "UINTEGER" => UInteger,
        "UINT32" => UInteger,
        "ULONG" => ULong,
        "UINT64" => ULong,
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
        "CLIPBOARD" => VBStandardTypes.Clipboard,
        "PICTURE" => VBStandardTypes.Picture,
        "STDPICTURE" => VBStandardTypes.Picture,
        "FONT" => VBStandardTypes.Font,
        "STDFONT" => VBStandardTypes.Font,
        "OBJECT" => VBStandardTypes.Object,
        "FORM" => VBStandardTypes.Form,
        "USERCONTROL" => VBStandardTypes.UserControl,
        "CONTROL" => VBStandardTypes.Control,
        "IPICTURE" => VBStandardTypes.Picture,
        "MSCOMCTLLIB.NODE" => VBStandardTypes.ExternalTreeNode,
        "BORDERSTYLECONSTANTS" or
        "MOUSEPOINTERCONSTANTS" or
        "VBCOMPAREMETHOD" or
        "VBMSGBOXRESULT" => Long,
        "CHECKBOX" or
        "COMBOBOX" or
        "COMMANDBUTTON" or
        "DIRLISTBOX" or
        "DRIVELISTBOX" or
        "FILELISTBOX" or
        "FRAME" or
        "HSCROLLBAR" or
        "IMAGE" or
        "IMAGECOMBO" or
        "LINE" or
        "LISTBOX" or
        "LISTVIEW" or
        "MENU" or
        "OPTIONBUTTON" or
        "PICTUREBOX" or
        "PROGRESSBAR" or
        "PROPERTYPAGE" or
        "RICHTEXTBOX" or
        "SHAPE" or
        "STATUSBAR" or
        "TABSTRIP" or
        "TEXTBOX" or
        "TOOLBAR" or
        "TREEVIEW" or
        "VSCROLLBAR" => VBStandardTypes.Control,
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

public abstract record VariableSymbol(string Name, TypeSymbol Type) : Symbol(Name)
{
    public bool IsConstant { get; init; }

    /// <summary>
    /// Marks an <c>As New</c> declarator -- local, module-level or class field alike. Unlike an
    /// ordinary object initializer, VB6 creates the object only when the variable is first read
    /// (and creates it again after the storage has been assigned <c>Nothing</c>).
    /// </summary>
    public bool IsAsNew { get; init; }
}

public sealed record LocalVariableSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type);

/// <summary>
/// A variable declared at module level. VB6 <c>Public</c>/<c>Global</c> module variables are
/// visible across the whole project; <c>Private</c>/<c>Dim</c> ones are module-local. Generated
/// symbols (built-in constants, enum members and host objects) keep the public default.
/// </summary>
public sealed record ModuleVariableSymbol(string Name, TypeSymbol Type)
    : VariableSymbol(Name, Type)
{
    /// <summary>Whether this declaration may be imported into another project module.</summary>
    public bool IsPublic { get; init; } = true;
}

/// <summary>Compile-time value used to initialize a Form/UserControl designer member.</summary>
public sealed record DesignerPropertyInitializer(string Name, object Value);

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
    LenB,
    Mid,
    MidB,
    Chr,
    ChrW,
    Left,
    LeftB,
    Right,
    RightB,
    UCase,
    LCase,
    Trim,
    LTrim,
    RTrim,
    Asc,
    AscW,
    AscB,
    ChrB,
    CLngLng,
    ErrorText,
    Tab,
    Spc,
    Val,
    Hex,
    Oct,
    Str,
    String,
    Format,
    StrReverse,
    FormatNumber,
    FormatCurrency,
    FormatPercent,
    FormatDateTime,
    Partition,
    IsNumeric,
    IsArray,
    IsDate,
    IsObject,
    InStr,
    InStrB,
    InStrRev,
    StrComp,
    Replace,
    Space,
    Split,
    Join,
    Filter,
    StrConv,
    Int,
    DoEvents,
    Kill,
    Dir,
    FileCopy,
    MkDir,
    RmDir,
    ChDir,
    CurDir,
    GetAttr,
    SetAttr,
    FileDateTime,
    MsgBox,
    InputBox,
    FileLen,
    Now,
    DateValue,
    TimeValue,
    Year,
    Month,
    Day,
    Hour,
    Minute,
    Second,
    Timer,
    DateSerial,
    TimeSerial,
    DateAdd,
    DateDiff,
    DatePart,
    Weekday,
    WeekdayName,
    MonthName,
    Erl,
    Command,
    LoadResString,
    LoadResData,
    LoadResPicture,
    Environ,
    Load,
    Unload,
    VarPtr,
    ObjPtr,
    StrPtr,
    LSet,
    RSet,
    CreateObject,
    GetObject,
    Shell,
    ErrNumber,
    ErrDescription,
    ErrSource,
    ErrHelpFile,
    ErrHelpContext,
    ErrLastDllError,
    ErrClear,
    ErrRaise,
    TypeName,
    Array,
    Switch,
    Choose,
    CallByName,
    QBColor,
    IsEmpty,
    IsNull,
    IsMissing,
    IsError,
    VarType,
    Empty,
    Null,
    Nothing,
    Missing,
    Reset,
    FreeFile,
    LOF,
    EOF,
    Loc,
    Input,
    Seek,
    Date,
    Time,
    CByte,
    CInt,
    CLng,
    CLngPtr,
    CUShort,
    CUInt,
    CULng,
    CCur,
    CDec,
    CDate,
    CVDate,
    CSng,
    CDbl,
    CBool,
    CStr,
    CVar,
    CVErr,
    Abs,
    Sgn,
    Fix,
    Round,
    Sqr,
    Exp,
    Log,
    Sin,
    Cos,
    Tan,
    Atn,
    FV,
    PV,
    PMT,
    IPMT,
    PPMT,
    NPER,
    RATE,
    NPV,
    IRR,
    MIRR,
    SLN,
    SYD,
    DDB,
    Rnd,
    Randomize,
    IIf,
    RGB,
    GetSetting,
    SaveSetting,
    DeleteSetting,
    GetAllSettings,
    SendKeys,
    PopupMenu,
    LoadPicture,
    PropertyChanged,
    ScaleX,
    ScaleY,
    TextWidth,
    TextHeight,
    Print,
    PaintPicture,
    Cls,
    Point,
    NamedArgument,
    FileAttr,
    IMEStatus
}

public sealed record ProcedureSymbol(
    string Name,
    ImmutableArray<ParameterSymbol> Parameters,
    TypeSymbol? ReturnType) : Symbol(Name)
{
    /// <summary>Whether this procedure is exported to the containing project scope.</summary>
    public bool IsPublic { get; init; } = true;

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

    /// <summary>DISPID imported from a COM automation/type-library member.</summary>
    public int? ComDispId { get; init; }

    /// <summary>
    /// Vtable slot index of a member imported from an IUnknown-derived interface. Such an interface
    /// has no IDispatch, so the member exists only as a slot -- the index, never the byte offset,
    /// because an offset depends on the pointer size of whoever read the library.
    /// </summary>
    public int? ComVTableSlot { get; init; }

    /// <summary>The declared VARIANT types of the vtable parameters, comma separated.</summary>
    public string? ComParameterTypes { get; init; }

    /// <summary>The VARIANT type of the retval parameter, or VT_VOID when the member returns none.</summary>
    public short? ComReturnType { get; init; }

    /// <summary>
    /// True for a vtable member that writes into storage the caller provides. Such a member is not
    /// modelled here, and a call to it is reported rather than routed somewhere that would answer
    /// "member not found" for a member the library plainly describes.
    /// </summary>
    public bool ComVTableOutParameters { get; init; }

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

/// <summary>
/// The accessors of a <c>Property</c> declared at module level, held together under one name.
/// </summary>
/// <remarks>
/// A standard module has no instance, so its properties are ordinary procedures of that module and
/// a call site binds to a call rather than to a property access. They cannot live in the name-keyed
/// procedure table because Get, Let and Set share a single name -- which is exactly what this type
/// exists to carry. Public accessors are also project-wide, so the instance held here is the one a
/// call in another module resolves to and the one the declaring body is bound to; two separate
/// instances would leave the caller pointing at a procedure with no body.
/// </remarks>
public sealed class ModulePropertySymbol
{
    public ProcedureSymbol? Get { get; set; }

    public ProcedureSymbol? Let { get; set; }

    public ProcedureSymbol? Set { get; set; }

    public ProcedureSymbol? For(PropertyAccessorKind accessor) => accessor switch
    {
        PropertyAccessorKind.Get => Get,
        PropertyAccessorKind.Let => Let,
        _ => Set
    };

    /// <summary>
    /// Makes a module-local view of the exported accessors without changing the project table.
    /// </summary>
    /// <remarks>
    /// A binder augments its view with the private accessors declared in its own module. Reusing
    /// the project-wide container for that would publish those accessors to binders that run
    /// later, merely because the names happen to match.
    /// </remarks>
    public ModulePropertySymbol Clone() => new()
    {
        Get = Get,
        Let = Let,
        Set = Set
    };

    /// <summary>Assigns the accessor in a module-local view.</summary>
    public void SetAccessor(PropertyAccessorKind accessor, ProcedureSymbol procedure)
    {
        switch (accessor)
        {
            case PropertyAccessorKind.Get:
                Get = procedure;
                break;
            case PropertyAccessorKind.Let:
                Let = procedure;
                break;
            default:
                Set = procedure;
                break;
        }
    }

    public void Add(PropertyAccessorKind accessor, ProcedureSymbol procedure)
    {
        switch (accessor)
        {
            case PropertyAccessorKind.Get:
                Get ??= procedure;
                break;
            case PropertyAccessorKind.Let:
                Let ??= procedure;
                break;
            default:
                Set ??= procedure;
                break;
        }
    }
}

public sealed record PropertySymbol(
    string Name,
    PropertyAccessorKind Accessor,
    TypeSymbol Type,
    ImmutableArray<ParameterSymbol> Parameters) : Symbol(Name)
{
    /// <summary>True when the property is resolved by the runtime object dispatch contract.</summary>
    public bool IsLateBound { get; init; }

    /// <summary>
    /// True when this property is the synthesized Get/Let pair of a class module variable
    /// rather than a declared Property Get. Only such a property denotes real storage, so
    /// only it can receive a ByRef write-back.
    /// </summary>
    public bool IsFieldBacked { get; init; }

    /// <summary>
    /// False for a <c>Private</c> class module variable. It stays in the member surface so the
    /// class can reach it through <c>Me</c>, and the binder refuses it from anywhere else.
    /// </summary>
    public bool IsPublic { get; init; } = true;
}

public sealed record EventSymbol(
    string Name,
    ImmutableArray<ParameterSymbol> Parameters) : Symbol(Name)
{
    /// <summary>Source-interface IID used by a COM connection point, when imported.</summary>
    public Guid? ComInterfaceId { get; init; }

    /// <summary>DISPID used by a COM connection point, when imported.</summary>
    public int? ComDispId { get; init; }
}

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
    MidAssignmentStatement,
    NewExpression,
    ArrayElementAssignmentStatement,
    IfStatement,
    ForStatement,
    WhileStatement,
    DoStatement,
    ExitLoopStatement,
    ReturnStatement,
    EndStatement,
    SelectCaseStatement,
    DebugPrintStatement,
    DebugAssertStatement,
    ErrorStatement,
    GraphicsLineStatement,
    GraphicsPSetStatement,
    GraphicsCircleStatement,
    FilePrintStatement,
    FileWriteStatement,
    FileLockStatement,
    FileUnlockStatement,
    InvocationStatement,
    ControlArrayElementStatement,
    LabelStatement,
    GoToStatement,
    GoSubStatement,
    GoSubReturnStatement,
    OnGoToStatement,
    OnGoSubStatement,
    OnErrorStatement,
    ResumeStatement,
    OpenStatement,
    NameStatement,
    CloseStatement,
    SeekStatement,
    GetStatement,
    PutStatement,
    LineInputStatement,
    FileInputStatement,
    WidthStatement,
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
    AddressOfExpression,
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
    BoundExpression Target,
    bool Deallocate)
    : BoundStatement(BoundNodeKind.EraseStatement);

/// <summary>
/// Assignment to a variable. <paramref name="IsSetAssignment"/> records that the source was written
/// with <c>Set</c>: VB6 demands an object there, and for a Variant source only the run time can
/// tell whether one arrived.
/// </summary>
public sealed record BoundAssignmentStatement(
    VariableSymbol Variable,
    BoundExpression Expression,
    bool IsSetAssignment = false)
    : BoundStatement(BoundNodeKind.AssignmentStatement);

public sealed record BoundMidAssignmentStatement(
    BoundExpression Target,
    BoundExpression Start,
    BoundExpression? Length,
    BoundExpression Replacement)
    : BoundStatement(BoundNodeKind.MidAssignmentStatement);

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

public sealed record BoundEndStatement()
    : BoundStatement(BoundNodeKind.EndStatement);

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
    : BoundStatement(BoundNodeKind.SelectCaseStatement)
{
    /// <summary>True when string case clauses inherit <c>Option Compare Text</c>.</summary>
    public bool UseTextCompare { get; init; }
}

public sealed record BoundDebugPrintStatement(
    BoundExpression? Expression,
    ImmutableArray<BoundExpression> Expressions = default,
    ImmutableArray<BoundFilePrintSeparator> Separators = default)
    : BoundStatement(BoundNodeKind.DebugPrintStatement);

public sealed record BoundDebugAssertStatement(BoundExpression Expression)
    : BoundStatement(BoundNodeKind.DebugAssertStatement);

public sealed record BoundErrorStatement(BoundExpression Number)
    : BoundStatement(BoundNodeKind.ErrorStatement);

/// <summary>
/// <c>PSet</c> with its bound coordinate pair. It carries the same Step and colour contract as
/// <c>Line</c>; only the second point and the B/F options are missing.
/// </summary>
/// <summary>
/// <c>Circle</c> with its bound centre, radius and optional colour, arc angles and aspect ratio.
/// A null optional means VB6 uses its documented default: the current ForeColor, a full circle,
/// and an aspect ratio of one.
/// </summary>
public sealed record BoundGraphicsCircleStatement(
    BoundExpression CenterX,
    BoundExpression CenterY,
    BoundExpression Radius,
    BoundExpression? Color,
    BoundExpression? Start,
    BoundExpression? End,
    BoundExpression? Aspect,
    bool IsStep,
    BoundExpression? Target = null)
    : BoundStatement(BoundNodeKind.GraphicsCircleStatement);

public sealed record BoundGraphicsPSetStatement(
    BoundExpression X,
    BoundExpression Y,
    BoundExpression? Color,
    bool IsStep,
    BoundExpression? Target = null)
    : BoundStatement(BoundNodeKind.GraphicsPSetStatement);

public sealed record BoundGraphicsLineStatement(
    BoundExpression StartX,
    BoundExpression StartY,
    BoundExpression EndX,
    BoundExpression EndY,
    BoundExpression? Color,
    bool IsStep,
    bool DrawBox,
    bool Fill,
    BoundExpression? Target = null)
    : BoundStatement(BoundNodeKind.GraphicsLineStatement);

public enum BoundFilePrintSeparator
{
    Semicolon,
    Comma
}

public sealed record BoundFilePrintStatement(
    BoundExpression FileNumber,
    BoundExpression? Expression,
    ImmutableArray<BoundExpression> Expressions = default,
    ImmutableArray<BoundFilePrintSeparator> Separators = default)
    : BoundStatement(BoundNodeKind.FilePrintStatement);

public sealed record BoundFileWriteStatement(
    BoundExpression FileNumber,
    ImmutableArray<BoundExpression> Expressions)
    : BoundStatement(BoundNodeKind.FileWriteStatement);

public sealed record BoundFileLockStatement(
    BoundExpression FileNumber,
    BoundExpression? Start,
    BoundExpression? End)
    : BoundStatement(BoundNodeKind.FileLockStatement);

public sealed record BoundFileUnlockStatement(
    BoundExpression FileNumber,
    BoundExpression? Start,
    BoundExpression? End)
    : BoundStatement(BoundNodeKind.FileUnlockStatement);

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

public enum BoundFileSharingMode
{
    Shared,
    LockRead,
    LockWrite,
    LockReadWrite
}

public enum BoundFileAccessMode
{
    Default,
    Read,
    Write,
    ReadWrite
}

public sealed record BoundOpenStatement(
    BoundExpression FileNumber,
    BoundExpression Path,
    BoundFileOpenMode Mode,
    BoundExpression? RecordLength = null,
    BoundFileSharingMode Sharing = BoundFileSharingMode.Shared,
    BoundFileAccessMode Access = BoundFileAccessMode.Default) : BoundStatement(BoundNodeKind.OpenStatement);

public sealed record BoundNameStatement(
    BoundExpression OldPath,
    BoundExpression NewPath) : BoundStatement(BoundNodeKind.NameStatement);

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

public sealed record BoundWidthStatement(
    BoundExpression FileNumber,
    BoundExpression Width) : BoundStatement(BoundNodeKind.WidthStatement);

public sealed record BoundArgument(
    ParameterSymbol? Parameter,
    BoundExpression Expression)
{
    public bool RequiresByRefTemporary { get; init; }

    /// <summary>
    /// Copy-in/copy-out: der Aufgerufene arbeitet auf einer Kopie, und ihr Wert wird nach dem
    /// Aufruf in den ursprünglichen Speicher zurückgeschrieben. VB6 übergibt ein `String * n` so
    /// an einen `ByRef s As String`.
    /// </summary>
    public bool WritesBackByRefTemporary { get; init; }
    public bool IsByValAtCallSite { get; init; }
    public bool IsOmitted { get; init; }
}

    public sealed record BoundInvocationStatement(
    ProcedureSymbol Procedure,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

/// <summary>
/// <c>Load ctlButton(3)</c> and <c>Unload ctlButton(3)</c> on a control array. This cannot be an
/// ordinary intrinsic call: VB6 addresses an array slot that does not exist yet, so evaluating the
/// element first — as every other argument is evaluated — would fail before Load could create it.
/// The target is therefore kept as an assignable array place, exactly like <c>ReDim Preserve</c>.
/// </summary>
public sealed record BoundControlArrayElementStatement(
    BoundExpression Target,
    BoundExpression Index,
    string Name,
    BoundExpression Owner,
    bool Unload)
    : BoundStatement(BoundNodeKind.ControlArrayElementStatement);

public sealed record BoundRaiseEventStatement(
    EventSymbol Event,
    ImmutableArray<BoundArgument> Arguments)
    : BoundStatement(BoundNodeKind.InvocationStatement);

public sealed record BoundLiteralExpression(object? Value, TypeSymbol LiteralType)
    : BoundExpression(BoundNodeKind.LiteralExpression, LiteralType);

public sealed record BoundAddressOfExpression(ProcedureSymbol Procedure)
    : BoundExpression(BoundNodeKind.AddressOfExpression, TypeSymbol.LongPtr);

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
    /// <summary>True when a string comparison inherits <c>Option Compare Text</c>.</summary>
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

    /// <summary>True when the variable came from a Form/UserControl designer envelope.</summary>
    public bool IsDesignerControl { get; init; }

    /// <summary>Qualified designer parent path for nested Form/UserControl controls.</summary>
    public string? DesignerParentName { get; init; }

    /// <summary>Original designer control type name preserved for host-side control creation.</summary>
    public string? DesignerTypeName { get; init; }

    /// <summary>Scalar designer values applied by the configured host after control creation.</summary>
    public ImmutableArray<DesignerPropertyInitializer> DesignerInitializers { get; init; } =
        ImmutableArray<DesignerPropertyInitializer>.Empty;

    /// <summary>Explicit one-dimensional indexes present in a designer control array.</summary>
    public ImmutableArray<int> DesignerArrayIndices { get; init; } =
        ImmutableArray<int>.Empty;

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
    /// <summary>
    /// True when the source module declares <c>Option Private Module</c>. The flag describes the
    /// module's external export policy; it does not hide public members from sibling modules in
    /// the same VB6 project.
    /// </summary>
    public bool IsPrivateModule { get; init; }

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

    /// <summary>Scalar values read from the containing Form/UserControl designer envelope.</summary>
    public ImmutableArray<DesignerPropertyInitializer> DesignerInitializers { get; init; } =
        ImmutableArray<DesignerPropertyInitializer>.Empty;

    public ClassTypeSymbol? ContainingClass { get; init; }
}
