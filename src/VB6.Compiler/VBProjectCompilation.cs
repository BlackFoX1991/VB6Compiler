using System.Collections.Immutable;
using VB6.CodeGen.CSharp;
using VB6.Parser;
using VB6.ProjectSystem;
using VB6.Semantics;
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

        foreach (var module in loadResult.Project.Modules)
        {
            var modulePath = module.GetFullPath(loadResult.Project.ProjectDirectory);
            if (!File.Exists(modulePath))
            {
                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0001",
                    $"Project module '{module.RelativePath}' was not found.",
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
                    $"Project module '{module.RelativePath}' could not be read: {exception.Message}",
                    modulePath));
                continue;
            }

            var text = SourceText.From(source, modulePath);
            var parseResult = new ParserType(text).ParseCompilationUnit();
            sourceDiagnostics.AddRange(parseResult.Diagnostics);
            parsedModules.Add(new ParsedProjectModule(module, modulePath, text, parseResult));
        }

        var userDefinedTypes = new ProjectUserDefinedTypeDeclarationBinder().Bind(
            parsedModules
                .Where(module => !module.ParseResult.Diagnostics.Any(
                    diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                .Select(module => new UserDefinedTypeModuleInput(module.Text, module.ParseResult.Root)));
        sourceDiagnostics.AddRange(userDefinedTypes.Diagnostics);
        var userDefinedTypesByPath = userDefinedTypes.Modules.ToDictionary(
            module => module.Module.Text.FilePath ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var procedureSymbols = DeclareProjectProcedures(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        var moduleVariableSymbols = DeclareProjectModuleVariables(
            parsedModules,
            userDefinedTypesByPath,
            projectDiagnostics);
        var units = ImmutableArray.CreateBuilder<VBProjectCompilationUnit>();
        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        var moduleVariables = ImmutableArray.CreateBuilder<BoundModuleVariable>();

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
            SemanticModel preliminaryModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                preliminaryModel = new Binder(module.Text)
                    .BindCompilationUnit(module.ParseResult.Root, procedureSymbols, moduleVariableSymbols);
            }

            var forEachLowering = ForEachArraySyntaxLowerer.Lower(
                module.Text,
                module.ParseResult.Root,
                preliminaryModel);

            SemanticModel semanticModel;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                semanticModel = new Binder(module.Text)
                    .BindCompilationUnit(forEachLowering.Root, procedureSymbols, moduleVariableSymbols);
            }

            var userDefinedTypeValueDiagnostics = moduleUserDefinedTypes is null
                ? ImmutableArray<Diagnostic>.Empty
                : UserDefinedTypeValueGuard.Validate(
                    module.Text,
                    forEachLowering.Root,
                    moduleUserDefinedTypes.Types);
            var variantOperationDiagnostics = VariantOperationGuard.Validate(module.Text, semanticModel);
            sourceDiagnostics.AddRange(forEachLowering.Diagnostics);
            sourceDiagnostics.AddRange(semanticModel.Diagnostics);
            sourceDiagnostics.AddRange(userDefinedTypeValueDiagnostics);
            sourceDiagnostics.AddRange(variantOperationDiagnostics);
            procedures.AddRange(semanticModel.Procedures);
            moduleVariables.AddRange(semanticModel.ModuleVariables);

            var unitDiagnostics = module.ParseResult.Diagnostics
                .AddRange(moduleUserDefinedTypes?.Diagnostics ?? ImmutableArray<Diagnostic>.Empty)
                .AddRange(forEachLowering.Diagnostics)
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
            ModuleVariables = moduleVariables.ToImmutable()
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

    public VBProjectCSharpGenerationResult GenerateCSharp()
    {
        var analysis = ValidateEntryPoint(Analyze());
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new VBProjectCSharpGenerationResult(analysis, null);
        }

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);
        return new VBProjectCSharpGenerationResult(analysis, source);
    }

    public VBProjectManagedApplicationEmitResult EmitManagedApplication(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var generation = GenerateCSharp();
        if (!generation.Success || generation.Source is null)
        {
            return new VBProjectManagedApplicationEmitResult(generation, null, null, null, null);
        }

        var artifacts = ManagedApplicationWriter.Emit(generation.Source, outputPath);
        return new VBProjectManagedApplicationEmitResult(
            generation,
            artifacts.BackendResult,
            artifacts.AssemblyPath,
            artifacts.RuntimeAssemblyPath,
            artifacts.RuntimeConfigPath);
    }

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

        foreach (var module in modules)
        {
            if (module.ParseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                continue;
            }

            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            ImmutableArray<ModuleVariableSymbol> symbols;
            using (UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes)))
            {
                symbols = Binder.CreateModuleVariableSymbols(module.Text, module.ParseResult.Root);
            }

            foreach (var symbol in symbols)
            {
                if (moduleVariables.TryAdd(symbol.Name, symbol))
                {
                    origins.Add(symbol.Name, module.Item.RelativePath);
                    continue;
                }

                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0004",
                    $"Module variable '{symbol.Name}' is declared in both '{origins[symbol.Name]}' and '{module.Item.RelativePath}'.",
                    module.FilePath));
            }
        }

        return moduleVariables;
    }

    private static Dictionary<string, ProcedureSymbol> DeclareProjectProcedures(
        IEnumerable<ParsedProjectModule> modules,
        IReadOnlyDictionary<string, UserDefinedTypeModuleResult> userDefinedTypesByPath,
        ImmutableArray<VBProjectCompilationDiagnostic>.Builder projectDiagnostics)
    {
        var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            if (module.ParseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                continue;
            }

            userDefinedTypesByPath.TryGetValue(module.FilePath, out var moduleUserDefinedTypes);
            using var typeScope = UserDefinedTypeLookupScope.Push(GetTypeScope(moduleUserDefinedTypes));
            foreach (var member in module.ParseResult.Root.Members)
            {
                ProcedureSymbol? symbol = member switch
                {
                    SubDeclarationSyntax sub => Binder.CreateProcedureSymbol(sub),
                    FunctionDeclarationSyntax function => Binder.CreateProcedureSymbol(function),
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

                projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                    "VB6PRJ0003",
                    $"Procedure '{symbol.Name}' is declared in both '{origins[symbol.Name]}' and '{module.Item.RelativePath}'.",
                    module.FilePath));
            }
        }

        return procedures;
    }

    private static IReadOnlyDictionary<string, UserDefinedTypeSymbol> GetTypeScope(
        UserDefinedTypeModuleResult? moduleUserDefinedTypes) =>
        moduleUserDefinedTypes?.Types ??
        ImmutableDictionary.Create<string, UserDefinedTypeSymbol>(StringComparer.OrdinalIgnoreCase);

    private static VBProjectCompilationAnalysis ValidateEntryPoint(VBProjectCompilationAnalysis analysis)
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
        ParseResult ParseResult);
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

public sealed record VBProjectCSharpGenerationResult(
    VBProjectCompilationAnalysis Analysis,
    string? Source)
{
    public bool Success => Analysis.Success && Source is not null;
}

public sealed record VBProjectManagedApplicationEmitResult(
    VBProjectCSharpGenerationResult Generation,
    AssemblyEmitResult? BackendResult,
    string? AssemblyPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => Generation.Success && BackendResult?.Success == true && AssemblyPath is not null;
}
