using System.Collections.Immutable;

namespace VB6.ProjectSystem;

public sealed class VBProjectGroupLoader
{
    public VBProjectGroupLoadResult Load(string groupFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupFilePath);

        var fullPath = Path.GetFullPath(groupFilePath);
        var text = File.ReadAllText(fullPath);
        return Parse(text, fullPath);
    }

    public VBProjectGroupLoadResult Parse(string text, string groupFilePath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupFilePath);

        var fullPath = Path.GetFullPath(groupFilePath);
        var projectDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var projects = ImmutableArray.CreateBuilder<VBProjectGroupProject>();
        var properties = ImmutableArray.CreateBuilder<VBProjectProperty>();
        var diagnostics = ImmutableArray.CreateBuilder<VBProjectGroupDiagnostic>();

        string? groupType = null;
        string? startupProject = null;

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
                diagnostics.Add(new VBProjectGroupDiagnostic(
                    "VB6VBG0001",
                    lineNumber,
                    "Project-group line does not contain a valid key/value assignment."));
                continue;
            }

            var key = trimmed[..equalsIndex].Trim();
            var value = trimmed[(equalsIndex + 1)..].Trim();
            properties.Add(new VBProjectProperty(key, value));

            switch (key.ToUpperInvariant())
            {
                case "TYPE":
                    groupType = Unquote(value);
                    break;

                case "PROJECT":
                    var projectPath = NormalizeRelativePath(Unquote(value));
                    if (projectPath.Length == 0)
                    {
                        diagnostics.Add(new VBProjectGroupDiagnostic(
                            "VB6VBG0002",
                            lineNumber,
                            "Project-group entry does not specify a project path."));
                    }
                    else
                    {
                        projects.Add(new VBProjectGroupProject(projectPath));
                    }
                    break;

                case "STARTUPPROJECT":
                case "STARTUP":
                    startupProject = NormalizeRelativePath(Unquote(value));
                    break;
            }
        }

        if (groupType is not null &&
            !string.Equals(groupType, "Group", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new VBProjectGroupDiagnostic(
                "VB6VBG0003",
                0,
                $"Project-group Type '{groupType}' is not 'Group'."));
        }

        if (projects.Count == 0)
        {
            diagnostics.Add(new VBProjectGroupDiagnostic(
                "VB6VBG0004",
                0,
                "Project group does not contain any Project= entries."));
        }

        var group = new VBProjectGroup(
            fullPath,
            projectDirectory,
            groupType,
            startupProject,
            projects.ToImmutable(),
            properties.ToImmutable());
        return new VBProjectGroupLoadResult(group, diagnostics.ToImmutable());
    }

    private static bool IsSectionHeader(string line) =>
        line.Length >= 2 && line[0] == '[' && line[^1] == ']';

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
