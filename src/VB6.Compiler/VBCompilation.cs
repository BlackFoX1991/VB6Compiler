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

        var implicitVariantRoot = ImplicitVariantSyntaxLowerer.Lower(parseResult.Root);
        var userDefinedTypes = new UserDefinedTypeDeclarationBinder(Text).Bind(implicitVariantRoot);
        var procedureSymbols = VBIntrinsicSymbols.CreateProcedureTable(implicitVariantRoot);
        SemanticModel preliminaryModel;
        using (UserDefinedTypeLookupScope.Push(userDefinedTypes.Types))
        {
            preliminaryModel = new Binder(Text)
                .BindCompilationUnit(implicitVariantRoot, procedureSymbols);
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
                .BindCompilationUnit(forEachLowering.Root, procedureSymbols);
            duplicateProcedureDiagnostics = new Binder(Text)
                .BindCompilationUnit(forEachLowering.Root)
                .Diagnostics
                .Where(diagnostic => diagnostic.Code == "VB6S0004")
                .ToImmutableArray();
        }
        semanticModel = semanticModel with
        {
            Diagnostics = semanticModel.Diagnostics.AddRange(duplicateProcedureDiagnostics)
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
            .AddRange(variantOperationDiagnostics);

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
