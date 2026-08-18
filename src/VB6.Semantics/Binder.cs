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
                symbol = new ProcedureSymbol(declaration.Identifier.Text);
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
            var symbol = new ProcedureSymbol(declaration.Identifier.Text);
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
        var locals = new Dictionary<string, LocalVariableSymbol>(StringComparer.OrdinalIgnoreCase);

        PredeclareLocals(declaration.Statements, locals);
        var body = BindStatements(declaration.Statements, locals, procedures);

        return new BoundProcedure(symbol, locals.Values.ToImmutableArray(), body);
    }

    private void PredeclareLocals(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, LocalVariableSymbol> locals)
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
                    if (!locals.TryAdd(variable.Name, variable))
                    {
                        Report(
                            "VB6S0002",
                            $"Local variable '{variable.Name}' is already declared.",
                            dim.Identifier.Span);
                    }

                    break;
                }

                case IfStatementSyntax ifStatement:
                    PredeclareLocals(ifStatement.Statements, locals);
                    break;
            }
        }
    }

    private BoundBlockStatement BindStatements(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, LocalVariableSymbol> locals,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var bound = ImmutableArray.CreateBuilder<BoundStatement>();

        foreach (var statement in statements)
        {
            var boundStatement = BindStatement(statement, locals, procedures);
            if (boundStatement is not null)
            {
                bound.Add(boundStatement);
            }
        }

        return new BoundBlockStatement(bound.ToImmutable());
    }

    private BoundStatement? BindStatement(
        StatementSyntax statement,
        Dictionary<string, LocalVariableSymbol> locals,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return statement switch
        {
            DimStatementSyntax dim => BindVariableDeclaration(dim, locals),
            AssignmentStatementSyntax assignment => BindAssignment(assignment, locals),
            IfStatementSyntax ifStatement => BindIf(ifStatement, locals, procedures),
            DebugPrintStatementSyntax debugPrint =>
                new BoundDebugPrintStatement(BindExpression(debugPrint.Expression, locals)),
            InvocationStatementSyntax invocation => BindInvocation(invocation, procedures),
            SkippedStatementSyntax => null,
            _ => null
        };
    }

    private BoundVariableDeclarationStatement BindVariableDeclaration(
        DimStatementSyntax syntax,
        Dictionary<string, LocalVariableSymbol> locals)
    {
        if (!locals.TryGetValue(syntax.Identifier.Text, out var variable))
        {
            variable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
        }

        return new BoundVariableDeclarationStatement(variable);
    }

    private BoundAssignmentStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        Dictionary<string, LocalVariableSymbol> locals)
    {
        var expression = BindExpression(syntax.Expression, locals);

        if (!locals.TryGetValue(syntax.Identifier.Text, out var variable))
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
        Dictionary<string, LocalVariableSymbol> locals,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var condition = BindExpression(syntax.Condition, locals);
        condition = BindConversion(condition, TypeSymbol.Boolean);
        var body = BindStatements(syntax.Statements, locals, procedures);
        return new BoundIfStatement(condition, body);
    }

    private BoundInvocationStatement BindInvocation(
        InvocationStatementSyntax syntax,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (procedures.TryGetValue(syntax.Identifier.Text, out var procedure))
        {
            return new BoundInvocationStatement(procedure);
        }

        Report(
            "VB6S0005",
            $"Procedure '{syntax.Identifier.Text}' is not declared.",
            syntax.Identifier.Span);
        return new BoundInvocationStatement(new ProcedureSymbol(syntax.Identifier.Text));
    }

    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        Dictionary<string, LocalVariableSymbol> locals)
    {
        return syntax switch
        {
            LiteralExpressionSyntax literal => BindLiteral(literal),
            NameExpressionSyntax name => BindName(name, locals),
            UnaryExpressionSyntax unary => BindUnary(unary, locals),
            BinaryExpressionSyntax binary => BindBinary(binary, locals),
            ParenthesizedExpressionSyntax parenthesized => BindExpression(parenthesized.Expression, locals),
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
        Dictionary<string, LocalVariableSymbol> locals)
    {
        if (locals.TryGetValue(syntax.IdentifierToken.Text, out var variable))
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
        Dictionary<string, LocalVariableSymbol> locals)
    {
        var operand = BindExpression(syntax.Operand, locals);
        if (operand.Type == TypeSymbol.Error)
        {
            return operand;
        }

        operand = BindConversion(operand, TypeSymbol.Integer);
        return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Integer);
    }

    private BoundExpression BindBinary(
        BinaryExpressionSyntax syntax,
        Dictionary<string, LocalVariableSymbol> locals)
    {
        var left = BindExpression(syntax.Left, locals);
        var right = BindExpression(syntax.Right, locals);

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
