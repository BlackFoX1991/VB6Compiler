using System.Text;
using VB6.Emit.Managed;

namespace VB6.Compiler;

/// <summary>
/// Turns an emitted managed application into the normal Windows .NET layout: a native apphost
/// next to the managed assembly. The compiler does not depend on the SDK at runtime, so the
/// apphost pack is located from the active .NET installation and patched in place.
/// </summary>
internal static class ManagedAppHostWriter
{
    private const int MaxAppBinaryPathSizeInBytes = 1024;
    private const string AppBinaryPathPlaceholder =
        "c3ab8ff13720e8ad9047dd39466b3c8974e592c2fa383d4a3960714caef0c4f2";

    public static bool ShouldCreateAppHost(string outputPath, ManagedEmitOptions options) =>
        OperatingSystem.IsWindows() &&
        options.OutputKind == ManagedOutputKind.Application &&
        string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase);

    public static bool TryCreate(
        string managedAssemblyPath,
        string appHostPath,
        ManagedPlatform platform)
    {
        var templatePath = FindTemplate(platform);
        if (templatePath is null)
        {
            return false;
        }

        var image = File.ReadAllBytes(templatePath);
        var placeholder = Encoding.ASCII.GetBytes(AppBinaryPathPlaceholder);
        var placeholderOffset = Find(image, placeholder);
        if (placeholderOffset < 0 || placeholderOffset + MaxAppBinaryPathSizeInBytes > image.Length)
        {
            return false;
        }

        var relativeAssemblyPath = Path.GetRelativePath(
                Path.GetDirectoryName(appHostPath)!,
                managedAssemblyPath)
            .Replace(Path.DirectorySeparatorChar, '/');
        var pathBytes = Encoding.UTF8.GetBytes(relativeAssemblyPath);
        if (pathBytes.Length >= MaxAppBinaryPathSizeInBytes)
        {
            return false;
        }

        var replacement = new byte[MaxAppBinaryPathSizeInBytes];
        pathBytes.CopyTo(replacement, 0);
        Buffer.BlockCopy(replacement, 0, image, placeholderOffset, replacement.Length);
        File.WriteAllBytes(appHostPath, image);
        return true;
    }

    private static string? FindTemplate(ManagedPlatform platform)
    {
        var runtimeIdentifier = platform == ManagedPlatform.X86 ||
                                (platform == ManagedPlatform.AnyCpu && !Environment.Is64BitOperatingSystem)
            ? "win-x86"
            : "win-x64";
        var packName = "Microsoft.NETCore.App.Host." + runtimeIdentifier;
        var roots = new[]
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) is { Length: > 0 } programFiles
                ? Path.Combine(programFiles, "dotnet")
                : null,
            Environment.GetEnvironmentVariable("ProgramFiles(x86)") is { Length: > 0 } programFilesX86
                ? Path.Combine(programFilesX86, "dotnet")
                : null,
            // A 32-bit compiler process sees ProgramFiles as "Program Files (x86)", while SDK
            // host packs are commonly installed under the 64-bit Program Files root. ProgramW6432
            // names that root from either process width, so cross-architecture emission does not
            // depend on which test host invoked the compiler.
            Environment.GetEnvironmentVariable("ProgramW6432") is { Length: > 0 } programFiles64
                ? Path.Combine(programFiles64, "dotnet")
                : null
        };

        foreach (var root in roots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var packRoot = Path.Combine(root!, "packs", packName);
            if (!Directory.Exists(packRoot))
            {
                continue;
            }

            IEnumerable<string> versions;
            try
            {
                versions = Directory.EnumerateDirectories(packRoot)
                    .OrderByDescending(path => IsRuntimeVersion(path, out var version) &&
                                                version.Major == Environment.Version.Major &&
                                                version.Minor == Environment.Version.Minor)
                    .ThenByDescending(path => GetRuntimeVersion(path));
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var version in versions)
            {
                var candidate = Path.Combine(version, "runtimes", runtimeIdentifier, "native", "apphost.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsRuntimeVersion(string path, out Version version)
    {
        return Version.TryParse(Path.GetFileName(path), out version!);
    }

    private static Version GetRuntimeVersion(string path) =>
        IsRuntimeVersion(path, out var version) ? version : new Version(0, 0);

    private static int Find(byte[] image, byte[] value)
    {
        for (var offset = 0; offset <= image.Length - value.Length; offset++)
        {
            var match = true;
            for (var index = 0; index < value.Length; index++)
            {
                if (image[offset + index] == value[index])
                {
                    continue;
                }

                match = false;
                break;
            }

            if (match)
            {
                return offset;
            }
        }

        return -1;
    }
}
