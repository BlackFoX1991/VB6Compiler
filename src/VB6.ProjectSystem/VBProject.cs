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
    /// <summary>Designer engine name from a legacy <c>Designer=...; ...</c> entry.</summary>
    public string? DesignerType { get; init; }

    public string GetFullPath(string projectDirectory) =>
        Path.GetFullPath(Path.Combine(projectDirectory, RelativePath));
}

public sealed record VBProjectReference
{
    public VBProjectReference(string rawValue)
    {
        ArgumentNullException.ThrowIfNull(rawValue);
        RawValue = rawValue;
        Metadata = VBProjectBindingMetadataParser.ParseReference(rawValue);
    }

    public string RawValue { get; }
    public VBProjectReferenceMetadata Metadata { get; }
}

public sealed record VBProjectObject
{
    public VBProjectObject(string rawValue)
    {
        ArgumentNullException.ThrowIfNull(rawValue);
        RawValue = rawValue;
        Metadata = VBProjectBindingMetadataParser.ParseObject(rawValue);
    }

    public string RawValue { get; }
    public VBProjectObjectMetadata Metadata { get; }
}

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
