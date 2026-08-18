using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

public sealed class Binder
{
    private readonly SourceText _text;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

    public Binder(SourceText text)
    {
        _text = text;
    }

    public static ProcedureSymbol CreateProcedureSymbol(SubDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        var parameters = declaration.Parameters
            .Select(parameter => new ParameterSymbol(
                parameter.Identifier.Text,
                TypeSymbol.Lookup(parameter.TypeToken.Text) ?? TypeSymbol.Error,
                parameter.PassingModeKeyword?.Kind == SyntaxKind.ByValKeyword
                    ? ParameterPassingMode.ByVal
                    : ParameterPassingMode.ByRef))
            .ToImmutableArray();

        return new ProcedureSymbol(declaration.Identifier.Text, parameters);
    }

    public SemanticModel BindCompilationUnit(CompilationUnitSyntax root)
    {
        var procedures = DeclareProcedures(root);
        return BindCompilationUnit(root, procedures);
    }

    public SemanticModel BindCompilationUnit(
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, ProcedureSymbol> availableProcedures)
    {
        ArgumentNullException.ThrowIfNull(availableProcedures);

        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        foreach (var declaration in root.Members.OfType<SubDeclarationSyntax>())
        {
            if (!availableProcedures.TryGetValue(declaration.Identifier.Text, out var symbol))
            {
                symbol = CreateProcedureSymbol(declaration);
            }

            procedures.Add(BindProcedure(declaration, symbol, availableProcedures));
        }

        return new SemanticModel(procedures.ToImmutable(), _diagnostics.ToImmutable());
    }

    private Dictionary<string, ProcedureSymbol> DeclareProcedures(CompilationUnitSyntax root)
    {
        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in root.Members.OfType<SubDeclarationSyntax>())
        {
            var symbol = CreateProcedureSymbol(declaration);
            if (!procedures.TryAdd(symbol.Name, symbol))
            {
                Report(
                    "VB6S0004",
                    $"Procedure '{declaration.Identifier.Text}' is already declared.",
                    declaration.Identifier.Span);
            }
        }

        return procedures;
    }

    private BoundProcedure BindProcedure(
        SubDeclarationSyntax declaration,
        ProcedureSymbol symbol,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var variables = new Dictionary<string, VariableSymbol>(StringComparer.OrdinalIgnoreCase);
        var locals = new Dictionary<string, LocalVariableSymbol>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < declaration.Parameters.Length; index++)
        {
            var syntax = declaration.Parameters[index];
            var parameter = index < symbol.Parameters.Length
                ? symbol.Parameters[index]
                : new ParameterSymbol(syntax.Identifier.Text, TypeSymbol.Error, ParameterPassingMode.ByRef);

            if (parameter.Type == TypeSymbol.Error)
            {
                Report(
                    "VB6S0003",
                    $"Unknown type '{syntax.TypeToken.Text}'.",
                    syntax.TypeToken.Span);
            }

            if (!variables.TryAdd(parameter.Name, parameter))
            {
                Report(
                    "VB6S0009",
                    $"Parameter '{parameter.Name}' is already declared.",
                    syntax.Identifier.Span);
            }
        }

        PredeclareLocals(declaration.Statements, locals, variables);
        var body = BindStatements(declaration.Statements, variables, procedures);

        return new BoundProcedure(symbol, locals.Values.ToImmutableArray(), body);
    }

    private void PredeclareLocals(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, LocalVariableSymbol> locals,
        Dictionary<string, VariableSymbol> variables)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DimStatementSyntax dim:
                {
                    var type = TypeSymbol.Lookup(dim.TypeToken.Text);
                    if (type is null)
                    {
                        Report(
                            "VB6S0003",
                            $"Unknown type '{dim.TypeToken.Text}'.",
                            dim.TypeToken.Span);
                        type = TypeSymbol.Error;
                    }

                    var variable = new LocalVariableSymbol(dim.Identifier.Text, type);
                    if (!variables.TryAdd(variable.Name, variable))
                    {
                        Report(
                            "VB6S0002",
                            $"Local variable '{variable.Name}' is already declared.",
                            dim.Identifier.Span);
                        break;
                    }

                    locals.Add(variable.Name, variable);
                    break;
                }

                case IfStatementSyntax ifStatement:
                    PredeclareLocals(ifStatement.Statements, locals, variables);
                    break;
            }
        }
    }

    private BoundBlockStatement BindStatements(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var bound = ImmutableArray.CreateBuilder<BoundStatement>();

        foreach (var statement in statements)
        {
            var boundStatement = BindStatement(statement, variables, procedures);
            if (boundStatement is not null)
            {
                bound.Add(boundStatement);
            }
        }

        return new BoundBlockStatement(bound.ToImmutable());
    }

    private BoundStatement? BindStatement(
        StatementSyntax statement,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return statement switch
        {
            DimStatementSyntax dim => BindVariableDeclaration(dim, variables),
            AssignmentStatementSyntax assignment => BindAssignment(assignment, variables),
            IfStatementSyntax ifStatement => BindIf(ifStatement, variables, procedures),
            DebugPrintStatementSyntax debugPrint =>
                new BoundDebugPrintStatement(BindExpression(debugPrint.Expression, variables)),
            InvocationStatementSyntax invocation => BindInvocation(invocation, variables, procedures),
            SkippedStatementSyntax => null,
            _ => null
        };
    }

    private BoundVariableDeclarationStatement BindVariableDeclaration(
        DimStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        if (!variables.TryGetValue(syntax.Identifier.Text, out var variable) ||
            variable is not LocalVariableSymbol local)
        {
            local = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
        }

        return new BoundVariableDeclarationStatement(local);
    }

    private BoundAssignmentStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        var expression = BindExpression(syntax.Expression, variables);

        if (!variables.TryGetValue(syntax.Identifier.Text, out var variable))
        {
            Report(
                "VB6S0001",
                $"Variable '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);

            variable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
            return new BoundAssignmentStatement(variable, expression);
        }

        return new BoundAssignmentStatement(variable, BindConversion(expression, variable.Type));
    }

    private BoundIfStatement BindIf(
        IfStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var condition = BindExpression(syntax.Condition, variables);
        condition = BindConversion(condition, TypeSymbol.Boolean);
        var body = BindStatements(syntax.Statements, variables, procedures);
        return new BoundIfStatement(condition, body);
    }

    private BoundInvocationStatement BindInvocation(
        InvocationStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!procedures.TryGetValue(syntax.Identifier.Text, out var procedure))
        {
            Report(
                "VB6S0005",
                $"Procedure '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);

            var unknownArguments = syntax.Arguments
                .Select(argument => new BoundArgument(null, BindExpression(argument, variables)))
                .ToImmutableArray();
            return new BoundInvocationStatement(new ProcedureSymbol(syntax.Identifier.Text), unknownArguments);
        }

        if (syntax.Arguments.Length != procedure.Parameters.Length)
        {
            Report(
                "VB6S0006",
                $"Procedure '{procedure.Name}' expects {procedure.Parameters.Length} argument(s), but {syntax.Arguments.Length} were supplied.",
                syntax.Identifier.Span);
        }

        var arguments = ImmutableArray.CreateBuilder<BoundArgument>();
        for (var index = 0; index < syntax.Arguments.Length; index++)
        {
            var expression = BindExpression(syntax.Arguments[index], variables);
            var parameter = index < procedure.Parameters.Length ? procedure.Parameters[index] : null;

            if (parameter is not null)
            {
                if (parameter.PassingMode == ParameterPassingMode.ByVal)
                {
                    expression = BindConversion(expression, parameter.Type);
                }
                else if (expression is not BoundVariableExpression variableExpression)
                {
                    Report(
                        "VB6S0007",
                        $"ByRef argument for parameter '{parameter.Name}' must be a variable in the current compiler subset.",
                        syntax.Identifier.Span);
                }
                else if (variableExpression.Variable.Type != parameter.Type &&
                         variableExpression.Variable.Type != TypeSymbol.Error &&
                         parameter.Type != TypeSymbol.Error)
                {
                    Report(
                        "VB6S0008",
                        $"ByRef argument type '{variableExpression.Variable.Type.Name}' does not match parameter type '{parameter.Type.Name}'.",
                        syntax.Identifier.Span);
                }
            }

            arguments.Add(new BoundArgument(parameter, expression));
        }

        return new BoundInvocationStatement(procedure, arguments.ToImmutable());
    }

    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        return syntax switch
        {
            LiteralExpressionSyntax literal => BindLiteral(literal),
            NameExpressionSyntax name => BindName(name, variables),
            UnaryExpressionSyntax unary => BindUnary(unary, variables),
            BinaryExpressionSyntax binary => BindBinary(binary, variables),
            ParenthesizedExpressionSyntax parenthesized => BindExpression(parenthesized.Expression, variables),
            _ => new BoundErrorExpression()
        };
    }

    private static BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        return syntax.LiteralToken.Kind switch
        {
            SyntaxKind.IntegerLiteralToken =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.Integer),
            SyntaxKind.StringLiteralToken =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.String),
            _ => new BoundErrorExpression()
        };
    }

    private BoundExpression BindName(
        NameExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        if (variables.TryGetValue(syntax.IdentifierToken.Text, out var variable))
        {
            return new BoundVariableExpression(variable);
        }

        Report(
            "VB6S0001",
            $"Variable '{syntax.IdentifierToken.Text}' is not declared.",
            syntax.IdentifierToken.Span);
        return new BoundErrorExpression();
    }

    private BoundExpression BindUnary(
        UnaryExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        var operand = BindExpression(syntax.Operand, variables);
        if (operand.Type == TypeSymbol.Error)
        {
            return operand;
        }

        operand = BindConversion(operand, TypeSymbol.Integer);
        return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Integer);
    }

    private BoundExpression BindBinary(
        BinaryExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        var left = BindExpression(syntax.Left, variables);
        var right = BindExpression(syntax.Right, variables);

        if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        {
            return new BoundErrorExpression();
        }

        switch (syntax.OperatorToken.Kind)
        {
            case SyntaxKind.EqualsToken:
            case SyntaxKind.LessGreaterToken:
            case SyntaxKind.LessToken:
            case SyntaxKind.LessOrEqualsToken:
            case SyntaxKind.GreaterToken:
            case SyntaxKind.GreaterOrEqualsToken:
                if (left.Type != right.Type)
                {
                    right = BindConversion(right, left.Type);
                }

                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Boolean);

            case SyntaxKind.AmpersandToken:
                left = BindConversion(left, TypeSymbol.String);
                right = BindConversion(right, TypeSymbol.String);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.String);

            case SyntaxKind.PlusToken when left.Type == TypeSymbol.String && right.Type == TypeSymbol.String:
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.String);

            case SyntaxKind.SlashToken:
                left = BindConversion(left, TypeSymbol.Double);
                right = BindConversion(right, TypeSymbol.Double);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Double);

            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.StarToken:
            case SyntaxKind.BackslashToken:
                left = BindConversion(left, TypeSymbol.Integer);
                right = BindConversion(right, TypeSymbol.Integer);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Integer);

            default:
                return new BoundErrorExpression();
        }
    }

    private static BoundExpression BindConversion(BoundExpression expression, TypeSymbol targetType)
    {
        if (expression.Type == TypeSymbol.Error || targetType == TypeSymbol.Error || expression.Type == targetType)
        {
            return expression;
        }

        return new BoundConversionExpression(targetType, expression);
    }

    private void Report(string code, string message, TextSpan span)
    {
        _diagnostics.Add(new Diagnostic(
            code,
            DiagnosticSeverity.Error,
            message,
            span,
            _text.FilePath));
    }
}
