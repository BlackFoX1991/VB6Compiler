using System.Collections.Immutable;
using VB6.CodeGen.CSharp;
using VB6.ProjectSystem;
using VB6.Semantics;
using VB6.Syntax.Diagnostics;

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
        var units = ImmutableArray.CreateBuilder<VBProjectCompilationUnit>();
        var procedures = ImmutableArray.CreateBuilder<BoundProcedure>();
        var procedureNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

            var analysis = VBCompilation.Create(source, modulePath).Analyze();
            units.Add(new VBProjectCompilationUnit(module, modulePath, analysis));
            sourceDiagnostics.AddRange(analysis.Diagnostics);

            if (analysis.SemanticModel is null)
            {
                continue;
            }

            foreach (var procedure in analysis.SemanticModel.Procedures)
            {
                if (procedureNames.TryGetValue(procedure.Symbol.Name, out var existingModule))
                {
                    projectDiagnostics.Add(new VBProjectCompilationDiagnostic(
                        "VB6PRJ0003",
                        $"Procedure '{procedure.Symbol.Name}' is declared in both '{existingModule}' and '{module.RelativePath}'.",
                        modulePath));
                    continue;
                }

                procedureNames.Add(procedure.Symbol.Name, module.RelativePath);
                procedures.Add(procedure);
            }
        }

        var semanticModel = new SemanticModel(procedures.ToImmutable(), sourceDiagnostics.ToImmutable());
        return new VBProjectCompilationAnalysis(
            loadResult.Project,
            units.ToImmutable(),
            semanticModel,
            sourceDiagnostics.ToImmutable(),
            projectDiagnostics.ToImmutable());
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
