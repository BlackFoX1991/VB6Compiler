using System.Collections.Immutable;
using System.Text;
using VB6.Parser;
using VB6.ProjectSystem;
using VB6.Runtime;
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
    private readonly VBCompilationOptions? _options;

    private VBProjectCompilation(string projectFilePath, VBCompilationOptions? options)
    {
        _projectFilePath = Path.GetFullPath(projectFilePath);
        _options = options;
    }

    public static VBProjectCompilation Create(
        string projectFilePath,
        VBCompilationOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);
        return new VBProjectCompilation(projectFilePath, options);
    }

    /// <summary>Options that apply to this project and all referenced project compilations.</summary>
    public VBCompilationOptions CompilationOptions =>
        (_options ?? new VBCompilationOptions()).NormalizeForProfile();

    public VBProjectCompilationAnalysis Analyze()
    {
        var activeProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Analyze(activeProjects);
    }

    /// <summary>
    /// Analyzes the project and applies the additional contracts required by managed emission.
    /// </summary>
    public VBProjectCompilationAnalysis AnalyzeForEmission() =>
        ValidateEntryPoint(Analyze());

    private VBProjectCompilationAnalysis Analyze(HashSet<string> activeProjects)
    {
        if (!activeProjects.Add(_projectFilePath))
        {
            throw new InvalidOperationException($"Project reference cycle reached '{_projectFilePath}'.");
        }

        using var activeProjectScope = new ActiveProjectScope(activeProjects, _projectFilePath);
        var loadResult = new VBProjectLoader().Load(_projectFilePath);
        var projectDiagnostics = ImmutableArray.CreateBuilder<VBProjectCompilationDiagnostic>();
        var sourceDiagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var parsedModules = ImmutableArray.CreateBuilder<ParsedProjectModule>();
        var designerDocuments = new Dictionary<string, VBDesignerDocument>(StringComparer.OrdinalIgnoreCase);

        if (_options?.CompatibilityProfile == VBCompatibilityProfile.VB6Sp6 &&
            _options.TargetIs64Bit == true)
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6C0001",
                "The VB6Sp6 compatibility profile supports x86 targets only.",
                _projectFilePath));
        }

        foreach (var diagnostic in loadResult.Diagnostics)
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                loadResult.Project.FilePath,
                diagnostic.Line));
        }

        ValidateProjectReferences(loadResult.Project, projectDiagnostics);

        foreach (var module in loadResult.Project.Items.Where(item => IsSourceModuleKind(item.Kind)))
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
                source = VB6TextFile.ReadAllText(modulePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0002",
                    $"Project source '{module.RelativePath}' could not be read: {exception.Message}",
                    modulePath));
                continue;
            }

            var preprocessed = VBConditionalCompilation.Process(
                source,
                modulePath,
                CreateProjectCompilationOptions(loadResult.Project));
            var preprocessedText = SourceText.From(preprocessed.Source, modulePath);
            foreach (var diagnostic in preprocessed.Diagnostics)
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    diagnostic.Code,
                    diagnostic.Message,
                    diagnostic.FilePath,
                    preprocessedText.GetLinePosition(diagnostic.Span.Start).Line + 1));
            }

            if (IsClassModuleKind(module.Kind))
            {
                var designerResult = VBDesignerParser.Parse(preprocessed.Source, modulePath);
                if (designerResult.Document is not null)
                {
                    designerDocuments[modulePath] = designerResult.Document;
                }

                foreach (var diagnostic in designerResult.Diagnostics)
                {
                    projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        diagnostic.FilePath,
                        diagnostic.Line));
                }
            }

            var normalizedSource = IsClassModuleKind(module.Kind)
                ? VBClassModuleSource.Normalize(preprocessed.Source)
                : preprocessed.Source;
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
        var externalTypeCatalog = VBExternalTypeCatalog.Create(loadResult.Project);
        using var externalTypeScope = UserDefinedTypeLookupScope.PushAliases(
            externalTypeCatalog.Aliases);
        var referencedClassTypes = LoadReferencedClassTypes(
            loadResult.Project,
            activeProjects,
            projectDiagnostics);
        var enumSymbols = VBEnumSymbols.Bind(parsedModules.Select(module => module.SemanticRoot));
        using var enumTypeScope = UserDefinedTypeLookupScope.PushAliases(enumSymbols.TypeAliases);
        var qualifiedEnumMembers = new Dictionary<string, long>(
            enumSymbols.QualifiedMembers,
            StringComparer.OrdinalIgnoreCase);
        foreach (var member in externalTypeCatalog.QualifiedEnumMembers)
        {
            qualifiedEnumMembers.TryAdd(member.Key, member.Value);
        }
        // Ein Modulname qualifiziert seine eigenen Member. Nur Standardmodule kommen infrage:
        // bei einer Klasse benennt derselbe Bezeichner den Typ, nicht einen Bereich.
        var moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parsedModule in parsedModules.Where(item => !IsClassModuleKind(item.Item.Kind)))
        {
            var moduleName = string.IsNullOrWhiteSpace(parsedModule.Item.Name)
                ? Path.GetFileNameWithoutExtension(parsedModule.FilePath)
                : parsedModule.Item.Name!;
            moduleNames.Add(moduleName);
        }

        var classTypeAliases = classTypes.ToDictionary(
            entry => entry.Key,
            entry => (TypeSymbol)entry.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in referencedClassTypes)
        {
            classTypeAliases.TryAdd(entry.Key, entry.Value);
        }
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
            designerDocuments,
            projectDiagnostics);

        var procedureSymbols = DeclareProjectProcedures(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        var moduleVariableSymbols = DeclareProjectModuleVariables(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        var modulePropertySymbols = DeclareProjectModuleProperties(parsedModules, projectDiagnostics);
        foreach (var item in loadResult.Project.Items.Where(item => IsHostModuleKind(item.Kind)))
        {
            var name = string.IsNullOrWhiteSpace(item.Name)
                ? Path.GetFileNameWithoutExtension(item.RelativePath)
                : item.Name!;
            if (classTypes.TryGetValue(name, out var objectType))
            {
                // A Form carries VB_PredeclaredId, so its own name is a default instance that VB6
                // creates on first use -- "frmMain.Show" is how nearly every VB6 program opens its
                // second window. Without As New the global stayed Nothing, and the call died with
                // "Object member access requires a non-empty object reference" in a WinForms
                // exception dialog. A UserControl has no default instance in VB6 and gets none here.
                moduleVariableSymbols.TryAdd(
                    name,
                    new ModuleVariableSymbol(name, objectType)
                    {
                        IsAsNew = item.Kind == VBProjectItemKind.Form
                    });
            }
        }

        // A class whose .cls declares VB_PredeclaredId owns a global instance named after itself,
        // exactly like a form. VB6 creates it on first use, which is what As New already means
        // here, so the default instance is an ordinary As New global rather than a second
        // mechanism.
        var predeclaredClassNames = parsedModules
            .Where(module => module.Item.Kind == VBProjectItemKind.Class &&
                             HasPredeclaredIdAttribute(module.SemanticRoot))
            .Select(module => string.IsNullOrWhiteSpace(module.Item.Name)
                ? Path.GetFileNameWithoutExtension(module.Item.RelativePath)
                : module.Item.Name!)
            .Where(classTypes.ContainsKey)
            .ToImmutableArray();
        foreach (var name in predeclaredClassNames)
        {
            moduleVariableSymbols.TryAdd(
                name,
                new ModuleVariableSymbol(name, classTypes[name]) { IsAsNew = true });
        }
        var visibleEnumConstants = enumSymbols.AddMemberSymbols(moduleVariableSymbols);
        var visibleExternalConstants = externalTypeCatalog.AddMemberSymbols(moduleVariableSymbols);
        var visibleBuiltInConstants = VBBuiltInConstants.AddTo(moduleVariableSymbols);
        var hostModuleVariables = loadResult.Project.Items
            .Where(item => IsHostModuleKind(item.Kind))
            .Select(item => string.IsNullOrWhiteSpace(item.Name)
                ? Path.GetFileNameWithoutExtension(item.RelativePath)
                : item.Name!)
            .Where(moduleVariableSymbols.ContainsKey)
            .Concat(predeclaredClassNames.Where(moduleVariableSymbols.ContainsKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new BoundModuleVariable(
                moduleVariableSymbols[name],
                Initializer: null,
                IsConstant: false))
            .ToImmutableArray();
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
            if (!IsClassModuleKind(module.Item.Kind))
            {
                containingClass = null;
            }
            var moduleVariablesForBinding = CreateModuleVariableScope(
                module,
                moduleVariableSymbols,
                designerDocuments);
            SemanticModel preliminaryModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                preliminaryModel = new Binder(module.Text, qualifiedEnumMembers, moduleNames)
                    .BindCompilationUnit(
                        module.SemanticRoot,
                        availableProcedures,
                        moduleVariablesForBinding,
                        containingClass,
                        modulePropertySymbols);
            }

            var forEachRoot = ForEachArraySyntaxLowerer.Lower(module.SemanticRoot, preliminaryModel);

            SemanticModel semanticModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                semanticModel = new Binder(module.Text, qualifiedEnumMembers, moduleNames)
                    .BindCompilationUnit(
                        forEachRoot,
                        availableProcedures,
                        moduleVariablesForBinding,
                        containingClass,
                        modulePropertySymbols);
            }
            if (containingClass is not null && HasDesignerSurface(module.Item.Kind))
            {
                var instanceVariables = semanticModel.InstanceVariables.ToBuilder();
                foreach (var control in ReadDesignerControls(module.FilePath, designerDocuments))
                {
                    if (!moduleVariablesForBinding.TryGetValue(control.Name, out var symbol) ||
                        instanceVariables.Any(variable =>
                            string.Equals(variable.Symbol.Name, symbol.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    instanceVariables.Add(new BoundModuleVariable(symbol, Initializer: null, IsConstant: false)
                    {
                        IsDesignerControl = true,
                        DesignerParentName = control.ParentName,
                        DesignerTypeName = control.TypeName,
                        DesignerInitializers = control.Initializers,
                        ArrayDimensions = control.ArrayDimensions,
                        DesignerArrayIndices = control.ArrayIndices
                    });
                }

                var designerInitializers = designerDocuments.TryGetValue(module.FilePath, out var designerDocument)
                    ? ReadDesignerInitializers(designerDocument.Root)
                    : ImmutableArray<DesignerPropertyInitializer>.Empty;

                semanticModel = semanticModel with
                {
                    InstanceVariables = instanceVariables.ToImmutable(),
                    DesignerInitializers = designerInitializers
                };
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
            staticVariables.AddRange(semanticModel.StaticVariables);
            if (module.Item.Kind == VBProjectItemKind.Module)
            {
                moduleVariables.AddRange(semanticModel.ModuleVariables);
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
                .AddRange(hostModuleVariables)
                .AddRange(visibleEnumConstants)
                .AddRange(visibleExternalConstants)
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
            UserDefinedTypes = userDefinedTypes,
            Designers = designerDocuments.Values.ToImmutableArray()
        };
    }

    private static Dictionary<string, ModuleVariableSymbol> CreateModuleVariableScope(
        ParsedProjectModule module,
        IReadOnlyDictionary<string, ModuleVariableSymbol> projectVariables,
        IReadOnlyDictionary<string, VBDesignerDocument> designerDocuments)
    {
        var variables = new Dictionary<string, ModuleVariableSymbol>(
            projectVariables,
            StringComparer.OrdinalIgnoreCase);
        if (!HasDesignerSurface(module.Item.Kind))
        {
            return variables;
        }

        foreach (var control in ReadDesignerControls(module.FilePath, designerDocuments))
        {
            // A designer control is a member of its containing form/UserControl, not a project
            // global. Keeping it module-local also lets a public Enum member retain its name in
            // ordinary modules (for example frmMain.Code versus ENUM_SECTION_TYPE.Code).
            variables[control.Name] = new ModuleVariableSymbol(control.Name, control.Type);
        }

        return variables;
    }

    private static void ValidateProjectReferences(
        VBProject project,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in project.References.Where(reference =>
                     reference.Metadata.Kind == VBProjectReferenceKind.Project))
        {
            var referencePath = reference.Metadata.GetFullPath(project.ProjectDirectory);
            if (referencePath is null)
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0013",
                    $"Project reference '{reference.RawValue}' does not specify a project file.",
                    project.FilePath));
                continue;
            }

            if (!seen.Add(referencePath))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0014",
                    $"Project reference '{referencePath}' occurs more than once.",
                    project.FilePath));
                continue;
            }

            if (string.Equals(referencePath, project.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0015",
                    "A project cannot reference itself.",
                    project.FilePath));
            }
            else if (!File.Exists(referencePath))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0016",
                    $"Referenced project '{reference.Metadata.FilePath}' was not found.",
                    referencePath));
            }
        }
    }

    private VBCompilationOptions CreateProjectCompilationOptions(VBProject project)
    {
        var projectConstants = ParseProjectConditionalConstants(project.ConditionalCompilation);
        return (_options ?? new VBCompilationOptions()).NormalizeForProfile() with
        {
            DefinedConstants = projectConstants
        };
    }

    private static IReadOnlyDictionary<string, string> ParseProjectConditionalConstants(
        string? value)
    {
        var constants = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value))
        {
            return constants;
        }

        foreach (var declaration in value.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = declaration.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var name = declaration[..equals].Trim();
            var expression = declaration[(equals + 1)..].Trim();
            if (name.Length > 0 && expression.Length > 0)
            {
                constants[name] = expression;
            }
        }

        return constants;
    }

    private Dictionary<string, ClassTypeSymbol> LoadReferencedClassTypes(
        VBProject project,
        HashSet<string> activeProjects,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var classTypes = new Dictionary<string, ClassTypeSymbol>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in project.References.Where(reference =>
                     reference.Metadata.Kind == VBProjectReferenceKind.Project))
        {
            var referencePath = reference.Metadata.GetFullPath(project.ProjectDirectory);
            if (referencePath is null || !File.Exists(referencePath))
            {
                continue;
            }

            if (activeProjects.Contains(referencePath))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0017",
                    $"Project reference cycle detected through '{referencePath}'.",
                    project.FilePath));
                continue;
            }

            var referencedAnalysis = new VBProjectCompilation(referencePath, _options).Analyze(activeProjects);
            if (!referencedAnalysis.Success || referencedAnalysis.SemanticModel is null)
            {
                var hasCycle = referencedAnalysis.ProjectDiagnostics.Any(diagnostic =>
                    diagnostic.Code == "VB6PRJ0017");
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    hasCycle ? "VB6PRJ0017" : "VB6PRJ0018",
                    hasCycle
                        ? $"Project reference cycle detected through '{referencePath}'."
                        : $"Referenced project '{referencePath}' has compilation errors and cannot provide class symbols.",
                    referencePath));
                continue;
            }

            var projectName = referencedAnalysis.Project.Name;
            if (string.IsNullOrWhiteSpace(projectName))
            {
                projectName = Path.GetFileNameWithoutExtension(referencePath);
            }

            foreach (var classType in referencedAnalysis.SemanticModel.ClassTypes)
            {
                var externalClassType = classType with
                {
                    ExternalAssemblyName = projectName
                };
                classTypes.TryAdd(classType.Name, externalClassType);
                classTypes.TryAdd($"{projectName}.{classType.Name}", externalClassType);
                if (!string.IsNullOrWhiteSpace(reference.Metadata.DisplayName))
                {
                    classTypes.TryAdd($"{reference.Metadata.DisplayName}.{classType.Name}", externalClassType);
                }
            }
        }

        return classTypes;
    }

    private static IEnumerable<DesignerControl> ReadDesignerControls(
        string path,
        IReadOnlyDictionary<string, VBDesignerDocument> designerDocuments)
    {
        if (!designerDocuments.TryGetValue(path, out var document))
        {
            yield break;
        }

        var controls = new Dictionary<
            string,
            (TypeSymbol Type, string TypeName, bool IsArray, int? ArrayLower, int? ArrayUpper,
                string? ParentName, ImmutableArray<DesignerPropertyInitializer> Initializers,
                ImmutableArray<int> ArrayIndices)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var child in document.Root.Children)
        {
            Visit(child, null);
        }

        foreach (var control in controls)
        {
            yield return new DesignerControl(
                control.Key,
                control.Value.IsArray
                    ? new ArrayTypeSymbol(control.Value.Type)
                    : control.Value.Type,
                control.Value.TypeName,
                control.Value.ParentName,
                control.Value.Initializers,
                CreateArrayDimensions(control.Value),
                control.Value.ArrayIndices);
        }

        void Visit(VBDesignerNode node, string? parentName)
        {
            var typeName = node.TypeName.StartsWith("VB.", StringComparison.OrdinalIgnoreCase)
                ? node.TypeName[3..]
                : node.TypeName;
            var type = TypeSymbol.Lookup(typeName) ?? VBStandardTypes.Control;
            var arrayIndex = node.ArrayIndex;
            if (controls.TryGetValue(node.Name, out var existing))
            {
                controls[node.Name] = (
                    existing.Type,
                    existing.TypeName,
                    true,
                    MinArrayBound(existing.ArrayLower, arrayIndex),
                    MaxArrayBound(existing.ArrayUpper, arrayIndex),
                    existing.ParentName,
                    existing.Initializers,
                    AddArrayIndex(existing.ArrayIndices, arrayIndex));
            }
            else
            {
                controls.Add(
                    node.Name,
                    (type, typeName, node.IsControlArray, arrayIndex, arrayIndex, parentName,
                        ReadDesignerInitializers(node),
                        arrayIndex is int index ? ImmutableArray.Create(index) : ImmutableArray<int>.Empty));
            }

            var currentName = parentName is null
                ? node.Name
                : parentName + "." + node.Name;
            foreach (var child in node.Children)
            {
                Visit(child, currentName);
            }
        }

        static ImmutableArray<BoundArrayDimension> CreateArrayDimensions(
            (TypeSymbol Type, string TypeName, bool IsArray, int? ArrayLower, int? ArrayUpper,
                string? ParentName, ImmutableArray<DesignerPropertyInitializer> Initializers,
                ImmutableArray<int> ArrayIndices) control)
        {
            if (!control.IsArray)
            {
                return ImmutableArray<BoundArrayDimension>.Empty;
            }

            var lower = control.ArrayLower ?? 0;
            var upper = control.ArrayUpper ?? lower;
            return ImmutableArray.Create(
                new BoundArrayDimension(
                    new BoundLiteralExpression((long)lower, TypeSymbol.Long),
                    new BoundLiteralExpression((long)upper, TypeSymbol.Long)));
        }

        static int? MinArrayBound(int? current, int? candidate) => current is null
            ? candidate
            : candidate is null
                ? current
                : Math.Min(current.Value, candidate.Value);

        static int? MaxArrayBound(int? current, int? candidate) => current is null
            ? candidate
            : candidate is null
                ? current
                : Math.Max(current.Value, candidate.Value);

        static ImmutableArray<int> AddArrayIndex(ImmutableArray<int> current, int? candidate)
        {
            if (candidate is not int index || current.Contains(index))
            {
                return current;
            }

            return current.Add(index);
        }

    }

    private static ImmutableArray<DesignerPropertyInitializer> ReadDesignerInitializers(VBDesignerNode node)
    {
        var initializers = ImmutableArray.CreateBuilder<DesignerPropertyInitializer>();
        foreach (var property in node.Properties)
        {
            if (!IsSupportedDesignerProperty(property.Name))
            {
                continue;
            }

            object? value = property.ResourceData is not null &&
                            property.Name.Equals("TextRTF", StringComparison.OrdinalIgnoreCase)
                ? Encoding.ASCII.GetString(property.ResourceData)
                : property.ResourceData is not null &&
                  (property.Name.Equals("Picture", StringComparison.OrdinalIgnoreCase) ||
                   property.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase) ||
                   IsImageListDesignerProperty(property.Name))
                    ? "__VB6_FRX_BASE64__" + Convert.ToBase64String(property.ResourceData)
                : property.ResourcePath is null
                    ? property.Value
                    : null;
            if (value is string or bool or long or int)
            {
                initializers.Add(new DesignerPropertyInitializer(property.Name, value));
            }
        }

        if (node.TypeName.Equals("VB.MDIForm", StringComparison.OrdinalIgnoreCase) ||
            node.TypeName.Equals("MDIForm", StringComparison.OrdinalIgnoreCase))
        {
            initializers.Add(new DesignerPropertyInitializer("MDIForm", true));
        }

        return initializers.ToImmutable();
    }

    private static bool IsSupportedDesignerProperty(string name) =>
        name.Equals("Caption", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Checked", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Shortcut", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("TextRTF", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Picture", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Icon", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Visible", StringComparison.OrdinalIgnoreCase) ||
        // Der Korpus schreibt beide für fast jedes Control; ohne sie stand die Tabulatorfolge
        // eines übersetzten Formulars in der Reihenfolge der Erzeugung statt in der des Designers.
        name.Equals("TabIndex", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("TabStop", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Left", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Top", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Width", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Height", StringComparison.OrdinalIgnoreCase) ||
        // A VB6 form never writes Width/Height at form level - the designer stores its size as
        // the client area instead. Without these two every emitted form kept the WinForms
        // default size no matter what the .frm said.
        name.Equals("ClientWidth", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ClientHeight", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BackColor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ForeColor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelStart", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelLength", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelText", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelColor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelBold", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelItalic", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SelUnderline", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("RightMargin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("HideSelection", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Interval", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BorderStyle", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BorderColor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BorderWidth", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Appearance", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("AutoRedraw", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BackStyle", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FillStyle", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FillColor", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Shape", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("X1", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Y1", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("X2", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Y2", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MousePointer", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ScaleMode", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Tag", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ToolTipText", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ControlBox", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MaxButton", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MinButton", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ShowInTaskbar", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("StartUpPosition", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("WindowState", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MDIChild", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ImageWidth", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ImageHeight", StringComparison.OrdinalIgnoreCase) ||
        IsImageListDesignerProperty(name);

    private static bool IsImageListDesignerProperty(string name) =>
        VBDesignerPropertyPath.TryReadListImageEntry(name, out _, out _);

    /// <summary>Lowers every module of the project to the IR the managed backend emits from.</summary>
    public VBProjectLoweringResult Lower() => DirectManagedCompilation.Lower(this);

    /// <summary>Emits an executable assembly, its debug information and its runtime files.</summary>
    public VBProjectManagedApplicationEmitResult EmitManagedApplication(
        string outputPath,
        VB6.Emit.Managed.ManagedEmitOptions? options = null) =>
        DirectManagedCompilation.EmitManaged(this, outputPath, options);
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

            foreach (var symbol in symbols.Where(symbol => symbol.IsPublic))
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
        foreach (var module in modules.Where(module => IsClassModuleKind(module.Item.Kind)))
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

        foreach (var item in projectItems.Where(item => IsClassModuleKind(item.Kind)))
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
        IReadOnlyDictionary<string, VBDesignerDocument> designerDocuments,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var interfaceRelations = new List<(ClassTypeSymbol Implementor, ImplementsStatementSyntax Declaration, string FilePath)>();
        foreach (var module in modules.Where(module => IsClassModuleKind(module.Item.Kind)))
        {
            var name = string.IsNullOrWhiteSpace(module.Item.Name)
                ? Path.GetFileNameWithoutExtension(module.FilePath)
                : module.Item.Name!;
            if (!classTypes.TryGetValue(name, out var classType))
            {
                continue;
            }

            // A duplicate class name has already been diagnosed while declaring class types. The
            // first module owns the shared symbol; do not attempt to define that symbol again for
            // the duplicate module, which would otherwise throw instead of returning PRJ0008.
            if (!string.Equals(classType.SourcePath, module.FilePath, StringComparison.OrdinalIgnoreCase))
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
            // Eine WithEvents-Variable ist kein einfacher Speicher: Ihre Zuweisung verdrahtet
            // die Ereignishandler neu. Sie bekommt deshalb keinen Set-Accessor -- sonst
            // bindet schon "Set held = New Src" innerhalb der Klasse an die Property und
            // umgeht die Verdrahtung.
            var withEventsNames = module.SemanticRoot.Members
                .OfType<ModuleVariableDeclarationSyntax>()
                .Where(declaration => declaration.WithEventsKeyword is not null)
                .SelectMany(declaration => declaration.Declarators)
                .Select(declarator => declarator.Identifier.Text)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var variable in Binder.CreateModuleVariableSymbols(module.Text, module.SemanticRoot))
            {
                AddReadWriteProperty(
                    properties,
                    variable.Name,
                    variable.Type,
                    isFieldBacked: true,
                    allowObjectAssignment: !withEventsNames.Contains(variable.Name),
                    isPublic: variable.IsPublic);
            }

            if (HasDesignerSurface(module.Item.Kind))
            {
                // A PropertyPage and a UserDocument are containers of the UserControl shape: they
                // carry designer controls and the same host properties, but unlike a form they
                // have no global instance of their own.
                var hostType = module.Item.Kind == VBProjectItemKind.Form
                    ? VBStandardTypes.Form
                    : VBStandardTypes.UserControl;
                foreach (var property in hostType.Properties)
                {
                    AddPropertyIfMissing(properties, property);
                }

                foreach (var control in ReadDesignerControls(module.FilePath, designerDocuments))
                {
                    AddReadWriteProperty(properties, control.Name, control.Type, isLateBound: true);
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

            // A .cls without these attributes is not a VB6-authored class module -- a hand-written
            // file or a form -- and keeps the permissive default.
            var exposed = TryReadBooleanAttribute(module.SemanticRoot, "VB_Exposed");
            var creatable = TryReadBooleanAttribute(module.SemanticRoot, "VB_Creatable");
            if (exposed is not null || creatable is not null)
            {
                classType.SetComInstancing(exposed ?? true, creatable ?? true);
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

        static bool? TryReadBooleanAttribute(CompilationUnitSyntax root, string attributeName)
        {
            foreach (var attribute in root.Members.OfType<AttributeSyntax>())
            {
                var tokens = attribute.Tokens;
                if (tokens.Length >= 3 &&
                    string.Equals(tokens[0].Text, attributeName, StringComparison.OrdinalIgnoreCase) &&
                    tokens[1].Kind == SyntaxKind.EqualsToken)
                {
                    return string.Equals(tokens[2].Text, "True", StringComparison.OrdinalIgnoreCase);
                }
            }

            return null;
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

        static void AddReadWriteProperty(
            List<PropertySymbol> properties,
            string name,
            TypeSymbol type,
            bool isLateBound = false,
            bool isFieldBacked = false,
            bool allowObjectAssignment = true,
            bool isPublic = true)
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
                ImmutableArray<ParameterSymbol>.Empty)
            {
                IsLateBound = isLateBound,
                IsFieldBacked = isFieldBacked,
                IsPublic = isPublic
            });
            properties.Add(new PropertySymbol(
                name,
                PropertyAccessorKind.Let,
                type,
                ImmutableArray<ParameterSymbol>.Empty)
            {
                IsLateBound = isLateBound,
                IsFieldBacked = isFieldBacked,
                IsPublic = isPublic
            });

            // Ein Feld, das eine Objektreferenz tragen kann, wird in VB6 mit Set zugewiesen.
            // Ohne diesen Accessor meldet der Binder VB6S0064, obwohl echter Speicher da ist.
            // Ein Variant zaehlt dazu: Set v = obj ist gueltig.
            if (allowObjectAssignment && (type is ClassTypeSymbol || type == TypeSymbol.Variant))
            {
                properties.Add(new PropertySymbol(
                    name,
                    PropertyAccessorKind.Set,
                    type,
                    ImmutableArray<ParameterSymbol>.Empty)
                {
                    IsLateBound = isLateBound,
                    IsFieldBacked = isFieldBacked,
                    IsPublic = isPublic
                });
            }
        }

        static void AddPropertyIfMissing(List<PropertySymbol> properties, PropertySymbol property)
        {
            if (properties.Any(existing =>
                    string.Equals(existing.Name, property.Name, StringComparison.OrdinalIgnoreCase) &&
                    existing.Accessor == property.Accessor))
            {
                return;
            }

            properties.Add(property);
        }

    }

    /// <summary>
    /// Collects the public module-level property accessors of the whole project.
    /// </summary>
    /// <remarks>
    /// A <c>Public Property Get</c> in a standard module is project-wide, exactly like a
    /// <c>Public Function</c>. It cannot ride along in the procedure table because Get, Let and Set
    /// share one name. Building the accessors once here also gives them a single identity: the
    /// symbol a call in another module resolves to is the one the declaring body is bound to.
    /// </remarks>
    private static Dictionary<string, ModulePropertySymbol> DeclareProjectModuleProperties(
        IEnumerable<ParsedProjectModule> modules,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var properties = new Dictionary<string, ModulePropertySymbol>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var reportedDuplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules.Where(module => module.Item.Kind == VBProjectItemKind.Module))
        {
            // Get, Let and Set form one module-level property name. A second public declaration
            // in another module would otherwise be silently merged into the first property's
            // symbol, so it must follow the same project-wide uniqueness rule as public methods.
            foreach (var property in module.SemanticRoot.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (string.Equals(property.VisibilityKeyword?.Text, "Private", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (origins.TryGetValue(property.Identifier.Text, out var origin) &&
                    !string.Equals(origin, module.Item.RelativePath, StringComparison.OrdinalIgnoreCase) &&
                    reportedDuplicates.Add(property.Identifier.Text))
                {
                    projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                        "VB6PRJ0003",
                        $"Property '{property.Identifier.Text}' is declared in both '{origin}' and '{module.Item.RelativePath}'.",
                        module.FilePath));
                }
                else
                {
                    origins.TryAdd(property.Identifier.Text, module.Item.RelativePath);
                }
            }

            Binder.AddModuleProperties(module.SemanticRoot, properties);
        }

        return properties;
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

                // Private module procedures are declared in the module's own scope below. Only
                // exported procedures participate in the project-wide lookup table; otherwise a
                // Private helper in one standard module would be callable from every other one.
                if (!symbol.IsPublic)
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
        var procedures = projectProcedures
            .Where(entry => entry.Value.IsPublic)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value,
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
                // A module member shadows a project-level declaration or intrinsic with the same
                // name. This also makes Private procedures visible only in their own module.
                // Standard-module Public symbols are already shared through the project table;
                // retaining that instance keeps bound call targets identical across modules.
                if (!symbol.IsPublic ||
                    IsClassModuleKind(module.Item.Kind) ||
                    !procedures.ContainsKey(symbol.Name))
                {
                    procedures[symbol.Name] = symbol;
                }
            }
        }

        if (HasDesignerSurface(module.Item.Kind))
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

        if (IsLibraryProjectType(analysis.Project.ProjectType))
        {
            return analysis;
        }

        var projectDiagnostics = analysis.ProjectDiagnostics.ToBuilder();
        var startupObject = analysis.Project.StartupObject;
        var hasFormStartup = !string.IsNullOrWhiteSpace(startupObject) &&
            !string.Equals(startupObject, "Sub Main", StringComparison.OrdinalIgnoreCase) &&
            TryGetStartupForm(analysis, out _);

        if (!string.IsNullOrWhiteSpace(startupObject) &&
            !string.Equals(startupObject, "Sub Main", StringComparison.OrdinalIgnoreCase) &&
            !hasFormStartup)
        {
            projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                "VB6PRJ0004",
                $"Startup object '{startupObject}' does not name a supported project Form. Project emission supports 'Sub Main' or a Form startup object.",
                analysis.Project.FilePath));
            return analysis with { ProjectDiagnostics = projectDiagnostics.ToImmutable() };
        }

        if (hasFormStartup)
        {
            return analysis;
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

    internal static bool TryGetStartupForm(
        VBProjectCompilationAnalysis analysis,
        out ClassTypeSymbol? startupForm)
    {
        startupForm = null;
        var startupObject = analysis.Project.StartupObject;
        if (string.IsNullOrWhiteSpace(startupObject) ||
            string.Equals(startupObject, "Sub Main", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var item in analysis.Project.Items.Where(item => item.Kind == VBProjectItemKind.Form))
        {
            var itemName = string.IsNullOrWhiteSpace(item.Name)
                ? Path.GetFileNameWithoutExtension(item.RelativePath)
                : item.Name!;
            if (!string.Equals(itemName, startupObject, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            startupForm = analysis.Units
                .Select(unit => unit.Analysis.SemanticModel?.ContainingClass)
                .FirstOrDefault(type => type is not null &&
                    string.Equals(type.Name, itemName, StringComparison.OrdinalIgnoreCase));
            return startupForm is not null;
        }

        return false;
    }

    /// <summary>
    /// <c>Attribute VB_PredeclaredId = True</c> in a .cls header. VB6 writes it for a class whose
    /// Instancing gives it a global default instance.
    /// </summary>
    private static bool HasPredeclaredIdAttribute(CompilationUnitSyntax root)
    {
        foreach (var attribute in root.Members.OfType<AttributeSyntax>())
        {
            var tokens = attribute.Tokens;
            if (tokens.Length >= 3 &&
                string.Equals(tokens[0].Text, "VB_PredeclaredId", StringComparison.OrdinalIgnoreCase) &&
                tokens[1].Kind == SyntaxKind.EqualsToken &&
                string.Equals(tokens[2].Text, "True", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The project kinds VB6 builds as an out-of-process COM server. They are libraries by
    /// contract but executables by form, because COM starts them as their own process.
    /// </summary>
    internal static bool IsLocalServerProjectType(string? projectType) =>
        projectType?.Trim().ToUpperInvariant() is "OLEEXE" or "ACTIVEX EXE";

    internal static bool IsLibraryProjectType(string? projectType) =>
        projectType?.Trim().ToUpperInvariant() is
            "OLEDLL" or
            "OLEEXE" or
            "CONTROL" or
            "DLL" or
            "ACTIVEX DLL" or
            "ACTIVEX EXE" or
            "ACTIVEX CONTROL";

    private static bool IsSourceModuleKind(VBProjectItemKind kind) =>
        kind is VBProjectItemKind.Module or
            VBProjectItemKind.Class or
            VBProjectItemKind.Form or
            VBProjectItemKind.UserControl or
            VBProjectItemKind.PropertyPage or
            VBProjectItemKind.UserDocument or
            VBProjectItemKind.Designer;

    private static bool IsClassModuleKind(VBProjectItemKind kind) =>
        kind is VBProjectItemKind.Class or
            VBProjectItemKind.Form or
            VBProjectItemKind.UserControl or
            VBProjectItemKind.PropertyPage or
            VBProjectItemKind.UserDocument or
            VBProjectItemKind.Designer;

    /// <summary>
    /// The kinds that own a global instance named after themselves. VB6 addresses a form by its
    /// name without creating one; a PropertyPage or UserDocument is never reachable that way.
    /// </summary>
    private static bool IsHostModuleKind(VBProjectItemKind kind) =>
        kind is VBProjectItemKind.Form or VBProjectItemKind.UserControl;

    /// <summary>
    /// The kinds that are drawn in a designer and therefore carry designer controls and the host
    /// property surface. This is the wider set: a PropertyPage has an OK button on it just like a
    /// form does, and its code has to see it.
    /// </summary>
    private static bool HasDesignerSurface(VBProjectItemKind kind) =>
        kind is VBProjectItemKind.Form or
            VBProjectItemKind.UserControl or
            VBProjectItemKind.PropertyPage or
            VBProjectItemKind.UserDocument;

    private sealed record ParsedProjectModule(
        VBProjectItem Item,
        string FilePath,
        SourceText Text,
        ParseResult ParseResult,
        CompilationUnitSyntax SemanticRoot);

    private sealed record DesignerControl(
        string Name,
        TypeSymbol Type,
        string TypeName,
        string? ParentName,
        ImmutableArray<DesignerPropertyInitializer> Initializers,
        ImmutableArray<BoundArrayDimension> ArrayDimensions,
        ImmutableArray<int> ArrayIndices);

    private sealed class ActiveProjectScope : IDisposable
    {
        private readonly HashSet<string> _activeProjects;
        private readonly string _projectFilePath;
        private bool _disposed;

        public ActiveProjectScope(HashSet<string> activeProjects, string projectFilePath)
        {
            _activeProjects = activeProjects;
            _projectFilePath = projectFilePath;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _activeProjects.Remove(_projectFilePath);
            _disposed = true;
        }
    }
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

    public ImmutableArray<VBDesignerDocument> Designers { get; init; } = ImmutableArray<VBDesignerDocument>.Empty;

    public bool Success =>
        ProjectDiagnostics.Length == 0 &&
        Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

