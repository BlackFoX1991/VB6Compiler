using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;

namespace VB6.Semantics;

public abstract record Symbol(string Name);

public sealed record TypeSymbol(string Name) : Symbol(Name)
{
    public static readonly TypeSymbol Error = new("<error>");
    public static readonly TypeSymbol Integer = new("Integer");
    public static readonly TypeSymbol String = new("String");
    public static readonly TypeSymbol Boolean = new("Boolean");
    public static readonly TypeSymbol Double = new("Double");

    public static TypeSymbol? Lookup(string name) => name.ToUpperInvariant() switch
    {
        "INTEGER" => Integer,
        "STRING" => String,
        "BOOLEAN" => Boolean,
        "DOUBLE" => Double,
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

public enum BoundNodeKind
{
    BlockStatement,
    VariableDeclarationStatement,
    AssignmentStatement,
    IfStatement,
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

public sealed record BoundIfStatement(BoundExpression Condition, BoundBlockStatement Body)
    : BoundStatement(BoundNodeKind.IfStatement);

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

public sealed record SemanticModel(
    ImmutableArray<BoundProcedure> Procedures,
    ImmutableArray<Diagnostic> Diagnostics);
