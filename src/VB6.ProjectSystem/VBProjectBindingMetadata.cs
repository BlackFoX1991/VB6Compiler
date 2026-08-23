namespace VB6.ProjectSystem;

public enum VBProjectReferenceKind
{
    Unknown,
    TypeLibrary,
    Project
}

/// <summary>
/// Parsed metadata from a VB6 <c>Reference=</c> entry. The raw value remains available because
/// older VB6 versions and add-ins emit several compatible layouts.
/// </summary>
public sealed record VBProjectReferenceMetadata(
    VBProjectReferenceKind Kind,
    Guid? LibraryId,
    int? MajorVersion,
    int? MinorVersion,
    int? LocaleId,
    string? FilePath,
    string? DisplayName,
    bool IsWellFormed)
{
    public string? GetFullPath(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        return string.IsNullOrWhiteSpace(FilePath)
            ? null
            : Path.GetFullPath(Path.Combine(projectDirectory, FilePath));
    }
}

/// <summary>Parsed metadata from a VB6 <c>Object=</c> ActiveX control entry.</summary>
public sealed record VBProjectObjectMetadata(
    Guid? ClassId,
    int? MajorVersion,
    int? MinorVersion,
    int? LocaleId,
    string? FilePath,
    string? DisplayName,
    bool IsWellFormed)
{
    public string? GetFullPath(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        return string.IsNullOrWhiteSpace(FilePath)
            ? null
            : Path.GetFullPath(Path.Combine(projectDirectory, FilePath));
    }
}

internal static class VBProjectBindingMetadataParser
{
    public static VBProjectReferenceMetadata ParseReference(string rawValue)
    {
        var fields = rawValue.Split('#', StringSplitOptions.None);
        if (fields.Length == 0)
        {
            return new(VBProjectReferenceKind.Unknown, null, null, null, null, null, null, false);
        }

        var identity = fields[0].Trim();
        var kind = identity.StartsWith("*\\G", StringComparison.OrdinalIgnoreCase)
            ? VBProjectReferenceKind.TypeLibrary
            : VBProjectReferenceKind.Unknown;
        var guid = TryParseGuid(identity.StartsWith("*\\G", StringComparison.OrdinalIgnoreCase)
            ? identity[3..]
            : identity);

        var version = fields.Length > 1 ? ParseVersion(fields[1]) : (null, null);
        var locale = fields.Length > 2 && int.TryParse(fields[2].Trim(), out var parsedLocale)
            ? parsedLocale
            : (int?)null;
        var filePath = fields.Length > 3 ? NullIfEmpty(fields[3]) : null;
        var displayName = fields.Length > 4
            ? NullIfEmpty(string.Join('#', fields.Skip(4)))
            : null;

        if (!string.IsNullOrWhiteSpace(filePath) &&
            string.Equals(Path.GetExtension(filePath), ".vbp", StringComparison.OrdinalIgnoreCase))
        {
            kind = VBProjectReferenceKind.Project;
        }

        var wellFormed = kind != VBProjectReferenceKind.Unknown &&
            guid.HasValue && version.Item1.HasValue && version.Item2.HasValue && locale.HasValue;
        return new(kind, guid, version.Item1, version.Item2, locale, filePath, displayName, wellFormed);
    }

    public static VBProjectObjectMetadata ParseObject(string rawValue)
    {
        var separator = rawValue.IndexOf(';');
        var identity = separator < 0 ? rawValue : rawValue[..separator];
        var fileOrName = separator < 0 ? null : NullIfEmpty(rawValue[(separator + 1)..]);
        var fields = identity.Split('#', StringSplitOptions.None);
        var guid = fields.Length > 0 ? TryParseGuid(fields[0].Trim()) : null;
        var version = fields.Length > 1 ? ParseVersion(fields[1]) : (null, null);
        var locale = fields.Length > 2 && int.TryParse(fields[2].Trim(), out var parsedLocale)
            ? parsedLocale
            : (int?)null;
        var filePath = LooksLikeFilePath(fileOrName) ? fileOrName : null;
        var displayName = filePath is null ? fileOrName : null;
        var wellFormed = guid.HasValue && version.Item1.HasValue && version.Item2.HasValue && locale.HasValue;
        return new(guid, version.Item1, version.Item2, locale, filePath, displayName, wellFormed);
    }

    private static (int? Major, int? Minor) ParseVersion(string value)
    {
        var parts = value.Trim().Split('.', StringSplitOptions.None);
        return parts.Length == 2 &&
            int.TryParse(parts[0], out var major) &&
            int.TryParse(parts[1], out var minor)
            ? (major, minor)
            : (null, null);
    }

    private static Guid? TryParseGuid(string value) =>
        Guid.TryParse(value.Trim(), out var guid) ? guid : null;

    private static string? NullIfEmpty(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool LooksLikeFilePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var extension = Path.GetExtension(value);
        return extension.Equals(".ocx", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tlb", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".olb", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vbp", StringComparison.OrdinalIgnoreCase);
    }
}
