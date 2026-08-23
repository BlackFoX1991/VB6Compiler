using System.Collections.Immutable;
using VB6.Parser;
using VB6.ProjectSystem;
using VB6.Semantics;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Compiler;

public sealed class VBProjectCompilation
{
    private readonly string _projectFilePath;

    private VBProjectCompilation(string projectFilePath)
    {
        _projectFilePath = Path.GetFullPath(projectFilePath);
    }

    public static VBProjectCompilation Create(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        return new VBProjectCompilation(projectFilePath);
    }

    public VBProjectCompilationAnalysis Analyze()
    {
        var loadResult = new VBProjectLoader().Load(_projectFilePath);
        var projectDiagnostics = ImmutableArray.CreateBuilder<VBProjectCompilationDiagnostic>();
        var sourceDiagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var parsedModules = ImmutableArray.CreateBuilder<ParsedProjectModule>();

        foreach (var diagnostic in loadResult.Diagnostics)
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                loadResult.Project.FilePath,
                diagnostic.Line));
        }

        foreach (var module in loadResult.Project.Items.Where(item =>
                     item.Kind is VBProjectItemKind.Module or VBProjectItemKind.Class or
                         VBProjectItemKind.Form or VBProjectItemKind.UserControl))
        {
            var modulePath = module.GetFullPath(loadResult.Project.ProjectDirectory);
            if (!File.Exists(modulePath))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0001",
                    $"Project source '{module.RelativePath}' was not found.",
                    modulePath));
                continue;
            }

            string source;
            try
            {
                source = File.ReadAllText(modulePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0002",
                    $"Project source '{module.RelativePath}' could not be read: {exception.Message}",
                    modulePath));
                continue;
            }

            var normalizedSource = module.Kind is VBProjectItemKind.Class or
                VBProjectItemKind.Form or VBProjectItemKind.UserControl
                ? VBClassModuleSource.Normalize(source)
                : source;
            var text = SourceText.From(normalizedSource, modulePath);
            var parseResult = new ParserType(text).ParseCompilationUnit();
            sourceDiagnostics.AddRange(parseResult.Diagnostics);
            var semanticRoot = ImplicitVariantSyntaxLowerer.Lower(parseResult.Root);
            parsedModules.Add(new ParsedProjectModule(
                module,
                modulePath,
                text,
                parseResult,
                semanticRoot));
        }

        // Types are collected from every module that produced a tree, for the same reason
        // procedures and variables are: comLinker.bas declares ENUM_APP_TYPE and has three syntax
        // errors, and hiding its enums made every variable typed with them undeclared elsewhere.
        var classTypes = DeclareProjectClassTypes(
            parsedModules,
            loadResult.Project.Items,
            projectDiagnostics);
        var enumSymbols = VBEnumSymbols.Bind(parsedModules.Select(module => module.SemanticRoot));
        using var enumTypeScope = UserDefinedTypeLookupScope.PushAliases(enumSymbols.TypeAliases);
        var classTypeAliases = classTypes.ToDictionary(
            entry => entry.Key,
            entry => (TypeSymbol)entry.Value,
            StringComparer.OrdinalIgnoreCase);
        using var classTypeScope = UserDefinedTypeLookupScope.PushAliases(classTypeAliases);
        var userDefinedTypes = new ProjectUserDefinedTypeDeclarationBinder().Bind(
            parsedModules.Select(module =>
                new UserDefinedTypeModuleInput(module.Text, module.SemanticRoot)));
        sourceDiagnostics.AddRange(userDefinedTypes.Diagnostics);
        var userDefinedTypesByPath = userDefinedTypes.Modules.ToDictionary(
            module => module.Module.Text.FilePath ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);
        DefineProjectClassMembers(
            parsedModules,
            classTypes,
            userDefinedTypesByPath,
            projectDiagnostics);

        var procedureSymbols = DeclareProjectProcedures(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        var moduleVariableSymbols = DeclareProjectModuleVariables(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        foreach (var item in loadResult.Project.Items.Where(item =>
                     item.Kind is VBProjectItemKind.Form or VBProjectItemKind.UserControl))
        {
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? Path.GetFileNameWithoutExtension(item.RelativePath)
                : item.Name!;
            if (classTypes.TryGetValue(name, out var objectType))
            {
                moduleVariableSymbols.TryAdd(name, new ModuleVariableSymbol(name, objectType));
            }

            var itemPath = item.GetFullPath(loadResult.Project.ProjectDirectory);
            if (!File.Exists(itemPath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(itemPath))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3 ||
                    !string.Equals(parts[0], "Begin", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var controlName = parts[2];
                moduleVariableSymbols.TryAdd(
                    controlName,
                    new ModuleVariableSymbol(controlName, VBStandardTypes.Control));
            }
        }
        var visibleEnumConstants = enumSymbols.AddMemberSymbols(moduleVariableSymbols);
        var visibleBuiltInConstants = VBBuiltInConstants.AddTo(moduleVariableSymbols);
        var units = ImmutableArray.CreateBuilder<VBProjectCompilationUnit>();
        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        var projectClassTypes = ImmutableArray.CreateBuilder<ClassTypeSymbol>();
        var properties = ImmutableArray.CreateBuilder<PropertySymbol>();
        var events = ImmutableArray.CreateBuilder<EventSymbol>();
        var moduleVariables = ImmutableArray.CreateBuilder<BoundModuleVariable>();
        var staticVariables = ImmutableArray.CreateBuilder<BoundModuleVariable>();

        foreach (var module in parsedModules)
        {
            if (module.ParseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                units.Add(new VBProjectCompilationUnit(
                    module.Item,
                    module.FilePath,
                    new CompilationAnalysis(module.ParseResult, null, module.ParseResult.Diagnostics)));
                continue;
            }

            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            var availableProcedures = GetProcedureScope(
                module,
                procedureSymbols,
                userDefinedTypesByPath);
            classTypes.TryGetValue(
                module.Item.Name ?? Path.GetFileNameWithoutExtension(module.FilePath),
                out var containingClass);
            if (module.Item.Kind is not (VBProjectItemKind.Class or VBProjectItemKind.Form or
                VBProjectItemKind.UserControl))
            {
                containingClass = null;
            }
            SemanticModel preliminaryModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                preliminaryModel = new Binder(module.Text, enumSymbols.QualifiedMembers)
                    .BindCompilationUnit(
                        module.SemanticRoot,
                        availableProcedures,
                        moduleVariableSymbols,
                        containingClass);
            }

            var forEachRoot = ForEachArraySyntaxLowerer.Lower(module.SemanticRoot, preliminaryModel);

            SemanticModel semanticModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                semanticModel = new Binder(module.Text, enumSymbols.QualifiedMembers)
                    .BindCompilationUnit(
                        forEachRoot,
                        availableProcedures,
                        moduleVariableSymbols,
                        containingClass);
            }
            var userDefinedTypeValueDiagnostics = moduleUserDefinedTypes is null
                ? ImmutableArray<Diagnostic>.Empty
                : UserDefinedTypeValueGuard.Validate(
                    module.Text,
                    forEachRoot,
                    moduleUserDefinedTypes.Types);
            var variantOperationDiagnostics = VariantOperationGuard.Validate(module.Text, semanticModel);
            sourceDiagnostics.AddRange(semanticModel.Diagnostics);
            sourceDiagnostics.AddRange(userDefinedTypeValueDiagnostics);
            sourceDiagnostics.AddRange(variantOperationDiagnostics);
            procedures.AddRange(semanticModel.Procedures);
            projectClassTypes.AddRange(classTypes.Values.Where(type =>
                string.Equals(type.SourcePath, module.FilePath, StringComparison.OrdinalIgnoreCase)));
            properties.AddRange(semanticModel.Properties);
            events.AddRange(semanticModel.Events);
            if (module.Item.Kind == VBProjectItemKind.Module)
            {
                moduleVariables.AddRange(semanticModel.ModuleVariables);
                staticVariables.AddRange(semanticModel.StaticVariables);
            }

            var unitDiagnostics = module.ParseResult.Diagnostics
                .AddRange(moduleUserDefinedTypes?.Diagnostics ?? ImmutableArray<Diagnostic>.Empty)
                .AddRange(semanticModel.Diagnostics)
                .AddRange(userDefinedTypeValueDiagnostics)
                .AddRange(variantOperationDiagnostics);
            var compilationAnalysis = new CompilationAnalysis(
                module.ParseResult,
                semanticModel,
                unitDiagnostics);
            if (moduleUserDefinedTypes is not null)
            {
                compilationAnalysis = compilationAnalysis with
                {
                    UserDefinedTypes = new UserDefinedTypeDeclarationResult(
                        moduleUserDefinedTypes.Types,
                        moduleUserDefinedTypes.Diagnostics)
                };
            }

            units.Add(new VBProjectCompilationUnit(
                module.Item,
                module.FilePath,
                compilationAnalysis));
        }

        var combinedDiagnostics = sourceDiagnostics.ToImmutable();
        var combinedSemanticModel = new SemanticModel(procedures.ToImmutable(), combinedDiagnostics)
        {
            ClassTypes = projectClassTypes.ToImmutable(),
            Properties = properties.ToImmutable(),
            Events = events.ToImmutable(),
            ModuleVariables = moduleVariables.ToImmutable()
                .AddRange(visibleEnumConstants)
                .AddRange(visibleBuiltInConstants),
            StaticVariables = staticVariables.ToImmutable()
        };
        return new VBProjectCompilationAnalysis(
            loadResult.Project,
            units.ToImmutable(),
            combinedSemanticModel,
            combinedDiagnostics,
            projectDiagnostics.ToImmutable())
        {
            UserDefinedTypes = userDefinedTypes
        };
    }

    /// <summary>Lowers every module of the project to the IR the managed backend emits from.</summary>
    public VBProjectLoweringResult Lower() => DirectManagedCompilation.Lower(this);

    /// <summary>Emits an executable assembly, its debug information and its runtime files.</summary>
    public VBProjectManagedApplicationEmitResult EmitManagedApplication(string outputPath) =>
        DirectManagedCompilation.EmitManaged(this, outputPath);
    /// <summary>
    /// VB6 <c>Public</c> module variables are visible project-wide, so they are declared across
    /// all modules before any module is bound - the same way procedures already are. The type
    /// lookup scope must match the variable's origin module because Private UDT names can shadow
    /// project-wide Public UDTs.
    /// </summary>
    private static Dictionary<string, ModuleVariableSymbol> DeclareProjectModuleVariables(
        IEnumerable<ParsedProjectModule> modules,
        IReadOnlyDictionary<string, UserDefinedTypeModuleResult> userDefinedTypesByPath,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var moduleVariables = new Dictionary<string, ModuleVariableSymbol>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Same reasoning as for procedures: a module with a syntax error still declares its
        // variables, and hiding them turns one parser gap into many name resolution errors.
        foreach (var module in modules.Where(module => module.Item.Kind == VBProjectItemKind.Module))
        {
            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            ImmutableArray<ModuleVariableSymbol> symbols;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                symbols = Binder.CreateModuleVariableSymbols(module.Text, module.SemanticRoot);
            }

            foreach (var symbol in symbols)
            {
                if (moduleVariables.TryAdd(symbol.Name, symbol))
                {
                    origins.Add(symbol.Name, module.Item.RelativePath);
                    continue;
                }

                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0006",
                    $"Module variable '{symbol.Name}' is declared in both '{origins[symbol.Name]}' and '{module.Item.RelativePath}'.",
                    module.FilePath));
            }
        }

        return moduleVariables;
    }

    private static Dictionary<string, ClassTypeSymbol> DeclareProjectClassTypes(
        IEnumerable<ParsedProjectModule> modules,
        IEnumerable<VBProjectItem> projectItems,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var classTypes = new Dictionary<string, ClassTypeSymbol>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules.Where(module => module.Item.Kind is
                     VBProjectItemKind.Class or VBProjectItemKind.Form or VBProjectItemKind.UserControl))
        {
            var name = string.IsNullOrWhiteSpace(module.Item.Name)
                ? Path.GetFileNameWithoutExtension(module.FilePath)
                : module.Item.Name!;
            var symbol = new ClassTypeSymbol(name, module.FilePath);
            if (classTypes.TryAdd(symbol.Name, symbol))
            {
                origins.Add(symbol.Name, module.Item.RelativePath);
                continue;
            }

            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0008",
                $"Class module '{symbol.Name}' is declared in both '{origins[symbol.Name]}' and '{module.Item.RelativePath}'.",
                module.FilePath));
        }

        foreach (var item in projectItems.Where(item => item.Kind is
                     VBProjectItemKind.Class or VBProjectItemKind.Form or VBProjectItemKind.UserControl))
        {
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? Path.GetFileNameWithoutExtension(item.RelativePath)
                : item.Name!;
            classTypes.TryAdd(
                name,
                new ClassTypeSymbol(name, item.RelativePath));
        }

        return classTypes;
    }

    private static void DefineProjectClassMembers(
        IEnumerable<ParsedProjectModule> modules,
        IReadOnlyDictionary<string, ClassTypeSymbol> classTypes,
        IReadOnlyDictionary<string, UserDefinedTypeModuleResult> userDefinedTypesByPath,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var interfaceRelations = new List<(ClassTypeSymbol Implementor, ImplementsStatementSyntax Declaration, string FilePath)>();
        foreach (var module in modules.Where(module => module.Item.Kind is
                     VBProjectItemKind.Class or VBProjectItemKind.Form or VBProjectItemKind.UserControl))
        {
            var name = string.IsNullOrWhiteSpace(module.Item.Name)
                ? Path.GetFileNameWithoutExtension(module.FilePath)
                : module.Item.Name!;
            if (!classTypes.TryGetValue(name, out var classType))
            {
                continue;
            }

            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            using var typeScope = UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes));
            var procedures = module.SemanticRoot.Members
                .Select(member => member switch
                {
                    SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                    FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                    DeclareDeclarationSyntax declare => Binder.CreateDeclareProcedureSymbol(declare),
                    _ => null
                })
                .Where(symbol => symbol is not null)
                .Cast<ProcedureSymbol>();
            var properties = module.SemanticRoot.Members
                .OfType<PropertyDeclarationSyntax>()
                .Select(Binder.CreatePropertySymbol)
                .ToList();

            // Form and UserControl module fields are visible as members of the generated object,
            // and designer controls are predeclared fields in VB6. Keep both contracts in the
            // class type so callers can bind `frmMain.RunEnabled` and `frmMain.cmdOk.Caption`
            // without coupling semantic analysis to the later forms host.
            foreach (var variable in Binder.CreateModuleVariableSymbols(module.Text, module.SemanticRoot))
            {
                AddReadWriteProperty(properties, variable.Name, variable.Type);
            }

            if (module.Item.Kind is VBProjectItemKind.Form or VBProjectItemKind.UserControl)
            {
                foreach (var controlName in ReadDesignerControlNames(module.FilePath))
                {
                    AddReadWriteProperty(properties, controlName, VBStandardTypes.Control);
                }
            }
            var events = module.SemanticRoot.Members
                .OfType<EventDeclarationSyntax>()
                .Select(Binder.CreateEventSymbol);

            foreach (var declaration in module.SemanticRoot.Members.OfType<ImplementsStatementSyntax>())
            {
                interfaceRelations.Add((classType, declaration, module.FilePath));
            }

            if (!classType.TryDefineMembers(procedures, properties, events, out var duplicateMemberName))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0009",
                    $"Class module '{classType.Name}' declares duplicate member '{duplicateMemberName}'.",
                    module.FilePath));
            }

            if (TryReadDefaultPropertyName(module.SemanticRoot) is { } defaultPropertyName)
            {
                classType.SetDefaultPropertyName(defaultPropertyName);
            }
        }

        foreach (var relation in interfaceRelations)
        {
            var interfaceName = relation.Declaration.TypeName?.Text ?? relation.Declaration.TypeToken.Text;
            if (!classTypes.TryGetValue(interfaceName, out var interfaceType))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0010",
                    $"Class '{relation.Implementor.Name}' implements unknown class '{interfaceName}'.",
                    relation.FilePath));
                continue;
            }

            if (ReferenceEquals(relation.Implementor, interfaceType))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0011",
                    $"Class '{relation.Implementor.Name}' cannot implement itself.",
                    relation.FilePath));
                continue;
            }

            interfaceType.MarkAsInterfaceContract();
            relation.Implementor.SetImplementedInterfaces(
                relation.Implementor.ImplementedInterfaces
                    .Append(interfaceType)
                    .Distinct());
            ValidateInterfaceContract(
                relation.Implementor,
                interfaceType,
                relation.FilePath,
                projectDiagnostics);
        }

        static string? TryReadDefaultPropertyName(CompilationUnitSyntax root)
        {
            foreach (var attribute in root.Members.OfType<AttributeSyntax>())
            {
                var tokens = attribute.Tokens;
                if (tokens.Length >= 5 &&
                    tokens[0].Kind == SyntaxKind.IdentifierToken &&
                    tokens[1].Kind == SyntaxKind.DotToken &&
                    string.Equals(tokens[2].Text, "VB_UserMemId", StringComparison.OrdinalIgnoreCase) &&
                    tokens[3].Kind == SyntaxKind.EqualsToken &&
                    string.Equals(tokens[4].Text, "0", StringComparison.Ordinal))
                {
                    return tokens[0].Text;
                }
            }

            return null;
        }

        static void ValidateInterfaceContract(
            ClassTypeSymbol implementor,
            ClassTypeSymbol interfaceType,
            string filePath,
            ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
        {
            foreach (var procedure in interfaceType.Procedures)
            {
                var implementationName = interfaceType.Name + "_" + procedure.Name;
                if (!implementor.TryGetProcedure(implementationName, out var implementation) ||
                    !HaveSameProcedureSignature(procedure, implementation))
                {
                    projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                        "VB6PRJ0012",
                        $"Class '{implementor.Name}' does not provide a compatible implementation for '{implementationName}'.",
                        filePath));
                }
            }

            foreach (var property in interfaceType.Properties)
            {
                var implementationName = interfaceType.Name + "_" + property.Name;
                if (!implementor.TryGetProperty(implementationName, property.Accessor, out var implementation) ||
                    !HaveSamePropertySignature(property, implementation))
                {
                    projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                        "VB6PRJ0012",
                        $"Class '{implementor.Name}' does not provide a compatible implementation for " +
                        $"'{implementationName}' ({property.Accessor}).",
                        filePath));
                }
            }
        }

        static bool HaveSameProcedureSignature(ProcedureSymbol expected, ProcedureSymbol actual) =>
            expected.ReturnType == actual.ReturnType &&
            HaveSameParameters(expected.Parameters, actual.Parameters);

        static bool HaveSamePropertySignature(PropertySymbol expected, PropertySymbol actual) =>
            expected.Type == actual.Type &&
            HaveSameParameters(expected.Parameters, actual.Parameters);

        static bool HaveSameParameters(
            ImmutableArray<ParameterSymbol> expected,
            ImmutableArray<ParameterSymbol> actual) =>
            expected.Length == actual.Length &&
            expected.Zip(actual).All(pair =>
                pair.First.Type == pair.Second.Type &&
                pair.First.PassingMode == pair.Second.PassingMode);

        static void AddReadWriteProperty(List<PropertySymbol> properties, string name, TypeSymbol type)
        {
            if (properties.Any(property =>
                    string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) &&
                    property.Accessor == PropertyAccessorKind.Get))
            {
                return;
            }

            properties.Add(new PropertySymbol(
                name,
                PropertyAccessorKind.Get,
                type,
                ImmutableArray<ParameterSymbol>.Empty));
            properties.Add(new PropertySymbol(
                name,
                PropertyAccessorKind.Let,
                type,
                ImmutableArray<ParameterSymbol>.Empty));
        }

        static IEnumerable<string> ReadDesignerControlNames(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            foreach (var line in File.ReadLines(path))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    string.Equals(parts[0], "Begin", StringComparison.OrdinalIgnoreCase))
                {
                    yield return parts[2];
                }
            }
        }
    }

    private static Dictionary<string, ProcedureSymbol> DeclareProjectProcedures(
        IEnumerable<ParsedProjectModule> modules,
        IReadOnlyDictionary<string, UserDefinedTypeModuleResult> userDefinedTypesByPath,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Declarations are collected from every module that produced a tree, including modules that
        // still have syntax errors. The parser is fault-tolerant on purpose, so a procedure whose
        // own header parsed is a real declaration - and skipping the whole module would hide it
        // from every caller. One syntax error in comSummary.bas suppressed ErrMessage and produced
        // 30 "not declared" errors across seven other files.
        foreach (var module in modules.Where(module => module.Item.Kind == VBProjectItemKind.Module))
        {
            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            using var typeScope = UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes));
            foreach (var member in module.SemanticRoot.Members)
            {
                ProcedureSymbol? symbol = member switch
                {
                    SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                    FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                    DeclareDeclarationSyntax declare => Binder.CreateDeclareProcedureSymbol(declare),
                    _ => null
                };

                if (symbol is null)
                {
                    continue;
                }

                if (procedures.TryAdd(symbol.Name, symbol))
                {
                    origins.Add(symbol.Name, module.Item.RelativePath);
                    continue;
                }

                if (procedures[symbol.Name].IsExternal && symbol.IsExternal)
                {
                    continue;
                }

                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0003",
                    $"Procedure '{symbol.Name}' is declared in both '{origins[symbol.Name]}' and '{module.Item.RelativePath}'.",
                    module.FilePath));
            }
        }

        VBIntrinsicSymbols.AddTo(procedures);
        return procedures;
    }

    private static IReadOnlyDictionary<string, ProcedureSymbol> GetProcedureScope(
        ParsedProjectModule module,
        IReadOnlyDictionary<string, ProcedureSymbol> projectProcedures,
        IReadOnlyDictionary<string, UserDefinedTypeModuleResult> userDefinedTypesByPath)
    {
            if (module.Item.Kind is not (VBProjectItemKind.Class or VBProjectItemKind.Form or
                VBProjectItemKind.UserControl))
        {
            return projectProcedures;
        }

        var procedures = new Dictionary<string, ProcedureSymbol>(
            projectProcedures,
            StringComparer.OrdinalIgnoreCase);
        userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
        using var typeScope = UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes));
        foreach (var member in module.SemanticRoot.Members)
        {
            var symbol = member switch
            {
                    SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                    FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
                    DeclareDeclarationSyntax declare => Binder.CreateDeclareProcedureSymbol(declare),
                    _ => null
            };

            if (symbol is not null)
            {
                // A class/form member shadows a project-level declaration or intrinsic with the
                // same name, just as it does in VB6 source lookup.
                procedures[symbol.Name] = symbol;
            }
        }

        if (module.Item.Kind is VBProjectItemKind.Form or VBProjectItemKind.UserControl)
        {
            VBIntrinsicSymbols.AddHostProcedures(procedures);
        }

        return procedures;
    }

    private static IReadOnlyDictionary<string, UserDefinedTypeSymbol> GetTypeScope(
        UserDefinedTypeModuleResult? moduleUserDefinedTypes) =>
        moduleUserDefinedTypes?.Types ??
        ImmutableDictionary.Create<string, UserDefinedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds the diagnostics that only emission cares about: VB6 allows a project without a
    /// <c>Sub Main</c>, an executable does not. Shared with the direct managed backend so both
    /// entry points into emission reject the same projects.
    /// </summary>
    internal static VBProjectCompilationAnalysis ValidateEntryPoint(VBProjectCompilationAnalysis analysis)
    {
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return analysis;
        }

        var projectDiagnostics = analysis.ProjectDiagnostics.ToBuilder();
        var startupObject = analysis.Project.StartupObject;

        if (!string.IsNullOrWhiteSpace(startupObject) &&
            !string.Equals(startupObject, "Sub Main", StringComparison.OrdinalIgnoreCase))
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0004",
                $"Startup object '{startupObject}' is not supported by project emission yet. Only 'Sub Main' is supported.",
                analysis.Project.FilePath));
            return analysis with { ProjectDiagnostics = projectDiagnostics.ToImmutable() };
        }

        var mainCount = analysis.SemanticModel.Procedures.Count(procedure =>
            !procedure.Symbol.IsFunction &&
            string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase));

        if (mainCount != 1)
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0005",
                mainCount == 0
                    ? "Project emission requires a Sub Main entry point."
                    : "Project emission found more than one Sub Main entry point.",
                analysis.Project.FilePath));
        }

        return analysis with { ProjectDiagnostics = projectDiagnostics.ToImmutable() };
    }

    private sealed record ParsedProjectModule(
        VBProjectItem Item,
        string FilePath,
        SourceText Text,
        ParseResult ParseResult,
        CompilationUnitSyntax SemanticRoot);
}

public sealed record VBProjectCompilationUnit(
    VBProjectItem Item,
    string FilePath,
    CompilationAnalysis Analysis);

public sealed record VBProjectCompilationDiagnostic(
    string Code,
    string Message,
    string? FilePath = null,
    int? Line = null)
{
    public override string ToString()
    {
        var location = FilePath is null
            ? string.Empty
            : Line is null
                ? $"{FilePath}: "
                : $"{FilePath}({Line}): ";
        return $"{location}{Code}: {Message}";
    }
}

public sealed record VBProjectCompilationAnalysis(
    VBProject Project,
    ImmutableArray<VBProjectCompilationUnit> Units,
    SemanticModel? SemanticModel,
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<VBProjectCompilationDiagnostic> ProjectDiagnostics)
{
    public ProjectUserDefinedTypeDeclarationResult? UserDefinedTypes { get; init; }

    public bool Success =>
        ProjectDiagnostics.Length == 0 &&
        Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

