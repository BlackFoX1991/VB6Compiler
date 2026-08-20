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
    private readonly IReadOnlyDictionary<string, TypeSymbol> _availableDeclaredTypes;
    private readonly Dictionary<string, UserDefinedTypeSymbol> _userDefinedTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, EnumTypeSymbol> _enumTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LoopBindingContext> _loopStack = new();
    private readonly List<BoundExpression> _withStack = new();
    private int _nextLoopId;
    private int _nextSelectId;
    private int _optionBaseLowerBound;
    private bool _declaredTypesCreated;

    public Binder(SourceText text, IReadOnlyDictionary<string, TypeSymbol>? availableDeclaredTypes = null)
    {
        _text = text;
        _availableDeclaredTypes = availableDeclaredTypes ??
            new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
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

    public static ImmutableArray<ProcedureSymbol> CreateProcedureSymbols(
        SourceText text,
        CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var binder = new Binder(text);
        binder._optionBaseLowerBound = GetOptionBaseLowerBound(root);
        binder.EnsureDeclaredTypes(root);
        return binder.DeclareProcedures(root).Values.ToImmutableArray();
    }

    public static ImmutableArray<TypeSymbol> CreateDeclaredTypeSymbols(CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var symbols = ImmutableArray.CreateBuilder<TypeSymbol>();

        foreach (var declaration in root.Members.OfType<TypeDeclarationSyntax>())
        {
            symbols.Add(new UserDefinedTypeSymbol(
                declaration.Identifier.Text,
                ImmutableArray<UserDefinedFieldSymbol>.Empty));
        }

        foreach (var declaration in root.Members.OfType<EnumDeclarationSyntax>())
        {
            symbols.Add(CreateEnumTypeSymbol(declaration));
        }

        return symbols.ToImmutable();
    }

    private static EnumTypeSymbol CreateEnumTypeSymbol(EnumDeclarationSyntax declaration)
    {
        var members = declaration.Members
            .Select(member => new EnumMemberSymbol(member.Identifier.Text))
            .ToImmutableArray();
        return new EnumTypeSymbol(declaration.Identifier.Text, members);
    }

    /// <summary>
    /// Module-level variables declared by a compilation unit. Exposed so that a project
    /// compilation can pre-declare them across modules, the way procedures already are.
    /// </summary>
    public static ImmutableArray<ModuleVariableSymbol> CreateModuleVariableSymbols(
        SourceText text,
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, TypeSymbol>? availableDeclaredTypes = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var binder = new Binder(text, availableDeclaredTypes);
        binder.EnsureDeclaredTypes(root);
        return binder.DeclareModuleVariables(root).Scope.Values.ToImmutableArray();
    }

    public SemanticModel BindCompilationUnit(CompilationUnitSyntax root)
    {
        _optionBaseLowerBound = GetOptionBaseLowerBound(root);
        EnsureDeclaredTypes(root);
        var procedures = DeclareProcedures(root);
        return BindCompilationUnit(root, procedures);
    }

    public SemanticModel BindCompilationUnit(
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, ProcedureSymbol> availableProcedures,
        IReadOnlyDictionary<string, ModuleVariableSymbol>? availableModuleVariables = null)
    {
        ArgumentNullException.ThrowIfNull(availableProcedures);

        _optionBaseLowerBound = GetOptionBaseLowerBound(root);
        var declaredTypes = EnsureDeclaredTypes(root);
        var declared = DeclareModuleVariables(root);
        var moduleVariables = MergeModuleVariables(availableModuleVariables, declared.Scope);
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

                case PropertyDeclarationSyntax declaration:
                {
                    var symbol = CreateProcedureSymbolWithDeclaredTypes(declaration);
                    procedures.Add(BindProcedure(
                        declaration.Identifier,
                        declaration.Parameters,
                        declaration.Statements,
                        declaration.TypeToken,
                        symbol,
                        availableProcedures,
                        moduleVariables));
                    break;
                }
            }
        }

        return new SemanticModel(procedures.ToImmutable(), _diagnostics.ToImmutable())
        {
            ModuleVariables = declared.Bound,
            UserDefinedTypes = declaredTypes.UserDefinedTypes,
            EnumTypes = declaredTypes.EnumTypes
        };
    }

    private static IReadOnlyDictionary<string, ModuleVariableSymbol> MergeModuleVariables(
        IReadOnlyDictionary<string, ModuleVariableSymbol>? availableModuleVariables,
        IReadOnlyDictionary<string, ModuleVariableSymbol> declaredModuleVariables)
    {
        if (availableModuleVariables is null)
        {
            return declaredModuleVariables;
        }

        var merged = new Dictionary<string, ModuleVariableSymbol>(
            availableModuleVariables,
            StringComparer.OrdinalIgnoreCase);
        foreach (var variable in declaredModuleVariables)
        {
            merged[variable.Key] = variable.Value;
        }

        return merged;
    }

    private (ImmutableArray<UserDefinedTypeSymbol> UserDefinedTypes, ImmutableArray<EnumTypeSymbol> EnumTypes)
        EnsureDeclaredTypes(CompilationUnitSyntax root)
    {
        if (_declaredTypesCreated)
        {
            return (_userDefinedTypes.Values.ToImmutableArray(), _enumTypes.Values.ToImmutableArray());
        }

        _declaredTypesCreated = true;
        foreach (var declaration in root.Members.OfType<TypeDeclarationSyntax>())
        {
            if (!_userDefinedTypes.TryAdd(
                declaration.Identifier.Text,
                new UserDefinedTypeSymbol(declaration.Identifier.Text, ImmutableArray<UserDefinedFieldSymbol>.Empty)))
            {
                Report(
                    "VB6S0033",
                    $"User-defined type '{declaration.Identifier.Text}' is already declared.",
                    declaration.Identifier.Span);
            }
        }

        foreach (var declaration in root.Members.OfType<EnumDeclarationSyntax>())
        {
            var symbol = CreateEnumTypeSymbol(declaration);
            if (!_enumTypes.TryAdd(symbol.Name, symbol))
            {
                Report(
                    "VB6S0048",
                    $"Enum '{declaration.Identifier.Text}' is already declared.",
                    declaration.Identifier.Span);
            }
        }

        var noVariables = new Dictionary<string, VariableSymbol>(StringComparer.OrdinalIgnoreCase);
        var noProcedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in root.Members.OfType<TypeDeclarationSyntax>())
        {
            if (!_userDefinedTypes.ContainsKey(declaration.Identifier.Text))
            {
                continue;
            }

            var fields = ImmutableArray.CreateBuilder<UserDefinedFieldSymbol>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in declaration.Fields)
            {
                if (!names.Add(field.Identifier.Text))
                {
                    Report(
                        "VB6S0034",
                        $"Field '{field.Identifier.Text}' is already declared in type '{declaration.Identifier.Text}'.",
                        field.Identifier.Span);
                    continue;
                }

                var type = ResolveVariableDeclaratorType(field);
                var dimensions = BindArrayDimensions(field, _optionBaseLowerBound, noVariables, noProcedures);
                var fixedStringLength = field.FixedStringLength is null
                    ? null
                    : BindConversion(BindExpression(field.FixedStringLength, noVariables, noProcedures), TypeSymbol.Long);
                fields.Add(new UserDefinedFieldSymbol(
                    field.Identifier.Text,
                    type,
                    dimensions,
                    fixedStringLength));
            }

            _userDefinedTypes[declaration.Identifier.Text] =
                new UserDefinedTypeSymbol(declaration.Identifier.Text, fields.ToImmutable());
        }

        return (_userDefinedTypes.Values.ToImmutableArray(), _enumTypes.Values.ToImmutableArray());
    }

    private static ImmutableArray<ParameterSymbol> CreateParameterSymbols(ImmutableArray<ParameterSyntax> parameters) =>
        parameters
            .Select(parameter => new ParameterSymbol(
                parameter.Identifier.Text,
                ResolveParameterType(parameter),
                ResolveParameterPassingMode(parameter),
                parameter.IsArray && !parameter.IsParamArray && !parameter.Dimensions.IsDefaultOrEmpty,
                parameter.OptionalKeyword is not null,
                parameter.DefaultValue,
                parameter.IsParamArray))
            .ToImmutableArray();

    private static TypeSymbol ResolveParameterType(ParameterSyntax parameter)
    {
        var elementType = parameter.TypeToken is null
            ? TypeSymbol.Variant
            : TypeSymbol.Lookup(parameter.TypeToken.Text) ?? TypeSymbol.Error;
        if (elementType == TypeSymbol.Error)
        {
            return TypeSymbol.Error;
        }

        if (parameter.IsParamArray)
        {
            return new ArrayTypeSymbol(elementType, 1);
        }

        if (!parameter.IsArray)
        {
            return elementType;
        }

        var rank = parameter.Dimensions.IsDefaultOrEmpty ? 1 : parameter.Dimensions.Length;
        return new ArrayTypeSymbol(elementType, rank);
    }

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
            SubDeclarationSyntax sub => CreateProcedureSymbolWithDeclaredTypes(sub),
            FunctionDeclarationSyntax function => CreateProcedureSymbolWithDeclaredTypes(function),
            _ => new ProcedureSymbol(name)
        };
    }

    private ProcedureSymbol CreateProcedureSymbolWithDeclaredTypes(SubDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbolsWithDeclaredTypes(declaration.Parameters));
    }

    private ProcedureSymbol CreateProcedureSymbolWithDeclaredTypes(FunctionDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbolsWithDeclaredTypes(declaration.Parameters),
            ResolveDeclaredType(declaration.ReturnTypeToken));
    }

    private ProcedureSymbol CreateProcedureSymbolWithDeclaredTypes(PropertyDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbolsWithDeclaredTypes(declaration.Parameters),
            declaration.IsGet
                ? declaration.TypeToken is null
                    ? TypeSymbol.Variant
                    : ResolveDeclaredType(declaration.TypeToken)
                : null);
    }

    private ImmutableArray<ParameterSymbol> CreateParameterSymbolsWithDeclaredTypes(
        ImmutableArray<ParameterSyntax> parameters) =>
        parameters
            .Select(parameter => new ParameterSymbol(
                parameter.Identifier.Text,
                ResolveParameterTypeWithDeclaredTypes(parameter),
                ResolveParameterPassingMode(parameter),
                parameter.IsArray && !parameter.IsParamArray && !parameter.Dimensions.IsDefaultOrEmpty,
                parameter.OptionalKeyword is not null,
                parameter.DefaultValue,
                parameter.IsParamArray))
            .ToImmutableArray();

    private TypeSymbol ResolveParameterTypeWithDeclaredTypes(ParameterSyntax parameter)
    {
        var elementType = parameter.TypeToken is null
            ? TypeSymbol.Variant
            : ResolveDeclaredType(parameter.TypeToken);
        if (elementType == TypeSymbol.Error)
        {
            return TypeSymbol.Error;
        }

        if (parameter.IsParamArray)
        {
            return new ArrayTypeSymbol(elementType, 1);
        }

        if (!parameter.IsArray)
        {
            return elementType;
        }

        var rank = parameter.Dimensions.IsDefaultOrEmpty ? 1 : parameter.Dimensions.Length;
        return new ArrayTypeSymbol(elementType, rank);
    }

    private static ParameterPassingMode ResolveParameterPassingMode(ParameterSyntax parameter) =>
        parameter.IsParamArray || parameter.PassingModeKeyword?.Kind == SyntaxKind.ByValKeyword
            ? ParameterPassingMode.ByVal
            : ParameterPassingMode.ByRef;

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
                    symbol = CreateProcedureSymbolWithDeclaredTypes(sub);
                    identifier = sub.Identifier;
                    break;
                case FunctionDeclarationSyntax function:
                    symbol = CreateProcedureSymbolWithDeclaredTypes(function);
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
        var optionBaseLowerBound = GetOptionBaseLowerBound(root);

        foreach (var member in root.Members)
        {
            switch (member)
            {
                case ModuleVariableDeclarationSyntax declaration:
                {
                    foreach (var declarator in declaration.Declarators)
                    {
                        var type = ResolveVariableDeclaratorType(declarator);
                        var symbol = new ModuleVariableSymbol(
                            declarator.Identifier.Text,
                            type,
                            declarator.IsArray && !declarator.Dimensions.IsDefaultOrEmpty);
                        var visible = scope.ToDictionary(
                            entry => entry.Key,
                            entry => (VariableSymbol)entry.Value,
                            StringComparer.OrdinalIgnoreCase);
                        var dimensions = BindArrayDimensions(
                            declarator,
                            optionBaseLowerBound,
                            visible,
                            noProcedures);
                        if (TryDeclareModuleVariable(scope, symbol, declarator.Identifier))
                        {
                            bound.Add(new BoundModuleVariable(
                                symbol,
                                null,
                                IsConstant: false,
                                dimensions));
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

                case EnumDeclarationSyntax declaration:
                {
                    var nextValue = 0L;
                    foreach (var enumMember in declaration.Members)
                    {
                        var visible = scope.ToDictionary(
                            entry => entry.Key,
                            entry => (VariableSymbol)entry.Value,
                            StringComparer.OrdinalIgnoreCase);
                        BoundExpression initializer;
                        if (enumMember.Value is null)
                        {
                            initializer = new BoundLiteralExpression(nextValue, TypeSymbol.Long);
                            nextValue++;
                        }
                        else
                        {
                            initializer = BindConversion(
                                BindExpression(enumMember.Value, visible, noProcedures),
                                TypeSymbol.Long);
                            nextValue = TryGetConstantInt64(initializer, out var value)
                                ? value + 1
                                : nextValue + 1;
                        }

                        var symbol = new ModuleVariableSymbol(enumMember.Identifier.Text, TypeSymbol.Long);
                        if (TryDeclareModuleVariable(scope, symbol, enumMember.Identifier))
                        {
                            bound.Add(new BoundModuleVariable(
                                symbol,
                                initializer,
                                IsConstant: true));
                        }
                    }

                    break;
                }
            }
        }

        return (scope, bound.ToImmutable());
    }

    private TypeSymbol ResolveVariableDeclaratorType(VariableDeclaratorSyntax declarator)
    {
        TypeSymbol elementType;
        if (declarator.TypeToken is not null)
        {
            elementType = ResolveDeclaredType(declarator.TypeToken);
        }
        else
        {
            elementType = TypeSymbol.Variant;
        }

        if (!declarator.IsArray || elementType == TypeSymbol.Error)
        {
            return elementType;
        }

        var rank = declarator.Dimensions.IsDefaultOrEmpty ? 1 : declarator.Dimensions.Length;
        return new ArrayTypeSymbol(elementType, rank);
    }

    private TypeSymbol ResolveDeclaredType(SyntaxToken typeToken)
    {
        var type = TypeSymbol.Lookup(typeToken.Text);
        if (type is not null)
        {
            return type;
        }

        if (_userDefinedTypes.TryGetValue(typeToken.Text, out var userDefinedType))
        {
            return userDefinedType;
        }

        if (_enumTypes.TryGetValue(typeToken.Text, out var enumType))
        {
            return enumType;
        }

        if (_availableDeclaredTypes.TryGetValue(typeToken.Text, out var declaredType))
        {
            return declaredType;
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

            if (parameter.Type == TypeSymbol.Error)
            {
                Report(
                    "VB6S0003",
                    $"Unknown type '{syntax.TypeToken?.Text ?? "Variant"}'.",
                    syntax.TypeToken?.Span ?? syntax.Identifier.Span);
            }

            if (syntax.IsParamArray)
            {
                if (index != parameterSyntaxes.Length - 1)
                {
                    Report(
                        "VB6S0042",
                        "ParamArray parameter must be the last parameter.",
                        syntax.ParamArrayKeyword!.Span);
                }

                if (syntax.OptionalKeyword is not null)
                {
                    Report(
                        "VB6S0043",
                        "ParamArray parameter cannot also be Optional.",
                        syntax.OptionalKeyword.Span);
                }

                if (!syntax.IsArray)
                {
                    Report(
                        "VB6S0044",
                        "ParamArray parameter must use array parentheses.",
                        syntax.Identifier.Span);
                }

                if (!syntax.Dimensions.IsDefaultOrEmpty)
                {
                    Report(
                        "VB6S0045",
                        "ParamArray parameter cannot declare fixed dimensions.",
                        syntax.OpenParenthesisToken!.Span);
                }
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
        var staticLocals = BindStaticLocals(statements, variables, procedures);
        var body = BindStatements(statements, variables, procedures);

        return new BoundProcedure(symbol, locals.Values.ToImmutableArray(), body, staticLocals);
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
                    PredeclareLocalDeclarators(dim.Declarators, locals, variables, isStatic: false);
                    break;
                case StaticStatementSyntax staticStatement:
                    PredeclareLocalDeclarators(staticStatement.Declarators, locals, variables, isStatic: true);
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
                case ForEachStatementSyntax forEachStatement:
                    PredeclareLocals(forEachStatement.Statements, locals, variables);
                    break;
                case WhileStatementSyntax whileStatement:
                    PredeclareLocals(whileStatement.Statements, locals, variables);
                    break;
                case DoStatementSyntax doStatement:
                    PredeclareLocals(doStatement.Statements, locals, variables);
                    break;
                case WithStatementSyntax withStatement:
                    PredeclareLocals(withStatement.Statements, locals, variables);
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
        Dictionary<string, VariableSymbol> variables,
        bool isStatic)
    {
        foreach (var declarator in declarators)
        {
            var type = ResolveVariableDeclaratorType(declarator);
            var variable = new LocalVariableSymbol(
                declarator.Identifier.Text,
                type,
                declarator.IsArray && !declarator.Dimensions.IsDefaultOrEmpty,
                isStatic);
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

    private ImmutableArray<BoundStaticLocal> BindStaticLocals(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var staticLocals = ImmutableArray.CreateBuilder<BoundStaticLocal>();
        BindStaticLocals(statements, variables, procedures, staticLocals);
        return staticLocals.ToImmutable();
    }

    private void BindStaticLocals(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        ImmutableArray<BoundStaticLocal>.Builder staticLocals)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case StaticStatementSyntax staticStatement:
                    foreach (var declarator in staticStatement.Declarators)
                    {
                        if (variables.TryGetValue(declarator.Identifier.Text, out var variable) &&
                            variable is LocalVariableSymbol { IsStatic: true } local)
                        {
                            var dimensions = BindArrayDimensions(declarator, _optionBaseLowerBound, variables, procedures);
                            staticLocals.Add(new BoundStaticLocal(local, dimensions));
                        }
                    }

                    break;
                case IfStatementSyntax ifStatement:
                    BindStaticLocals(ifStatement.Statements, variables, procedures, staticLocals);
                    foreach (var elseIfClause in ifStatement.ElseIfClauses)
                    {
                        BindStaticLocals(elseIfClause.Statements, variables, procedures, staticLocals);
                    }
                    BindStaticLocals(ifStatement.ElseStatements, variables, procedures, staticLocals);
                    break;
                case ForStatementSyntax forStatement:
                    BindStaticLocals(forStatement.Statements, variables, procedures, staticLocals);
                    break;
                case ForEachStatementSyntax forEachStatement:
                    BindStaticLocals(forEachStatement.Statements, variables, procedures, staticLocals);
                    break;
                case WhileStatementSyntax whileStatement:
                    BindStaticLocals(whileStatement.Statements, variables, procedures, staticLocals);
                    break;
                case DoStatementSyntax doStatement:
                    BindStaticLocals(doStatement.Statements, variables, procedures, staticLocals);
                    break;
                case WithStatementSyntax withStatement:
                    BindStaticLocals(withStatement.Statements, variables, procedures, staticLocals);
                    break;
                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        BindStaticLocals(caseBlock.Statements, variables, procedures, staticLocals);
                    }
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
            if (statement is DimStatementSyntax dim)
            {
                foreach (var declarator in dim.Declarators)
                {
                    bound.Add(BindVariableDeclaration(declarator, variables, procedures));
                }
                continue;
            }

            if (statement is StaticStatementSyntax)
            {
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
            ImplicitMemberAssignmentStatementSyntax implicitMemberAssignment =>
                BindImplicitMemberAssignment(implicitMemberAssignment, variables, procedures),
            ReDimStatementSyntax redim => BindReDim(redim, variables, procedures),
            EraseStatementSyntax erase => BindErase(erase, variables),
            IfStatementSyntax ifStatement => BindIf(ifStatement, variables, procedures),
            ForStatementSyntax forStatement => BindFor(forStatement, variables, procedures),
            ForEachStatementSyntax forEachStatement => BindForEach(forEachStatement, variables, procedures),
            WhileStatementSyntax whileStatement => BindWhile(whileStatement, variables, procedures),
            DoStatementSyntax doStatement => BindDo(doStatement, variables, procedures),
            WithStatementSyntax withStatement => BindWith(withStatement, variables, procedures),
            ExitStatementSyntax exitStatement => BindExit(exitStatement),
            SelectCaseStatementSyntax selectStatement => BindSelectCase(selectStatement, variables, procedures),
            DebugPrintStatementSyntax debugPrint =>
                new BoundDebugPrintStatement(BindExpression(debugPrint.Expression, variables, procedures)),
            RaiseEventStatementSyntax raiseEvent => BindRaiseEvent(raiseEvent, variables, procedures),
            InvocationStatementSyntax invocation => BindInvocation(invocation, variables, procedures),
            SkippedStatementSyntax => null,
            _ => null
        };
    }

    private BoundStatement? BindRaiseEvent(
        RaiseEventStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        foreach (var argument in syntax.Arguments)
        {
            _ = BindExpression(argument, variables, procedures);
        }

        return null;
    }

    private BoundVariableDeclarationStatement BindVariableDeclaration(
        VariableDeclaratorSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!variables.TryGetValue(syntax.Identifier.Text, out var variable) ||
            variable is not LocalVariableSymbol local)
        {
            local = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
        }

        var dimensions = BindArrayDimensions(syntax, _optionBaseLowerBound, variables, procedures);
        return new BoundVariableDeclarationStatement(local, dimensions);
    }

    private BoundStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);

        if (syntax.Target is MemberAccessExpressionSyntax memberTarget)
        {
            return BindMemberTargetAssignment(memberTarget, expression, variables, procedures);
        }

        if (syntax.IsMember)
        {
            var target = BindName(new NameExpressionSyntax(syntax.Identifier), variables);
            var access = BindMemberAccessExpression(target, syntax.MemberIdentifier ?? syntax.Identifier);
            if (access is not BoundMemberAccessExpression memberAccess)
            {
                return new BoundMemberAssignmentStatement(target, new UserDefinedFieldSymbol(string.Empty, TypeSymbol.Error), expression);
            }

            if (syntax.IsIndexed)
            {
                if (memberAccess.Field.Type is not ArrayTypeSymbol arrayType)
                {
                    Report(
                        "VB6S0026",
                        $"Field '{memberAccess.Field.Name}' is not an array.",
                        (syntax.MemberIdentifier ?? syntax.Identifier).Span);
                    return new BoundMemberArrayElementAssignmentStatement(
                        target,
                        memberAccess.Field,
                        ImmutableArray<BoundExpression>.Empty,
                        expression);
                }

                var indices = BindArrayIndices(
                    syntax.MemberIdentifier ?? syntax.Identifier,
                    syntax.Indices,
                    arrayType,
                    variables,
                    procedures);
                return new BoundMemberArrayElementAssignmentStatement(
                    target,
                    memberAccess.Field,
                    indices,
                    BindConversion(expression, arrayType.ElementType));
            }

            return new BoundMemberAssignmentStatement(
                target,
                memberAccess.Field,
                BindConversion(expression, memberAccess.Field.Type));
        }

        if (syntax.IsIndexed)
        {
            return BindArrayElementAssignment(syntax, expression, variables, procedures);
        }

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

    private BoundStatement BindMemberTargetAssignment(
        MemberAccessExpressionSyntax targetSyntax,
        BoundExpression expression,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = BindMemberAccess(targetSyntax, variables, procedures);
        return target switch
        {
            BoundMemberArrayElementExpression arrayElement => new BoundMemberArrayElementAssignmentStatement(
                arrayElement.Target,
                arrayElement.Field,
                arrayElement.Indices,
                BindConversion(expression, arrayElement.Type)),
            BoundMemberAccessExpression memberAccess => new BoundMemberAssignmentStatement(
                memberAccess.Target,
                memberAccess.Field,
                BindConversion(expression, memberAccess.Type)),
            _ => new BoundMemberAssignmentStatement(
                new BoundErrorExpression(),
                new UserDefinedFieldSymbol(string.Empty, TypeSymbol.Error),
                expression)
        };
    }

    private BoundStatement? BindImplicitMemberAssignment(
        ImplicitMemberAssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);
        var target = GetCurrentWithTarget(syntax.MemberIdentifier);
        if (target is null)
        {
            return null;
        }

        var access = BindMemberAccessExpression(target, syntax.MemberIdentifier);
        if (access is not BoundMemberAccessExpression memberAccess)
        {
            return new BoundMemberAssignmentStatement(
                target,
                new UserDefinedFieldSymbol(string.Empty, TypeSymbol.Error),
                expression);
        }

        if (syntax.IsIndexed)
        {
            if (memberAccess.Field.Type is not ArrayTypeSymbol arrayType)
            {
                Report(
                    "VB6S0026",
                    $"Field '{memberAccess.Field.Name}' is not an array.",
                    syntax.MemberIdentifier.Span);
                return new BoundMemberArrayElementAssignmentStatement(
                    target,
                    memberAccess.Field,
                    ImmutableArray<BoundExpression>.Empty,
                    expression);
            }

            var indices = BindArrayIndices(
                syntax.MemberIdentifier,
                syntax.Indices,
                arrayType,
                variables,
                procedures);
            return new BoundMemberArrayElementAssignmentStatement(
                target,
                memberAccess.Field,
                indices,
                BindConversion(expression, arrayType.ElementType));
        }

        return new BoundMemberAssignmentStatement(
            target,
            memberAccess.Field,
            BindConversion(expression, memberAccess.Field.Type));
    }

    private BoundArrayElementAssignmentStatement BindArrayElementAssignment(
        AssignmentStatementSyntax syntax,
        BoundExpression expression,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var access = BindArrayElementExpression(
            syntax.Identifier,
            syntax.Indices,
            variables,
            procedures);

        if (access is not BoundArrayElementExpression arrayElement)
        {
            return new BoundArrayElementAssignmentStatement(
                new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error),
                ImmutableArray<BoundExpression>.Empty,
                expression);
        }

        return new BoundArrayElementAssignmentStatement(
            arrayElement.Array,
            arrayElement.Indices,
            BindConversion(expression, arrayElement.Type));
    }

    private BoundStatement BindReDim(
        ReDimStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var declarator = syntax.Declarators.FirstOrDefault();
        if (declarator is null)
        {
            Report(
                "VB6S0029",
                "ReDim requires an array variable.",
                syntax.ReDimKeyword.Span);
            return new BoundReDimStatement(
                new LocalVariableSymbol(string.Empty, TypeSymbol.Error),
                ImmutableArray<BoundArrayDimension>.Empty,
                syntax.PreserveKeyword is not null);
        }

        if (syntax.Declarators.Length > 1)
        {
            Report(
                "VB6S0030",
                "ReDim currently supports one array variable per statement.",
                syntax.Declarators[1].Identifier.Span);
        }

        if (!variables.TryGetValue(declarator.Identifier.Text, out var variable))
        {
            Report(
                "VB6S0001",
                $"Variable '{declarator.Identifier.Text}' is not declared.",
                declarator.Identifier.Span);
            variable = new LocalVariableSymbol(declarator.Identifier.Text, TypeSymbol.Error);
        }

        if (variable.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0026",
                $"Variable '{declarator.Identifier.Text}' is not an array.",
                declarator.Identifier.Span);
        }
        else if (declarator.Dimensions.Length != arrayType.Rank)
        {
            Report(
                "VB6S0027",
                $"Array '{declarator.Identifier.Text}' expects {arrayType.Rank} subscript(s), but {declarator.Dimensions.Length} were supplied.",
                declarator.Identifier.Span);
        }

        var dimensions = BindArrayDimensions(declarator, _optionBaseLowerBound, variables, procedures);
        return new BoundReDimStatement(variable, dimensions, syntax.PreserveKeyword is not null);
    }

    private BoundEraseStatement BindErase(
        EraseStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables)
    {
        var erased = ImmutableArray.CreateBuilder<VariableSymbol>();
        foreach (var identifier in syntax.Identifiers)
        {
            if (!variables.TryGetValue(identifier.Text, out var variable))
            {
                Report(
                    "VB6S0001",
                    $"Variable '{identifier.Text}' is not declared.",
                    identifier.Span);
                continue;
            }

            if (variable.Type is not ArrayTypeSymbol)
            {
                Report(
                    "VB6S0026",
                    $"Variable '{identifier.Text}' is not an array.",
                    identifier.Span);
                continue;
            }

            erased.Add(variable);
        }

        return new BoundEraseStatement(erased.ToImmutable());
    }

    private ImmutableArray<BoundArrayDimension> BindArrayDimensions(
        VariableDeclaratorSyntax syntax,
        int optionBaseLowerBound,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!syntax.IsArray || syntax.Dimensions.IsDefaultOrEmpty)
        {
            return ImmutableArray<BoundArrayDimension>.Empty;
        }

        var dimensions = ImmutableArray.CreateBuilder<BoundArrayDimension>();
        foreach (var dimension in syntax.Dimensions)
        {
            var lowerBound = dimension.LowerBound is null
                ? new BoundLiteralExpression((long)optionBaseLowerBound, TypeSymbol.Long)
                : BindConversion(
                    BindExpression(dimension.LowerBound, variables, procedures),
                    TypeSymbol.Long);
            var upperBound = BindConversion(
                BindExpression(dimension.UpperBound, variables, procedures),
                TypeSymbol.Long);
            dimensions.Add(new BoundArrayDimension(lowerBound, upperBound));
        }

        return dimensions.ToImmutable();
    }

    private static int GetOptionBaseLowerBound(CompilationUnitSyntax root)
    {
        var lowerBound = 0;
        foreach (var directive in root.Members.OfType<OptionBaseSyntax>())
        {
            if (int.TryParse(directive.ValueToken.Text, out var value) && value is 0 or 1)
            {
                lowerBound = value;
            }
        }

        return lowerBound;
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

    private BoundForEachStatement BindForEach(
        ForEachStatementSyntax syntax,
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

        if (syntax.NextIdentifier is not null &&
            !string.Equals(syntax.NextIdentifier.Text, syntax.Identifier.Text, StringComparison.OrdinalIgnoreCase))
        {
            Report(
                "VB6S0013",
                $"Next variable '{syntax.NextIdentifier.Text}' does not match For Each variable '{syntax.Identifier.Text}'.",
                syntax.NextIdentifier.Span);
        }

        var collection = BindExpression(syntax.Collection, variables, procedures);
        TypeSymbol elementType = TypeSymbol.Error;
        if (collection.Type is ArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
        }
        else if (collection.Type != TypeSymbol.Error)
        {
            Report(
                "VB6S0032",
                "For Each currently requires an array expression.",
                syntax.InKeyword.Span);
        }

        var loopId = _nextLoopId++;
        _loopStack.Add(new LoopBindingContext(BoundLoopKind.For, loopId));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _loopStack.RemoveAt(_loopStack.Count - 1);

        return new BoundForEachStatement(loopId, controlVariable, collection, elementType, body);
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

    private BoundWithStatement BindWith(
        WithStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = BindExpression(syntax.Target, variables, procedures);
        _withStack.Add(target);
        var body = BindStatements(syntax.Statements, variables, procedures);
        _withStack.RemoveAt(_withStack.Count - 1);

        return new BoundWithStatement(target, body);
    }

    private BoundStatement BindExit(ExitStatementSyntax syntax)
    {
        if (syntax.TargetKeyword.Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword or SyntaxKind.PropertyKeyword)
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
            BindArguments(
                syntax.Identifier,
                syntax.Arguments,
                procedure,
                variables,
                procedures,
                allowByRefTemporaries: true));
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
            MemberAccessExpressionSyntax memberAccess => BindMemberAccess(memberAccess, variables, procedures),
            ImplicitMemberAccessExpressionSyntax implicitMemberAccess =>
                BindImplicitMemberAccess(implicitMemberAccess, variables, procedures),
            InvocationExpressionSyntax invocation => BindInvocationExpression(invocation, variables, procedures),
            CallSiteByValExpressionSyntax byVal => BindExpression(byVal.Expression, variables, procedures),
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
        if (variables.TryGetValue(syntax.Identifier.Text, out var variable) &&
            variable.Type is ArrayTypeSymbol)
        {
            return BindArrayElementExpression(
                syntax.Identifier,
                syntax.Arguments,
                variables,
                procedures);
        }

        if (IsIdentifierText(syntax.Identifier, "LBound") || IsIdentifierText(syntax.Identifier, "UBound"))
        {
            return BindArrayBoundExpression(
                syntax,
                variables,
                procedures,
                isUpperBound: IsIdentifierText(syntax.Identifier, "UBound"));
        }

        if (IsVariantIntrinsic(syntax.Identifier.Text, out var resultType))
        {
            return BindVariantIntrinsicExpression(syntax, variables, procedures, resultType);
        }

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
            BindArguments(
                syntax.Identifier,
                syntax.Arguments,
                procedure,
                variables,
                procedures,
                allowByRefTemporaries: true));
    }

    private BoundExpression BindArrayBoundExpression(
        InvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        bool isUpperBound)
    {
        if (syntax.Arguments.Length is < 1 or > 2)
        {
            Report(
                "VB6S0031",
                $"{syntax.Identifier.Text} expects one array argument and an optional dimension.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        var arrayExpression = BindExpression(syntax.Arguments[0], variables, procedures);
        if (arrayExpression is not BoundVariableExpression variableExpression ||
            variableExpression.Variable.Type is not ArrayTypeSymbol)
        {
            Report(
                "VB6S0026",
                $"{syntax.Identifier.Text} requires an array variable.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        var dimension = syntax.Arguments.Length == 2
            ? BindConversion(BindExpression(syntax.Arguments[1], variables, procedures), TypeSymbol.Long)
            : new BoundLiteralExpression(1L, TypeSymbol.Long);
        return new BoundArrayBoundExpression(variableExpression.Variable, dimension, isUpperBound);
    }

    private BoundExpression BindVariantIntrinsicExpression(
        InvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        TypeSymbol resultType)
    {
        if (syntax.Arguments.Length != 1)
        {
            Report(
                "VB6S0038",
                $"{syntax.Identifier.Text} expects one argument.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        return new BoundVariantIntrinsicExpression(
            syntax.Identifier.Text,
            ImmutableArray.Create(BindExpression(syntax.Arguments[0], variables, procedures)),
            resultType);
    }

    private BoundExpression BindMemberAccess(
        MemberAccessExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = BindExpression(syntax.Target, variables, procedures);
        var access = BindMemberAccessExpression(target, syntax.Identifier);
        if (!syntax.IsIndexed)
        {
            return access;
        }

        if (access is not BoundMemberAccessExpression memberAccess)
        {
            return new BoundErrorExpression();
        }

        if (memberAccess.Field.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0026",
                $"Field '{memberAccess.Field.Name}' is not an array.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        return new BoundMemberArrayElementExpression(
            target,
            memberAccess.Field,
            BindArrayIndices(syntax.Identifier, syntax.Indices, arrayType, variables, procedures),
            arrayType.ElementType);
    }

    private BoundExpression BindImplicitMemberAccess(
        ImplicitMemberAccessExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = GetCurrentWithTarget(syntax.Identifier);
        if (target is null)
        {
            return new BoundErrorExpression();
        }

        var access = BindMemberAccessExpression(target, syntax.Identifier);
        if (!syntax.IsIndexed)
        {
            return access;
        }

        if (access is not BoundMemberAccessExpression memberAccess)
        {
            return new BoundErrorExpression();
        }

        if (memberAccess.Field.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0026",
                $"Field '{memberAccess.Field.Name}' is not an array.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        return new BoundMemberArrayElementExpression(
            target,
            memberAccess.Field,
            BindArrayIndices(syntax.Identifier, syntax.Indices, arrayType, variables, procedures),
            arrayType.ElementType);
    }

    private BoundExpression BindMemberAccessExpression(BoundExpression target, SyntaxToken identifier)
    {
        if (target.Type is not UserDefinedTypeSymbol userDefinedType)
        {
            Report(
                "VB6S0035",
                $"Expression of type '{target.Type.Name}' does not have fields.",
                identifier.Span);
            return new BoundErrorExpression();
        }

        var field = userDefinedType.FindField(identifier.Text);
        if (field is null)
        {
            Report(
                "VB6S0036",
                $"Type '{userDefinedType.Name}' does not contain field '{identifier.Text}'.",
                identifier.Span);
            return new BoundErrorExpression();
        }

        return new BoundMemberAccessExpression(target, field);
    }

    private BoundExpression? GetCurrentWithTarget(SyntaxToken identifier)
    {
        if (_withStack.Count > 0)
        {
            return _withStack[^1];
        }

        Report(
            "VB6S0037",
            "Implicit member access requires an active With block.",
            identifier.Span);
        return null;
    }

    private BoundExpression BindArrayElementExpression(
        SyntaxToken identifier,
        ImmutableArray<ExpressionSyntax> indexSyntaxes,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!variables.TryGetValue(identifier.Text, out var variable))
        {
            Report(
                "VB6S0001",
                $"Variable '{identifier.Text}' is not declared.",
                identifier.Span);
            return new BoundErrorExpression();
        }

        if (variable.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0026",
                $"Variable '{identifier.Text}' is not an array.",
                identifier.Span);
            return new BoundErrorExpression();
        }

        var indices = BindArrayIndices(identifier, indexSyntaxes, arrayType, variables, procedures);
        return new BoundArrayElementExpression(variable, indices, arrayType.ElementType);
    }

    private ImmutableArray<BoundExpression> BindArrayIndices(
        SyntaxToken identifier,
        ImmutableArray<ExpressionSyntax> indexSyntaxes,
        ArrayTypeSymbol arrayType,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (indexSyntaxes.Length != arrayType.Rank)
        {
            Report(
                "VB6S0027",
                $"Array '{identifier.Text}' expects {arrayType.Rank} subscript(s), but {indexSyntaxes.Length} were supplied.",
                identifier.Span);
        }

        return indexSyntaxes
            .Select(index => BindConversion(BindExpression(index, variables, procedures), TypeSymbol.Long))
            .ToImmutableArray();
    }

    private static bool IsIdentifierText(SyntaxToken token, string text) =>
        token.Kind == SyntaxKind.IdentifierToken &&
        string.Equals(token.Text, text, StringComparison.OrdinalIgnoreCase);

    private static bool IsVariantIntrinsic(string name, out TypeSymbol resultType)
    {
        if (string.Equals(name, "VarType", StringComparison.OrdinalIgnoreCase))
        {
            resultType = TypeSymbol.Integer;
            return true;
        }

        if (string.Equals(name, "CVErr", StringComparison.OrdinalIgnoreCase))
        {
            resultType = TypeSymbol.Variant;
            return true;
        }

        if (string.Equals(name, "IsEmpty", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "IsNull", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "IsError", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "IsMissing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "IsNumeric", StringComparison.OrdinalIgnoreCase))
        {
            resultType = TypeSymbol.Boolean;
            return true;
        }

        resultType = TypeSymbol.Error;
        return false;
    }

    private ImmutableArray<BoundArgument> BindArguments(
        SyntaxToken invocationIdentifier,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        ProcedureSymbol procedure,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        bool allowByRefTemporaries)
    {
        var hasParamArray = procedure.Parameters.Length > 0 && procedure.Parameters[^1].IsParamArray;
        var fixedParameterCount = hasParamArray
            ? procedure.Parameters.Length - 1
            : procedure.Parameters.Length;
        var fixedParameters = procedure.Parameters.Take(fixedParameterCount);
        var requiredParameterCount = fixedParameters.Count(parameter => !parameter.IsOptional);
        if (argumentSyntaxes.Length < requiredParameterCount ||
            (!hasParamArray && argumentSyntaxes.Length > procedure.Parameters.Length))
        {
            Report(
                "VB6S0006",
                hasParamArray
                    ? requiredParameterCount == fixedParameterCount
                        ? $"Procedure '{procedure.Name}' expects at least {requiredParameterCount} argument(s), but {argumentSyntaxes.Length} were supplied."
                        : $"Procedure '{procedure.Name}' expects at least {requiredParameterCount} fixed argument(s), but {argumentSyntaxes.Length} were supplied."
                    : procedure.Parameters.Length == requiredParameterCount
                    ? $"Procedure '{procedure.Name}' expects {procedure.Parameters.Length} argument(s), but {argumentSyntaxes.Length} were supplied."
                    : $"Procedure '{procedure.Name}' expects between {requiredParameterCount} and {procedure.Parameters.Length} argument(s), but {argumentSyntaxes.Length} were supplied.",
                invocationIdentifier.Span);
        }

        var arguments = ImmutableArray.CreateBuilder<BoundArgument>();
        var argumentCount = Math.Min(argumentSyntaxes.Length, fixedParameterCount);
        for (var index = 0; index < argumentCount; index++)
        {
            var expression = BindExpression(argumentSyntaxes[index], variables, procedures);
            var parameter = procedure.Parameters[index];

            var isByRefTemporary = ValidateAndConvertArgument(
                invocationIdentifier,
                parameter,
                argumentSyntaxes[index],
                allowByRefTemporaries,
                ref expression,
                out var copyBackTarget);
            arguments.Add(new BoundArgument(parameter, expression, isByRefTemporary, copyBackTarget));
        }

        if (!hasParamArray && argumentSyntaxes.Length > procedure.Parameters.Length)
        {
            for (var index = procedure.Parameters.Length; index < argumentSyntaxes.Length; index++)
            {
                arguments.Add(new BoundArgument(null, BindExpression(argumentSyntaxes[index], variables, procedures)));
            }
        }

        for (var index = argumentSyntaxes.Length; index < fixedParameterCount; index++)
        {
            var parameter = procedure.Parameters[index];
            if (!parameter.IsOptional)
            {
                continue;
            }

            if (parameter.PassingMode == ParameterPassingMode.ByRef)
            {
                if (!allowByRefTemporaries)
                {
                    Report(
                        "VB6S0046",
                        $"Omitted optional ByRef parameter '{parameter.Name}' is not implemented for function-call expressions yet.",
                        invocationIdentifier.Span);
                    continue;
                }

                var defaultExpression = BindConversion(BindOptionalDefaultValue(parameter, procedures), parameter.Type);
                arguments.Add(new BoundArgument(parameter, defaultExpression, IsByRefTemporary: true));
                continue;
            }

            var expression = BindOptionalDefaultValue(parameter, procedures);
            ValidateAndConvertArgument(
                invocationIdentifier,
                parameter,
                argumentSyntax: null,
                allowByRefTemporaries: false,
                ref expression,
                out _);
            arguments.Add(new BoundArgument(parameter, expression));
        }

        if (hasParamArray)
        {
            var parameter = procedure.Parameters[^1];
            var arrayType = parameter.Type as ArrayTypeSymbol ?? new ArrayTypeSymbol(TypeSymbol.Error, 1);
            var values = ImmutableArray.CreateBuilder<BoundExpression>();
            for (var index = fixedParameterCount; index < argumentSyntaxes.Length; index++)
            {
                var expression = BindExpression(argumentSyntaxes[index], variables, procedures);
                values.Add(BindConversion(expression, arrayType.ElementType));
            }

            arguments.Add(new BoundArgument(
                parameter,
                new BoundParamArrayExpression(arrayType, values.ToImmutable())));
        }

        return arguments.ToImmutable();
    }

    private bool ValidateAndConvertArgument(
        SyntaxToken invocationIdentifier,
        ParameterSymbol parameter,
        ExpressionSyntax? argumentSyntax,
        bool allowByRefTemporaries,
        ref BoundExpression expression,
        out VariableSymbol? copyBackTarget)
    {
        copyBackTarget = null;

        if (parameter.PassingMode == ParameterPassingMode.ByVal)
        {
            expression = BindConversion(expression, parameter.Type);
            return false;
        }

        if (argumentSyntax is (ParenthesizedExpressionSyntax or CallSiteByValExpressionSyntax) &&
            allowByRefTemporaries)
        {
            expression = BindConversion(expression, parameter.Type);
            return true;
        }

        if (argumentSyntax is (ParenthesizedExpressionSyntax or CallSiteByValExpressionSyntax))
        {
            Report(
                "VB6S0046",
                $"ByRef temporary argument for parameter '{parameter.Name}' is not implemented for function-call expressions yet.",
                invocationIdentifier.Span);
            return false;
        }

        if (!IsByRefAssignableExpression(expression))
        {
            Report(
                "VB6S0007",
                $"ByRef argument for parameter '{parameter.Name}' must be a variable in the current compiler subset.",
                invocationIdentifier.Span);
            return false;
        }

        if (expression.Type != parameter.Type &&
            expression.Type != TypeSymbol.Error &&
            parameter.Type != TypeSymbol.Error)
        {
            if (expression is BoundVariableExpression variableExpression &&
                CanUseByRefCopyBackConversion(variableExpression.Variable.Type, parameter.Type))
            {
                copyBackTarget = variableExpression.Variable;
                expression = BindConversion(expression, parameter.Type);
                return true;
            }

            Report(
                "VB6S0008",
                $"ByRef argument type '{expression.Type.Name}' does not match parameter type '{parameter.Type.Name}'.",
                invocationIdentifier.Span);
        }

        return false;
    }

    private static bool IsByRefAssignableExpression(BoundExpression expression) =>
        expression is BoundVariableExpression or
            BoundMemberAccessExpression or
            BoundArrayElementExpression or
            BoundMemberArrayElementExpression;

    private static bool CanUseByRefCopyBackConversion(TypeSymbol sourceType, TypeSymbol parameterType) =>
        IsScalarRuntimeType(sourceType) && IsScalarRuntimeType(parameterType);

    private static bool IsScalarRuntimeType(TypeSymbol type) =>
        type is EnumTypeSymbol ||
        type == TypeSymbol.Byte ||
        type == TypeSymbol.Integer ||
        type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong ||
        type == TypeSymbol.Single ||
        type == TypeSymbol.Double ||
        type == TypeSymbol.Decimal ||
        type == TypeSymbol.Currency ||
        type == TypeSymbol.String ||
        type == TypeSymbol.Boolean ||
        type == TypeSymbol.Variant;

    private BoundExpression BindOptionalDefaultValue(
        ParameterSymbol parameter,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (parameter.DefaultValueSyntax is not null)
        {
            return BindExpression(
                parameter.DefaultValueSyntax,
                new Dictionary<string, VariableSymbol>(StringComparer.OrdinalIgnoreCase),
                procedures);
        }

        return CreateDefaultArgumentExpression(parameter.Type);
    }

    private static BoundExpression CreateDefaultArgumentExpression(TypeSymbol type)
    {
        if (type == TypeSymbol.Variant)
        {
            return new BoundLiteralExpression(VBVariantLiteral.Missing, TypeSymbol.Variant);
        }

        if (type == TypeSymbol.Object || type is ClassTypeSymbol)
        {
            return new BoundLiteralExpression(null, type);
        }

        if (type == TypeSymbol.String)
        {
            return new BoundLiteralExpression(string.Empty, TypeSymbol.String);
        }

        if (type == TypeSymbol.Boolean)
        {
            return new BoundLiteralExpression(false, TypeSymbol.Boolean);
        }

        if (type == TypeSymbol.Byte)
        {
            return new BoundLiteralExpression(0L, TypeSymbol.Byte);
        }

        if (type == TypeSymbol.Integer)
        {
            return new BoundLiteralExpression(0L, TypeSymbol.Integer);
        }

        if (type == TypeSymbol.Long)
        {
            return new BoundLiteralExpression(0L, TypeSymbol.Long);
        }

        if (type == TypeSymbol.LongLong)
        {
            return new BoundLiteralExpression(0L, TypeSymbol.LongLong);
        }

        if (type == TypeSymbol.Single)
        {
            return new BoundLiteralExpression(0f, TypeSymbol.Single);
        }

        if (type == TypeSymbol.Double)
        {
            return new BoundLiteralExpression(0d, TypeSymbol.Double);
        }

        if (type == TypeSymbol.Decimal)
        {
            return new BoundLiteralExpression(0m, TypeSymbol.Decimal);
        }

        if (type == TypeSymbol.Currency)
        {
            return new BoundLiteralExpression(0m, TypeSymbol.Currency);
        }

        if (type is EnumTypeSymbol)
        {
            return new BoundLiteralExpression(0L, TypeSymbol.Long);
        }

        return new BoundErrorExpression();
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
            SyntaxKind.EmptyKeyword =>
                new BoundLiteralExpression(VBVariantLiteral.Empty, TypeSymbol.Variant),
            SyntaxKind.NullKeyword =>
                new BoundLiteralExpression(VBVariantLiteral.Null, TypeSymbol.Variant),
            SyntaxKind.NothingKeyword =>
                new BoundLiteralExpression(VBVariantLiteral.Nothing, TypeSymbol.Variant),
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

    private static bool TryGetConstantInt64(BoundExpression expression, out long value)
    {
        switch (expression)
        {
            case BoundLiteralExpression literal:
                value = Convert.ToInt64(literal.Value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            case BoundConversionExpression conversion:
                return TryGetConstantInt64(conversion.Expression, out value);
            case BoundUnaryExpression { OperatorKind: SyntaxKind.MinusToken } unary
                when TryGetConstantInt64(unary.Operand, out var operand):
                value = -operand;
                return true;
            default:
                value = 0;
                return false;
        }
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

        if (operand.Type == TypeSymbol.Variant)
        {
            return syntax.OperatorToken.Kind switch
            {
                SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.NotKeyword =>
                    new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Variant),
                _ => operand
            };
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

        if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
        {
            return BindVariantBinary(syntax, left, right);
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
                var resultType = left.Type == TypeSymbol.Decimal || right.Type == TypeSymbol.Decimal
                    ? TypeSymbol.Decimal
                    : IsSingleDivisionOperand(left.Type) && IsSingleDivisionOperand(right.Type)
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

    private BoundExpression BindVariantBinary(
        BinaryExpressionSyntax syntax,
        BoundExpression left,
        BoundExpression right)
    {
        return syntax.OperatorToken.Kind switch
        {
            SyntaxKind.CaretToken or
            SyntaxKind.PlusToken or
            SyntaxKind.MinusToken or
            SyntaxKind.StarToken or
            SyntaxKind.SlashToken or
            SyntaxKind.BackslashToken or
            SyntaxKind.ModKeyword or
            SyntaxKind.AmpersandToken or
            SyntaxKind.AndKeyword or
            SyntaxKind.OrKeyword or
            SyntaxKind.XorKeyword or
            SyntaxKind.EqvKeyword or
            SyntaxKind.ImpKeyword => new BoundBinaryExpression(
                left,
                syntax.OperatorToken.Kind,
                right,
                TypeSymbol.Variant),
            SyntaxKind.EqualsToken or
            SyntaxKind.LessGreaterToken or
            SyntaxKind.LessToken or
            SyntaxKind.LessOrEqualsToken or
            SyntaxKind.GreaterToken or
            SyntaxKind.GreaterOrEqualsToken => new BoundBinaryExpression(
                left,
                syntax.OperatorToken.Kind,
                right,
                TypeSymbol.Variant),
            _ => new BoundErrorExpression()
        };
    }

    private static bool IsNumericType(TypeSymbol type) =>
        type is EnumTypeSymbol ||
        type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong || type == TypeSymbol.Single || type == TypeSymbol.Double ||
        type == TypeSymbol.Decimal || type == TypeSymbol.Currency;

    private static bool IsFloatingOrFixedPointType(TypeSymbol type)
    {
        type = GetNumericOperationType(type);
        return type == TypeSymbol.Single || type == TypeSymbol.Double || type == TypeSymbol.Decimal ||
            type == TypeSymbol.Currency;
    }

    private static bool IsSingleDivisionOperand(TypeSymbol type)
    {
        type = GetNumericOperationType(type);
        return type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Single;
    }

    private static bool IsBitwiseOperandType(TypeSymbol type) =>
        IsNumericType(type) || type == TypeSymbol.Boolean;

    /// <summary>
    /// Result type of the VB6 operators that work on whole numbers: '\\', 'Mod' and the bitwise
    /// operators. Floating-point and Currency operands are rounded to Long first.
    /// </summary>
    private static TypeSymbol GetIntegerOperationType(TypeSymbol left, TypeSymbol right)
    {
        left = GetNumericOperationType(left);
        right = GetNumericOperationType(right);
        return left == TypeSymbol.LongLong || right == TypeSymbol.LongLong
            ? TypeSymbol.LongLong
            : IsFloatingOrFixedPointType(left) || IsFloatingOrFixedPointType(right) ||
              left == TypeSymbol.Long || right == TypeSymbol.Long
                ? TypeSymbol.Long
                : left == TypeSymbol.Byte && right == TypeSymbol.Byte
                    ? TypeSymbol.Byte
                    : TypeSymbol.Integer;
    }

    private static TypeSymbol GetCommonNumericType(TypeSymbol left, TypeSymbol right)
    {
        left = GetNumericOperationType(left);
        right = GetNumericOperationType(right);

        if (left == TypeSymbol.Double || right == TypeSymbol.Double)
        {
            return TypeSymbol.Double;
        }

        if (left == TypeSymbol.Decimal || right == TypeSymbol.Decimal)
        {
            return TypeSymbol.Decimal;
        }

        if (left == TypeSymbol.Currency || right == TypeSymbol.Currency)
        {
            return TypeSymbol.Currency;
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

    private static TypeSymbol GetNumericOperationType(TypeSymbol type) =>
        type is EnumTypeSymbol ? TypeSymbol.Long : type;

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
