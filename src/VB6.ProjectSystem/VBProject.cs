using System.Collections.Immutable;

namespace VB6.ProjectSystem;

public enum VBProjectItemKind
{
    Module,
    Class,
    Form,
    UserControl,
    PropertyPage,
    UserDocument,
    Designer
}

public sealed record VBProjectItem(
    VBProjectItemKind Kind,
    string? Name,
    string RelativePath)
{
    public string GetFullPath(string projectDirectory) =>
        Path.GetFullPath(Path.Combine(projectDirectory, RelativePath));
}

public sealed record VBProjectReference(string RawValue);

public sealed record VBProjectObject(string RawValue);

public sealed record VBProjectProperty(string Name, string Value);

public sealed record VBProject(
    string FilePath,
    string ProjectDirectory,
    string? ProjectType,
    string? Name,
    string? StartupObject,
    string? ExecutableName,
    ImmutableArray<VBProjectItem> Items,
    ImmutableArray<VBProjectReference> References,
    ImmutableArray<VBProjectObject> Objects,
    ImmutableArray<VBProjectProperty> Properties)
{
    public IEnumerable<VBProjectItem> Modules =>
        Items.Where(item => item.Kind == VBProjectItemKind.Module);

    public IEnumerable<VBProjectItem> Classes =>
        Items.Where(item => item.Kind == VBProjectItemKind.Class);

    public IEnumerable<VBProjectItem> Forms =>
        Items.Where(item => item.Kind == VBProjectItemKind.Form);
}
