using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using VB6.ProjectSystem;

namespace VB6.Compiler;

/// <summary>
/// Resolves legacy COM bindings when a VBP stores only the registered file name.
/// </summary>
internal static class VBRegisteredInteropPathResolver
{
    public static string? Resolve(
        VBProjectReferenceMetadata metadata,
        string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        var projectPath = metadata.GetFullPath(projectDirectory);
        if (projectPath is not null && File.Exists(projectPath))
        {
            return projectPath;
        }

        return OperatingSystem.IsWindows() && metadata.LibraryId is Guid libraryId
            ? ResolveTypeLibrary(libraryId, metadata.MajorVersion, metadata.MinorVersion, metadata.LocaleId)
            : projectPath;
    }

    public static string? Resolve(
        VBProjectObjectMetadata metadata,
        string projectDirectory)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);

        var projectPath = metadata.GetFullPath(projectDirectory);
        if (projectPath is not null && File.Exists(projectPath))
        {
            return projectPath;
        }

        return OperatingSystem.IsWindows() && metadata.ClassId is Guid classId
            ? ResolveClassServer(classId)
            : projectPath;
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveTypeLibrary(
        Guid libraryId,
        int? majorVersion,
        int? minorVersion,
        int? localeId)
    {
        using var typeLibraryKey = Registry.ClassesRoot.OpenSubKey(
            $"TypeLib\\{libraryId:B}");
        if (typeLibraryKey is null)
        {
            return null;
        }

        foreach (var versionName in GetVersionNames(typeLibraryKey, majorVersion, minorVersion))
        {
            using var versionKey = typeLibraryKey.OpenSubKey(versionName);
            if (versionKey is null)
            {
                continue;
            }

            foreach (var localeName in GetLocaleNames(versionKey, localeId))
            {
                using var localeKey = versionKey.OpenSubKey(localeName);
                if (localeKey is null)
                {
                    continue;
                }

                foreach (var architectureName in GetArchitectureNames())
                {
                    using var architectureKey = localeKey.OpenSubKey(architectureName);
                    var path = architectureKey?.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? ResolveClassServer(Guid classId)
    {
        using var classKey = Registry.ClassesRoot.OpenSubKey($"CLSID\\{classId:B}");
        if (classKey is null)
        {
            return null;
        }

        foreach (var serverName in new[] { "InprocServer32", "LocalServer32" })
        {
            using var serverKey = classKey.OpenSubKey(serverName);
            var path = serverKey?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> GetVersionNames(
        RegistryKey typeLibraryKey,
        int? majorVersion,
        int? minorVersion)
    {
        if (majorVersion is int major && minorVersion is int minor)
        {
            yield return $"{major}.{minor}";
            yield break;
        }

        foreach (var version in typeLibraryKey.GetSubKeyNames()
                     .Select(name => (Name: name, Parsed: Version.TryParse(name, out var value) ? value : null))
                     .Where(entry => entry.Parsed is not null)
                     .OrderByDescending(entry => entry.Parsed)
                     .Select(entry => entry.Name))
        {
            yield return version;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> GetLocaleNames(RegistryKey versionKey, int? localeId)
    {
        if (localeId is int locale)
        {
            yield return locale.ToString(CultureInfo.InvariantCulture);
            if (locale != 0)
            {
                yield return "0";
            }

            yield break;
        }

        yield return "0";
        foreach (var name in versionKey.GetSubKeyNames().Where(name => name != "0"))
        {
            yield return name;
        }
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string> GetArchitectureNames()
    {
        if (Environment.Is64BitProcess)
        {
            yield return "win64";
            yield return "win32";
        }
        else
        {
            yield return "win32";
            yield return "win64";
        }
    }
}
