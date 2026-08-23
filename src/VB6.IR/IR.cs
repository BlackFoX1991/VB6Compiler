using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Text;

namespace VB6.IR;

/// <summary>
/// Where the statement an instruction came from was written. Lines and columns are carried
/// alongside the offsets because that is the form debug information needs and the source text is
/// no longer available at this layer.
/// </summary>
public sealed record IrSourceLocation(string FilePath, TextSpan Span, LinePositionSpan Lines = default);

public abstract record IrNode
{
    public IrSourceLocation? SourceLocation { get; init; }
}

public sealed record IrProgram(
    ImmutableArray<IrModule> Modules,
    ImmutableArray<IrTypeDefinition> TypeDefinitions,
    IrProcedure? EntryPoint,
    ImmutableArray<IrClassDefinition> ClassDefinitions = default) : IrNode;

public sealed record IrModule(
    string Name,
    string? SourcePath,
    ImmutableArray<IrGlobal> Globals,
    ImmutableArray<IrProcedure> Procedures) : IrNode;

public sealed record IrTypeDefinition(
    UserDefinedTypeSymbol Symbol,
    string Name,
    ImmutableArray<IrField> Fields,
    ImmutableArray<IrProcedure> Methods) : IrNode;

public sealed record IrClassDefinition(
    ClassTypeSymbol Symbol,
    string Name,
    ImmutableArray<IrField> Fields,
    ImmutableArray<IrProcedure> Methods,
    bool IsInterface = false) : IrNode;

public sealed record IrField(
    string Name,
    TypeSymbol Type,
    bool IsStatic = false,
    bool IsCompilerGenerated = false) : IrNode;

public sealed record IrGlobal(
    ModuleVariableSymbol Symbol,
    string Name,
    TypeSymbol Type,
    IrExpression? Initializer,
    bool IsConstant) : IrNode;

public sealed record IrProcedure(
    ProcedureSymbol? Symbol,
    string Name,
    TypeSymbol? ReturnType,
    ImmutableArray<IrParameter> Parameters,
    ImmutableArray<IrLocal> Locals,
    ImmutableArray<IrBasicBlock> Blocks,
    UserDefinedTypeSymbol? DeclaringType = null,
    bool IsStatic = true,
    bool IsCompilerGenerated = false,
    bool IsExternal = false,
    string? ExternalLibrary = null,
    string? ExternalAlias = null,
    ClassTypeSymbol? DeclaringClass = null) : IrNode;

public sealed record IrParameter(
    ParameterSymbol? Symbol,
    int Index,
    string Name,
    TypeSymbol Type,
    ParameterPassingMode PassingMode,
    bool IsCompilerGenerated = false) : IrNode;

public sealed record IrLocal(
    int Id,
    string Name,
    TypeSymbol Type,
    bool IsCompilerGenerated = false,
    bool IsManagedAddress = false) : IrNode;

public sealed record IrBasicBlock(
    int Id,
    string Label,
    ImmutableArray<IrInstruction> Instructions,
    IrTerminator Terminator) : IrNode;

public abstract record IrInstruction : IrNode;

public sealed record IrStoreInstruction(IrPlace Target, IrExpression Value) : IrInstruction;

public sealed record IrStoreAddressInstruction(IrLocal AddressLocal, IrExpression Address) : IrInstruction;

public sealed record IrEvaluateInstruction(IrExpression Expression) : IrInstruction;

public sealed record IrNopInstruction : IrInstruction;

/// <summary>Calls the CLR base finalizer after a generated Class_Terminate body.</summary>
public sealed record IrBaseFinalizeInstruction : IrInstruction;

/// <summary>
/// Raises a class event. Event subscription/storage is backend-specific; retaining the event
/// identity in IR keeps the compiler contract explicit for the native/COM and managed backends.
/// </summary>
public sealed record IrRaiseEventInstruction(
    EventSymbol Event,
    ImmutableArray<IrExpression> Arguments,
    ClassTypeSymbol? DeclaringClass = null) : IrInstruction;

public sealed record IrSubscribeEventInstruction(
    IrExpression Source,
    EventSymbol Event,
    IrExpression Target,
    ProcedureSymbol Handler) : IrInstruction;

/// <summary>Starts a per-statement Resume Next protected region in the managed emitter.</summary>
public sealed record IrErrorBoundaryStartInstruction(int? HandlerBlockId = null) : IrInstruction;

/// <summary>Ends a per-statement Resume Next protected region in the managed emitter.</summary>
public sealed record IrErrorBoundaryEndInstruction : IrInstruction;

public enum IrResumeKind
{
    Same,
    Next
}

public sealed record IrResumeInstruction(IrResumeKind Kind) : IrInstruction;

public abstract record IrTerminator : IrNode;

public sealed record IrGotoTerminator(int TargetBlockId) : IrTerminator;

public sealed record IrConditionalTerminator(
    IrExpression Condition,
    int TrueBlockId,
    int FalseBlockId) : IrTerminator;

public sealed record IrGoSubTerminator(
    int TargetBlockId,
    int ReturnIndex) : IrTerminator;

public sealed record IrGoSubReturnTerminator(
    ImmutableArray<int> ReturnTargetBlockIds) : IrTerminator;

public sealed record IrOnGoToTerminator(
    IrExpression Index,
    ImmutableArray<int> TargetBlockIds,
    int DefaultBlockId) : IrTerminator;

public sealed record IrOnGoSubTerminator(
    IrExpression Index,
    ImmutableArray<int> TargetBlockIds,
    int ReturnIndex,
    int DefaultBlockId) : IrTerminator;

public sealed record IrReturnTerminator(IrExpression? Value) : IrTerminator;

public abstract record IrPlace(TypeSymbol Type) : IrNode;

public sealed record IrLocalPlace(IrLocal Local) : IrPlace(Local.Type);

public sealed record IrParameterPlace(IrParameter Parameter) : IrPlace(Parameter.Type);

public sealed record IrGlobalPlace(IrGlobal Global) : IrPlace(Global.Type);

public sealed record IrThisPlace(ClassTypeSymbol ClassType) : IrPlace(ClassType);

public sealed record IrFieldPlace(
    IrPlace Receiver,
    IrField Field) : IrPlace(Field.Type);

public sealed record IrArrayElementPlace(
    IrExpression Array,
    ImmutableArray<IrExpression> Indices,
    TypeSymbol ElementType) : IrPlace(ElementType);

public sealed record IrArrayFlatElementPlace(
    IrExpression Array,
    IrExpression Index,
    TypeSymbol ElementType) : IrPlace(ElementType);

public sealed record IrIndirectPlace(
    IrExpression Address,
    TypeSymbol ElementType) : IrPlace(ElementType);

public sealed record IrAccessorPlace(
    IrExpression? Receiver,
    ProcedureSymbol? Getter,
    ProcedureSymbol? Setter,
    TypeSymbol ValueType,
    ImmutableArray<IrExpression> Arguments = default) : IrPlace(ValueType);

public abstract record IrExpression(TypeSymbol Type) : IrNode;

public sealed record IrConstantExpression(object? Value, TypeSymbol ConstantType)
    : IrExpression(ConstantType);

public sealed record IrDefaultExpression(TypeSymbol DefaultType)
    : IrExpression(DefaultType);

public sealed record IrLoadExpression(IrPlace Place)
    : IrExpression(Place.Type);

public sealed record IrAddressExpression(IrPlace Place)
    : IrExpression(Place.Type);

public sealed record IrLocalAddressExpression(IrLocal Local)
    : IrExpression(Local.Type);

public sealed record IrAddressOfExpression(
    ProcedureSymbol Procedure,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrRuntimeCallExpression(
    IrRuntimeMethod Method,
    ImmutableArray<IrCallArgument> Arguments,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrProcedureCallExpression(
    ProcedureSymbol Procedure,
    ImmutableArray<IrCallArgument> Arguments,
    TypeSymbol ResultType,
    IrExpression? Receiver = null)
    : IrExpression(ResultType);

public sealed record IrSyntheticCallExpression(
    IrProcedure Procedure,
    IrExpression? Receiver,
    ImmutableArray<IrCallArgument> Arguments,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrNewVBArrayExpression(
    ArrayTypeSymbol ArrayType,
    ImmutableArray<IrArrayBound> Bounds)
    : IrExpression(ArrayType);

public sealed record IrNewClassExpression(ClassTypeSymbol ClassType)
    : IrExpression(ClassType);

public sealed record IrTypeOfExpression(
    IrExpression Expression,
    ClassTypeSymbol TargetType)
    : IrExpression(TypeSymbol.Boolean);

public sealed record IrReDimPreserveExpression(
    IrExpression Array,
    ArrayTypeSymbol ArrayType,
    ImmutableArray<IrArrayBound> Bounds)
    : IrExpression(ArrayType);

/// <summary>
/// Reads a fixed-size array member of a user-defined type, creating its storage on first access.
/// A UDT is a struct, so a default instance - including every element of an array of that type -
/// starts with a null member; the declared bounds are the only place the size is known.
/// <see cref="Storage"/> is addressed rather than loaded so the created array lands in the member
/// itself instead of a copy.
/// </summary>
public sealed record IrEnsureArrayExpression(
    IrPlace Storage,
    ArrayTypeSymbol ArrayType,
    ImmutableArray<IrArrayBound> Bounds)
    : IrExpression(ArrayType);

/// <summary>
/// Duplicates the array held by a fixed array member of a user-defined type. Assigning a VB6 UDT
/// copies it by value, but the CLR struct copy only duplicates the array reference - both values
/// would keep indexing the same array. The declared bounds travel along because a member that was
/// never touched has no storage to take them from.
/// </summary>
public sealed record IrCopyArrayExpression(
    IrExpression Source,
    ArrayTypeSymbol ArrayType,
    ImmutableArray<IrArrayBound> Bounds)
    : IrExpression(ArrayType);

public sealed record IrArrayBound(IrExpression Lower, IrExpression Upper);

public enum IrCallArgumentKind
{
    Value,
    Address
}

public sealed record IrCallArgument(
    IrExpression Expression,
    IrCallArgumentKind Kind = IrCallArgumentKind.Value);

public enum IrRuntimeMethod
{
    CByte,
    CInt,
    CLng,
    CLngPtr,
    CUShort,
    CUInt,
    CULng,
    CDec,
    CDate,
    DateToVariant,
    CLngLng,
    VariantToBoolean,
    CCur,
    CSng,
    CDbl,
    CBool,
    CStr,

    AddByte,
    AddInteger,
    AddLong,
    AddLongLong,
    AddLongPtr,
    AddUShort,
    AddUInteger,
    AddULong,
    AddCurrency,
    AddSingle,
    AddDouble,
    AddVariant,
    AddStringVariant,
    SubtractByte,
    SubtractInteger,
    SubtractLong,
    SubtractLongLong,
    SubtractLongPtr,
    SubtractUShort,
    SubtractUInteger,
    SubtractULong,
    SubtractCurrency,
    SubtractSingle,
    SubtractDouble,
    SubtractVariant,
    MultiplyByte,
    MultiplyInteger,
    MultiplyLong,
    MultiplyLongLong,
    MultiplyLongPtr,
    MultiplyUShort,
    MultiplyUInteger,
    MultiplyULong,
    MultiplyCurrency,
    MultiplySingle,
    MultiplyDouble,
    MultiplyVariant,
    NegateInteger,
    NegateLong,
    NegateLongLong,
    NegateLongPtr,
    NegateUShort,
    NegateUInteger,
    NegateULong,
    NegateCurrency,
    NegateSingle,
    NegateDouble,
    IntegerDivideByte,
    IntegerDivideInteger,
    IntegerDivideLong,
    IntegerDivideLongLong,
    IntegerDivideLongPtr,
    IntegerDivideUShort,
    IntegerDivideUInteger,
    IntegerDivideULong,
    ModByte,
    ModInteger,
    ModLong,
    ModLongLong,
    ModLongPtr,
    ModUShort,
    ModUInteger,
    ModULong,
    DivideSingle,
    DivideDouble,
    DivideVariant,
    IntegerDivideVariant,
    ModVariant,
    Power,
    PowerVariant,
    NotBoolean,
    NotInteger,
    NotLong,
    NotLongLong,
    NotLongPtr,
    NotUShort,
    NotUInteger,
    NotULong,
    NotVariant,
    NegateVariant,
    AndBoolean,
    AndByte,
    AndInteger,
    AndLong,
    AndLongLong,
    AndLongPtr,
    AndUShort,
    AndUInteger,
    AndULong,
    OrBoolean,
    OrByte,
    OrInteger,
    OrLong,
    OrLongLong,
    OrLongPtr,
    OrUShort,
    OrUInteger,
    OrULong,
    XorBoolean,
    XorByte,
    XorInteger,
    XorLong,
    XorLongLong,
    XorLongPtr,
    XorUShort,
    XorUInteger,
    XorULong,
    EqvBoolean,
    EqvInteger,
    EqvLong,
    EqvLongLong,
    EqvLongPtr,
    EqvUShort,
    EqvUInteger,
    EqvULong,
    ImpBoolean,
    ImpInteger,
    ImpLong,
    ImpLongLong,
    ImpLongPtr,
    ImpUShort,
    ImpUInteger,
    ImpULong,
    AndVariant,
    OrVariant,
    XorVariant,
    EqvVariant,
    ImpVariant,
    Concat,
    ConcatVariant,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    VariantEqual,
    VariantNotEqual,
    VariantLess,
    VariantLessOrEqual,
    VariantGreater,
    VariantGreaterOrEqual,
    StringVariantEqual,
    StringVariantNotEqual,
    StringVariantLess,
    StringVariantLessOrEqual,
    StringVariantGreater,
    StringVariantGreaterOrEqual,

    DebugPrint,
    GraphicsLine,
    EndProgram,

    StringLen,
    StringMid,
    StringChr,
    StringLeft,
    StringRight,
    StringUCase,
    StringLCase,
    StringTrim,
    StringLTrim,
    StringRTrim,
    StringAsc,
    StringVal,
    StringHex,
    StringRepeat,
    StringFormat,
    StringIsNumeric,
    StringLike,
    StringInStr,
    StringInStrRev,
    StringReplace,
    StringSpace,
    StringSplit,
    StringStrConv,
    ConversionInt,
    MathAbs,
    MathSgn,
    MathFix,
    MathRound,
    MathSqr,
    MathExp,
    MathLog,
    MathSin,
    MathCos,
    MathTan,
    MathAtn,

    VariantEmpty,
    VariantNull,
    VariantNothing,
    VariantMissing,
    VariantIsEmpty,
    VariantIsNull,
    VariantIsMissing,
    VariantVarType,

    FileOpenBinary,
    FileOpenInput,
    FileOpenOutput,
    FileOpenAppend,
    FileOpenRandom,
    FileRecordStart,
    FileRecordEnd,
    FilePrint,
    FileClose,
    FileCloseAll,
    FileSeek,
    FileGetByte,
    FileGetInteger,
    FileGetLong,
    FileGetLongLong,
    FileGetSingle,
    FileGetDouble,
    FileGetCurrency,
    FileGetBoolean,
    FileGetString,
    FileGetRawByte,
    FileGetRawInteger,
    FileGetRawLong,
    FileGetRawLongLong,
    FileGetRawSingle,
    FileGetRawDouble,
    FileGetRawCurrency,
    FileGetRawBoolean,
    FileGetRawString,
    FileGetRawFixedString,
    FileGetDynamicArray,
    FilePut,
    FilePutRaw,
    FilePutRawFixedString,
    FilePutDynamicArrayDescriptor,
    FileLineInput,
    FileInputField,
    FileInput,
    FileFreeFile,
    FileLength,
    FileEndOfFile,
    FilePosition,
    FileKill,
    FileDir,
    FileLengthByPath,

    InteractionDoEvents,
    InteractionMsgBox,
    InteractionInputBox,
    InteractionLoad,
    InteractionUnload,
    InteractionCreateObject,
    InteractionGetObject,
    InteractionShell,
    InteractionCommand,
    MemoryVarPtr,
    MemoryObjPtr,
    MemoryStrPtr,
    MemoryLSet,
    CollectionCreate,
    CollectionEnumerateValues,
    ControlEnumerateValues,
    CollectionCount,
    CollectionItem,
    CollectionAdd,
    CollectionRemove,
    DateTimeNow,
    DateTimeValue,
    TimeDateValue,
    DateTimeYear,
    DateTimeMonth,
    DateTimeDay,
    DateTimeHour,
    DateTimeMinute,
    DateTimeSecond,
    DateTimeTimer,
    DateTimeSerial,
    TimeDateSerial,
    DateTimeAdd,
    DateTimeDiff,
    DateTimePart,
    DateTimeWeekday,
    DateTimeWeekdayName,
    DateTimeMonthName,
    ErrorNumber,
    ErrorDescription,
    ErrorSource,
    ErrorLineNumber,
    ErrorClear,
    ErrorRaise,
    FunctionTypeName,
    FunctionSwitch,
    FunctionIIf,
    FunctionRGB,
    ObjectIs,
    DynamicGetMember,
    DynamicGetIndexedMember,
    DynamicSetMember,
    DynamicSetIndexedMember,
    DynamicInvokeMember,
    InteractionGetSetting,
    InteractionSaveSetting,
    InteractionSendKeys,
    InteractionPopupMenu,
    InteractionLoadPicture,
    InteractionPropertyChanged,
    InteractionScaleX,
    InteractionScaleY,
    InteractionTextWidth,
    InteractionTextHeight,
    InteractionPrint,
    InteractionPaintPicture,

    ArrayClear,
    ArrayLBound,
    ArrayUBound,
    ArrayIsAllocated,
    ArrayRequireAllocated,
    ArrayEnumerateValues,

    FixedStringRead,
    FixedStringWrite
}
