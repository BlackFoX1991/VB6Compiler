using System.Globalization;
using System.Collections.Immutable;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;

namespace VB6.Semantics;

public sealed class Binder
{
    private static readonly ProcedureSymbol MissingValueProcedure =
        new("Missing", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.Variant)
        {
            IntrinsicKind = VBIntrinsicKind.Missing,
            IntrinsicTarget = "VBVariants.MissingValue"
        };

    private readonly SourceText _text;
    private readonly IReadOnlyDictionary<string, long> _qualifiedEnumMembers;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private readonly ImmutableArray<BoundModuleVariable>.Builder _staticVariables =
        ImmutableArray.CreateBuilder<BoundModuleVariable>();
    private readonly List<LoopBindingContext> _loopStack = new();
    /// <summary>Labels declared anywhere in the current procedure body.</summary>
    private readonly HashSet<string> _procedureLabels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<WithBindingContext> _withStack = new();
    private ClassTypeSymbol? _containingClass;
    private Dictionary<string, LocalVariableSymbol>? _activeLocals;
    private Dictionary<string, BoundExpression>? _activeConstantInitializers;
    private bool _optionExplicit;
    private int _nextLoopId;
    private int _nextSelectId;
    private int _nextWithId;
    private int _optionBase;
    private bool _optionCompareText;

    public Binder(
        SourceText text,
        IReadOnlyDictionary<string, long>? qualifiedEnumMembers = null)
    {
        _text = text;
        _qualifiedEnumMembers = qualifiedEnumMembers ??
            ImmutableDictionary.Create<string, long>(StringComparer.OrdinalIgnoreCase);
    }

    public static ProcedureSymbol CreateProcedureSymbol(SubDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ProcedureSymbol(declaration.Identifier.Text, CreateParameterSymbols(declaration.Parameters));
    }

    public static ProcedureSymbol CreateProcedureSymbol(FunctionDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        // A missing As clause means Variant. ImplicitVariantSyntaxLowerer normally fills it in
        // before binding; this keeps a directly bound tree on the same rule instead of failing.
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbols(declaration.Parameters),
            CreateProcedureReturnType(
                declaration.ReturnTypeToken,
                declaration.ReturnTypeName,
                declaration.ReturnOpenParenthesisToken is not null,
                declaration.Identifier));
    }

    public static ImmutableArray<ModuleVariableSymbol> CreateModuleVariableSymbols(
        SourceText text,
        CompilationUnitSyntax root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var binder = new Binder(text);
        binder.ApplyModuleOptions(root);
        return binder.DeclareModuleVariables(root).Scope.Values.ToImmutableArray();
    }

    public SemanticModel BindCompilationUnit(CompilationUnitSyntax root)
    {
        var procedures = DeclareProcedures(root);
        return BindCompilationUnit(root, procedures);
    }

    public SemanticModel BindCompilationUnit(
        CompilationUnitSyntax root,
        IReadOnlyDictionary<string, ProcedureSymbol> availableProcedures,
        IReadOnlyDictionary<string, ModuleVariableSymbol>? availableModuleVariables = null,
        ClassTypeSymbol? containingClass = null)
    {
        ArgumentNullException.ThrowIfNull(availableProcedures);

        _containingClass = containingClass;
        ApplyModuleOptions(root);
        var declared = DeclareModuleVariables(root, availableModuleVariables);
        var moduleVariables = new Dictionary<string, ModuleVariableSymbol>(
            declared.Scope,
            StringComparer.OrdinalIgnoreCase);
        if (availableModuleVariables is not null)
        {
            foreach (var entry in availableModuleVariables)
            {
                // A class/module-local declaration shadows a project-wide public variable with
                // the same name. Existing project symbols are still reused when there is no local
                // declaration, preserving identity for cross-module references.
                moduleVariables.TryAdd(entry.Key, entry.Value);
            }
        }
        if (containingClass is not null)
        {
            moduleVariables.TryAdd("Me", new ModuleVariableSymbol("Me", containingClass));
        }
        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        var properties = ImmutableArray.CreateBuilder<PropertySymbol>();
        var events = ImmutableArray.CreateBuilder<EventSymbol>();
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
                    if (symbol.ReturnType == TypeSymbol.Error && declaration.ReturnTypeToken is not null)
                    {
                        Report(
                            "VB6S0011",
                            $"Unknown function return type '{GetDeclaredTypeName(declaration.ReturnTypeToken, declaration.ReturnTypeName)}'.",
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
                    var property = CreatePropertySymbol(declaration);
                    var propertyProcedure = CreatePropertyProcedureSymbol(declaration);
                    properties.Add(property);
                    if (propertyProcedure.ReturnType == TypeSymbol.Error && declaration.ReturnTypeToken is not null)
                    {
                        Report(
                            "VB6S0011",
                            $"Unknown property return type '{GetDeclaredTypeName(declaration.ReturnTypeToken, declaration.ReturnTypeName)}'.",
                            declaration.ReturnTypeToken.Span);
                    }

                    procedures.Add(BindProcedure(
                        declaration.Identifier,
                        declaration.Parameters,
                        declaration.Statements,
                        declaration.ReturnTypeToken,
                        propertyProcedure,
                        availableProcedures,
                        moduleVariables));
                    break;
                }

                case EventDeclarationSyntax declaration:
                    events.Add(CreateEventSymbol(declaration));
                    break;
            }
        }

        var externalProcedures = root.Members
            .OfType<DeclareDeclarationSyntax>()
            .Select(declaration =>
                availableProcedures.TryGetValue(declaration.Identifier.Text, out var procedure)
                    ? procedure
                    : CreateDeclareProcedureSymbol(declaration))
            .ToImmutableArray();

        return new SemanticModel(procedures.ToImmutable(), _diagnostics.ToImmutable())
        {
            ExternalProcedures = externalProcedures,
            Properties = properties.ToImmutable(),
            Events = events.ToImmutable(),
            ModuleVariables = declared.Bound,
            StaticVariables = _staticVariables.ToImmutable(),
            InstanceVariables = _containingClass is null
                ? ImmutableArray<BoundModuleVariable>.Empty
                : declared.Bound,
            ContainingClass = _containingClass
        };
    }

    private void ApplyModuleOptions(CompilationUnitSyntax root)
    {
        _optionBase = 0;
        _optionExplicit = root.Members.OfType<OptionExplicitSyntax>().Any();
        _optionCompareText = false;
        foreach (var member in root.Members)
        {
            if (member is OptionBaseSyntax optionBase)
            {
                _optionBase = optionBase.ValueToken.Text == "1" ? 1 : 0;
            }
            else if (member is OptionCompareSyntax optionCompare)
            {
                _optionCompareText = string.Equals(
                    optionCompare.ModeToken.Text,
                    "Text",
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static ImmutableArray<ParameterSymbol> CreateParameterSymbols(ImmutableArray<ParameterSyntax> parameters) =>
        parameters
            .Select(parameter =>
            {
                var declaredTypeName = GetDeclaredTypeName(parameter.TypeToken, parameter.TypeName);
                var suffixType = GetIdentifierType(parameter.Identifier);
                var elementType = parameter.IsParamArray
                    ? TypeSymbol.Variant
                    : parameter.TypeToken is null
                    ? suffixType ?? TypeSymbol.Variant
                    : string.Equals(declaredTypeName, "Any", StringComparison.OrdinalIgnoreCase)
                    ? TypeSymbol.Variant
                    : TypeSymbol.Lookup(declaredTypeName!) ?? TypeSymbol.Error;
                var type = (parameter.IsArray || parameter.IsParamArray) && elementType != TypeSymbol.Error
                    ? new ArrayTypeSymbol(elementType)
                    : elementType;

                return new ParameterSymbol(
                    parameter.Identifier.Text,
                    type,
                    parameter.IsParamArray || parameter.PassingModeKeyword?.Kind == SyntaxKind.ByValKeyword
                        ? ParameterPassingMode.ByVal
                        : ParameterPassingMode.ByRef)
                {
                    IsOptional = parameter.OptionalKeyword is not null,
                    IsParamArray = parameter.IsParamArray,
                    IsAny = string.Equals(declaredTypeName, "Any", StringComparison.OrdinalIgnoreCase),
                    DefaultValue = (parameter.DefaultValue as LiteralExpressionSyntax)?.LiteralToken.Value
                };
            })
            .ToImmutableArray();

    private static ProcedureSymbol CreateErrProcedure(
        string name,
        VBIntrinsicKind kind,
        string target,
        TypeSymbol? returnType,
        params ParameterSymbol[] parameters) =>
        new(name, parameters.ToImmutableArray(), returnType)
        {
            IntrinsicKind = kind,
            IntrinsicTarget = target
        };

    private static bool IsErrReceiver(BoundExpression receiver) =>
        receiver is BoundVariableExpression variable &&
        string.Equals(variable.Variable.Name, "Err", StringComparison.OrdinalIgnoreCase);

    private static bool IsErrReceiver(ExpressionSyntax receiver) =>
        receiver is NameExpressionSyntax name &&
        string.Equals(name.IdentifierToken.Text, "Err", StringComparison.OrdinalIgnoreCase);

    private static ProcedureSymbol? GetErrMemberProcedure(string memberName)
    {
        if (string.Equals(memberName, "Number", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrProcedure("Err.Number", VBIntrinsicKind.ErrNumber, "VBErrors.NumberValue", TypeSymbol.Long);
        }

        if (string.Equals(memberName, "Description", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrProcedure("Err.Description", VBIntrinsicKind.ErrDescription, "VBErrors.DescriptionValue", TypeSymbol.String);
        }

        if (string.Equals(memberName, "Source", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrProcedure("Err.Source", VBIntrinsicKind.ErrSource, "VBErrors.SourceValue", TypeSymbol.String);
        }

        if (string.Equals(memberName, "Clear", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrProcedure("Err.Clear", VBIntrinsicKind.ErrClear, "VBErrors.Clear", null);
        }

        if (string.Equals(memberName, "Raise", StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrProcedure(
                "Err.Raise",
                VBIntrinsicKind.ErrRaise,
                "VBErrors.Raise",
                null,
                new ParameterSymbol("Number", TypeSymbol.Long, ParameterPassingMode.ByVal),
                new ParameterSymbol("Source", TypeSymbol.String, ParameterPassingMode.ByVal)
                {
                    IsOptional = true,
                    DefaultValue = string.Empty
                },
                new ParameterSymbol("Description", TypeSymbol.String, ParameterPassingMode.ByVal)
                {
                    IsOptional = true,
                    DefaultValue = string.Empty
                },
                new ParameterSymbol("HelpFile", TypeSymbol.String, ParameterPassingMode.ByVal)
                {
                    IsOptional = true,
                    DefaultValue = string.Empty
                },
                new ParameterSymbol("HelpContext", TypeSymbol.Long, ParameterPassingMode.ByVal)
                {
                    IsOptional = true,
                    DefaultValue = 0L
                });
        }

        return null;
    }

    public static ProcedureSymbol CreateDeclareProcedureSymbol(DeclareDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        var isFunction = declaration.ProcedureKindKeyword.Kind == SyntaxKind.FunctionKeyword;
        var returnType = !isFunction
            ? null
            : CreateProcedureReturnType(
                declaration.ReturnTypeToken,
                declaration.ReturnTypeName,
                declaration.ReturnOpenParenthesisToken is not null,
                declaration.Identifier);
        return new ProcedureSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbols(declaration.Parameters),
            returnType)
        {
            IsExternal = true,
            ExternalLibrary = declaration.LibraryName.Value as string ?? declaration.LibraryName.Text,
            ExternalAlias = declaration.AliasName?.Value as string ?? declaration.AliasName?.Text
        };
    }

    public static PropertySymbol CreatePropertySymbol(PropertyDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        var parameters = CreateParameterSymbols(declaration.Parameters);
        var type = declaration.IsGet
            ? declaration.ReturnTypeToken is null
                ? GetIdentifierType(declaration.Identifier) ?? TypeSymbol.Variant
                : CreateProcedureReturnType(
                    declaration.ReturnTypeToken,
                    declaration.ReturnTypeName,
                    declaration.ReturnOpenParenthesisToken is not null,
                    declaration.Identifier)
            : parameters.Length == 0
                ? TypeSymbol.Variant
                : parameters[^1].Type;
        return new PropertySymbol(
            declaration.Identifier.Text,
            declaration.IsGet
                ? PropertyAccessorKind.Get
                : declaration.IsLet
                ? PropertyAccessorKind.Let
                : PropertyAccessorKind.Set,
            type,
            parameters);
    }

    private static ProcedureSymbol CreatePropertyProcedureSymbol(PropertyDeclarationSyntax declaration)
    {
        var property = CreatePropertySymbol(declaration);
        return new ProcedureSymbol(
            property.Name,
            property.Parameters,
            declaration.IsGet ? property.Type : null)
        {
            PropertyAccessor = property.Accessor
        };
    }

    public static EventSymbol CreateEventSymbol(EventDeclarationSyntax declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new EventSymbol(
            declaration.Identifier.Text,
            CreateParameterSymbols(declaration.Parameters));
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
                case DeclareDeclarationSyntax declare:
                    symbol = CreateDeclareProcedureSymbol(declare);
                    identifier = declare.Identifier;
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
    /// Declares the module-level variables of <paramref name="root"/>.
    ///
    /// When the caller already holds a symbol table - the project pipeline builds one up front so
    /// that Public variables are shared across modules - the existing symbol has to be reused
    /// rather than replaced by an equal-looking new one. Procedure bodies bind against the
    /// caller's table, so a fresh instance here would leave the bound model referring to symbols
    /// that no consumer can match by identity.
    /// </summary>
    private (Dictionary<string, ModuleVariableSymbol> Scope, ImmutableArray<BoundModuleVariable> Bound)
        DeclareModuleVariables(
            CompilationUnitSyntax root,
            IReadOnlyDictionary<string, ModuleVariableSymbol>? existingSymbols = null)
    {
        var scope = new Dictionary<string, ModuleVariableSymbol>(StringComparer.OrdinalIgnoreCase);
        var availableScope = existingSymbols is null
            ? new Dictionary<string, ModuleVariableSymbol>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ModuleVariableSymbol>(
                existingSymbols,
                StringComparer.OrdinalIgnoreCase);
        var bound = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        var noProcedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);

        ModuleVariableSymbol Declare(string name, TypeSymbol type) =>
            existingSymbols is not null &&
            existingSymbols.TryGetValue(name, out var existing) &&
            existing.Type == type
                ? existing
                : new ModuleVariableSymbol(name, type);

        foreach (var member in root.Members)
        {
            switch (member)
            {
                case ModuleVariableDeclarationSyntax declaration:
                {
                    foreach (var declarator in declaration.Declarators)
                    {
                        var visible = CreateVisibleModuleScope();
                        var type = ResolveVariableDeclaratorType(declarator);
                        var dimensions = BindArrayDimensions(declarator, visible, noProcedures);
                        var symbol = Declare(declarator.Identifier.Text, type);
                        if (TryDeclareModuleVariable(scope, symbol, declarator.Identifier))
                        {
                            availableScope[symbol.Name] = symbol;
                            bound.Add(new BoundModuleVariable(
                                symbol,
                                BindImplicitObjectInitializer(declarator, type),
                                IsConstant: false,
                                dimensions)
                            {
                                IsWithEvents = declaration.WithEventsKeyword is not null
                            });
                        }
                    }

                    break;
                }

                case ConstDeclarationSyntax declaration:
                {
                    var visible = CreateVisibleModuleScope();
                    var value = BindExpression(declaration.Value, visible, noProcedures);
                    var type = declaration.TypeToken is null
                        ? GetIdentifierType(declaration.Identifier) ?? value.Type
                        : ResolveDeclaredType(declaration.TypeToken, declaration.TypeName);
                    var symbol = Declare(declaration.Identifier.Text, type) with
                    {
                        IsConstant = true
                    };
                    if (TryDeclareModuleVariable(scope, symbol, declaration.Identifier))
                    {
                        availableScope[symbol.Name] = symbol;
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

        Dictionary<string, VariableSymbol> CreateVisibleModuleScope()
        {
            var visible = availableScope.ToDictionary(
                entry => entry.Key,
                entry => (VariableSymbol)entry.Value,
                StringComparer.OrdinalIgnoreCase);
            foreach (var entry in scope)
            {
                visible[entry.Key] = entry.Value;
            }

            return visible;
        }
    }

    private TypeSymbol ResolveVariableDeclaratorType(VariableDeclaratorSyntax declarator)
    {
        if (declarator.TypeToken is null)
        {
            var suffixType = GetIdentifierType(declarator.Identifier);
            if (suffixType is not null)
            {
                if (!declarator.IsArray)
                {
                    return ValidateImplicitObjectType(declarator, suffixType);
                }

                return ValidateImplicitObjectType(
                    declarator,
                    declarator.Dimensions.IsDefaultOrEmpty
                    ? new ArrayTypeSymbol(suffixType)
                    : new ArrayTypeSymbol(suffixType, declarator.Dimensions.Length));
            }

            Report(
                "VB6S0020",
                $"Variable '{declarator.Identifier.Text}' has implicit Variant type, which is not supported yet.",
                declarator.Identifier.Span);
            return TypeSymbol.Error;
        }

        var elementType = ResolveDeclaredType(declarator.TypeToken, declarator.TypeName);
        var type = !declarator.IsArray || elementType == TypeSymbol.Error
            ? elementType
            : declarator.Dimensions.IsDefaultOrEmpty
            ? new ArrayTypeSymbol(elementType)
            : new ArrayTypeSymbol(elementType, declarator.Dimensions.Length);
        return ValidateImplicitObjectType(declarator, type);
    }

    private TypeSymbol ValidateImplicitObjectType(VariableDeclaratorSyntax declarator, TypeSymbol type)
    {
        if (declarator.NewKeyword is not null && type is not ClassTypeSymbol)
        {
            Report(
                "VB6S0063",
                $"As New requires an object type, but '{type.Name}' is not an object type.",
                declarator.NewKeyword.Span);
        }

        return type;
    }

    private static BoundExpression? BindImplicitObjectInitializer(
        VariableDeclaratorSyntax declarator,
        TypeSymbol type) =>
        declarator.NewKeyword is not null && type is ClassTypeSymbol classType
            ? new BoundNewExpression(classType)
            : null;

    private ImmutableArray<BoundArrayDimension> BindArrayDimensions(
        VariableDeclaratorSyntax declarator,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return !declarator.IsArray || declarator.Dimensions.IsDefaultOrEmpty
            ? ImmutableArray<BoundArrayDimension>.Empty
            : BindArrayDimensionList(declarator.Dimensions, variables, procedures);
    }

    private ImmutableArray<BoundArrayDimension> BindArrayDimensionList(
        ImmutableArray<ArrayDimensionSyntax> syntaxes,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (syntaxes.IsDefaultOrEmpty)
        {
            return ImmutableArray<BoundArrayDimension>.Empty;
        }

        var dimensions = ImmutableArray.CreateBuilder<BoundArrayDimension>(syntaxes.Length);
        foreach (var dimension in syntaxes)
        {
            var lowerBound = dimension.LowerBound is null
                ? new BoundLiteralExpression((long)_optionBase, TypeSymbol.Long)
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

    private static string? GetDeclaredTypeName(SyntaxToken? typeToken, TypeNameSyntax? typeName) =>
        typeName?.Text ?? typeToken?.Text;

    private static TypeSymbol? GetIdentifierType(SyntaxToken identifier) => identifier.TypeSuffix switch
    {
        '$' => TypeSymbol.String,
        '%' => TypeSymbol.Integer,
        '&' => TypeSymbol.Long,
        '!' => TypeSymbol.Single,
        '#' => TypeSymbol.Double,
        '@' => TypeSymbol.Currency,
        _ => null
    };

    private static TypeSymbol CreateProcedureReturnType(
        SyntaxToken? typeToken,
        TypeNameSyntax? typeName,
        bool isArray,
        SyntaxToken? identifier = null)
    {
        if (typeToken is null)
        {
            return identifier is null
                ? TypeSymbol.Variant
                : GetIdentifierType(identifier) ?? TypeSymbol.Variant;
        }

        if (string.Equals(GetDeclaredTypeName(typeToken, typeName), "Any", StringComparison.OrdinalIgnoreCase))
        {
            return TypeSymbol.Variant;
        }

        var type = TypeSymbol.Lookup(GetDeclaredTypeName(typeToken, typeName)!) ?? TypeSymbol.Error;
        return isArray && type != TypeSymbol.Error
            ? new ArrayTypeSymbol(type)
            : type;
    }

    private TypeSymbol ResolveDeclaredType(SyntaxToken typeToken, TypeNameSyntax? typeName = null)
    {
        var declaredTypeName = GetDeclaredTypeName(typeToken, typeName)!;
        var type = TypeSymbol.Lookup(declaredTypeName);
        if (type is not null)
        {
            return type;
        }

        Report("VB6S0003", $"Unknown type '{declaredTypeName}'.", typeToken.Span);
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

        foreach (var moduleVariable in moduleVariables)
        {
            variables[moduleVariable.Key] = moduleVariable.Value;
        }

        if (_containingClass is not null)
        {
            variables["Me"] = new ModuleVariableSymbol("Me", _containingClass);
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
                    $"Unknown type '{GetDeclaredTypeName(syntax.TypeToken, syntax.TypeName) ?? "Variant"}'.",
                    syntax.TypeToken?.Span ?? syntax.Identifier.Span);
            }

            if (syntax.IsArray && !syntax.IsParamArray &&
                syntax.PassingModeKeyword?.Kind == SyntaxKind.ByValKeyword)
            {
                Report(
                    "VB6S0028",
                    $"Array parameter '{syntax.Identifier.Text}' must be passed ByRef.",
                    syntax.PassingModeKeyword.Span);
            }

            if (syntax.IsArray && !syntax.Dimensions.IsDefaultOrEmpty)
            {
                Report(
                    "VB6S0032",
                    $"Array parameter '{syntax.Identifier.Text}' cannot specify a fixed rank or bounds.",
                    syntax.Identifier.Span);
            }

            if (syntax.IsParamArray)
            {
                if (index != parameterSyntaxes.Length - 1)
                {
                    Report(
                        "VB6S0062",
                        $"ParamArray parameter '{syntax.Identifier.Text}' must be the last parameter.",
                        syntax.Identifier.Span);
                }

                if (parameterSyntaxes.Any(parameterSyntax => parameterSyntax.OptionalKeyword is not null) ||
                    syntax.PassingModeKeyword is not null)
                {
                    Report(
                        "VB6S0063",
                        $"ParamArray parameter '{syntax.Identifier.Text}' cannot be Optional, ByVal, or ByRef.",
                        syntax.Identifier.Span);
                }

                if (!syntax.IsArray)
                {
                    Report(
                        "VB6S0064",
                        $"ParamArray parameter '{syntax.Identifier.Text}' must be declared as an array.",
                        syntax.Identifier.Span);
                }
                else if (syntax.TypeToken is not null &&
                         !string.Equals(GetDeclaredTypeName(syntax.TypeToken, syntax.TypeName), "Variant", StringComparison.OrdinalIgnoreCase))
                {
                    Report(
                        "VB6S0064",
                        $"ParamArray parameter '{syntax.Identifier.Text}' must be an array of Variant.",
                        syntax.TypeName?.FirstToken.Span ?? syntax.TypeToken.Span);
                }

                if (syntax.DefaultValue is not null)
                {
                    Report(
                        "VB6S0065",
                        $"ParamArray parameter '{syntax.Identifier.Text}' cannot have a default value.",
                        syntax.EqualsToken?.Span ?? syntax.Identifier.Span);
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

        _activeConstantInitializers = new Dictionary<string, BoundExpression>(StringComparer.OrdinalIgnoreCase);
        PredeclareLocals(statements, locals, variables, procedures, identifier.Text);

        _procedureLabels.Clear();
        CollectProcedureLabels(statements);

        _activeLocals = locals;
        BoundBlockStatement body;
        try
        {
            body = BindStatements(statements, variables, procedures);
        }
        finally
        {
            _activeLocals = null;
            _activeConstantInitializers = null;
        }

        return new BoundProcedure(symbol, locals.Values.ToImmutableArray(), body);
    }

    private void CollectProcedureLabels(ImmutableArray<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case LabelStatementSyntax label:
                    _procedureLabels.Add(label.Identifier.Text);
                    break;
                case IfStatementSyntax @if:
                    CollectProcedureLabels(@if.Statements);
                    foreach (var clause in @if.ElseIfClauses)
                    {
                        CollectProcedureLabels(clause.Statements);
                    }

                    CollectProcedureLabels(@if.ElseStatements);
                    break;
                case ForStatementSyntax @for:
                    CollectProcedureLabels(@for.Statements);
                    break;
                case ForEachStatementSyntax forEach:
                    CollectProcedureLabels(forEach.Statements);
                    break;
                case WhileStatementSyntax @while:
                    CollectProcedureLabels(@while.Statements);
                    break;
                case DoStatementSyntax @do:
                    CollectProcedureLabels(@do.Statements);
                    break;
                case WithStatementSyntax with:
                    CollectProcedureLabels(with.Statements);
                    break;
                case SelectCaseStatementSyntax select:
                    foreach (var @case in select.Cases)
                    {
                        CollectProcedureLabels(@case.Statements);
                    }
                    break;
            }
        }
    }

    private void PredeclareLocals(
        ImmutableArray<StatementSyntax> statements,
        Dictionary<string, LocalVariableSymbol> locals,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        string procedureName)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DimStatementSyntax dim:
                    PredeclareLocalDeclarators(dim.Declarators, locals, variables);
                    break;
                case ConstStatementSyntax constant:
                    PredeclareLocalConstant(constant, locals, variables, procedures);
                    break;
                case StaticStatementSyntax staticStatement:
                    PredeclareStaticDeclarators(
                        staticStatement.Declarators,
                        procedureName,
                        variables);
                    break;
                case IfStatementSyntax ifStatement:
                    PredeclareLocals(ifStatement.Statements, locals, variables, procedures, procedureName);
                    foreach (var elseIfClause in ifStatement.ElseIfClauses)
                    {
                        PredeclareLocals(elseIfClause.Statements, locals, variables, procedures, procedureName);
                    }
                    PredeclareLocals(ifStatement.ElseStatements, locals, variables, procedures, procedureName);
                    break;
                case ForStatementSyntax forStatement:
                    PredeclareLocals(forStatement.Statements, locals, variables, procedures, procedureName);
                    break;
                case ForEachStatementSyntax forEachStatement:
                    PredeclareLocals(forEachStatement.Statements, locals, variables, procedures, procedureName);
                    break;
                case WhileStatementSyntax whileStatement:
                    PredeclareLocals(whileStatement.Statements, locals, variables, procedures, procedureName);
                    break;
                case DoStatementSyntax doStatement:
                    PredeclareLocals(doStatement.Statements, locals, variables, procedures, procedureName);
                    break;
                case WithStatementSyntax withStatement:
                    PredeclareLocals(withStatement.Statements, locals, variables, procedures, procedureName);
                    break;
                case SelectCaseStatementSyntax selectStatement:
                    foreach (var caseBlock in selectStatement.Cases)
                    {
                        PredeclareLocals(caseBlock.Statements, locals, variables, procedures, procedureName);
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

    private void PredeclareLocalConstant(
        ConstStatementSyntax syntax,
        Dictionary<string, LocalVariableSymbol> locals,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var value = BindExpression(syntax.Value, variables, procedures);
        var type = syntax.TypeToken is null
            ? GetIdentifierType(syntax.Identifier) ?? value.Type
            : ResolveDeclaredType(syntax.TypeToken, syntax.TypeName);
        var variable = new LocalVariableSymbol(syntax.Identifier.Text, type)
        {
            IsConstant = true
        };

        if (!TryDeclareInProcedureScope(variables, variable.Name, variable))
        {
            Report(
                "VB6S0002",
                $"Local variable '{variable.Name}' is already declared.",
                syntax.Identifier.Span);
            return;
        }

        locals.Add(variable.Name, variable);
        _activeConstantInitializers![variable.Name] = BindConversion(value, type);
    }

    private void PredeclareStaticDeclarators(
        ImmutableArray<VariableDeclaratorSyntax> declarators,
        string procedureName,
        Dictionary<string, VariableSymbol> variables)
    {
        foreach (var declarator in declarators)
        {
            var type = ResolveVariableDeclaratorType(declarator);
            var storageName = $"__static_{_text.FilePath ?? "Module1"}_{procedureName}_{declarator.Identifier.Text}";
            var variable = new ModuleVariableSymbol(storageName, type);
            if (!TryDeclareInProcedureScope(variables, declarator.Identifier.Text, variable))
            {
                Report(
                    "VB6S0002",
                    $"Local variable '{declarator.Identifier.Text}' is already declared.",
                    declarator.Identifier.Span);
                continue;
            }

            _staticVariables.Add(new BoundModuleVariable(
                variable,
                BindImplicitObjectInitializer(declarator, type),
                IsConstant: false,
                ImmutableArray<BoundArrayDimension>.Empty));
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
            // Dim, ReDim and Erase each bind one statement into several bound statements, one per
            // declarator or identifier. They all share the position of the statement they were
            // written as, which is what a debugger steps to.
            if (statement is ConstStatementSyntax constant)
            {
                if (variables.TryGetValue(constant.Identifier.Text, out var variable) &&
                    variable is LocalVariableSymbol local &&
                    _activeConstantInitializers is not null &&
                    _activeConstantInitializers.TryGetValue(local.Name, out var initializer))
                {
                    bound.Add(WithLocation(
                        new BoundVariableDeclarationStatement(
                            local,
                            ImmutableArray<BoundArrayDimension>.Empty,
                            initializer),
                        statement));
                }

                continue;
            }

            if (statement is DimStatementSyntax dim)
            {
                foreach (var declarator in dim.Declarators)
                {
                    bound.Add(WithLocation(
                        BindVariableDeclaration(declarator, variables, procedures),
                        statement));
                }
                continue;
            }

            if (statement is ReDimStatementSyntax reDim)
            {
                foreach (var declarator in reDim.Declarators)
                {
                    bound.Add(WithLocation(
                        BindReDim(
                            declarator,
                            reDim.PreserveKeyword is not null,
                            variables,
                            procedures),
                        statement));
                }

                foreach (var target in reDim.QualifiedTargets)
                {
                    var boundTarget = BindQualifiedReDim(
                        target,
                        reDim.PreserveKeyword is not null,
                        variables,
                        procedures);
                    if (boundTarget is not null)
                    {
                        bound.Add(WithLocation(boundTarget, statement));
                    }
                }
                continue;
            }

            if (statement is EraseStatementSyntax erase)
            {
                if (erase.MemberDotToken is not null)
                {
                    if (!erase.Identifiers.IsDefaultOrEmpty)
                    {
                        var memberSyntax = new MemberAccessExpressionSyntax(
                            new WithReceiverExpressionSyntax(),
                            erase.MemberDotToken,
                            erase.Identifiers[0]);
                        var memberTarget = BindMemberAccess(memberSyntax, variables, procedures);
                        if (memberTarget.Type != TypeSymbol.Error)
                        {
                            bound.Add(WithLocation(
                                BindErase(memberTarget, erase.Identifiers[0]),
                                statement));
                        }
                    }

                    continue;
                }

                foreach (var eraseIdentifier in erase.Identifiers)
                {
                    bound.Add(WithLocation(BindErase(eraseIdentifier, variables), statement));
                }
                continue;
            }

            if (statement is StaticStatementSyntax)
            {
                foreach (var declarator in ((StaticStatementSyntax)statement).Declarators)
                {
                    if (!variables.TryGetValue(declarator.Identifier.Text, out var variable) ||
                        variable is not ModuleVariableSymbol staticVariable)
                    {
                        continue;
                    }

                    for (var index = 0; index < _staticVariables.Count; index++)
                    {
                        if (ReferenceEquals(_staticVariables[index].Symbol, staticVariable))
                        {
                            _staticVariables[index] = _staticVariables[index] with
                            {
                                ArrayDimensions = BindArrayDimensions(declarator, variables, procedures)
                            };
                            break;
                        }
                    }
                }

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

    /// <summary>
    /// Binds one statement and records where it came from. Every bound statement passes through
    /// here, so attaching the position once covers the whole language - and it is attached
    /// referentially, to the node the statement produced, rather than by counting lines later.
    /// </summary>
    private BoundStatement? BindStatement(
        StatementSyntax statement,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var bound = BindStatementCore(statement, variables, procedures);
        return bound is null ? null : WithLocation(bound, statement);
    }

    /// <summary>
    /// Attaches the source position of <paramref name="statement"/> unless the bound node already
    /// carries one - a lowering pass that rewrote the statement knows better where it belongs.
    /// </summary>
    private BoundStatement WithLocation(BoundStatement bound, StatementSyntax statement)
    {
        if (bound.SourceLocation is not null)
        {
            return bound;
        }

        var token = SyntaxNavigator.GetFirstToken(statement);
        return token is null
            ? bound
            : bound with { SourceLocation = new SourceLocation(_text.FilePath, token.Span, _text.GetLinePositionSpan(token.Span)) };
    }

    private BoundStatement? BindStatementCore(
        StatementSyntax statement,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return statement switch
        {
            AssignmentStatementSyntax assignment => BindAssignment(assignment, variables, procedures),
            SetAssignmentStatementSyntax setAssignment => BindSetAssignment(setAssignment, variables, procedures),
            ArrayElementAssignmentStatementSyntax arrayAssignment => BindArrayElementAssignment(arrayAssignment, variables, procedures),
            MemberAssignmentStatementSyntax memberAssignment => BindMemberAssignment(memberAssignment, variables, procedures),
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
            LineStatementSyntax line => BindGraphicsLine(line, variables, procedures),
            FilePrintStatementSyntax filePrint => new BoundFilePrintStatement(
                BindFileNumber(filePrint.FileNumber, variables, procedures),
                BindExpression(filePrint.Expression, variables, procedures)),
            InvocationStatementSyntax invocation => BindInvocation(invocation, variables, procedures),
            OpenStatementSyntax open => BindOpen(open, variables, procedures),
            CloseStatementSyntax close => BindClose(close, variables, procedures),
            GetStatementSyntax get => BindGetOrPut(
                get.FileNumber, get.RecordPosition, get.Target, get.GetKeyword, isGet: true, variables, procedures),
            PutStatementSyntax put => BindGetOrPut(
                put.FileNumber, put.RecordPosition, put.Target, put.PutKeyword, isGet: false, variables, procedures),
            SeekStatementSyntax seek => BindSeek(seek, variables, procedures),
            LineInputStatementSyntax lineInput => BindLineInput(lineInput, variables, procedures),
            FileInputStatementSyntax fileInput => BindFileInput(fileInput, variables, procedures),
            EndStatementSyntax => new BoundEndStatement(),
            QualifiedInvocationStatementSyntax qualified => BindQualifiedInvocation(
                qualified,
                variables,
                procedures),
            OnErrorStatementSyntax onError => BindOnError(onError),
            ResumeStatementSyntax resume => BindResume(resume),
            GoToStatementSyntax goTo => _procedureLabels.Contains(goTo.LabelToken.Text)
                ? new BoundGoToStatement(goTo.LabelToken.Text)
                : ReportControlFlowGap(
                    $"GoTo {goTo.LabelToken.Text}",
                    goTo.GoToKeyword.Span),
            GoSubStatementSyntax goSub => _procedureLabels.Contains(goSub.LabelToken.Text)
                ? new BoundGoSubStatement(goSub.LabelToken.Text)
                : ReportControlFlowGap(
                    $"GoSub {goSub.LabelToken.Text}",
                    goSub.GoSubKeyword.Span),
            GoSubReturnStatementSyntax goSubReturn => new BoundGoSubReturnStatement(),
            OnGoToStatementSyntax onGoTo => BindOnGoTo(onGoTo, variables, procedures),
            OnGoSubStatementSyntax onGoSub => BindOnGoSub(onGoSub, variables, procedures),
            LabelStatementSyntax label => _procedureLabels.Contains(label.Identifier.Text)
                ? new BoundLabelStatement(label.Identifier.Text)
                : ReportControlFlowGap(
                    $"Label '{label.Identifier.Text}'",
                    label.Identifier.Span),
            SkippedStatementSyntax => null,
            _ => null
        };
    }

    /// <summary>
    /// Jumps and error handling need the lowered IR with basic blocks: the backend still lowers
    /// control flow while emitting, which cannot express a jump into the middle of a block or a
    /// handler that guards every statement. Reported rather than dropped, because binding returns
    /// null for what it does not understand and the statement would vanish silently.
    /// </summary>
    /// <summary>
    /// Calls and arguments that need the object model. Reported rather than dropped, for the same
    /// reason every other unbound statement is.
    /// </summary>
    private BoundStatement? ReportObjectModelGap(string construct, TextSpan span)
    {
        Report(
            "VB6S0062",
            $"{construct} needs the object type model, which is not implemented yet.",
            span);
        return null;
    }

    private BoundExpression ReportObjectModelExpressionGap(string construct, TextSpan span)
    {
        Report(
            "VB6S0062",
            $"{construct} needs the object type model, which is not implemented yet.",
            span);
        return new BoundErrorExpression();
    }

    /// <summary>Best-effort span for an expression, used only to place a diagnostic.</summary>
    private static TextSpan GetSpan(ExpressionSyntax expression) => expression switch
    {
        NameExpressionSyntax name => name.IdentifierToken.Span,
        MemberAccessExpressionSyntax member => member.MemberToken.Span,
        MemberInvocationExpressionSyntax invocation => invocation.Target.MemberToken.Span,
        ElementAccessExpressionSyntax elementAccess => GetSpan(elementAccess.Receiver),
        InvocationExpressionSyntax invocation => invocation.Identifier.Span,
        _ => new TextSpan(0, 0)
    };

    private BoundStatement? BindOnError(OnErrorStatementSyntax syntax)
    {
        if (syntax.ActionKeyword.Kind == SyntaxKind.ResumeKeyword &&
            syntax.TargetToken.Kind == SyntaxKind.NextKeyword)
        {
            return new BoundOnErrorStatement(BoundErrorHandlingMode.ResumeNext);
        }

        if (syntax.ActionKeyword.Kind == SyntaxKind.GoToKeyword && syntax.TargetToken.Text == "0")
        {
            return new BoundOnErrorStatement(BoundErrorHandlingMode.Disable);
        }

        if (syntax.ActionKeyword.Kind == SyntaxKind.GoToKeyword &&
            _procedureLabels.Contains(syntax.TargetToken.Text))
        {
            return new BoundOnErrorStatement(
                BoundErrorHandlingMode.GoToLabel,
                syntax.TargetToken.Text);
        }

        return ReportControlFlowGap(
            $"On Error {syntax.ActionKeyword.Text} {syntax.TargetToken.Text}",
            syntax.OnKeyword.Span);
    }

    private BoundStatement? BindOnGoTo(
        OnGoToStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var labels = syntax.LabelTokens.Select(token => token.Text).ToImmutableArray();
        var invalid = labels.FirstOrDefault(label => !_procedureLabels.Contains(label));
        if (invalid is not null)
        {
            return ReportControlFlowGap(
                $"On ... GoTo {invalid}",
                syntax.GoToKeyword.Span);
        }

        return new BoundOnGoToStatement(
            BindConversion(BindExpression(syntax.Expression, variables, procedures), TypeSymbol.Long),
            labels);
    }

    private BoundStatement? BindOnGoSub(
        OnGoSubStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var labels = syntax.LabelTokens.Select(token => token.Text).ToImmutableArray();
        var invalid = labels.FirstOrDefault(label => !_procedureLabels.Contains(label));
        if (invalid is not null)
        {
            return ReportControlFlowGap(
                $"On ... GoSub {invalid}",
                syntax.GoSubKeyword.Span);
        }

        return new BoundOnGoSubStatement(
            BindConversion(BindExpression(syntax.Expression, variables, procedures), TypeSymbol.Long),
            labels);
    }

    private BoundStatement? BindResume(ResumeStatementSyntax syntax)
    {
        if (syntax.TargetToken is null)
        {
            return new BoundResumeStatement(IsNext: false);
        }

        if (syntax.TargetToken.Kind == SyntaxKind.NextKeyword)
        {
            return new BoundResumeStatement(IsNext: true);
        }

        if (_procedureLabels.Contains(syntax.TargetToken.Text))
        {
            return new BoundResumeStatement(IsNext: false, syntax.TargetToken.Text);
        }

        return ReportControlFlowGap(
            $"Resume {syntax.TargetToken.Text}",
            syntax.ResumeKeyword.Span);
    }

    private BoundStatement? ReportControlFlowGap(string construct, TextSpan span)
    {
        Report(
            "VB6S0061",
            $"{construct} needs the lowered control flow representation, which is not implemented yet.",
            span);
        return null;
    }

    private BoundExpression BindFileNumber(
        FileNumberSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures) =>
        BindConversion(BindExpression(syntax.Expression, variables, procedures), TypeSymbol.Long);

    private BoundStatement? BindOpen(
        OpenStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var mode = syntax.ModeToken.Text.ToUpperInvariant() switch
        {
            "BINARY" => BoundFileOpenMode.Binary,
            "INPUT" => BoundFileOpenMode.Input,
            "OUTPUT" => BoundFileOpenMode.Output,
            "APPEND" => BoundFileOpenMode.Append,
            "RANDOM" => BoundFileOpenMode.Random,
            _ => (BoundFileOpenMode?)null
        };
        if (mode is null)
        {
            Report(
                "VB6S0057",
                $"Open mode '{syntax.ModeToken.Text}' is not implemented yet; use For Binary, Input, Output, Append, or Random.",
                syntax.ModeToken.Span);
            return null;
        }

        if (syntax.RecordLength is not null &&
            (mode == BoundFileOpenMode.Input ||
             mode == BoundFileOpenMode.Output ||
             mode == BoundFileOpenMode.Append))
        {
            Report(
                "VB6S0057",
                "The Len clause is only supported for Random access in the current compiler profile.",
                syntax.LenKeyword!.Span);
            return null;
        }

        var path = BindConversion(
            BindExpression(syntax.PathExpression, variables, procedures),
            TypeSymbol.String);
        var recordLength = mode == BoundFileOpenMode.Random
            ? syntax.RecordLength is null
                ? new BoundLiteralExpression(128L, TypeSymbol.Long)
                : BindConversion(
                    BindExpression(syntax.RecordLength, variables, procedures),
                    TypeSymbol.Long)
            : null;
        return new BoundOpenStatement(
            BindFileNumber(syntax.FileNumber, variables, procedures),
            path,
            mode.Value,
            recordLength);
    }

    private BoundStatement BindClose(
        CloseStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures) =>
        new BoundCloseStatement(syntax.FileNumbers
            .Select(fileNumber => BindFileNumber(fileNumber, variables, procedures))
            .ToImmutableArray());

    private BoundStatement BindSeek(
        SeekStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures) =>
        new BoundSeekStatement(
            BindFileNumber(syntax.FileNumber, variables, procedures),
            BindConversion(BindExpression(syntax.Position, variables, procedures), TypeSymbol.LongLong));

    private BoundStatement? BindLineInput(
        LineInputStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = BindExpression(syntax.Target, variables, procedures);
        if (target.Type != TypeSymbol.String || target is not (
            BoundVariableExpression or
            BoundArrayAccessExpression or
            BoundElementAccessExpression or
            BoundMemberAccessExpression))
        {
            Report(
                "VB6S0060",
                "Line Input requires a String variable, array element, or user-defined type member.",
                syntax.LineKeyword.Span);
            return null;
        }

        return new BoundLineInputStatement(
            BindFileNumber(syntax.FileNumber, variables, procedures),
            target);
    }

    private BoundStatement? BindGraphicsLine(
        LineStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var startX = BindLineCoordinate(syntax.StartPoint.XExpression, variables, procedures, syntax.LineKeyword.Span);
        var startY = BindLineCoordinate(syntax.StartPoint.YExpression, variables, procedures, syntax.LineKeyword.Span);
        var endX = BindLineCoordinate(syntax.EndPoint.XExpression, variables, procedures, syntax.LineKeyword.Span);
        var endY = BindLineCoordinate(syntax.EndPoint.YExpression, variables, procedures, syntax.LineKeyword.Span);

        BoundExpression? color = null;
        if (syntax.ColorExpression is not null)
        {
            color = BindExpression(syntax.ColorExpression, variables, procedures);
            if (color.Type != TypeSymbol.Error && !IsNumericType(color.Type) && color.Type != TypeSymbol.Variant)
            {
                Report(
                    "VB6S0060",
                    "Graphics Line color must be a numeric or Variant expression.",
                    GetSpan(syntax.ColorExpression));
            }
        }

        var drawBox = false;
        var fill = false;
        foreach (var option in syntax.Options)
        {
            if (option is not NameExpressionSyntax name)
            {
                Report(
                    "VB6S0062",
                    "Graphics Line options must be B or F.",
                    GetSpan(option));
                continue;
            }

            if (string.Equals(name.IdentifierToken.Text, "B", StringComparison.OrdinalIgnoreCase))
            {
                drawBox = true;
            }
            else if (string.Equals(name.IdentifierToken.Text, "F", StringComparison.OrdinalIgnoreCase))
            {
                fill = true;
            }
            else
            {
                Report(
                    "VB6S0062",
                    $"Graphics Line option '{name.IdentifierToken.Text}' is not supported; use B or F.",
                    name.IdentifierToken.Span);
            }
        }

        if (startX.Type == TypeSymbol.Error || startY.Type == TypeSymbol.Error ||
            endX.Type == TypeSymbol.Error || endY.Type == TypeSymbol.Error ||
            color?.Type == TypeSymbol.Error)
        {
            return null;
        }

        return new BoundGraphicsLineStatement(
            startX,
            startY,
            endX,
            endY,
            color,
            syntax.StepKeyword is not null,
            drawBox,
            fill);
    }

    private BoundExpression BindLineCoordinate(
        ExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        TextSpan diagnosticSpan)
    {
        var expression = BindExpression(syntax, variables, procedures);
        if (expression.Type != TypeSymbol.Error && !IsNumericType(expression.Type) && expression.Type != TypeSymbol.Variant)
        {
            Report(
                "VB6S0060",
                "Graphics Line coordinates must be numeric or Variant expressions.",
                diagnosticSpan);
        }

        return BindConversion(expression, TypeSymbol.Single);
    }

    private BoundStatement? BindFileInput(
        FileInputStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var targets = syntax.Targets
            .Select(target => BindExpression(target, variables, procedures))
            .ToImmutableArray();
        if (targets.Any(target => !IsSupportedFileInputType(target.Type) || target is not (
                BoundVariableExpression or
                BoundArrayAccessExpression or
                BoundElementAccessExpression or
                BoundMemberAccessExpression)))
        {
            Report(
                "VB6S0062",
                "Input # requires String, numeric, Boolean, or Currency variables, array elements, or user-defined type members.",
                syntax.InputKeyword.Span);
            return null;
        }

        return new BoundFileInputStatement(
            BindFileNumber(syntax.FileNumber, variables, procedures),
            targets);
    }

    private static bool IsSupportedFileInputType(TypeSymbol type) =>
        type == TypeSymbol.String ||
        type == TypeSymbol.Byte ||
        type == TypeSymbol.Integer ||
        type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong ||
        type == TypeSymbol.Single ||
        type == TypeSymbol.Date ||
        type == TypeSymbol.Double ||
        type == TypeSymbol.Boolean ||
        type == TypeSymbol.Currency;

    /// <summary>
    /// Get and Put share their shape. Fixed-size scalar values, supported typed arrays, and
    /// user-defined types with a supported record layout are transferable.
    /// </summary>
    private BoundStatement? BindGetOrPut(
        FileNumberSyntax fileNumberSyntax,
        ExpressionSyntax? positionSyntax,
        ExpressionSyntax targetSyntax,
        SyntaxToken keyword,
        bool isGet,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var fileNumber = BindFileNumber(fileNumberSyntax, variables, procedures);
        var position = positionSyntax is null
            ? null
            : BindConversion(BindExpression(positionSyntax, variables, procedures), TypeSymbol.LongLong);
        var target = BindExpression(targetSyntax, variables, procedures);

        if (target.Type == TypeSymbol.Error)
        {
            return null;
        }

        if (!IsTransferableFileType(target.Type))
        {
            Report(
                "VB6S0058",
                $"{keyword.Text} of type '{target.Type.Name}' is not implemented yet; " +
                "fixed-size numeric types, Strings, and supported UDT record layouts are transferable.",
                keyword.Span);
            return null;
        }

        if (!isGet)
        {
            return new BoundPutStatement(fileNumber, position, target);
        }

        if (target is not BoundVariableExpression
            and not BoundArrayAccessExpression
            and not BoundElementAccessExpression
            and not BoundMemberAccessExpression)
        {
            Report(
                "VB6S0059",
                "Get requires a variable, array element, or user-defined type member to read into.",
                keyword.Span);
            return null;
        }

        return new BoundGetStatement(fileNumber, position, target);
    }

    /// <summary>
    /// <c>TypeOf x Is T</c> asks whether an object reference has a given class, so it needs the
    /// object model that class modules and controls will bring. Guarded rather than approximated,
    /// the same way binary <c>Is</c> is.
    /// </summary>
    /// <summary>
    /// <c>List.Add , , "General"</c> leaves arguments out for Optional parameters. Without the
    /// Optional call semantics there is nothing to put in their place, and inventing a zero would
    /// change what the callee sees.
    /// </summary>
    private BoundExpression BindOmittedArgument(OmittedArgumentExpressionSyntax syntax)
    {
        _ = syntax;
        Report(
            "VB6S0063",
            "An omitted argument needs the Optional parameter semantics, which are not implemented yet.",
            new TextSpan(0, 0));
        return new BoundErrorExpression();
    }

    private BoundExpression BindTypeOf(
        TypeOfExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);
        var typeName = GetDeclaredTypeName(syntax.TypeToken, syntax.TypeName)!;
        var type = TypeSymbol.Lookup(typeName);
        if (type is null)
        {
            Report(
                "VB6S0003",
                $"Unknown type '{typeName}'.",
                syntax.TypeToken.Span);
            return new BoundErrorExpression();
        }

        if (type is not ClassTypeSymbol classType)
        {
            Report(
                "VB6S0060",
                $"TypeOf ... Is requires a class module type, but '{type.Name}' is not an object type.",
                syntax.TypeToken.Span);
            return new BoundErrorExpression();
        }

        if (expression.Type != TypeSymbol.Variant && expression.Type is not ClassTypeSymbol)
        {
            Report(
                "VB6S0060",
                $"TypeOf cannot test a value of type '{expression.Type.Name}' against a class type.",
                syntax.TypeOfKeyword.Span);
            return new BoundErrorExpression();
        }

        return new BoundTypeOfExpression(expression, classType);
    }

    private static bool IsTransferableFileType(TypeSymbol type) =>
        type == TypeSymbol.Byte ||
        type == TypeSymbol.Integer ||
        type == TypeSymbol.Long ||
        type == TypeSymbol.LongLong ||
        type == TypeSymbol.Single ||
        type == TypeSymbol.Date ||
        type == TypeSymbol.Double ||
        type == TypeSymbol.Currency ||
        type == TypeSymbol.Boolean ||
        type == TypeSymbol.String ||
        type is ArrayTypeSymbol { ElementType: UserDefinedTypeSymbol elementType } &&
        UserDefinedTypeFileLayout.IsBinaryTransferableElement(elementType) ||
        type is UserDefinedTypeSymbol userDefinedType &&
        UserDefinedTypeFileLayout.IsBinaryTransferable(userDefinedType);

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

        return new BoundVariableDeclarationStatement(
            local,
            BindArrayDimensions(syntax, variables, procedures),
            BindImplicitObjectInitializer(syntax, local.Type));
    }

    private BoundReDimStatement BindReDim(
        VariableDeclaratorSyntax syntax,
        bool preserve,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var dimensions = BindArrayDimensions(syntax, variables, procedures);

        if (!variables.TryGetValue(syntax.Identifier.Text, out var variable))
        {
            Report(
                "VB6S0001",
                $"Variable '{syntax.Identifier.Text}' is not declared.",
                syntax.Identifier.Span);
            variable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
            return new BoundReDimStatement(new BoundVariableExpression(variable), dimensions, preserve);
        }

        if (variable is ParameterSymbol { IsParamArray: true })
        {
            Report(
                "VB6S0066",
                $"ReDim cannot be used with ParamArray parameter '{syntax.Identifier.Text}'.",
                syntax.Identifier.Span);
            return new BoundReDimStatement(new BoundVariableExpression(variable), dimensions, preserve);
        }

        if (variable.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0029",
                $"ReDim target '{syntax.Identifier.Text}' is not a dynamic array.",
                syntax.Identifier.Span);
            return new BoundReDimStatement(new BoundVariableExpression(variable), dimensions, preserve);
        }

        if (arrayType.HasKnownRank)
        {
            Report(
                "VB6S0029",
                $"ReDim target '{syntax.Identifier.Text}' is a fixed array.",
                syntax.Identifier.Span);
        }

        if (!syntax.IsArray || syntax.Dimensions.IsDefaultOrEmpty)
        {
            Report(
                "VB6S0030",
                $"ReDim target '{syntax.Identifier.Text}' requires at least one dimension.",
                syntax.Identifier.Span);
        }

        if (syntax.TypeToken is not null)
        {
            var reDimElementType = ResolveDeclaredType(syntax.TypeToken, syntax.TypeName);
            if (reDimElementType != TypeSymbol.Error && reDimElementType != arrayType.ElementType)
            {
                Report(
                    "VB6S0031",
                    $"ReDim cannot change array '{syntax.Identifier.Text}' from element type '{arrayType.ElementType.Name}' to '{reDimElementType.Name}'.",
                    syntax.TypeToken.Span);
            }
        }

        return new BoundReDimStatement(new BoundVariableExpression(variable), dimensions, preserve);
    }

    /// <summary>
    /// <c>ReDim Section(0).Bytes(0)</c>. The receiver is bound like any other expression, so the
    /// element selection on the way in already works; only the array it lands on has to be dynamic.
    /// </summary>
    private BoundReDimStatement? BindQualifiedReDim(
        ReDimQualifiedTargetSyntax syntax,
        bool preserve,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = BindExpression(syntax.Target, variables, procedures);
        if (target.Type == TypeSymbol.Error)
        {
            return null;
        }

        var dimensions = BindArrayDimensionList(syntax.Dimensions, variables, procedures);

        if (target.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0029",
                $"ReDim target of type '{target.Type.Name}' is not a dynamic array.",
                syntax.OpenParenthesisToken.Span);
            return null;
        }

        if (arrayType.HasKnownRank)
        {
            Report(
                "VB6S0029",
                "ReDim target is a fixed array.",
                syntax.OpenParenthesisToken.Span);
        }

        if (dimensions.IsDefaultOrEmpty)
        {
            Report(
                "VB6S0030",
                "ReDim target requires at least one dimension.",
                syntax.OpenParenthesisToken.Span);
        }

        // The element type may be restated, as in ReDim Preserve Section(2).Bytes(n) As Byte, but
        // it cannot be changed.
        if (syntax.TypeToken is not null)
        {
            var restated = ResolveDeclaredType(syntax.TypeToken, syntax.TypeName);
            if (restated != TypeSymbol.Error && restated != arrayType.ElementType)
            {
                Report(
                    "VB6S0031",
                    $"ReDim cannot change the element type from '{arrayType.ElementType.Name}' to '{restated.Name}'.",
                    syntax.TypeToken.Span);
            }
        }

        return new BoundReDimStatement(target, dimensions, preserve);
    }

    private BoundEraseStatement BindErase(
        SyntaxToken identifier,
        Dictionary<string, VariableSymbol> variables)
    {
        if (!variables.TryGetValue(identifier.Text, out var variable))
        {
            Report(
                "VB6S0001",
                $"Variable '{identifier.Text}' is not declared.",
                identifier.Span);
            variable = new LocalVariableSymbol(identifier.Text, TypeSymbol.Error);
            return new BoundEraseStatement(new BoundVariableExpression(variable), Deallocate: false);
        }

        return BindErase(new BoundVariableExpression(variable), identifier);
    }

    private BoundEraseStatement BindErase(BoundExpression target, SyntaxToken anchor)
    {
        var targetName = target switch
        {
            BoundVariableExpression variable => variable.Variable.Name,
            BoundMemberAccessExpression member => member.Member.Name,
            _ => "expression"
        };

        if (target.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0033",
                $"Erase target '{targetName}' is not an array.",
                anchor.Span);
            return new BoundEraseStatement(target, Deallocate: false);
        }

        if (target is BoundVariableExpression { Variable: ParameterSymbol { IsParamArray: true } parameter })
        {
            Report(
                "VB6S0066",
                $"Erase cannot be used with ParamArray parameter '{parameter.Name}'.",
                anchor.Span);
            return new BoundEraseStatement(target, Deallocate: false);
        }

        if (target is BoundVariableExpression { Variable: ParameterSymbol parameterVariable })
        {
            Report(
                "VB6S0036",
                $"Erase on array parameter '{parameterVariable.Name}' requires caller allocation semantics, which are not implemented yet.",
                anchor.Span);
            return new BoundEraseStatement(target, Deallocate: false);
        }

        return new BoundEraseStatement(target, Deallocate: !arrayType.HasKnownRank);
    }

    private BoundStatement BindAssignment(
        AssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var expression = BindExpression(syntax.Expression, variables, procedures);

        if (!variables.TryGetValue(syntax.Identifier.Text, out var variable))
        {
            if (TryGetContainingClassProperty(
                    syntax.Identifier.Text,
                    PropertyAccessorKind.Let,
                    variables,
                    out var propertyTarget))
            {
                return new BoundMemberAssignmentStatement(
                    propertyTarget,
                    BindConversion(expression, propertyTarget.Type));
            }

            if (!_optionExplicit && _activeLocals is not null)
            {
                var implicitLocal = new LocalVariableSymbol(
                    syntax.Identifier.Text,
                    GetIdentifierType(syntax.Identifier) ?? TypeSymbol.Variant);
                variable = implicitLocal;
                variables[variable.Name] = variable;
                _activeLocals[implicitLocal.Name] = implicitLocal;
            }
            else
            {
                Report(
                    "VB6S0001",
                    $"Variable '{syntax.Identifier.Text}' is not declared.",
                    syntax.Identifier.Span);
                variable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
            }

            return new BoundAssignmentStatement(variable, expression);
        }

        return new BoundAssignmentStatement(variable, BindConversion(expression, variable.Type));
    }

    private BoundStatement BindSetAssignment(
        SetAssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = syntax.Target switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                BindMemberAccess(memberAccess, variables, procedures, PropertyAccessorKind.Set),
            ElementAccessExpressionSyntax elementAccess =>
                BindElementAccess(elementAccess, variables, procedures, PropertyAccessorKind.Set),
            NameExpressionSyntax name when TryGetContainingClassProperty(
                name.IdentifierToken.Text,
                PropertyAccessorKind.Set,
                variables,
                out var propertyTarget) => propertyTarget,
            _ => BindExpression(syntax.Target, variables, procedures)
        };
        var expression = BindExpression(syntax.Expression, variables, procedures);
        if (target is BoundVariableExpression variable)
        {
            return new BoundAssignmentStatement(variable.Variable, BindConversion(expression, variable.Variable.Type));
        }

        return new BoundMemberAssignmentStatement(target, BindConversion(expression, target.Type));
    }

    private BoundMemberAssignmentStatement BindMemberAssignment(
        MemberAssignmentStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var target = syntax.Target switch
        {
            MemberAccessExpressionSyntax memberAccess =>
                BindMemberAccess(memberAccess, variables, procedures, PropertyAccessorKind.Let),
            ElementAccessExpressionSyntax elementAccess =>
                BindElementAccess(elementAccess, variables, procedures, PropertyAccessorKind.Let),
            _ => BindExpression(syntax.Target, variables, procedures)
        };
        var expression = BindExpression(syntax.Expression, variables, procedures);
        return new BoundMemberAssignmentStatement(target, BindConversion(expression, target.Type));
    }

    private BoundStatement BindArrayElementAssignment(
        ArrayElementAssignmentStatementSyntax syntax,
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
            return new BoundArrayElementAssignmentStatement(
                variable,
                BindArrayIndices(syntax.Identifier, syntax.Indices, null, variables, procedures),
                expression);
        }

        if (variable.Type is not ArrayTypeSymbol arrayType)
        {
            if (variable.Type == TypeSymbol.Variant)
            {
                var target = new BoundVariantArrayAccessExpression(
                    new BoundVariableExpression(variable),
                    BindArrayIndices(syntax.Identifier, syntax.Indices, null, variables, procedures));
                return new BoundMemberAssignmentStatement(
                    target,
                    BindConversion(expression, TypeSymbol.Variant));
            }

            if (variable.Type is ClassTypeSymbol classType &&
                (classType.TryGetDefaultProperty(PropertyAccessorKind.Let, out _) ||
                 classType.TryGetDefaultProperty(PropertyAccessorKind.Set, out _)))
            {
                var target = BindDefaultPropertyInvocation(
                    new BoundVariableExpression(variable),
                    syntax.Identifier,
                    syntax.Indices,
                    variables,
                    procedures,
                    PropertyAccessorKind.Let);
                return new BoundMemberAssignmentStatement(
                    target,
                    BindConversion(expression, target.Type));
            }

            Report(
                "VB6S0026",
                $"Variable '{syntax.Identifier.Text}' is not an array.",
                syntax.Identifier.Span);
            return new BoundArrayElementAssignmentStatement(
                variable,
                BindArrayIndices(syntax.Identifier, syntax.Indices, null, variables, procedures),
                expression);
        }

        return new BoundArrayElementAssignmentStatement(
            variable,
            BindArrayIndices(syntax.Identifier, syntax.Indices, arrayType, variables, procedures),
            BindConversion(expression, arrayType.ElementType));
    }

    private ImmutableArray<BoundExpression> BindArrayIndices(
        SyntaxToken identifier,
        ImmutableArray<ExpressionSyntax> indexSyntaxes,
        ArrayTypeSymbol? arrayType,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (arrayType?.Rank is int rank && indexSyntaxes.Length != rank)
        {
            Report(
                "VB6S0027",
                $"Array '{identifier.Text}' has rank {rank}, but {indexSyntaxes.Length} index(es) were supplied.",
                identifier.Span);
        }

        return indexSyntaxes
            .Select(index =>
            {
                var expression = BindExpression(index, variables, procedures);
                return arrayType is null
                    ? expression
                    : BindConversion(expression, TypeSymbol.Long);
            })
            .ToImmutableArray();
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

        if (controlVariable.Type != TypeSymbol.Byte &&
            controlVariable.Type != TypeSymbol.Integer &&
            controlVariable.Type != TypeSymbol.Long &&
            controlVariable.Type != TypeSymbol.LongLong &&
            controlVariable.Type != TypeSymbol.LongPtr &&
            controlVariable.Type != TypeSymbol.UShort &&
            controlVariable.Type != TypeSymbol.UInteger &&
            controlVariable.Type != TypeSymbol.ULong &&
            controlVariable.Type != TypeSymbol.Single &&
            controlVariable.Type != TypeSymbol.Double &&
            controlVariable.Type != TypeSymbol.Currency &&
            controlVariable.Type != TypeSymbol.Date &&
            controlVariable.Type != TypeSymbol.Error)
        {
            Report(
                "VB6S0012",
                $"For control variable '{controlVariable.Name}' must be a numeric type.",
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
            ? DefaultForStep(controlVariable.Type)
            : BindConversion(BindExpression(syntax.Step, variables, procedures), controlVariable.Type);

        var loopId = _nextLoopId++;
        _loopStack.Add(new LoopBindingContext(BoundLoopKind.For, loopId));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _loopStack.RemoveAt(_loopStack.Count - 1);

        return new BoundForStatement(loopId, controlVariable, initialValue, limit, step, body);
    }

    private static BoundLiteralExpression DefaultForStep(TypeSymbol type) =>
        ReferenceEquals(type, TypeSymbol.Byte) ? new BoundLiteralExpression((byte)1, type) :
        ReferenceEquals(type, TypeSymbol.Integer) ? new BoundLiteralExpression((short)1, type) :
        ReferenceEquals(type, TypeSymbol.Long) ? new BoundLiteralExpression(1, type) :
        ReferenceEquals(type, TypeSymbol.LongLong) ? new BoundLiteralExpression(1L, type) :
        ReferenceEquals(type, TypeSymbol.LongPtr) ? new BoundLiteralExpression(1L, type) :
        ReferenceEquals(type, TypeSymbol.UShort) ? new BoundLiteralExpression((ushort)1, type) :
        ReferenceEquals(type, TypeSymbol.UInteger) ? new BoundLiteralExpression(1u, type) :
        ReferenceEquals(type, TypeSymbol.ULong) ? new BoundLiteralExpression(1UL, type) :
        ReferenceEquals(type, TypeSymbol.Single) ? new BoundLiteralExpression(1f, type) :
        ReferenceEquals(type, TypeSymbol.Double) ? new BoundLiteralExpression(1d, type) :
        ReferenceEquals(type, TypeSymbol.Currency) ? new BoundLiteralExpression(1m, type) :
        ReferenceEquals(type, TypeSymbol.Date) ? new BoundLiteralExpression(1d, type) :
        new BoundLiteralExpression((short)1, type);

    private BoundForEachStatement BindForEach(
        ForEachStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!variables.TryGetValue(syntax.Identifier.Text, out var controlVariable))
        {
            if (!_optionExplicit && _activeLocals is not null)
            {
                var implicitControlVariable = new LocalVariableSymbol(
                    syntax.Identifier.Text,
                    GetIdentifierType(syntax.Identifier) ?? TypeSymbol.Variant);
                controlVariable = implicitControlVariable;
                variables[implicitControlVariable.Name] = implicitControlVariable;
                _activeLocals[implicitControlVariable.Name] = implicitControlVariable;
            }
            else
            {
                Report(
                    "VB6S0001",
                    $"Variable '{syntax.Identifier.Text}' is not declared.",
                    syntax.Identifier.Span);
                controlVariable = new LocalVariableSymbol(syntax.Identifier.Text, TypeSymbol.Error);
            }
        }

        if (controlVariable.Type != TypeSymbol.Variant &&
            controlVariable.Type != TypeSymbol.Error &&
            controlVariable.Type is not ClassTypeSymbol)
        {
            Report(
                "VB6S0054",
                $"For Each control variable '{controlVariable.Name}' must be Variant or an object type in the current compiler subset.",
                syntax.Identifier.Span);
        }

        var collection = BindExpression(syntax.Collection, variables, procedures);
        ArrayTypeSymbol arrayType;
        var isCollection = ReferenceEquals(collection.Type, VBStandardTypes.Collection);
        var isHostCollection = IsHostCollectionType(collection.Type);
        if (collection.Type is ArrayTypeSymbol boundArrayType)
        {
            arrayType = boundArrayType;
        }
        else if (isCollection || isHostCollection)
        {
            arrayType = new ArrayTypeSymbol(TypeSymbol.Variant);
        }
        else
        {
            if (collection.Type != TypeSymbol.Error)
            {
                Report(
                    "VB6S0055",
                    $"For Each collection type '{collection.Type.Name}' is not an array or Collection in the current compiler subset.",
                    syntax.InKeyword.Span);
            }

            arrayType = new ArrayTypeSymbol(TypeSymbol.Error);
        }

        // Not a compiler gap: For Each requires a Variant control variable, and VB6 coerces a
        // user-defined type into a Variant only for public types declared in public object
        // modules. A Type in a standard module never qualifies.
        if (!isCollection && !isHostCollection && arrayType.ElementType is UserDefinedTypeSymbol elementUserDefinedType)
        {
            Report(
                "VB6S0056",
                $"For Each cannot iterate an array of user-defined type '{elementUserDefinedType.Name}': " +
                "VB6 coerces a user-defined type into the Variant control variable only for public " +
                "types declared in public object modules.",
                syntax.InKeyword.Span);
        }

        if (syntax.NextIdentifier is not null &&
            !string.Equals(syntax.NextIdentifier.Text, syntax.Identifier.Text, StringComparison.OrdinalIgnoreCase))
        {
            Report(
                "VB6S0013",
                $"Next variable '{syntax.NextIdentifier.Text}' does not match For variable '{syntax.Identifier.Text}'.",
                syntax.NextIdentifier.Span);
        }

        var loopId = _nextLoopId++;
        _loopStack.Add(new LoopBindingContext(BoundLoopKind.For, loopId));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _loopStack.RemoveAt(_loopStack.Count - 1);

        return new BoundForEachStatement(
            loopId,
            controlVariable,
            collection,
            arrayType,
            isCollection,
            isHostCollection,
            body);
    }

    private static bool IsHostCollectionType(TypeSymbol type) =>
        ReferenceEquals(type, VBStandardTypes.Object) ||
        ReferenceEquals(type, VBStandardTypes.Control) ||
        ReferenceEquals(type, VBStandardTypes.Form) ||
        ReferenceEquals(type, VBStandardTypes.UserControl) ||
        type is ClassTypeSymbol classType &&
        classType.TryGetProperty("Controls", PropertyAccessorKind.Get, out _);

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
        var target = BindExpression(syntax.Expression, variables, procedures);
        var contextReceiver = target;

        if (target.Type != TypeSymbol.Error &&
            target.Type is not UserDefinedTypeSymbol and not ClassTypeSymbol)
        {
            Report(
                "VB6S0050",
                $"With target type '{target.Type.Name}' is not a user-defined type in the current compiler subset.",
                syntax.WithKeyword.Span);
            contextReceiver = new BoundErrorExpression();
        }
        else if (target.Type != TypeSymbol.Error && !IsAddressableExpression(target))
        {
            Report(
                "VB6S0051",
                "With target must be an addressable variable, array element, or user-defined type member in the current compiler subset.",
                syntax.WithKeyword.Span);
            contextReceiver = new BoundErrorExpression();
        }

        var withId = _nextWithId++;
        _withStack.Add(new WithBindingContext(withId, contextReceiver));
        var body = BindStatements(syntax.Statements, variables, procedures);
        _withStack.RemoveAt(_withStack.Count - 1);
        return new BoundWithStatement(withId, target, body);
    }

    private BoundStatement BindExit(ExitStatementSyntax syntax)
    {
        if (syntax.TargetKeyword.Kind is SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword ||
            string.Equals(syntax.TargetKeyword.Text, "Property", StringComparison.OrdinalIgnoreCase))
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

    private BoundStatement? BindInvocation(
        InvocationStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (string.Equals(syntax.Identifier.Text, "RaiseEvent", StringComparison.OrdinalIgnoreCase))
        {
            return BindRaiseEvent(syntax, variables, procedures);
        }

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

    private BoundStatement? BindRaiseEvent(
        InvocationStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (_containingClass is null)
        {
            return ReportObjectModelGap("Raising an event outside a class module", syntax.Identifier.Span);
        }

        if (syntax.Arguments.Length != 1)
        {
            return ReportEventGap(
                "RaiseEvent requires an event name followed by its arguments",
                syntax.Identifier.Span);
        }

        var (eventName, argumentSyntaxes) = syntax.Arguments[0] switch
        {
            NameExpressionSyntax name =>
                (name.IdentifierToken.Text, ImmutableArray<ExpressionSyntax>.Empty),
            InvocationExpressionSyntax invocation =>
                (invocation.Identifier.Text, invocation.Arguments),
            _ => (string.Empty, ImmutableArray<ExpressionSyntax>.Empty)
        };

        if (string.IsNullOrEmpty(eventName) || !_containingClass.TryGetEvent(eventName, out var @event))
        {
            return ReportEventGap(
                $"Class '{_containingClass.Name}' has no event '{eventName}'.",
                syntax.Identifier.Span);
        }

        var eventProcedure = new ProcedureSymbol(
            @event.Name,
            @event.Parameters,
            ReturnType: null);
        return new BoundRaiseEventStatement(
            @event,
            BindArguments(
                syntax.Identifier,
                argumentSyntaxes,
                eventProcedure,
                variables,
                procedures));
    }

    private BoundStatement? ReportEventGap(string message, TextSpan span)
    {
        Report("VB6S0066", message, span);
        return null;
    }

    private BoundStatement? BindQualifiedInvocation(
        QualifiedInvocationStatementSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (syntax.Target is MemberAccessExpressionSyntax errTarget &&
            IsErrReceiver(errTarget.Receiver))
        {
            var errProcedure = GetErrMemberProcedure(errTarget.MemberToken.Text);
            if (errProcedure is null)
            {
                return ReportObjectModelGap(
                    $"Err member '{errTarget.MemberToken.Text}'",
                    errTarget.MemberToken.Span);
            }

            return new BoundInvocationStatement(
                errProcedure,
                BindArguments(
                    errTarget.MemberToken,
                    syntax.Arguments,
                    errProcedure,
                    variables,
                    procedures));
        }

        MemberAccessExpressionSyntax? memberAccess = syntax.Target switch
        {
            MemberAccessExpressionSyntax member => member,
            ElementAccessExpressionSyntax
            {
                Receiver: MemberAccessExpressionSyntax member
            } => member,
            _ => null
        };
        if (memberAccess is null)
        {
            return ReportObjectModelGap("Calling a method on an object", GetSpan(syntax.Target));
        }

        var argumentSyntaxes = syntax.Target is ElementAccessExpressionSyntax elementAccess &&
            elementAccess.Receiver is MemberAccessExpressionSyntax
            ? elementAccess.Indices.AddRange(
                syntax.Arguments.Length > 0 &&
                syntax.Arguments[0] is OmittedArgumentExpressionSyntax
                    ? syntax.Arguments.RemoveAt(0)
                    : syntax.Arguments)
            : syntax.Arguments;

        var receiver = memberAccess.Receiver is WithReceiverExpressionSyntax
            ? BindWithReceiver(memberAccess.DotToken)
            : BindExpression(memberAccess.Receiver, variables, procedures);
        if (receiver.Type == TypeSymbol.Error)
        {
            return null;
        }

        if (receiver.Type == TypeSymbol.Variant)
        {
            var dynamicProcedure = CreateDynamicObjectProcedure(memberAccess.MemberToken.Text, isFunction: false);
            return new BoundMemberInvocationStatement(
                receiver,
                dynamicProcedure,
                BindArguments(
                    memberAccess.MemberToken,
                    argumentSyntaxes,
                    dynamicProcedure,
                    variables,
                    procedures));
        }

        if (receiver.Type is not ClassTypeSymbol classType)
        {
            return ReportObjectModelGap(
                "Calling a method on an object",
                memberAccess.MemberToken.Span);
        }

        if (IsLateBoundObjectType(classType))
        {
            return new BoundMemberInvocationStatement(
                receiver,
                CreateDynamicObjectProcedure(memberAccess.MemberToken.Text, isFunction: false),
                BindArguments(
                    memberAccess.MemberToken,
                    argumentSyntaxes,
                    CreateDynamicObjectProcedure(memberAccess.MemberToken.Text, isFunction: false),
                    variables,
                    procedures));
        }

        if (!classType.TryGetProcedure(memberAccess.MemberToken.Text, out var procedure))
        {
            return ReportObjectModelGap(
                "Calling a method on an object",
                memberAccess.MemberToken.Span);
        }

        // VB6 permits a statement-form call to a Function when its return value is intentionally
        // discarded. Keep the call as an evaluated statement so side effects still execute.
        return new BoundMemberInvocationStatement(
            receiver,
            procedure,
            BindArguments(memberAccess.MemberToken, argumentSyntaxes, procedure, variables, procedures));
    }

    private BoundExpression BindExpression(
        ExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        return syntax switch
        {
            LiteralExpressionSyntax literal => BindLiteral(literal),
            NewExpressionSyntax @new => BindNew(@new),
            AddressOfExpressionSyntax addressOf => BindAddressOf(addressOf, procedures),
            NameExpressionSyntax name => BindName(name, variables, procedures),
            InvocationExpressionSyntax invocation => BindInvocationExpression(invocation, variables, procedures),
            MemberInvocationExpressionSyntax memberInvocation => BindMemberInvocationExpression(
                memberInvocation,
                variables,
                procedures),
            MemberAccessExpressionSyntax memberAccess => BindMemberAccess(memberAccess, variables, procedures),
            ElementAccessExpressionSyntax elementAccess => BindElementAccess(elementAccess, variables, procedures),
            UnaryExpressionSyntax unary => BindUnary(unary, variables, procedures),
            BinaryExpressionSyntax binary => BindBinary(binary, variables, procedures),
            ParenthesizedExpressionSyntax parenthesized => BindExpression(parenthesized.Expression, variables, procedures),
            TypeOfExpressionSyntax typeOf => BindTypeOf(typeOf, variables, procedures),
            // An omitted argument is only meaningful once Optional parameters carry defaults.
            OmittedArgumentExpressionSyntax omitted => BindOmittedArgument(omitted),
            // The keyword only decides how the argument is passed; the value itself is the operand.
            ArgumentPassingModeExpressionSyntax passingMode =>
                BindExpression(passingMode.Expression, variables, procedures),
            _ => new BoundErrorExpression()
        };
    }

    private BoundExpression BindNew(NewExpressionSyntax syntax)
    {
        var typeName = GetDeclaredTypeName(syntax.TypeToken, syntax.TypeName)!;
        var type = TypeSymbol.Lookup(typeName);
        if (type is null)
        {
            Report(
                "VB6S0003",
                $"Unknown type '{typeName}'.",
                syntax.TypeToken.Span);
            return new BoundErrorExpression();
        }

        if (type is not ClassTypeSymbol classType)
        {
            Report(
                "VB6S0063",
                $"New requires a class module type, but '{type.Name}' is not an object type.",
                syntax.TypeToken.Span);
            return new BoundErrorExpression();
        }

        if (classType.IsInterfaceContract)
        {
            Report(
                "VB6S0068",
                $"Interface contract '{classType.Name}' cannot be instantiated with New.",
                syntax.TypeToken.Span);
            return new BoundErrorExpression();
        }

        return new BoundNewExpression(classType);
    }

    private BoundExpression BindAddressOf(
        AddressOfExpressionSyntax syntax,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (!procedures.TryGetValue(syntax.TargetToken.Text, out var procedure))
        {
            Report(
                "VB6S0001",
                $"Procedure '{syntax.TargetToken.Text}' is not declared.",
                syntax.TargetToken.Span);
            return new BoundErrorExpression();
        }

        return new BoundAddressOfExpression(procedure);
    }

    private BoundExpression BindMemberAccess(
        MemberAccessExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        PropertyAccessorKind accessor = PropertyAccessorKind.Get)
    {
        if (accessor == PropertyAccessorKind.Get &&
            syntax.Receiver is NameExpressionSyntax enumName &&
            _qualifiedEnumMembers.TryGetValue(
                $"{enumName.IdentifierToken.Text}.{syntax.MemberToken.Text}",
                out var enumValue))
        {
            return new BoundLiteralExpression(enumValue, TypeSymbol.Long);
        }

        var receiver = syntax.Receiver is WithReceiverExpressionSyntax
            ? BindWithReceiver(syntax.DotToken)
            : BindExpression(syntax.Receiver, variables, procedures);
        if (receiver.Type == TypeSymbol.Error)
        {
            return receiver;
        }

        if (IsErrReceiver(receiver))
        {
            var errProcedure = GetErrMemberProcedure(syntax.MemberToken.Text);
            if (errProcedure is not null && errProcedure.IsFunction)
            {
                return new BoundInvocationExpression(errProcedure, ImmutableArray<BoundArgument>.Empty);
            }

            Report(
                "VB6S0065",
                $"Err has no readable member '{syntax.MemberToken.Text}'.",
                syntax.MemberToken.Span);
            return new BoundErrorExpression();
        }

        if (accessor == PropertyAccessorKind.Get &&
            receiver.Type is ArrayTypeSymbol &&
            (syntax.MemberToken.Text.Equals("UBound", StringComparison.OrdinalIgnoreCase) ||
             syntax.MemberToken.Text.Equals("LBound", StringComparison.OrdinalIgnoreCase)))
        {
            return new BoundArrayBoundExpression(
                receiver,
                new BoundLiteralExpression(1L, TypeSymbol.Long),
                IsUpperBound: syntax.MemberToken.Text.Equals("UBound", StringComparison.OrdinalIgnoreCase));
        }

        if (receiver.Type is ClassTypeSymbol classType)
        {
            if (classType.TryGetProperty(syntax.MemberToken.Text, accessor, out var property))
            {
                return new BoundPropertyAccessExpression(receiver, property);
            }

            if (accessor == PropertyAccessorKind.Get &&
                classType.TryGetProcedure(syntax.MemberToken.Text, out var procedure) &&
                procedure.IsFunction)
            {
                return new BoundMemberInvocationExpression(
                    receiver,
                    procedure,
                    ImmutableArray<BoundArgument>.Empty);
            }

            if (IsLateBoundObjectType(classType))
            {
                return new BoundPropertyAccessExpression(
                    receiver,
                    new PropertySymbol(
                        syntax.MemberToken.Text,
                        accessor,
                        VBStandardTypes.Object,
                        ImmutableArray<ParameterSymbol>.Empty)
                    {
                        IsLateBound = true
                    });
            }

            Report(
                "VB6S0064",
                $"Class '{classType.Name}' has no {GetPropertyAccessorDescription(accessor)} property '{syntax.MemberToken.Text}'.",
                syntax.MemberToken.Span);
            return new BoundErrorExpression();
        }

        if (receiver.Type == TypeSymbol.Variant)
        {
            return new BoundPropertyAccessExpression(
                receiver,
                new PropertySymbol(
                    syntax.MemberToken.Text,
                    accessor,
                    TypeSymbol.Variant,
                    ImmutableArray<ParameterSymbol>.Empty)
                {
                    IsLateBound = true
                });
        }

        if (receiver.Type is not UserDefinedTypeSymbol userDefinedType)
        {
            Report(
                "VB6S0047",
                $"Type '{receiver.Type.Name}' does not expose user-defined type members.",
                syntax.MemberToken.Span);
            return new BoundErrorExpression();
        }

        if (!userDefinedType.TryGetMember(syntax.MemberToken.Text, out var member))
        {
            Report(
                "VB6S0048",
                $"User-defined type '{userDefinedType.Name}' has no member '{syntax.MemberToken.Text}'.",
                syntax.MemberToken.Span);
            return new BoundErrorExpression();
        }

        return new BoundMemberAccessExpression(receiver, member);
    }

    private static string GetPropertyAccessorDescription(PropertyAccessorKind accessor) => accessor switch
    {
        PropertyAccessorKind.Get => "readable",
        PropertyAccessorKind.Let => "assignable",
        PropertyAccessorKind.Set => "object-assignable",
        _ => "compatible"
    };

    private BoundExpression BindElementAccess(
        ElementAccessExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        PropertyAccessorKind accessor = PropertyAccessorKind.Get)
    {
        if (syntax.Receiver is MemberAccessExpressionSyntax memberAccess)
        {
            var memberReceiver = memberAccess.Receiver is WithReceiverExpressionSyntax
                ? BindWithReceiver(memberAccess.DotToken)
                : BindExpression(memberAccess.Receiver, variables, procedures);
            if (memberReceiver.Type is ClassTypeSymbol || memberReceiver.Type == TypeSymbol.Variant)
            {
                return BindClassMemberInvocation(
                    memberAccess,
                    syntax.Indices,
                    variables,
                    procedures,
                    accessor);
            }
        }

        var receiver = BindExpression(syntax.Receiver, variables, procedures);
        if (receiver.Type == TypeSymbol.Error)
        {
            return receiver;
        }

        if (receiver.Type is ClassTypeSymbol collectionType &&
            collectionType.TryGetDefaultProperty(accessor, out var defaultProperty))
        {
            return new BoundPropertyInvocationExpression(
                receiver,
                defaultProperty,
                BindPropertyArguments(
                    SyntaxNavigator.GetFirstToken(syntax.Receiver) ?? syntax.OpenParenthesisToken,
                    syntax.Indices,
                    defaultProperty,
                    variables,
                    procedures));
        }

        if (receiver.Type == TypeSymbol.Variant)
        {
            return new BoundVariantArrayAccessExpression(
                receiver,
                syntax.Indices
                    .Select(index => BindExpression(index, variables, procedures))
                    .ToImmutableArray());
        }

        if (receiver.Type is not ArrayTypeSymbol arrayType)
        {
            Report(
                "VB6S0026",
                $"Expression of type '{receiver.Type.Name}' is not an array.",
                syntax.OpenParenthesisToken.Span);
            return new BoundErrorExpression();
        }

        if (arrayType.Rank is int rank && syntax.Indices.Length != rank)
        {
            Report(
                "VB6S0027",
                $"Array expression has rank {rank}, but {syntax.Indices.Length} index(es) were supplied.",
                syntax.OpenParenthesisToken.Span);
        }

        var indices = syntax.Indices
            .Select(index => BindConversion(BindExpression(index, variables, procedures), TypeSymbol.Long))
            .ToImmutableArray();
        return new BoundElementAccessExpression(receiver, indices, arrayType.ElementType);
    }

    private BoundExpression BindWithReceiver(SyntaxToken dotToken)
    {
        if (_withStack.Count == 0)
        {
            Report(
                "VB6S0049",
                "Implicit member access requires an active With block.",
                dotToken.Span);
            return new BoundErrorExpression();
        }

        var context = _withStack[^1];
        return context.Receiver.Type == TypeSymbol.Error
            ? new BoundErrorExpression()
            : new BoundWithReceiverExpression(context.WithId, context.Receiver.Type);
    }

    private BoundExpression BindInvocationExpression(
        InvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (string.Equals(syntax.Identifier.Text, "LBound", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(syntax.Identifier.Text, "UBound", StringComparison.OrdinalIgnoreCase))
        {
            return BindArrayBoundExpression(syntax, variables, procedures);
        }

        if (variables.TryGetValue(syntax.Identifier.Text, out var variable) &&
            variable.Type is ArrayTypeSymbol arrayType)
        {
            return new BoundArrayAccessExpression(
                variable,
                BindArrayIndices(syntax.Identifier, syntax.Arguments, arrayType, variables, procedures),
                arrayType.ElementType);
        }

        if (variables.TryGetValue(syntax.Identifier.Text, out variable) &&
            variable.Type is ClassTypeSymbol defaultPropertyType &&
            defaultPropertyType.TryGetDefaultProperty(PropertyAccessorKind.Get, out var defaultProperty))
        {
            return new BoundPropertyInvocationExpression(
                new BoundVariableExpression(variable),
                defaultProperty,
                BindPropertyArguments(
                    syntax.Identifier,
                    syntax.Arguments,
                    defaultProperty,
                    variables,
                    procedures));
        }

        if (variables.TryGetValue(syntax.Identifier.Text, out variable) &&
            variable.Type == TypeSymbol.Variant &&
            syntax.Arguments.Length > 0)
        {
            return new BoundVariantArrayAccessExpression(
                new BoundVariableExpression(variable),
                syntax.Arguments
                    .Select(index => BindExpression(index, variables, procedures))
                    .ToImmutableArray());
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
            BindArguments(syntax.Identifier, syntax.Arguments, procedure, variables, procedures));
    }

    private BoundExpression BindMemberInvocationExpression(
        MemberInvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
        => BindClassMemberInvocation(syntax.Target, syntax.Arguments, variables, procedures);

    private BoundExpression BindClassMemberInvocation(
        MemberAccessExpressionSyntax target,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        PropertyAccessorKind accessor = PropertyAccessorKind.Get)
    {
        var receiver = target.Receiver is WithReceiverExpressionSyntax
            ? BindWithReceiver(target.DotToken)
            : BindExpression(target.Receiver, variables, procedures);
        if (receiver.Type == TypeSymbol.Error)
        {
            return receiver;
        }

        if (receiver.Type == TypeSymbol.Variant)
        {
            var dynamicProcedure = CreateDynamicObjectProcedure(target.MemberToken.Text, isFunction: true);
            return new BoundMemberInvocationExpression(
                receiver,
                dynamicProcedure,
                BindArguments(
                    target.MemberToken,
                    argumentSyntaxes,
                    dynamicProcedure,
                    variables,
                    procedures));
        }

        if (receiver.Type is not ClassTypeSymbol classType)
        {
            return ReportObjectModelExpressionGap(
                "Calling a method on an object",
                target.MemberToken.Span);
        }

        if (IsLateBoundObjectType(classType))
        {
            var dynamicProcedure = CreateDynamicObjectProcedure(target.MemberToken.Text, isFunction: true);
            return new BoundMemberInvocationExpression(
                receiver,
                dynamicProcedure,
                BindArguments(
                    target.MemberToken,
                    argumentSyntaxes,
                    dynamicProcedure,
                    variables,
                    procedures));
        }

        ProcedureSymbol? procedure = null;
        if (classType.TryGetProcedure(target.MemberToken.Text, out var method))
        {
            procedure = method;
        }
        else if (classType.TryGetProperty(target.MemberToken.Text, accessor, out var property))
        {
                return new BoundPropertyInvocationExpression(
                receiver,
                property,
                BindPropertyArguments(
                    target.MemberToken,
                    argumentSyntaxes,
                    property,
                    variables,
                    procedures));
        }

        if (procedure is null)
        {
            Report(
                "VB6S0065",
                $"Class '{classType.Name}' has no method or indexed property '{target.MemberToken.Text}'.",
                target.MemberToken.Span);
            return new BoundErrorExpression();
        }

        if (!procedure.IsFunction)
        {
            Report(
                "VB6S0010",
                $"Sub '{procedure.Name}' cannot be used as an expression.",
                target.MemberToken.Span);
            return new BoundErrorExpression();
        }

        return new BoundMemberInvocationExpression(
            receiver,
            procedure,
            BindArguments(
                target.MemberToken,
                argumentSyntaxes,
                procedure,
                variables,
                procedures));
    }

    private ImmutableArray<BoundArgument> BindPropertyArguments(
        SyntaxToken anchor,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        PropertySymbol property,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        var parameters = property.Accessor is PropertyAccessorKind.Let or PropertyAccessorKind.Set &&
            property.Parameters.Length > 0
            ? property.Parameters.RemoveAt(property.Parameters.Length - 1)
            : property.Parameters;
        var procedure = new ProcedureSymbol(
            property.Name,
            parameters,
            property.Accessor == PropertyAccessorKind.Get ? property.Type : null)
        {
            PropertyAccessor = property.Accessor
        };
        return BindArguments(anchor, argumentSyntaxes, procedure, variables, procedures);
    }

    private BoundPropertyInvocationExpression BindDefaultPropertyInvocation(
        BoundExpression receiver,
        SyntaxToken anchor,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures,
        PropertyAccessorKind accessor)
    {
        if (receiver.Type is not ClassTypeSymbol classType ||
            !classType.TryGetDefaultProperty(accessor, out var property))
        {
            return new BoundPropertyInvocationExpression(
                receiver,
                new PropertySymbol(
                    "Item",
                    accessor,
                    TypeSymbol.Error,
                    ImmutableArray<ParameterSymbol>.Empty),
                ImmutableArray<BoundArgument>.Empty);
        }

        return new BoundPropertyInvocationExpression(
            receiver,
            property,
            BindPropertyArguments(anchor, argumentSyntaxes, property, variables, procedures));
    }

    private static bool IsLateBoundObjectType(ClassTypeSymbol type) =>
        ReferenceEquals(type, VBStandardTypes.Object) ||
        ReferenceEquals(type, VBStandardTypes.Control) ||
        ReferenceEquals(type, VBStandardTypes.Form) ||
        ReferenceEquals(type, VBStandardTypes.UserControl) ||
        ReferenceEquals(type, VBStandardTypes.PropertyBag) ||
        type.IsLateBoundObject ||
        (type.SourcePath is not null &&
         Path.GetExtension(type.SourcePath) is ".frm" or ".ctl");

    private static ProcedureSymbol CreateDynamicObjectProcedure(string name, bool isFunction) =>
        new(
            name,
            ImmutableArray.Create(
                new ParameterSymbol(
                    "Arguments",
                    new ArrayTypeSymbol(TypeSymbol.Variant),
                    ParameterPassingMode.ByVal)
                {
                    IsParamArray = true
                }),
            isFunction ? TypeSymbol.Variant : null)
        {
            IsLateBound = true
        };

    private BoundExpression BindArrayBoundExpression(
        InvocationExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (syntax.Arguments.Length is < 1 or > 2)
        {
            Report(
                "VB6S0034",
                $"{syntax.Identifier.Text} expects one array and an optional dimension.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        // VB6 also permits the array name with empty parentheses: UBound(values()). That syntax
        // is otherwise an array-element invocation, so preserve the array variable in this context.
        var array = BindArrayBoundTarget(syntax.Arguments[0], variables, procedures);
        if (array.Type == TypeSymbol.Error)
        {
            return new BoundErrorExpression();
        }

        if (array.Type is not ArrayTypeSymbol && array.Type != TypeSymbol.Variant)
        {
            Report(
                "VB6S0035",
                $"{syntax.Identifier.Text} requires an array, but '{array.Type.Name}' was supplied.",
                syntax.Identifier.Span);
            return new BoundErrorExpression();
        }

        var dimension = syntax.Arguments.Length == 2
            ? BindConversion(BindExpression(syntax.Arguments[1], variables, procedures), TypeSymbol.Long)
            : new BoundLiteralExpression(1L, TypeSymbol.Long);

        return new BoundArrayBoundExpression(
            array,
            dimension,
            IsUpperBound: string.Equals(syntax.Identifier.Text, "UBound", StringComparison.OrdinalIgnoreCase));
    }

    private BoundExpression BindArrayBoundTarget(
        ExpressionSyntax syntax,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (syntax is InvocationExpressionSyntax invocation &&
            invocation.Arguments.Length == 0 &&
            variables.TryGetValue(invocation.Identifier.Text, out var variable) &&
            variable.Type is ArrayTypeSymbol)
        {
            return new BoundVariableExpression(variable);
        }

        // Any array-valued expression works, not just a bare name: UBound(Section(2).Bytes) asks for
        // the bounds of an array that lives inside a user-defined type element.
        return BindExpression(syntax, variables, procedures);
    }

    private ImmutableArray<BoundArgument> BindArguments(
        SyntaxToken invocationIdentifier,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        ProcedureSymbol procedure,
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        argumentSyntaxes = NormalizeNamedArguments(invocationIdentifier, argumentSyntaxes, procedure);

        // InStr is the one intrinsic whose first parameter is optional in the middle of the
        // signature: two arguments mean (string1, string2), while three and four arguments mean
        // (start, string1, string2[, compare]). Normalize the two-argument form here so every
        // backend receives the same four values.
        if (procedure.IntrinsicKind == VBIntrinsicKind.InStr &&
            argumentSyntaxes.Length == 2 &&
            argumentSyntaxes.All(argument => argument is not OmittedArgumentExpressionSyntax))
        {
            return ImmutableArray.Create(
                new BoundArgument(procedure.Parameters[0], new BoundLiteralExpression(1L, TypeSymbol.Long)),
                new BoundArgument(
                    procedure.Parameters[1],
                    BindConversion(BindExpression(argumentSyntaxes[0], variables, procedures), TypeSymbol.String)),
                new BoundArgument(
                    procedure.Parameters[2],
                    BindConversion(BindExpression(argumentSyntaxes[1], variables, procedures), TypeSymbol.String)),
                new BoundArgument(procedure.Parameters[3], CreateDefaultArgument(procedure.Parameters[3])));
        }

        // Optional parameters may be left out at the call site, so the accepted count is a range.
        var minimumArguments = procedure.IntrinsicMinimumArguments
            ?? procedure.Parameters.Count(parameter => !parameter.IsOptional && !parameter.IsParamArray);
        var paramArrayIndex = -1;
        for (var parameterIndex = 0; parameterIndex < procedure.Parameters.Length; parameterIndex++)
        {
            if (procedure.Parameters[parameterIndex].IsParamArray)
            {
                paramArrayIndex = parameterIndex;
                break;
            }
        }

        var fixedParameterCount = paramArrayIndex >= 0 ? paramArrayIndex : procedure.Parameters.Length;
        if ((paramArrayIndex < 0 && argumentSyntaxes.Length > fixedParameterCount) ||
            argumentSyntaxes.Length < minimumArguments)
        {
            var expected = paramArrayIndex >= 0
                ? $"at least {minimumArguments.ToString(CultureInfo.InvariantCulture)}"
                : minimumArguments == procedure.Parameters.Length
                ? procedure.Parameters.Length.ToString(CultureInfo.InvariantCulture)
                : $"{minimumArguments.ToString(CultureInfo.InvariantCulture)} to " +
                  procedure.Parameters.Length.ToString(CultureInfo.InvariantCulture);
            Report(
                "VB6S0006",
                $"Procedure '{procedure.Name}' expects {expected} argument(s), but {argumentSyntaxes.Length} were supplied.",
                invocationIdentifier.Span);
        }

        var arguments = ImmutableArray.CreateBuilder<BoundArgument>();
        var suppliedFixedCount = Math.Min(argumentSyntaxes.Length, fixedParameterCount);
        for (var index = 0; index < suppliedFixedCount; index++)
        {
            var parameter = index < procedure.Parameters.Length ? procedure.Parameters[index] : null;

            if (argumentSyntaxes[index] is OmittedArgumentExpressionSyntax &&
                parameter is not null &&
                (parameter.IsOptional ||
                 procedure.IntrinsicKind == VBIntrinsicKind.InStr && index == 0))
            {
                arguments.Add(new BoundArgument(
                    parameter,
                    procedure.IntrinsicKind == VBIntrinsicKind.InStr && index == 0
                        ? new BoundLiteralExpression(1L, TypeSymbol.Long)
                        : CreateDefaultArgument(parameter))
                {
                    IsOmitted = true
                });
                continue;
            }

            var expression = BindExpression(argumentSyntaxes[index], variables, procedures);

            var requiresByRefTemporary = false;

            // An explicit ByVal at the call site overrides a ByRef parameter, the same way
            // parentheses do: CopyMemory dst, ByVal VarPtr(src), 4 hands over a value.
            var forcedByValue = argumentSyntaxes[index] is ArgumentPassingModeExpressionSyntax
            {
                PassingModeKeyword.Kind: SyntaxKind.ByValKeyword
            };

            if (parameter is not null)
            {
                if (procedure.IsExternal && parameter.IsAny && forcedByValue)
                {
                    // The native As Any contract consumes a pointer value. Keep the original
                    // expression so the lowering phase can recognize VarPtr(variable).
                }
                else if (parameter.PassingMode == ParameterPassingMode.ByVal)
                {
                    expression = BindConversion(expression, parameter.Type);
                }
                else if (forcedByValue ||
                         argumentSyntaxes[index] is ParenthesizedExpressionSyntax ||
                         expression is BoundVariableExpression { Variable.IsConstant: true } ||
                         expression is not BoundVariableExpression &&
                         expression is not BoundArrayAccessExpression &&
                         expression is not BoundElementAccessExpression &&
                         expression is not BoundVariantArrayAccessExpression &&
                         expression is not BoundMemberAccessExpression)
                {
                    // Not an error: VB6 accepts a literal, an expression, or a function result for
                    // a ByRef parameter by passing a temporary of the parameter type and throwing
                    // the write-back away. The conversion is the same one ByVal would apply.
                    //
                    // Parentheses around an argument do the same thing on purpose - Foo (x) forces
                    // x to be evaluated to a value, so the callee cannot write back to it. That is
                    // decided on the syntax because BindExpression unwraps the parentheses.
                    expression = BindConversion(expression, parameter.Type);
                    requiresByRefTemporary = true;
                }
                else if (!parameter.IsAny && !AreByRefTypesCompatible(expression.Type, parameter.Type) &&
                         expression.Type != TypeSymbol.Error &&
                         parameter.Type != TypeSymbol.Error)
                {
                    // A variable of the wrong type stays an error. VB6 reports "ByRef argument type
                    // mismatch" here rather than silently converting, because the write-back would
                    // have nowhere to go.
                    Report(
                        "VB6S0008",
                        $"ByRef argument type '{expression.Type.Name}' does not match parameter type '{parameter.Type.Name}'.",
                        invocationIdentifier.Span);
                }
            }

            arguments.Add(new BoundArgument(parameter, expression)
            {
                RequiresByRefTemporary = requiresByRefTemporary,
                IsByValAtCallSite = forcedByValue
            });
        }

        if (paramArrayIndex >= 0)
        {
            var parameter = procedure.Parameters[paramArrayIndex];
            var elements = ImmutableArray.CreateBuilder<BoundExpression>(
                Math.Max(0, argumentSyntaxes.Length - fixedParameterCount));
            for (var index = fixedParameterCount; index < argumentSyntaxes.Length; index++)
            {
                if (argumentSyntaxes[index] is OmittedArgumentExpressionSyntax)
                {
                    elements.Add(new BoundInvocationExpression(
                        MissingValueProcedure,
                        ImmutableArray<BoundArgument>.Empty));
                    continue;
                }

                elements.Add(BindConversion(
                    BindExpression(argumentSyntaxes[index], variables, procedures),
                    TypeSymbol.Variant));
            }

            arguments.Add(new BoundArgument(
                parameter,
                new BoundArrayLiteralExpression(
                    (ArrayTypeSymbol)parameter.Type,
                    elements.ToImmutable())));
        }

        // Fill in the Optional parameters the call site left out. A missing default means the
        // default of the type, which is what VB6 hands the callee. The value is a temporary, so a
        // ByRef Optional parameter has nowhere to write back to - exactly as in VB6.
        for (var index = suppliedFixedCount; index < fixedParameterCount; index++)
        {
            var parameter = procedure.Parameters[index];
            if (!parameter.IsOptional)
            {
                break;
            }

            arguments.Add(new BoundArgument(parameter, CreateDefaultArgument(parameter))
            {
                RequiresByRefTemporary = parameter.PassingMode == ParameterPassingMode.ByRef,
                IsOmitted = true
            });
        }

        return arguments.ToImmutable();
    }

    private ImmutableArray<ExpressionSyntax> NormalizeNamedArguments(
        SyntaxToken invocationIdentifier,
        ImmutableArray<ExpressionSyntax> argumentSyntaxes,
        ProcedureSymbol procedure)
    {
        if (!argumentSyntaxes.Any(argument => argument is NamedArgumentExpressionSyntax))
        {
            return argumentSyntaxes;
        }

        var paramArrayIndex = -1;
        for (var index = 0; index < procedure.Parameters.Length; index++)
        {
            if (procedure.Parameters[index].IsParamArray)
            {
                paramArrayIndex = index;
                break;
            }
        }

        var fixedParameterCount = paramArrayIndex >= 0 ? paramArrayIndex : procedure.Parameters.Length;
        var slots = new ExpressionSyntax?[fixedParameterCount];
        var extraArguments = ImmutableArray.CreateBuilder<ExpressionSyntax>();
        var nextPositional = 0;
        var sawNamed = false;

        foreach (var argument in argumentSyntaxes)
        {
            if (argument is NamedArgumentExpressionSyntax named)
            {
                sawNamed = true;
                var parameterIndex = -1;
                for (var index = 0; index < fixedParameterCount; index++)
                {
                    if (string.Equals(
                            procedure.Parameters[index].Name,
                            named.NameToken.Text,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        parameterIndex = index;
                        break;
                    }
                }

                if (parameterIndex < 0)
                {
                    Report(
                        "VB6S0069",
                        $"Named argument '{named.NameToken.Text}' is not a parameter of procedure '{procedure.Name}'.",
                        named.NameToken.Span);
                    continue;
                }

                if (slots[parameterIndex] is not null)
                {
                    Report(
                        "VB6S0069",
                        $"Named argument '{named.NameToken.Text}' was supplied more than once.",
                        named.NameToken.Span);
                    continue;
                }

                slots[parameterIndex] = named.Expression;
                continue;
            }

            if (sawNamed)
            {
                Report(
                    "VB6S0069",
                    "A positional argument cannot follow a named argument.",
                    invocationIdentifier.Span);
            }

            while (nextPositional < fixedParameterCount && slots[nextPositional] is not null)
            {
                nextPositional++;
            }

            if (nextPositional < fixedParameterCount)
            {
                slots[nextPositional++] = argument;
            }
            else
            {
                extraArguments.Add(argument);
            }
        }

        var lastSlot = Array.FindLastIndex(slots, slot => slot is not null);
        var normalized = ImmutableArray.CreateBuilder<ExpressionSyntax>(Math.Max(0, lastSlot + 1) + extraArguments.Count);
        for (var index = 0; index <= lastSlot; index++)
        {
            normalized.Add(slots[index] ?? new OmittedArgumentExpressionSyntax());
        }

        normalized.AddRange(extraArguments);
        return normalized.ToImmutable();
    }

    /// <summary>
    /// The value an omitted Optional argument carries: the declared default, or the default of the
    /// parameter type when the declaration gave none.
    /// </summary>
    private BoundExpression CreateDefaultArgument(ParameterSymbol parameter)
    {
        if (parameter.DefaultValue is not null)
        {
            return BindConversion(
                new BoundLiteralExpression(parameter.DefaultValue, InferLiteralType(parameter.DefaultValue)),
                parameter.Type);
        }

        if (parameter.Type == TypeSymbol.String)
        {
            return new BoundLiteralExpression(string.Empty, TypeSymbol.String);
        }

        if (parameter.Type == TypeSymbol.Boolean)
        {
            return new BoundLiteralExpression(false, TypeSymbol.Boolean);
        }

        if (parameter.Type == TypeSymbol.Variant)
        {
            return new BoundInvocationExpression(
                MissingValueProcedure,
                ImmutableArray<BoundArgument>.Empty);
        }

        return BindConversion(new BoundLiteralExpression(0L, TypeSymbol.Long), parameter.Type);
    }

    private static TypeSymbol InferLiteralType(object value) => value switch
    {
        string => TypeSymbol.String,
        bool => TypeSymbol.Boolean,
        double => TypeSymbol.Double,
        _ => TypeSymbol.Long
    };

    private static bool AreByRefTypesCompatible(TypeSymbol argumentType, TypeSymbol parameterType)
    {
        if (argumentType == parameterType)
        {
            return true;
        }

        if (argumentType is ArrayTypeSymbol argumentArray && parameterType is ArrayTypeSymbol parameterArray)
        {
            return argumentArray.ElementType == parameterArray.ElementType &&
                (!argumentArray.HasKnownRank || !parameterArray.HasKnownRank || argumentArray.Rank == parameterArray.Rank);
        }

        if (parameterType == VBStandardTypes.Control &&
            (argumentType is ClassTypeSymbol { IsControlContract: true } ||
             ReferenceEquals(argumentType, VBStandardTypes.Form) ||
             ReferenceEquals(argumentType, VBStandardTypes.UserControl)))
        {
            return true;
        }

        return false;
    }

    private static BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        return syntax.LiteralToken.Kind switch
        {
            SyntaxKind.IntegerLiteralToken => BindIntegerLiteral(syntax.LiteralToken.Value),
            SyntaxKind.FloatingLiteralToken when syntax.LiteralToken.Value is decimal =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.Currency),
            SyntaxKind.FloatingLiteralToken when syntax.LiteralToken.Value is float =>
                new BoundLiteralExpression(syntax.LiteralToken.Value, TypeSymbol.Single),
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
        Dictionary<string, VariableSymbol> variables,
        IReadOnlyDictionary<string, ProcedureSymbol> procedures)
    {
        if (variables.TryGetValue(syntax.IdentifierToken.Text, out var variable))
        {
            return new BoundVariableExpression(variable);
        }

        if (TryGetContainingClassProperty(
                syntax.IdentifierToken.Text,
                PropertyAccessorKind.Get,
                variables,
                out var property))
        {
            return property;
        }

        // A bare name is also how VB6 calls a function that takes no arguments, as in
        // FileNum = FreeFile. A variable of that name would have won above, which is the right
        // precedence.
        if (procedures.TryGetValue(syntax.IdentifierToken.Text, out var procedure) &&
            procedure.IsFunction &&
            procedure.Parameters.All(parameter => parameter.IsOptional))
        {
            return new BoundInvocationExpression(procedure, ImmutableArray<BoundArgument>.Empty);
        }

        if (!_optionExplicit && _activeLocals is not null)
        {
            var implicitLocal = new LocalVariableSymbol(
                syntax.IdentifierToken.Text,
                GetIdentifierType(syntax.IdentifierToken) ?? TypeSymbol.Variant);
            variables[implicitLocal.Name] = implicitLocal;
            _activeLocals[implicitLocal.Name] = implicitLocal;
            return new BoundVariableExpression(implicitLocal);
        }

        Report(
            "VB6S0001",
            $"Variable '{syntax.IdentifierToken.Text}' is not declared.",
            syntax.IdentifierToken.Span);
        return new BoundErrorExpression();
    }

    private bool TryGetContainingClassProperty(
        string name,
        PropertyAccessorKind accessor,
        Dictionary<string, VariableSymbol> variables,
        out BoundPropertyAccessExpression propertyAccess)
    {
        if (_containingClass is not null &&
            variables.TryGetValue("Me", out var me) &&
            _containingClass.TryGetProperty(name, accessor, out var property))
        {
            propertyAccess = new BoundPropertyAccessExpression(
                new BoundVariableExpression(me),
                property);
            return true;
        }

        propertyAccess = null!;
        return false;
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

        if (operand.Type == TypeSymbol.Variant && syntax.OperatorToken.Kind == SyntaxKind.NotKeyword)
        {
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Variant);
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

        if (operand.Type == TypeSymbol.Variant)
        {
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, TypeSymbol.Variant);
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
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Variant);
                }

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
            {
                if (left.Type != TypeSymbol.String && left.Type != TypeSymbol.Variant)
                {
                    left = BindConversion(left, TypeSymbol.String);
                }

                if (right.Type != TypeSymbol.String && right.Type != TypeSymbol.Variant)
                {
                    right = BindConversion(right, TypeSymbol.String);
                }

                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Boolean)
                {
                    UseTextCompare = _optionCompareText
                };
            }

            case SyntaxKind.IsKeyword:
                if (!IsObjectIdentityOperand(left.Type) || !IsObjectIdentityOperand(right.Type))
                {
                    Report(
                        "VB6S0024",
                        "Operator 'Is' requires object or Variant operands.",
                        syntax.OperatorToken.Span);
                    return new BoundErrorExpression();
                }

                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    TypeSymbol.Boolean);

            case SyntaxKind.EqualsToken:
            case SyntaxKind.LessGreaterToken:
            case SyntaxKind.LessToken:
            case SyntaxKind.LessOrEqualsToken:
            case SyntaxKind.GreaterToken:
            case SyntaxKind.GreaterOrEqualsToken:
                if (left.Type != TypeSymbol.Variant && right.Type != TypeSymbol.Variant &&
                    IsNumericType(left.Type) && IsNumericType(right.Type))
                {
                    var comparisonType = GetCommonNumericType(left.Type, right.Type);
                    left = BindConversion(left, comparisonType);
                    right = BindConversion(right, comparisonType);
                }
                else if (left.Type != TypeSymbol.Variant && right.Type != TypeSymbol.Variant &&
                    left.Type != right.Type)
                {
                    right = BindConversion(right, left.Type);
                }

                return new BoundBinaryExpression(
                    left,
                    syntax.OperatorToken.Kind,
                    right,
                    left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant
                        ? TypeSymbol.Variant
                        : TypeSymbol.Boolean);

            case SyntaxKind.AndKeyword:
            case SyntaxKind.OrKeyword:
            case SyntaxKind.XorKeyword:
            case SyntaxKind.EqvKeyword:
            case SyntaxKind.ImpKeyword:
            {
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Variant);
                }

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

                var resultType = GetIntegerOperationType(
                    left.Type == TypeSymbol.Boolean ? TypeSymbol.Integer : left.Type,
                    right.Type == TypeSymbol.Boolean ? TypeSymbol.Integer : right.Type);

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
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.String);
                }

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
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Variant);
                }

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
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Variant);
                }

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
                if (left.Type == TypeSymbol.Variant || right.Type == TypeSymbol.Variant)
                {
                    return new BoundBinaryExpression(
                        left,
                        syntax.OperatorToken.Kind,
                        right,
                        TypeSymbol.Variant);
                }

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
        type == TypeSymbol.LongLong || type == TypeSymbol.LongPtr || type == TypeSymbol.UShort ||
        type == TypeSymbol.UInteger || type == TypeSymbol.ULong || type == TypeSymbol.Single || type == TypeSymbol.Double ||
        type == TypeSymbol.Currency;

    private static bool IsFloatingOrFixedPointType(TypeSymbol type) =>
        type == TypeSymbol.Single || type == TypeSymbol.Double || type == TypeSymbol.Currency;

    private static bool IsSingleDivisionOperand(TypeSymbol type) =>
        type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Single;

    private static bool IsBitwiseOperandType(TypeSymbol type) =>
        IsNumericType(type) || type == TypeSymbol.Boolean;

    private static bool IsObjectIdentityOperand(TypeSymbol type) =>
        type == TypeSymbol.Variant || type is ClassTypeSymbol;

    private static bool IsAddressableExpression(BoundExpression expression) =>
        expression is BoundVariableExpression or
            BoundArrayAccessExpression or
            BoundElementAccessExpression or
            BoundMemberAccessExpression or
            BoundPropertyAccessExpression or
            BoundPropertyInvocationExpression { Type: ClassTypeSymbol } or
            BoundWithReceiverExpression;

    private static TypeSymbol GetIntegerOperationType(TypeSymbol left, TypeSymbol right) =>
        left == TypeSymbol.ULong || right == TypeSymbol.ULong
            ? TypeSymbol.ULong
            : left == TypeSymbol.LongLong || right == TypeSymbol.LongLong
            ? TypeSymbol.LongLong
            : left == TypeSymbol.LongPtr || right == TypeSymbol.LongPtr
                ? TypeSymbol.LongPtr
            : left == TypeSymbol.Long || right == TypeSymbol.Long
                ? left == TypeSymbol.UInteger || right == TypeSymbol.UInteger
                    ? TypeSymbol.LongLong
                    : TypeSymbol.Long
            : left == TypeSymbol.UInteger || right == TypeSymbol.UInteger
                ? TypeSymbol.UInteger
            : left == TypeSymbol.UShort || right == TypeSymbol.UShort
                ? TypeSymbol.UShort
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

        if ((left == TypeSymbol.Single && (right == TypeSymbol.Long || right == TypeSymbol.LongLong || right == TypeSymbol.LongPtr || right == TypeSymbol.ULong)) ||
            (right == TypeSymbol.Single && (left == TypeSymbol.Long || left == TypeSymbol.LongLong || left == TypeSymbol.LongPtr || left == TypeSymbol.ULong)))
        {
            return TypeSymbol.Double;
        }

        if (left == TypeSymbol.Single || right == TypeSymbol.Single)
        {
            return TypeSymbol.Single;
        }

        if (left == TypeSymbol.ULong || right == TypeSymbol.ULong)
        {
            return TypeSymbol.ULong;
        }

        if (left == TypeSymbol.LongLong || right == TypeSymbol.LongLong)
        {
            return TypeSymbol.LongLong;
        }

        if (left == TypeSymbol.LongPtr || right == TypeSymbol.LongPtr)
        {
            return TypeSymbol.LongPtr;
        }

        if (left == TypeSymbol.UInteger || right == TypeSymbol.UInteger)
        {
            return left == TypeSymbol.Long || right == TypeSymbol.Long
                ? TypeSymbol.LongLong
                : TypeSymbol.UInteger;
        }

        if (left == TypeSymbol.UShort || right == TypeSymbol.UShort)
        {
            return left == TypeSymbol.Long || right == TypeSymbol.Long
                ? TypeSymbol.Long
                : TypeSymbol.UShort;
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
    private readonly record struct WithBindingContext(int WithId, BoundExpression Receiver);
}
