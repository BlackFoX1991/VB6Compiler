using System.Security.Cryptography;
using System.Text;

namespace VB6.ProjectSystem;

/// <summary>The result of writing an input manifest.</summary>
public sealed record VBInputManifestResult(
    bool Success,
    string OutputPath,
    int InputCount,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// The exact set of files a VB6 project or project group is built from, each with the hash of its
/// content. MSBuild uses it to decide whether a rebuild is needed, so it is declaration-based
/// rather than glob-based: a file the .vbp does not name is not an input, even when it sits in the
/// same directory.
///
/// The logic lives here rather than in the CLI because both the CLI and the MSBuild resolver task
/// need it, and a second implementation would drift from the first without anyone noticing.
/// </summary>
public static class VBInputManifest
{
    public static VBInputManifestResult Write(string inputPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var fullOutputPath = Path.GetFullPath(outputPath);
        var inputs = new List<string> { Path.GetFullPath(inputPath) };
        var diagnostics = new List<string>();
        var success = true;

        if (string.Equals(Path.GetExtension(inputPath), ".vbp", StringComparison.OrdinalIgnoreCase))
        {
            var result = new VBProjectLoader().Load(inputPath);
            foreach (var diagnostic in result.Diagnostics)
            {
                diagnostics.Add($"{diagnostic.Code} line {diagnostic.Line}: {diagnostic.Message}");
            }

            success = result.Success;
            inputs.AddRange(CollectProjectInputs(result.Project));
        }
        else if (string.Equals(Path.GetExtension(inputPath), ".vbg", StringComparison.OrdinalIgnoreCase))
        {
            var groupResult = new VBProjectGroupLoader().Load(inputPath);
            foreach (var diagnostic in groupResult.Diagnostics)
            {
                diagnostics.Add($"{diagnostic.Code} line {diagnostic.Line}: {diagnostic.Message}");
            }

            success = groupResult.Success;
            foreach (var project in groupResult.Group.Projects)
            {
                var projectPath = project.GetFullPath(groupResult.Group.ProjectDirectory);
                inputs.Add(projectPath);
                var projectResult = new VBProjectLoader().Load(projectPath);
                foreach (var diagnostic in projectResult.Diagnostics)
                {
                    diagnostics.Add(
                        $"{projectPath}: {diagnostic.Code} line {diagnostic.Line}: {diagnostic.Message}");
                }

                success &= projectResult.Success;
                inputs.AddRange(CollectProjectInputs(projectResult.Project));
            }
        }
        else
        {
            return new VBInputManifestResult(
                false,
                fullOutputPath,
                0,
                new[] { "Input manifest generation requires a .vbp or .vbg file." });
        }

        if (!success)
        {
            return new VBInputManifestResult(false, fullOutputPath, 0, diagnostics);
        }

        var lines = inputs
            .Where(pathValue => !string.Equals(pathValue, fullOutputPath, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(pathValue => pathValue, StringComparer.OrdinalIgnoreCase)
            .Select(CreateInputManifestLine)
            .ToArray();

        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? Directory.GetCurrentDirectory());

        // Rewriting an unchanged manifest would move its timestamp and defeat the very
        // incrementality it exists for.
        if (!File.Exists(fullOutputPath) ||
            !lines.SequenceEqual(File.ReadAllLines(fullOutputPath), StringComparer.Ordinal))
        {
            File.WriteAllLines(fullOutputPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return new VBInputManifestResult(true, fullOutputPath, lines.Length, diagnostics);
    }

    private static IEnumerable<string> CollectProjectInputs(VBProject project)
    {
        foreach (var item in project.Items)
        {
            var itemPath = item.GetFullPath(project.ProjectDirectory);
            yield return itemPath;

            // A designer file carries its binary resources next to it, under the same name.
            if (item.Kind is VBProjectItemKind.Form or
                VBProjectItemKind.UserControl or
                VBProjectItemKind.PropertyPage or
                VBProjectItemKind.UserDocument)
            {
                yield return Path.ChangeExtension(itemPath, ".frx");
            }
        }

        foreach (var reference in project.References)
        {
            if (reference.Metadata.GetFullPath(project.ProjectDirectory) is { } referencePath)
            {
                yield return referencePath;
            }
        }

        foreach (var component in project.Objects)
        {
            if (component.Metadata.GetFullPath(project.ProjectDirectory) is { } componentPath)
            {
                yield return componentPath;
            }
        }

        foreach (var property in project.Properties)
        {
            if (property.Name.StartsWith("RESFILE", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(property.Value))
            {
                yield return Path.GetFullPath(Path.Combine(
                    project.ProjectDirectory,
                    property.Value.Trim().Trim('"')));
            }
        }
    }

    /// <summary>
    /// One manifest line: the path, a tab, and the content hash. A file the project names but that
    /// does not exist is recorded as MISSING rather than skipped -- its appearance later has to
    /// count as a change.
    /// </summary>
    private static string CreateInputManifestLine(string path)
    {
        var normalized = path.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        if (!File.Exists(path))
        {
            return $"{normalized}\tMISSING";
        }

        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return $"{normalized}\t{hash}";
    }
}
