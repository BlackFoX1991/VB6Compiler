using System.Collections.Immutable;
using VB6.CodeGen.CSharp;
using VB6.Parser;
using VB6.Semantics;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Compiler;

public sealed class VBCompilation
{
    private VBCompilation(SourceText text)
    {
        Text = text;
    }

    public SourceText Text { get; }

    public static VBCompilation Create(string source, string? filePath = null) =>
        new(SourceText.From(source, filePath));

    public CompilationAnalysis Analyze()
    {
        var parseResult = new ParserType(Text).ParseCompilationUnit();
        if (parseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new CompilationAnalysis(
                parseResult,
                null,
                parseResult.Diagnostics);
        }

        var fileIoDiagnostics = FileIoSyntaxGuard.Validate(Text, parseResult.Root);
        var typeOfDiagnostics = TypeOfSyntaxGuard.Validate(Text, parseResult.Root);
        var implicitVariantRoot = ImplicitVariantSyntaxLowerer.Lower(parseResult.Root);
        var enumSymbols = VBEnumSymbols.Bind(new[] { implicitVariantRoot });
        using var enumTypeScope = UserDefinedTypeLookupScope.PushAliases(enumSymbols.TypeAliases);

        var userDefinedTypes = new UserDefinedTypeDeclarationBinder(Text).Bind(implicitVariantRoot);
        Dictionary<string, ProcedureSymbol> procedureSymbols;
        Dictionary<string, ModuleVariableSymbol> moduleVariableSymbols;
        ImmutableArray<BoundModuleVariable> visibleEnumConstants;
        ImmutableArray<BoundModuleVariable> visibleBuiltInConstants;
        SemanticModel preliminaryModel;
        using (UserDefinedTypeLookupScope.Push(userDefinedTypes.Types))
        {
            procedureSymbols = VBIntrinsicSymbols.CreateProcedureTable(implicitVariantRoot);
            moduleVariableSymbols = Binder.CreateModuleVariableSymbols(Text, implicitVariantRoot)
                .ToDictionary(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase);
            visibleEnumConstants = enumSymbols.AddMemberSymbols(moduleVariableSymbols);
            visibleBuiltInConstants = VBBuiltInConstants.AddTo(moduleVariableSymbols);
            preliminaryModel = new Binder(Text)
                .BindCompilationUnit(implicitVariantRoot, procedureSymbols, moduleVariableSymbols);
        }

        var forEachLowering = ForEachArraySyntaxLowerer.Lower(
            Text,
            implicitVariantRoot,
            preliminaryModel);

        SemanticModel semanticModel;
        ImmutableArray<Diagnostic> duplicateProcedureDiagnostics;
        using (UserDefinedTypeLookupScope.Push(userDefinedTypes.Types))
        {
            semanticModel = new Binder(Text)
                .BindCompilationUnit(forEachLowering.Root, procedureSymbols, moduleVariableSymbols);
            duplicateProcedureDiagnostics = new Binder(Text)
                .BindCompilationUnit(forEachLowering.Root)
                .Diagnostics
                .Where(diagnostic => diagnostic.Code == "VB6S0004")
                .ToImmutableArray();
        }
        semanticModel = semanticModel with
        {
            Diagnostics = semanticModel.Diagnostics.AddRange(duplicateProcedureDiagnostics),
            ModuleVariables = semanticModel.ModuleVariables
                .AddRange(visibleEnumConstants)
                .AddRange(visibleBuiltInConstants)
        };
        semanticModel = VariantMultiplyLowerer.Lower(semanticModel);

        var userDefinedTypeValueDiagnostics = UserDefinedTypeValueGuard.Validate(
            Text,
            forEachLowering.Root,
            userDefinedTypes.Types);
        var variantOperationDiagnostics = VariantOperationGuard.Validate(Text, semanticModel);
        var diagnostics = parseResult.Diagnostics
            .AddRange(userDefinedTypes.Diagnostics)
            .AddRange(forEachLowering.Diagnostics)
            .AddRange(semanticModel.Diagnostics)
            .AddRange(userDefinedTypeValueDiagnostics)
            .AddRange(variantOperationDiagnostics)
            .AddRange(fileIoDiagnostics)
            .AddRange(typeOfDiagnostics);

        return new CompilationAnalysis(parseResult, semanticModel, diagnostics)
        {
            UserDefinedTypes = userDefinedTypes
        };
    }

    public CSharpGenerationResult GenerateCSharp()
    {
        var analysis = Analyze();
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new CSharpGenerationResult(analysis, null);
        }

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);
        source = VBIntrinsicSymbols.RewriteGeneratedCalls(source);
        return new CSharpGenerationResult(analysis, source);
    }

    public ManagedApplicationEmitResult EmitManagedApplication(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var generation = GenerateCSharp();
        if (!generation.Success || generation.Source is null)
        {
            return new ManagedApplicationEmitResult(generation, null, null, null, null);
        }

        var artifacts = ManagedApplicationWriter.Emit(generation.Source, outputPath);
        return new ManagedApplicationEmitResult(
            generation,
            artifacts.BackendResult,
            artifacts.AssemblyPath,
            artifacts.RuntimeAssemblyPath,
            artifacts.RuntimeConfigPath);
    }
}

public sealed record CompilationAnalysis(
    ParseResult ParseResult,
    SemanticModel? SemanticModel,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public UserDefinedTypeDeclarationResult? UserDefinedTypes { get; init; }

    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

public sealed record CSharpGenerationResult(
    CompilationAnalysis Analysis,
    string? Source)
{
    public bool Success => Analysis.Success && Source is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Analysis.Diagnostics;
}

public sealed record ManagedApplicationEmitResult(
    CSharpGenerationResult Generation,
    AssemblyEmitResult? BackendResult,
    string? AssemblyPath,
    string? RuntimeAssemblyPath,
    string? RuntimeConfigPath)
{
    public bool Success => Generation.Success && BackendResult?.Success == true && AssemblyPath is not null;
    public ImmutableArray<Diagnostic> Diagnostics => Generation.Diagnostics;
}
