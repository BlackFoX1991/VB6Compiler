using System.Collections.Immutable;
using VB6.Emit.Managed;
using VB6.ProjectSystem;

namespace VB6.Compiler;

public sealed class VBProjectGroupCompilation
{
    private readonly string _groupFilePath;

    private VBProjectGroupCompilation(string groupFilePath)
    {
        _groupFilePath = Path.GetFullPath(groupFilePath);
    }

    public static VBProjectGroupCompilation Create(string groupFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupFilePath);
        return new VBProjectGroupCompilation(groupFilePath);
    }

    public VBProjectGroupAnalysis Analyze()
    {
        var loadResult = new VBProjectGroupLoader().Load(_groupFilePath);
        var groupDiagnostics = ImmutableArray.CreateBuilder<VBProjectGroupCompilationDiagnostic>();
        foreach (var diagnostic in loadResult.Diagnostics)
        {
            groupDiagnostics.Add(new VBProjectGroupCompilationDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                loadResult.Group.FilePath,
                diagnostic.Line));
        }

        var projects = ImmutableArray.CreateBuilder<VBProjectGroupProjectAnalysis>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in loadResult.Group.Projects)
        {
            var projectPath = project.GetFullPath(loadResult.Group.ProjectDirectory);
            var projectDiagnostics = ImmutableArray.CreateBuilder<VBProjectGroupCompilationDiagnostic>();
            if (!seenPaths.Add(projectPath))
            {
                projectDiagnostics.Add(new VBProjectGroupCompilationDiagnostic(
                    "VB6VBG0005",
                    $"Project '{project.RelativePath}' occurs more than once in the project group.",
                    projectPath));
            }

            if (!File.Exists(projectPath))
            {
                projectDiagnostics.Add(new VBProjectGroupCompilationDiagnostic(
                    "VB6VBG0006",
                    $"Project '{project.RelativePath}' was not found.",
                    projectPath));
            }

            VBProjectCompilationAnalysis? compilation = null;
            if (projectDiagnostics.Count == 0)
            {
                compilation = VBProjectCompilation.Create(projectPath).Analyze();
            }

            projects.Add(new VBProjectGroupProjectAnalysis(
                project,
                projectPath,
                compilation,
                projectDiagnostics.ToImmutable()));
        }

        if (!string.IsNullOrWhiteSpace(loadResult.Group.StartupProject))
        {
            var startupPath = Path.GetFullPath(Path.Combine(
                loadResult.Group.ProjectDirectory,
                loadResult.Group.StartupProject));
            if (!loadResult.Group.Projects.Any(project =>
                    string.Equals(
                        project.GetFullPath(loadResult.Group.ProjectDirectory),
                        startupPath,
                        StringComparison.OrdinalIgnoreCase)))
            {
                groupDiagnostics.Add(new VBProjectGroupCompilationDiagnostic(
                    "VB6VBG0007",
                    $"Startup project '{loadResult.Group.StartupProject}' is not declared in the project group.",
                    loadResult.Group.FilePath));
            }
        }

        return new VBProjectGroupAnalysis(
            loadResult.Group,
            groupDiagnostics.ToImmutable(),
            projects.ToImmutable());
    }

    public VBProjectGroupEmitResult EmitManagedApplications(
        string outputDirectory,
        ManagedEmitOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var analysis = Analyze();
        var fullOutputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var emittedProjects = ImmutableArray.CreateBuilder<VBProjectGroupProjectEmitResult>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in OrderProjectsForEmission(analysis))
        {
            if (!project.Success || project.Compilation is null)
            {
                continue;
            }

            var stem = GetOutputStem(project.Compilation.Project);
            var uniqueStem = stem;
            var suffix = 2;
            while (!usedNames.Add(uniqueStem))
            {
                uniqueStem = $"{stem}_{suffix++}";
            }

            var outputExtension = VBProjectCompilation.IsLibraryProjectType(
                project.Compilation.Project.ProjectType)
                ? ".dll"
                : ".exe";
            var outputPath = Path.Combine(fullOutputDirectory, uniqueStem + outputExtension);
            var emit = VBProjectCompilation.Create(project.FullPath)
                .EmitManagedApplication(outputPath, options);
            emittedProjects.Add(new VBProjectGroupProjectEmitResult(project, outputPath, emit));
        }

        return new VBProjectGroupEmitResult(analysis, emittedProjects.ToImmutable());
    }

    private static ImmutableArray<VBProjectGroupProjectAnalysis> OrderProjectsForEmission(
        VBProjectGroupAnalysis analysis)
    {
        var projectsByPath = new Dictionary<string, VBProjectGroupProjectAnalysis>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var project in analysis.Projects)
        {
            projectsByPath.TryAdd(project.FullPath, project);
        }
        var state = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        var ordered = ImmutableArray.CreateBuilder<VBProjectGroupProjectAnalysis>();

        foreach (var project in analysis.Projects)
        {
            Visit(project);
        }

        return ordered.ToImmutable();

        void Visit(VBProjectGroupProjectAnalysis project)
        {
            if (state.TryGetValue(project.FullPath, out var currentState))
            {
                if (currentState is VisitState.Visited or VisitState.Visiting)
                {
                    return;
                }
            }

            state[project.FullPath] = VisitState.Visiting;
            if (project.Compilation is not null)
            {
                foreach (var reference in project.Compilation.Project.References.Where(reference =>
                             reference.Metadata.Kind == VBProjectReferenceKind.Project))
                {
                    var referencePath = reference.Metadata.GetFullPath(
                        project.Compilation.Project.ProjectDirectory);
                    if (referencePath is not null &&
                        projectsByPath.TryGetValue(referencePath, out var dependency))
                    {
                        Visit(dependency);
                    }
                }
            }

            state[project.FullPath] = VisitState.Visited;
            ordered.Add(project);
        }
    }

    private enum VisitState
    {
        Visiting,
        Visited
    }

    private static string GetOutputStem(VBProject project)
    {
        var requestedName = VBProjectCompilation.IsLibraryProjectType(project.ProjectType) ||
                            string.IsNullOrWhiteSpace(project.ExecutableName)
            ? project.Name
            : Path.GetFileNameWithoutExtension(
                project.ExecutableName!
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar));
        requestedName = string.IsNullOrWhiteSpace(requestedName)
            ? Path.GetFileNameWithoutExtension(project.FilePath)
            : requestedName;
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(requestedName
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "VB6Project" : sanitized;
    }
}

public sealed record VBProjectGroupCompilationDiagnostic(
    string Code,
    string Message,
    string FilePath,
    int? Line = null)
{
    public override string ToString() =>
        Line is null
            ? $"{Code} {FilePath}: {Message}"
            : $"{Code} {FilePath}:{Line}: {Message}";
}

public sealed record VBProjectGroupProjectAnalysis(
    VBProjectGroupProject Project,
    string FullPath,
    VBProjectCompilationAnalysis? Compilation,
    ImmutableArray<VBProjectGroupCompilationDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Length == 0 && Compilation?.Success == true;
}

public sealed record VBProjectGroupAnalysis(
    VBProjectGroup Group,
    ImmutableArray<VBProjectGroupCompilationDiagnostic> GroupDiagnostics,
    ImmutableArray<VBProjectGroupProjectAnalysis> Projects)
{
    public bool Success =>
        GroupDiagnostics.Length == 0 && Projects.Length > 0 && Projects.All(project => project.Success);
}

public sealed record VBProjectGroupProjectEmitResult(
    VBProjectGroupProjectAnalysis Project,
    string OutputPath,
    VBProjectManagedApplicationEmitResult Emit)
{
    public bool Success => Emit.Success;
}

public sealed record VBProjectGroupEmitResult(
    VBProjectGroupAnalysis Analysis,
    ImmutableArray<VBProjectGroupProjectEmitResult> Projects)
{
    public bool Success =>
        Analysis.Success &&
        Projects.Length == Analysis.Projects.Length &&
        Projects.All(project => project.Success);
}
