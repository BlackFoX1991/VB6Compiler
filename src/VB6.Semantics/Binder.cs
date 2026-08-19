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
    private readonly List<LoopBindingContext> _loopStack = new();
    private int _nextLoopId;
    private int _nextSelectId;

    public Binder(SourceText text)
    {
        _text = text;
    }

    public static ProcedureSymbol CreateProcedureSymbol(SubDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(declaration.Identifier.Text, CreateParameterSymbols(declaration.Parameters));
    }

    public static ProcedureSymbol CreateProcedureSymbol(FunctionDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbols(declaration.Parameters),
            TypeSymbol.Lookup(declaration.ReturnTypeToken.Text) ?? TypeSymbol.Error);
    }

    /// <summary>
    /// Module-level variables declared by a compilation unit. Exposed so that a project
    /// compilation can pre-declare them across modules, the way procedures already are.
    /// </summary>
    public static ImmutableArray<ModuleVariableSymbol> CreateModuleVariableSymbols(
        SourceText text,
        CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return new Binder(text).DeclareModuleVariables(root).Scope.Values.ToImmutableArray();
    }

    public SemanticModel BindCompilationUnit(CompilationUnitSyntax root)
    {
        var procedures = DeclareProcedures(root);
        return BindCompilationUnit(root, procedures);
    }

    public SemanticModel BindCompilationUnit(
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, ProcedureSymbol> availableProcedures,
        IReadOnlyDictionary<string, ModuleVariableSymbol>? availableModuleVariables = null)
    {
        ArgumentNullException.ThrowIfNull(availableProcedures);

        var declared = DeclareModuleVariables(root);
        var moduleVariables = availableModuleVariables ?? declared.Scope;
        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        foreach (var member in root.Members)
        {
            switch (member)
            {
                case SubDeclarationSyntax declaration:
                {
                    var symbol = ResolveProcedureSymbol(declaration.Identifier.Text, declaration, availableProcedures);
                    procedures.Add(BindProcedure(
                        declaration.Identifier,
                        declaration.Parameters,
                        declaration.Statements,
                        null,
                        symbol,
                        availableProcedures,
                        moduleVariables));
                    break;
                }

                case FunctionDeclarationSyntax declaration:
                {
                    var symbol = ResolveProcedureSymbol(declaration.Identifier.Text, declaration, availableProcedures);
                    if (symbol.ReturnType == TypeSymbol.Error)
                    {
                        Report(
                            "VB6S0011",
                            $"Unknown function return type '{declaration.ReturnTypeToken.Text}'.",
                            declaration.ReturnTypeToken.Span);
                    }

                    procedures.Add(BindProcedure(
                        declaration.Identifier,
                        declaration.Parameters,
                        declaration.Statements,
                        declaration.ReturnTypeToken,
                        symbol,
                        availableProcedures,
                        moduleVariables));
                    break;
                }
            }
        }

        return new SemanticModel(procedures.ToImmutable(), _diagnostics.ToImmutable())
        {
            ModuleVariables = declared.Bound
        };
    }

    private static ImmutableArray<ParameterSymbol> CreateParameterSymbols(ImmutableArray<ParameterSyntax> parameters) =>
        parameters
            .Select(parameter => new ParameterSymbol(
                parameter.Identifier.Text,
                parameter.IsArray
                    ? TypeSymbol.Error
                    : TypeSymbol.Lookup(parameter.TypeToken.Text) ?? TypeSymbol.Error,
                parameter.PassingModeKeyword?.Kind == SyntaxKind.ByValKeyword
                    ? ParameterPassingMode.ByVal
                    : ParameterPassingMode.ByRef))
            .ToImmutableArray();

    private ProcedureSymbol ResolveProcedureSymbol(
        string name,
        MemberSyntax declaration,
        IReadOnlyDictionary<string, ProcedureSymbol> availableProcedures)
    {
        if (availableProcedures.TryGetValue(name, out var symbol))
        {
            return symbol;
        }

        return declaration switch
        {
            SubDeclarationSyntax sub => CreateProcedureSymbol(sub),
            FunctionDeclarationSyntax function => CreateProcedureSymbol(function),
            _ => new ProcedureSymbol(name)
        };
    }

    private Dictionary<string, ProcedureSymbol> DeclareProcedures(CompilationUnitSyntax root)
    {
        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in root.Members)
        {
            ProcedureSymbol? symbol = null;
            SyntaxToken? identifier = null;

            switch (member)
            {
                case SubDeclarationSyntax sub:
                    symbol = CreateProcedureSymbol(sub);
                    identifier = sub.Identifier;
                    break;
                case FunctionDeclarationSyntax function:
                    symbol = CreateProcedureSymbol(function);
                    identifier = function.Identifier;
                    break;
            }

            if (symbol is null || identifier is null)
            {
                continue;
            }

            if (!procedures.TryAdd(symbol.Name, symbol))
            {
                Report(
                    "VB6S0004",
                    $"Procedure '{identifier.Text}' is already declared.",
                    identifier.Span);
            }
        }

        return procedures;
    }

    /// <summary>
    /// Declares a parameter or local in the procedure scope. A name already taken by a module
    /// variable is shadowed, which is what VB6 does; a clash with another parameter or local is
    /// a real redeclaration and fails.
    /// </summary>
    private static bool TryDeclareInProcedureScope(
        Dictionary<string, VariableSymbol> variables,
        string name,
        VariableSymbol symbol)
    {
        if (variables.TryGetValue(name, out var existing) && existing is not ModuleVariableSymbol)
        {
            return false;
        }

        variables[name] = symbol;
        return true;
    }

    /// <summary>
    /// Collects the module-level declarations of one compilation unit. Constants are bound in
    /// declaration order against the scope built so far, so a constant may refer to one declared
    /// above it.
    /// </summary>
    private (Dictionary<string, ModuleVariableSymbol> Scope, ImmutableArray<BoundModuleVariable> Bound)
        DeclareModuleVariables(CompilationUnitSyntax root)
    {
        var scope = new Dictionary<string, ModuleVariableSymbol>(StringComparer.OrdinalIgnoreCase);
        var bound = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        var noProcedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in root.Members)
        {
            switch (member)
            {
                case ModuleVariableDeclarationSyntax declaration:
                {
                    foreach (var declarator in declaration.Declarators)
                    {
                        var type = ResolveVariableDeclaratorType(declarator);
                        var symbol = new ModuleVariableSymbol(declarator.Identifier.Text, type);
                        if (TryDeclareModuleVariable(scope, symbol, declarator.Identifier))
                        {
                            bound.Add(new BoundModuleVariable(symbol, null, IsConstant: false));
                        }
                    }

                    break;
                }

                case ConstDeclarationSyntax declaration:
                {
                    var visible = scope.ToDictionary(
                        entry => entry.Key,
                        entry => (VariableSymbol)entry.Value,
                        StringComparer.OrdinalIgnoreCase);
                    var value = BindExpression(declaration.Value, visible, noProcedures);

                    // Without 'As' the constant takes the type of its value.
                    var type = declaration.TypeToken is null
                        ? value.Type
                        : ResolveDeclaredType(declaration.TypeToken);
                    var symbol = new ModuleVariableSymbol(declaration.Identifier.Text, type);
                    if (TryDeclareModuleVariable(scope, symbol, declaration.Identifier))
                    {
                        bound.Add(new BoundModuleVariable(
                            symbol,
                            BindConversion(value, type),
                            IsConstant: true));
                    }

                    break;
                }
            }
        }

        return (scope, bound.ToImmutable());
    }

    private TypeSymbol ResolveVariableDeclaratorType(VariableDeclaratorSyntax declarator)
    {
        if (declarator.IsArray)
        {
            Report(
                "VB6S0025",
                $"Array variable '{declarator.Identifier.Text}' requires array type semantics, which are not implemented yet.",
                declarator.Identifier.Span);
            return TypeSymbol.Error;
        }

        if (declarator.TypeToken is not null)
        {
            return ResolveDeclaredType(declarator.TypeToken);
        }

        Report(
            "VB6S0020",
            $"Variable '{declarator.Identifier.Text}' has implicit Variant type, which is not supported yet.",
            declarator.Identifier.Span);
        return TypeSymbol.Error;
    }

    private TypeSymbol ResolveDeclaredType(SyntaxToken typeToken)
    {
        var type = TypeSymbol.Lookup(typeToken.Text);
        if (type is not null)
        {
            return type;
        }

        Report("VB6S0003", $"Unknown type '{typeToken.Text}'.", typeToken.Span);
        return TypeSymbol.Error;
    }

    private bool TryDeclareModuleVariable(
        Dictionary<string, ModuleVariableSymbol> scope,
        ModuleVariableSymbol symbol,
        SyntaxToken identifier)
    {
        if (scope.TryAdd(symbol.Name, symbol))
        {
            return true;
        }

        Report(
            "VB6S0019",
            $"Module variable '{symbol.Name}' is already declared.",
            identifier.Span);
        return false;
    }

    private BoundProcedure BindProcedure(
        SyntaxToken identifier,
        ImmutableArray<ParameterSyntax> parameterSyntaxes,
        ImmutableArray<StatementSyntax> statements,
        SyntaxToken? returnTypeSyntax,
        ProcedureSymbol symbol,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        IReadOnlyDictionary<string, ModuleVariableSymbol> moduleVariables)
    {
        var variables = new Dictionary<string, VariableSymbol>(StringComparer.OrdinalIgnoreCase);

        // Module scope is the outermost scope; parameters and locals shadow it.
        foreach (var moduleVariable in moduleVariables)
        {
            variables[moduleVariable.Key] = moduleVariable.Value;
        }

        var locals = new Dictionary<string, LocalVariableSymbol>(StringComparer.OrdinalIgnoreCase);

        if (symbol.IsFunction)
        {
            variables.Add(symbol.Name, new ReturnValueSymbol(symbol.Name, symbol.ReturnType ?? TypeSymbol.Error));
        }

        for (var index = 0; index < parameterSyntaxes.Length; index++)
        {
            var syntax = parameterSyntaxes[index];
            var parameter = index < symbol.Parameters.Length
                ? symbol.Parameters[index]
                : new ParameterSymbol(syntax.Identifier.Text, TypeSymbol.Error, ParameterPassingMode.ByRef);

            if (syntax.IsArray)
            {
                Report(
                    "VB6S0025",
                    $"Array parameter '{syntax.Identifier.Text}' requires array type semantics, which are not implemented yet.",
                    syntax.Identifier.Span);
            }
            else if (parameter.Type == TypeSymbol.Error)
            {
                Report(
                    "VB6S0003",
                    $"Unknown type '{syntax.TypeToken.Text}'.",
                    syntax.TypeToken.Span);
            }

            if (!TryDeclareInProcedureScope(variables, parameter.Name, parameter))
            {
                Report(
                    "VB6S0009",
                    $"Parameter '{parameter.Name}' is already declared.",
                    syntax.Identifier.Span);
            }
        }

        PredeclareLocals(statements, locals, variables);
        var body = BindStatements(statements, variables, procedures);

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
                    PredeclareLocalDeclarators(dim.Declarators, locals, variables);
                    break;
                case StaticStatementSyntax staticStatement:
                    PredeclareLocalDeclarators(staticStatement.Declarators, locals, variables);
                    break;
                case IfStatementSyntax ifStatement:
                    PredeclareLocals(ifStatement.Statements, locals, variables);
                    foreach (var elseIfClause in ifStatement.ElseIfClauses)
                    {
                        PredeclareLocals(elseIfClause.Statements, locals, variables);
                    }
                    PredeclareLocals(ifStatement.ElseStatements, locals, variables);
                    break;
                case ForStatementSyntax forStatement:
                    PredeclareLocals(forStatement.Statements, locals, variables);
                    break;
                case WhileStatementSyntax whileStatement:
                    PredeclareLocals(whileStatement.Statements, locals, variables);
                    break;
                case DoStatementSyntax doStatement:
                    PredeclareLocals(doStatement.Statements, locals, variables);
                    break;
                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        PredeclareLocals(caseBlock.Statements, locals, variables);
                    }
                    break;
            }
        }
    }

    private void PredeclareLocalDeclarators(
        ImmutableArray<VariableDeclaratorSyntax> declarators,
        Dictionary<string, LocalVariableSymbol> locals,
        Dictionary<string, VariableSymbol> variables)
    {
        foreach (var declarator in declarators)
        {
            var type = ResolveVariableDeclaratorType(declarator);
            var variable = new LocalVariableSymbol(declarator.Identifier.Text, type);
            if (!TryDeclareInProcedureScope(variables, variable.Name, variable))
            {
                Report(
                    "VB6S0002",
                    $"Local variable '{variable.Name}' is already declared.",
                    declarator.Identifier.Span);
                continue;
            }

            locals.Add(variable.Name, variable);
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
            if (statement is DimStatementSyntax dim)
            {
                foreach (var declarator in dim.Declarators)
                {
                    bound.Add(BindVariableDeclaration(declarator, variables));
                }
                continue;
            }

            if (statement is StaticStatementSyntax staticStatement)
            {
                Report(
                    "VB6S0021",
                    "Static local lifetime semantics are not implemented yet.",
                    staticStatement.StaticKeyword.Span);
                continue;
            }

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
            AssignmentStatementSyntax assignment => BindAssignment(assignment, variables, procedures),
            IfStatementSyntax ifStatement => BindIf(ifStatement, variables, procedures),
            ForStatementSyntax forStatement => BindFor(forStatement, variables, procedures),
            WhileStatementSyntax whileStatement => BindWhile(whileStatement, variables, procedures),
            DoStatementSyntax doStatement => BindDo(doStatement, variables, procedures),
            ExitStatementSyntax exitStatement => BindExit(exitStatement),
            SelectCaseStatementSyntax selectStatement => BindSelectCase(selectStatement, variables, procedures),
            DebugPrintStatementSyntax debugPrint =>
                new BoundDebugPrintStatement(BindExpression(debugPrint.Expression, variables, procedures)),
            InvocationStatementSyntax invocation => BindInvocation(invocation, variables, procedures),
            SkippedStatementSyntax => null,
            _ => null
        };
    }

    private BoundVariableDeclarationStatement BindVariableDeclaration(
        VariableDeclaratorSyntax syntax,
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
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);

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
        var condition = BindConversion(
            BindExpression(syntax.Condition, variables, procedures),
            TypeSymbol.Boolean);
        var body = BindStatements(syntax.Statements, variables, procedures);
        var elseIfClauses = ImmutableArray.CreateBuilder<BoundElseIfClause>();

        foreach (var clause in syntax.ElseIfClauses)
        {
            var elseIfCondition = BindConversion(
                BindExpression(clause.Condition, variables, procedures),
                TypeSymbol.Boolean);
            elseIfClauses.Add(new BoundElseIfClause(
                elseIfCondition,
                BindStatements(clause.Statements, variables, procedures)));
        }

        var elseBody = syntax.ElseKeyword is null
            ? null
            : BindStatements(syntax.ElseStatements, variables, procedures);

        return new BoundIfStatement(condition, body, elseIfClauses.ToImmutable(), elseBody);
    }

    private BoundForStatement BindFor(
        ForStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!variables.TryGetValue(syntax.Identifier.Text, out var controlVariable))
        {
            Report(
                "VB6S0001",
                $"Variable '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);
            controlVariable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
        }

        if (controlVariable.Type != TypeSymbol.Integer &&
            controlVariable.Type != TypeSymbol.Long &&
            controlVariable.Type != TypeSymbol.LongLong &&
            controlVariable.Type != TypeSymbol.Error)
        {
            Report(
                "VB6S0012",
                $"For control variable '{controlVariable.Name}' must be Integer, Long, or LongLong in the current compiler subset.",
                syntax.Identifier.Span);
        }

        if (syntax.NextIdentifier is not null &&
            !string.Equals(syntax.NextIdentifier.Text, syntax.Identifier.Text, StringComparison.OrdinalIgnoreCase))
        {
            Report(
                "VB6S0013",
                $"Next variable '{syntax.NextIdentifier.Text}' does not match For variable '{syntax.Identifier.Text}'.",
                syntax.NextIdentifier.Span);
        }

        var initialValue = BindConversion(
            BindExpression(syntax.InitialValue, variables, procedures),
            controlVariable.Type);
        var limit = BindConversion(
            BindExpression(syntax.Limit, variables, procedures),
            controlVariable.Type);
        var step = syntax.Step is null
            ? new BoundLiteralExpression(
                1L,
                controlVariable.Type == TypeSymbol.LongLong
                    ? TypeSymbol.LongLong
                    : controlVariable.Type == TypeSymbol.Long
                        ? TypeSymbol.Long
                        : TypeSymbol.Integer)
            : BindConversion(BindExpression(syntax.Step, variables, procedures), controlVariable.Type);

        var loopId = _nextLoopId++;
        _loopStack.Add(new LoopBindingContext(BoundLoopKind.For, loopId));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _loopStack.RemoveAt(_loopStack.Count - 1);

        return new BoundForStatement(loopId, controlVariable, initialValue, limit, step, body);
    }

    private BoundWhileStatement BindWhile(
        WhileStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var condition = BindConversion(
            BindExpression(syntax.Condition, variables, procedures),
            TypeSymbol.Boolean);
        var body = BindStatements(syntax.Statements, variables, procedures);
        return new BoundWhileStatement(condition, body);
    }

    private BoundDoStatement BindDo(
        DoStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (syntax.PreCondition is not null && syntax.PostCondition is not null)
        {
            Report(
                "VB6S0014",
                "Do loop cannot have both a pre-test and a post-test condition.",
                syntax.DoKeyword.Span);
        }

        var conditionSyntax = syntax.PreCondition ?? syntax.PostCondition;
        var conditionKeyword = syntax.PreConditionKeyword ?? syntax.PostConditionKeyword;
        BoundExpression? condition = null;
        if (conditionSyntax is not null)
        {
            condition = BindConversion(
                BindExpression(conditionSyntax, variables, procedures),
                TypeSymbol.Boolean);
        }

        var loopId = _nextLoopId++;
        _loopStack.Add(new LoopBindingContext(BoundLoopKind.Do, loopId));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _loopStack.RemoveAt(_loopStack.Count - 1);

        return new BoundDoStatement(
            loopId,
            condition,
            syntax.PreCondition is null && syntax.PostCondition is not null,
            conditionKeyword?.Kind == SyntaxKind.UntilKeyword,
            body);
    }

    private BoundStatement BindExit(ExitStatementSyntax syntax)
    {
        if (syntax.TargetKeyword.Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword)
        {
            return new BoundReturnStatement();
        }

        var loopKind = syntax.TargetKeyword.Kind == SyntaxKind.DoKeyword
            ? BoundLoopKind.Do
            : BoundLoopKind.For;

        for (var index = _loopStack.Count - 1; index >= 0; index--)
        {
            if (_loopStack[index].Kind == loopKind)
            {
                return new BoundExitLoopStatement(loopKind, _loopStack[index].LoopId);
            }
        }

        Report(
            "VB6S0015",
            $"Exit {syntax.TargetKeyword.Text} is not inside an active {syntax.TargetKeyword.Text} loop.",
            syntax.ExitKeyword.Span);
        return new BoundExitLoopStatement(loopKind, -1);
    }

    private BoundSelectCaseStatement BindSelectCase(
        SelectCaseStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);
        var cases = ImmutableArray.CreateBuilder<BoundCaseBlock>();
        var hasElse = false;

        for (var caseIndex = 0; caseIndex < syntax.Cases.Length; caseIndex++)
        {
            var syntaxCase = syntax.Cases[caseIndex];
            var clauses = ImmutableArray.CreateBuilder<BoundCaseClause>();

            foreach (var clause in syntaxCase.Clauses)
            {
                switch (clause)
                {
                    case CaseValueClauseSyntax valueClause:
                        clauses.Add(new BoundCaseValueClause(BindConversion(
                            BindExpression(valueClause.Value, variables, procedures),
                            expression.Type)));
                        break;

                    case CaseRangeClauseSyntax rangeClause:
                        clauses.Add(new BoundCaseRangeClause(
                            BindConversion(
                                BindExpression(rangeClause.LowerBound, variables, procedures),
                                expression.Type),
                            BindConversion(
                                BindExpression(rangeClause.UpperBound, variables, procedures),
                                expression.Type)));
                        break;

                    case CaseRelationalClauseSyntax relationalClause:
                        clauses.Add(new BoundCaseRelationalClause(
                            relationalClause.OperatorToken.Kind,
                            BindConversion(
                                BindExpression(relationalClause.Value, variables, procedures),
                                expression.Type)));
                        break;

                    case CaseElseClauseSyntax elseClause:
                        if (hasElse || caseIndex != syntax.Cases.Length - 1)
                        {
                            Report(
                                "VB6S0016",
                                "Case Else must appear once and as the final Case block.",
                                elseClause.ElseKeyword.Span);
                        }

                        hasElse = true;
                        clauses.Add(new BoundCaseElseClause());
                        break;
                }
            }

            cases.Add(new BoundCaseBlock(
                clauses.ToImmutable(),
                BindStatements(syntaxCase.Statements, variables, procedures)));
        }

        return new BoundSelectCaseStatement(_nextSelectId++, expression, cases.ToImmutable());
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
                .Select(argument => new BoundArgument(null, BindExpression(argument, variables, procedures)))
                .ToImmutableArray();
            return new BoundInvocationStatement(new ProcedureSymbol(syntax.Identifier.Text), unknownArguments);
        }

        return new BoundInvocationStatement(
            procedure,
            BindArguments(syntax.Identifier, syntax.Arguments, procedure, variables, procedures));
    }

    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return syntax switch
        {
            LiteralExpressionSyntax literal => BindLiteral(literal),
            NameExpressionSyntax name => BindName(name, variables),
            InvocationExpressionSyntax invocation => BindInvocationExpression(invocation, variables, procedures),
            UnaryExpressionSyntax unary => BindUnary(unary, variables, procedures),
            BinaryExpressionSyntax binary => BindBinary(binary, variables, procedures),
            ParenthesizedExpressionSyntax parenthesized => BindExpression(parenthesized.Expression, variables, procedures),
            _ => new BoundErrorExpression()
        };
    }

    private BoundExpression BindInvocationExpression(
        InvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!procedures.TryGetValue(syntax.Identifier.Text, out var procedure))
        {
            Report(
                "VB6S0005",
                $"Procedure '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        if (!procedure.IsFunction)
        {
            Report(
                "VB6S0010",
                $"Sub '{procedure.Name}' cannot be used as an expression.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        return new BoundInvocationExpression(
            procedure,
            BindArguments(syntax.Identifier, syntax.Arguments, procedure, variables, procedures));
    }

    private ImmutableArray<BoundArgument> BindArguments(
        SyntaxToken invocationIdentifier,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        ProcedureSymbol procedure,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (argumentSyntaxes.Length != procedure.Parameters.Length)
        {
            Report(
                "VB6S0006",
                $"Procedure '{procedure.Name}' expects {procedure.Parameters.Length} argument(s), but {argumentSyntaxes.Length} were supplied.",
                invocationIdentifier.Span);
        }

        var arguments = ImmutableArray.CreateBuilder<BoundArgument>();
        for (var index = 0; index < argumentSyntaxes.Length; index++)
        {
            var expression = BindExpression(argumentSyntaxes[index], variables, procedures);
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
                        invocationIdentifier.Span);
                }
                else if (variableExpression.Variable.Type != parameter.Type &&
                         variableExpression.Variable.Type != TypeSymbol.Error &&
                         parameter.Type != TypeSymbol.Error)
                {
                    Report(
                        "VB6S0008",
                        $"ByRef argument type '{variableExpression.Variable.Type.Name}' does not match parameter type '{parameter.Type.Name}'.",
                        invocationIdentifier.Span);
                }
            }

            arguments.Add(new BoundArgument(parameter, expression));
        }

        return arguments.ToImmutable();
    }

    private static BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        return syntax.LiteralToken.Kind switch
        {
            SyntaxKind.IntegerLiteralToken => BindIntegerLiteral(syntax.LiteralToken.Value),
            SyntaxKind.FloatingLiteralToken when syntax.LiteralToken.Value is decimal =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.Currency),
            SyntaxKind.FloatingLiteralToken =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.Double),
            SyntaxKind.StringLiteralToken =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.String),
            SyntaxKind.TrueKeyword =>
                new BoundLiteralExpression(true, TypeSymbol.Boolean),
            SyntaxKind.FalseKeyword =>
                new BoundLiteralExpression(false, TypeSymbol.Boolean),
            _ => new BoundErrorExpression()
        };
    }

    private static BoundExpression BindIntegerLiteral(object? value)
    {
        // Radix literals and literals with a type suffix already carry their VB6 type in the
        // boxed CLR type; only plain decimal literals are typed by magnitude.
        if (value is short shortValue)
        {
            return new BoundLiteralExpression((long)shortValue, TypeSymbol.Integer);
        }

        if (value is int intValue)
        {
            return new BoundLiteralExpression((long)intValue, TypeSymbol.Long);
        }

        var numericValue = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
        var type = numericValue <= short.MaxValue
            ? TypeSymbol.Integer
            : numericValue <= int.MaxValue
                ? TypeSymbol.Long
                : TypeSymbol.LongLong;
        return new BoundLiteralExpression(numericValue, type);
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
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var operand = BindExpression(syntax.Operand, variables, procedures);
        if (operand.Type == TypeSymbol.Error)
        {
            return operand;
        }

        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword)
        {
            if (operand.Type == TypeSymbol.Boolean)
            {
                return new BoundUnaryExpression(SyntaxKind.NotKeyword, operand, TypeSymbol.Boolean);
            }

            if (!IsNumericType(operand.Type))
            {
                Report(
                    "VB6S0017",
                    "Operator 'Not' requires a Boolean or numeric operand.",
                    syntax.OperatorToken.Span);
                return new BoundErrorExpression();
            }

            // The complement of a Byte is negative, so VB6 widens it to Integer.
            var notType = operand.Type == TypeSymbol.Byte
                ? TypeSymbol.Integer
                : GetIntegerOperationType(operand.Type, operand.Type);
            operand = BindConversion(operand, notType);
            return new BoundUnaryExpression(SyntaxKind.NotKeyword, operand, notType);
        }

        if (syntax.OperatorToken.Kind == SyntaxKind.MinusToken && operand.Type == TypeSymbol.Byte)
        {
            operand = BindConversion(operand, TypeSymbol.Integer);
            return new BoundUnaryExpression(SyntaxKind.MinusToken, operand, TypeSymbol.Integer);
        }

        if (IsNumericType(operand.Type))
        {
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, operand.Type);
        }

        operand = BindConversion(operand, TypeSymbol.Integer);
        return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Integer);
    }

    private BoundExpression BindBinary(
        BinaryExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var left = BindExpression(syntax.Left, variables, procedures);
        var right = BindExpression(syntax.Right, variables, procedures);

        if (left.Type == TypeSymbol.Error || right.Type == TypeSymbol.Error)
        {
            return new BoundErrorExpression();
        }

        switch (syntax.OperatorToken.Kind)
        {
            case SyntaxKind.CaretToken:
                if (!IsNumericType(left.Type) || !IsNumericType(right.Type))
                {
                    Report(
                        "VB6S0022",
                        "Operator '^' requires numeric operands.",
                        syntax.OperatorToken.Span);
                    return new BoundErrorExpression();
                }

                left = BindConversion(left, TypeSymbol.Double);
                right = BindConversion(right, TypeSymbol.Double);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Double);

            case SyntaxKind.LikeKeyword:
                Report(
                    "VB6S0023",
                    "Like pattern-matching semantics, including Option Compare, are not implemented yet.",
                    syntax.OperatorToken.Span);
                return new BoundErrorExpression();

            case SyntaxKind.IsKeyword:
                Report(
                    "VB6S0024",
                    "Is object-reference identity semantics are not implemented yet.",
                    syntax.OperatorToken.Span);
                return new BoundErrorExpression();

            case SyntaxKind.EqualsToken:
            case SyntaxKind.LessGreaterToken:
            case SyntaxKind.LessToken:
            case SyntaxKind.LessOrEqualsToken:
            case SyntaxKind.GreaterToken:
            case SyntaxKind.GreaterOrEqualsToken:
                if (IsNumericType(left.Type) && IsNumericType(right.Type))
                {
                    var comparisonType = GetCommonNumericType(left.Type, right.Type);
                    left = BindConversion(left, comparisonType);
                    right = BindConversion(right, comparisonType);
                }
                else if (left.Type != right.Type)
                {
                    right = BindConversion(right, left.Type);
                }

                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Boolean);

            case SyntaxKind.AndKeyword:
            case SyntaxKind.OrKeyword:
            case SyntaxKind.XorKeyword:
            case SyntaxKind.EqvKeyword:
            case SyntaxKind.ImpKeyword:
            {
                if (left.Type == TypeSymbol.Boolean && right.Type == TypeSymbol.Boolean)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Boolean);
                }

                if (!IsBitwiseOperandType(left.Type) || !IsBitwiseOperandType(right.Type))
                {
                    Report(
                        "VB6S0018",
                        $"Operator '{syntax.OperatorToken.Text}' requires Boolean or numeric operands.",
                        syntax.OperatorToken.Span);
                    return new BoundErrorExpression();
                }

                // A Boolean mixed with a number takes part as its VB6 numeric value (-1 / 0).
                var resultType = GetIntegerOperationType(
                    left.Type == TypeSymbol.Boolean ? TypeSymbol.Integer : left.Type,
                    right.Type == TypeSymbol.Boolean ? TypeSymbol.Integer : right.Type);

                // The complement produced by Eqv and Imp does not fit the unsigned Byte range.
                if (resultType == TypeSymbol.Byte &&
                    syntax.OperatorToken.Kind is SyntaxKind.EqvKeyword or SyntaxKind.ImpKeyword)
                {
                    resultType = TypeSymbol.Integer;
                }

                left = BindConversion(left, resultType);
                right = BindConversion(right, resultType);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    resultType);
            }

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
            {
                var resultType = IsSingleDivisionOperand(left.Type) && IsSingleDivisionOperand(right.Type)
                    ? TypeSymbol.Single
                    : TypeSymbol.Double;
                left = BindConversion(left, resultType);
                right = BindConversion(right, resultType);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    resultType);
            }

            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.StarToken:
            {
                var resultType = IsNumericType(left.Type) && IsNumericType(right.Type)
                    ? GetCommonNumericType(left.Type, right.Type)
                    : TypeSymbol.Integer;
                left = BindConversion(left, resultType);
                right = BindConversion(right, resultType);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    resultType);
            }

            case SyntaxKind.BackslashToken:
            case SyntaxKind.ModKeyword:
            {
                var resultType = GetIntegerOperationType(left.Type, right.Type);
                left = BindConversion(left, resultType);
                right = BindConversion(right, resultType);
                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    resultType);
            }

            default:
                return new BoundErrorExpression();
        }
    }

    private static bool IsNumericType(TypeSymbol type) =>
        type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong || type == TypeSymbol.Single || type == TypeSymbol.Double ||
        type == TypeSymbol.Currency;

    private static bool IsFloatingOrFixedPointType(TypeSymbol type) =>
        type == TypeSymbol.Single || type == TypeSymbol.Double || type == TypeSymbol.Currency;

    private static bool IsSingleDivisionOperand(TypeSymbol type) =>
        type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Single;

    private static bool IsBitwiseOperandType(TypeSymbol type) =>
        IsNumericType(type) || type == TypeSymbol.Boolean;

    /// <summary>
    /// Result type of the VB6 operators that work on whole numbers: '\\', 'Mod' and the bitwise
    /// operators. Floating-point and Currency operands are rounded to Long first.
    /// </summary>
    private static TypeSymbol GetIntegerOperationType(TypeSymbol left, TypeSymbol right) =>
        left == TypeSymbol.LongLong || right == TypeSymbol.LongLong
            ? TypeSymbol.LongLong
            : IsFloatingOrFixedPointType(left) || IsFloatingOrFixedPointType(right) ||
              left == TypeSymbol.Long || right == TypeSymbol.Long
                ? TypeSymbol.Long
                : left == TypeSymbol.Byte && right == TypeSymbol.Byte
                    ? TypeSymbol.Byte
                    : TypeSymbol.Integer;

    private static TypeSymbol GetCommonNumericType(TypeSymbol left, TypeSymbol right)
    {
        if (left == TypeSymbol.Currency || right == TypeSymbol.Currency)
        {
            return TypeSymbol.Currency;
        }

        if (left == TypeSymbol.Double || right == TypeSymbol.Double)
        {
            return TypeSymbol.Double;
        }

        if ((left == TypeSymbol.Single && (right == TypeSymbol.Long || right == TypeSymbol.LongLong)) ||
            (right == TypeSymbol.Single && (left == TypeSymbol.Long || left == TypeSymbol.LongLong)))
        {
            return TypeSymbol.Double;
        }

        if (left == TypeSymbol.Single || right == TypeSymbol.Single)
        {
            return TypeSymbol.Single;
        }

        if (left == TypeSymbol.LongLong || right == TypeSymbol.LongLong)
        {
            return TypeSymbol.LongLong;
        }

        if (left == TypeSymbol.Long || right == TypeSymbol.Long)
        {
            return TypeSymbol.Long;
        }

        if (left == TypeSymbol.Integer || right == TypeSymbol.Integer)
        {
            return TypeSymbol.Integer;
        }

        return TypeSymbol.Byte;
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

    private readonly record struct LoopBindingContext(BoundLoopKind Kind, int LoopId);
}