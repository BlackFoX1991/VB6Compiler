using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax.Text;

namespace VB6.IR;

public sealed record IrSourceLocation(string FilePath, TextSpan Span);

public abstract record IrNode
{
    public IrSourceLocation? SourceLocation { get; init; }
}

public sealed record IrProgram(
    ImmutableArray<IrModule> Modules,
    ImmutableArray<IrTypeDefinition> TypeDefinitions,
    IrProcedure? EntryPoint) : IrNode;

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
    bool IsCompilerGenerated = false) : IrNode;

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

public abstract record IrTerminator : IrNode;

public sealed record IrGotoTerminator(int TargetBlockId) : IrTerminator;

public sealed record IrConditionalTerminator(
    IrExpression Condition,
    int TrueBlockId,
    int FalseBlockId) : IrTerminator;

public sealed record IrReturnTerminator(IrExpression? Value) : IrTerminator;

public abstract record IrPlace(TypeSymbol Type) : IrNode;

public sealed record IrLocalPlace(IrLocal Local) : IrPlace(Local.Type);

public sealed record IrParameterPlace(IrParameter Parameter) : IrPlace(Parameter.Type);

public sealed record IrGlobalPlace(IrGlobal Global) : IrPlace(Global.Type);

public sealed record IrFieldPlace(
    IrPlace Receiver,
    IrField Field) : IrPlace(Field.Type);

public sealed record IrArrayElementPlace(
    IrExpression Array,
    ImmutableArray<IrExpression> Indices,
    TypeSymbol ElementType) : IrPlace(ElementType);

public sealed record IrIndirectPlace(
    IrExpression Address,
    TypeSymbol ElementType) : IrPlace(ElementType);

public sealed record IrAccessorPlace(
    IrExpression? Receiver,
    IrProcedure Getter,
    IrProcedure? Setter,
    TypeSymbol ValueType) : IrPlace(ValueType);

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

public sealed record IrRuntimeCallExpression(
    IrRuntimeMethod Method,
    ImmutableArray<IrCallArgument> Arguments,
    TypeSymbol ResultType)
    : IrExpression(ResultType);

public sealed record IrProcedureCallExpression(
    ProcedureSymbol Procedure,
    ImmutableArray<IrCallArgument> Arguments,
    TypeSymbol ResultType)
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

public sealed record IrReDimPreserveExpression(
    IrExpression Array,
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
    CLngLng,
    CCur,
    CSng,
    CDbl,
    CBool,
    CStr,

    AddByte,
    AddInteger,
    AddLong,
    AddLongLong,
    AddCurrency,
    AddSingle,
    AddDouble,
    SubtractByte,
    SubtractInteger,
    SubtractLong,
    SubtractLongLong,
    SubtractCurrency,
    SubtractSingle,
    SubtractDouble,
    MultiplyByte,
    MultiplyInteger,
    MultiplyLong,
    MultiplyLongLong,
    MultiplyCurrency,
    MultiplySingle,
    MultiplyDouble,
    MultiplyVariant,
    NegateInteger,
    NegateLong,
    NegateLongLong,
    NegateCurrency,
    NegateSingle,
    NegateDouble,
    IntegerDivideByte,
    IntegerDivideInteger,
    IntegerDivideLong,
    IntegerDivideLongLong,
    ModByte,
    ModInteger,
    ModLong,
    ModLongLong,
    DivideSingle,
    DivideDouble,
    Power,
    NotBoolean,
    NotInteger,
    NotLong,
    NotLongLong,
    AndBoolean,
    AndByte,
    AndInteger,
    AndLong,
    AndLongLong,
    OrBoolean,
    OrByte,
    OrInteger,
    OrLong,
    OrLongLong,
    XorBoolean,
    XorByte,
    XorInteger,
    XorLong,
    XorLongLong,
    EqvBoolean,
    EqvInteger,
    EqvLong,
    EqvLongLong,
    ImpBoolean,
    ImpInteger,
    ImpLong,
    ImpLongLong,
    Concat,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,

    DebugPrint,

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
    StringIsNumeric,

    FileOpenBinary,
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
    FilePut,
    FileFreeFile,
    FileLength,
    FileEndOfFile,
    FilePosition,

    ArrayClear,
    ArrayLBound,
    ArrayUBound,
    ArrayEnumerateValues
}
