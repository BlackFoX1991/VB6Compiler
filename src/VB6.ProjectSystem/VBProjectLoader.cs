using System.Collections.Immutable;

namespace VB6.ProjectSystem;

public sealed class VBProjectLoader
{
    public VBProjectLoadResult Load(string projectFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var fullPath = Path.GetFullPath(projectFilePath);
        var text = VB6TextFile.ReadAllText(fullPath);
        return Parse(text, fullPath);
    }

    public VBProjectLoadResult Parse(string text, string projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFilePath);

        var fullPath = Path.GetFullPath(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var items = ImmutableArray.CreateBuilder<VBProjectItem>();
        var references = ImmutableArray.CreateBuilder<VBProjectReference>();
        var objects = ImmutableArray.CreateBuilder<VBProjectObject>();
        var properties = ImmutableArray.CreateBuilder<VBProjectProperty>();
        var diagnostics = ImmutableArray.CreateBuilder<VBProjectDiagnostic>();

        string? projectType = null;
        string? projectName = null;
        string? startupObject = null;
        string? executableName = null;

        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || IsSectionHeader(trimmed))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex <= 0)
            {
                diagnostics.Add(new VBProjectDiagnostic(
                    "VB6VBP0001",
                    lineNumber,
                    "Project line does not contain a valid key/value assignment."));
                continue;
            }

            var key = trimmed[..equalsIndex].Trim();
            var value = trimmed[(equalsIndex + 1)..].Trim();
            properties.Add(new VBProjectProperty(key, value));

            switch (key.ToUpperInvariant())
            {
                case "TYPE":
                    projectType = Unquote(value);
                    break;

                case "NAME":
                    projectName = Unquote(value);
                    break;

                case "STARTUP":
                    startupObject = Unquote(value);
                    break;

                case "EXENAME32":
                    executableName = Unquote(value);
                    break;

                case "MODULE":
                    items.Add(ParseNamedItem(VBProjectItemKind.Module, value));
                    break;

                case "CLASS":
                    items.Add(ParseNamedItem(VBProjectItemKind.Class, value));
                    break;

                case "FORM":
                    items.Add(ParsePathItem(VBProjectItemKind.Form, value));
                    break;

                case "USERCONTROL":
                    items.Add(ParsePathItem(VBProjectItemKind.UserControl, value));
                    break;

                case "PROPERTYPAGE":
                    items.Add(ParsePathItem(VBProjectItemKind.PropertyPage, value));
                    break;

                case "USERDOCUMENT":
                    items.Add(ParsePathItem(VBProjectItemKind.UserDocument, value));
                    break;

                case "DESIGNER":
                    items.Add(ParsePathItem(VBProjectItemKind.Designer, value));
                    break;

                case "REFERENCE":
                    references.Add(new VBProjectReference(value));
                    break;

                case "OBJECT":
                    objects.Add(new VBProjectObject(value));
                    break;
            }
        }

        var project = new VBProject(
            fullPath,
            projectDirectory,
            projectType,
            projectName,
            startupObject,
            executableName,
            items.ToImmutable(),
            references.ToImmutable(),
            objects.ToImmutable(),
            properties.ToImmutable());

        return new VBProjectLoadResult(project, diagnostics.ToImmutable());
    }

    private static bool IsSectionHeader(string line) =>
        line.Length >= 2 && line[0] == '[' && line[^1] == ']';

    private static VBProjectItem ParseNamedItem(VBProjectItemKind kind, string value)
    {
        var separatorIndex = value.IndexOf(';');
        if (separatorIndex < 0)
        {
            return ParsePathItem(kind, value);
        }

        var name = Unquote(value[..separatorIndex].Trim());
        var path = NormalizeRelativePath(Unquote(value[(separatorIndex + 1)..].Trim()));
        return new VBProjectItem(kind, name, path);
    }

    private static VBProjectItem ParsePathItem(VBProjectItemKind kind, string value)
    {
        var path = NormalizeRelativePath(Unquote(value));
        var inferredName = Path.GetFileNameWithoutExtension(path);
        return new VBProjectItem(kind, inferredName, path);
    }

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return trimmed;
    }
}

public sealed record VBProjectDiagnostic(
    string Code,
    int Line,
    string Message);

public sealed record VBProjectLoadResult(
    VBProject Project,
    ImmutableArray<VBProjectDiagnostic> Diagnostics)
{
    public bool Success => Diagnostics.Length == 0;
}
