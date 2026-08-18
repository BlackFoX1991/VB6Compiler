using System.Collections.Immutable;
using VB6.CodeGen.CSharp;
using VB6.Parser;
using VB6.Runtime;
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

        var semanticModel = new Binder(Text).BindCompilationUnit(parseResult.Root);
        var diagnostics = parseResult.Diagnostics.AddRange(semanticModel.Diagnostics);

        return new CompilationAnalysis(parseResult, semanticModel, diagnostics);
    }

    public CSharpGenerationResult GenerateCSharp()
    {
        var analysis = Analyze();
        if (!analysis.Success || analysis.SemanticModel is null)
        {
            return new CSharpGenerationResult(analysis, null);
        }

        var source = new CSharpGenerator().Generate(analysis.SemanticModel);
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

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath)!;
        Directory.CreateDirectory(outputDirectory);

        var assemblyName = Path.GetFileNameWithoutExtension(fullOutputPath);
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            assemblyName = "VB6Program";
        }

        AssemblyEmitResult backendResult;
        using (var peStream = File.Create(fullOutputPath))
        {
            backendResult = new CSharpAssemblyEmitter().Emit(
                generation.Source,
                assemblyName,
                peStream);
        }

        if (!backendResult.Success)
        {
            File.Delete(fullOutputPath);
            return new ManagedApplicationEmitResult(generation, backendResult, null, null, null);
        }

        var runtimeSourcePath = typeof(VBConversions).Assembly.Location;
        var runtimeOutputPath = Path.Combine(outputDirectory, "VB6.Runtime.dll");
        if (!string.Equals(
                Path.GetFullPath(runtimeSourcePath),
                Path.GetFullPath(runtimeOutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(runtimeSourcePath, runtimeOutputPath, overwrite: true);
        }

        var runtimeConfigPath = Path.Combine(
            outputDirectory,
            Path.GetFileNameWithoutExtension(fullOutputPath) + ".runtimeconfig.json");
        File.WriteAllText(runtimeConfigPath, CreateRuntimeConfig());

        return new ManagedApplicationEmitResult(
            generation,
            backendResult,
            fullOutputPath,
            runtimeOutputPath,
            runtimeConfigPath);
    }

    private static string CreateRuntimeConfig()
    {
        var targetFramework = $"net{Environment.Version.Major}.{Environment.Version.Minor}";
        var frameworkVersion = $"{Environment.Version.Major}.{Environment.Version.Minor}.0";

        return $$"""
            {
              "runtimeOptions": {
                "tfm": "{{targetFramework}}",
                "framework": {
                  "name": "Microsoft.NETCore.App",
                  "version": "{{frameworkVersion}}"
                }
              }
            }
            """;
    }
}

public sealed record CompilationAnalysis(
    ParseResult ParseResult,
    SemanticModel? SemanticModel,
    ImmutableArray<Diagnostic> Diagnostics)
{
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
