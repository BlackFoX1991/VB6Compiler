using System.Collections.Immutable;
using VB6.Parser;
using VB6.Runtime;
using VB6.Semantics;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Nodes;
using VB6.Syntax.Text;
using ParserType = VB6.Parser.Parser;

namespace VB6.Compiler;

public sealed class VBCompilation
{
    private VBCompilation(
        SourceText text,
        ImmutableArray<Diagnostic> conditionalCompilationDiagnostics,
        VBCompilationOptions compilationOptions)
    {
        Text = text;
        _conditionalCompilationDiagnostics = conditionalCompilationDiagnostics;
        CompilationOptions = compilationOptions;
    }

    private readonly ImmutableArray<Diagnostic> _conditionalCompilationDiagnostics;

    public SourceText Text { get; }

    /// <summary>Options that were used to preprocess and bind this compilation.</summary>
    public VBCompilationOptions CompilationOptions { get; }

    public static VBCompilation Create(
        string source,
        string? filePath = null,
        VBCompilationOptions? options = null)
    {
        var requestedOptions = options ?? new VBCompilationOptions();
        var compilationOptions = requestedOptions.NormalizeForProfile();
        var preprocessed = VBConditionalCompilation.Process(source, filePath, compilationOptions);
        var diagnostics = preprocessed.Diagnostics;
        if (requestedOptions.CompatibilityProfile == VBCompatibilityProfile.VB6Sp6 &&
            requestedOptions.TargetIs64Bit == true)
        {
            diagnostics = diagnostics.Add(new Diagnostic(
                "VB6C0001",
                DiagnosticSeverity.Error,
                "The VB6Sp6 compatibility profile supports x86 targets only.",
                new TextSpan(0, 0),
                filePath));
        }

        return new(
            SourceText.From(preprocessed.Source, filePath),
            diagnostics,
            compilationOptions);
    }

    public CompilationAnalysis Analyze()
    {
        var parseResult = new ParserType(Text).ParseCompilationUnit();
        if (parseResult.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return new CompilationAnalysis(
                parseResult,
                null,
                _conditionalCompilationDiagnostics.AddRange(parseResult.Diagnostics));
        }

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
            preliminaryModel = new Binder(Text, enumSymbols.QualifiedMembers)
                .BindCompilationUnit(implicitVariantRoot, procedureSymbols, moduleVariableSymbols);
        }

        var forEachRoot = ForEachArraySyntaxLowerer.Lower(implicitVariantRoot, preliminaryModel);

        SemanticModel semanticModel;
        ImmutableArray<Diagnostic> duplicateProcedureDiagnostics;
        using (UserDefinedTypeLookupScope.Push(userDefinedTypes.Types))
        {
            semanticModel = new Binder(Text, enumSymbols.QualifiedMembers)
                .BindCompilationUnit(forEachRoot, procedureSymbols, moduleVariableSymbols);
            duplicateProcedureDiagnostics = new Binder(Text, enumSymbols.QualifiedMembers)
                .BindCompilationUnit(forEachRoot)
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
        var userDefinedTypeValueDiagnostics = UserDefinedTypeValueGuard.Validate(
            Text,
            forEachRoot,
            userDefinedTypes.Types);
        var variantOperationDiagnostics = VariantOperationGuard.Validate(Text, semanticModel);
        var diagnostics = _conditionalCompilationDiagnostics
            .AddRange(parseResult.Diagnostics)
            .AddRange(userDefinedTypes.Diagnostics)
            .AddRange(semanticModel.Diagnostics)
            .AddRange(userDefinedTypeValueDiagnostics)
            .AddRange(variantOperationDiagnostics);

        return new CompilationAnalysis(parseResult, semanticModel, diagnostics)
        {
            UserDefinedTypes = userDefinedTypes
        };
    }

    /// <summary>Lowers the bound tree to the IR the managed backend emits from.</summary>
    public LoweringResult Lower() => DirectManagedCompilation.Lower(this);

    /// <summary>Emits an executable assembly, its debug information and its runtime files.</summary>
    public ManagedApplicationEmitResult EmitManagedApplication(
        string outputPath,
        VB6.Emit.Managed.ManagedEmitOptions? options = null) =>
        DirectManagedCompilation.EmitManaged(this, outputPath, options);
}

/// <summary>
/// Target information used while evaluating VB6 conditional-compilation constants. A null value
/// follows the current compiler process width; explicit x86/x64 CLI targets set it so a build does
/// not choose a branch from the host process by accident.
/// </summary>
public sealed record VBCompilationOptions(
    bool? TargetIs64Bit = null,
    IReadOnlyDictionary<string, string>? DefinedConstants = null)
{
    /// <summary>
    /// Selects the source and runtime compatibility contract. Existing callers retain the
    /// deterministic behavior unless they explicitly opt into VB6Sp6.
    /// </summary>
    public VBCompatibilityProfile CompatibilityProfile { get; init; } = VBCompatibilityProfile.Deterministic;

    /// <summary>Normalizes profile defaults before conditional compilation is evaluated.</summary>
    public VBCompilationOptions NormalizeForProfile() =>
        CompatibilityProfile == VBCompatibilityProfile.VB6Sp6
            ? this with { TargetIs64Bit = false }
            : this;
}

public sealed record CompilationAnalysis(
    ParseResult ParseResult,
    SemanticModel? SemanticModel,
    ImmutableArray<Diagnostic> Diagnostics)
{
    public UserDefinedTypeDeclarationResult? UserDefinedTypes { get; init; }

    public bool Success => Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}

