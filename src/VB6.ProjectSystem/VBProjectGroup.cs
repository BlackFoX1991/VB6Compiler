using System.Collections.Immutable;

namespace VB6.ProjectSystem;

public sealed record VBProjectGroupProject(string RelativePath)
{
    public string GetFullPath(string projectDirectory) =>
        Path.GetFullPath(Path.Combine(projectDirectory, RelativePath));
}

public sealed record VBProjectGroup(
    string FilePath,
    string ProjectDirectory,
    string? GroupType,
    string? StartupProject,
    ImmutableArray<VBProjectGroupProject> Projects,
    ImmutableArray<VBProjectProperty> Properties);

public sealed record VBProjectGroupDiagnostic(
    string Code,
    int Line,
    string Message);

public sealed record VBProjectGroupLoadResult(
    VBProjectGroup Group,
    ImmutableArray<VBProjectGroupDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Length == 0;
}
