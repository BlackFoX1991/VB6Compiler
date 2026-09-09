using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using VB6.Emit.Managed;

namespace VB6.Compiler;

/// <summary>
/// Writes the side-by-side activation manifest for an emitted managed COM library. The native
/// comhost remains the loader; this manifest maps its exported CLSIDs without requiring registry
/// registration.
/// </summary>
internal static class ManagedComManifestWriter
{
    private const string ManifestNamespace = "urn:schemas-microsoft-com:asm.v1";

    public static string Create(
        string managedAssemblyPath,
        string comHostPath,
        ManagedPlatform platform,
        string? manifestPath = null,
        string? typeLibraryPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedAssemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(comHostPath);

        var assemblyPath = Path.GetFullPath(managedAssemblyPath);
        var hostPath = Path.GetFullPath(comHostPath);
        if (!File.Exists(assemblyPath))
        {
            throw new ManagedArtifactException(
                $"Cannot create a COM manifest because '{assemblyPath}' does not exist.");
        }

        if (!File.Exists(hostPath))
        {
            throw new ManagedArtifactException(
                $"Cannot create a COM manifest because '{hostPath}' does not exist.");
        }

        if (!hostPath.EndsWith(".comhost.dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new ManagedArtifactException(
                $"COM manifest input must be a '.comhost.dll' file: '{hostPath}'.");
        }

        var outputPath = Path.GetFullPath(
            manifestPath ?? Path.ChangeExtension(assemblyPath, ".manifest"));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var classes = ReadComClasses(assemblyPath);
        if (classes.Length == 0)
        {
            throw new ManagedArtifactException(
                $"The managed assembly '{assemblyPath}' contains no ComVisible classes with COM identities.");
        }

        var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
        var version = NormalizeVersion(assemblyName.Version);
        var architecture = platform switch
        {
            ManagedPlatform.X86 => "x86",
            ManagedPlatform.X64 => "amd64",
            _ => "*"
        };

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize
        };

        using (var stream = File.Create(outputPath))
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("assembly", ManifestNamespace);
            writer.WriteAttributeString("manifestVersion", "1.0");

            writer.WriteStartElement("assemblyIdentity", ManifestNamespace);
            writer.WriteAttributeString("type", "win32");
            writer.WriteAttributeString("name", assemblyName.Name ?? Path.GetFileNameWithoutExtension(assemblyPath));
            writer.WriteAttributeString("version", version.ToString());
            writer.WriteAttributeString("processorArchitecture", architecture);
            writer.WriteEndElement();

            writer.WriteStartElement("file", ManifestNamespace);
            writer.WriteAttributeString("name", Path.GetFileName(hostPath));
            foreach (var comClass in classes)
            {
                writer.WriteStartElement("comClass", ManifestNamespace);
                writer.WriteAttributeString("clsid", comClass.ClassId.ToString("B").ToUpperInvariant());
                writer.WriteAttributeString("threadingModel", "Both");
                if (comClass.ProgId is not null)
                {
                    writer.WriteAttributeString("progid", comClass.ProgId);
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            // Die Typbibliothek gehoert in dieselbe Aktivierungsumgebung. Ohne sie findet ein
            // Client die Beschreibung nur ueber die Registrierung, und alles, was zur Laufzeit
            // aufgeloest werden muss -- IRecordInfo fuer einen Record, die Schnittstelle eines
            // fruehgebundenen Aufrufs -- steht ihm registrierungsfrei nicht zur Verfuegung.
            if (typeLibraryPath is not null &&
                File.Exists(typeLibraryPath) &&
                OperatingSystem.IsWindows() &&
                TryReadTypeLibraryIdentity(typeLibraryPath, out var libraryId, out var libraryVersion))
            {
                writer.WriteStartElement("file", ManifestNamespace);
                writer.WriteAttributeString("name", Path.GetFileName(typeLibraryPath));
                writer.WriteStartElement("typelib", ManifestNamespace);
                writer.WriteAttributeString("tlbid", libraryId.ToString("B").ToUpperInvariant());
                writer.WriteAttributeString("version", libraryVersion);
                writer.WriteAttributeString("helpdir", string.Empty);
                writer.WriteAttributeString("resourceid", "0");
                writer.WriteAttributeString("flags", "HASDISKIMAGE");
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return outputPath;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool TryReadTypeLibraryIdentity(string path, out Guid libraryId, out string version)
    {
        libraryId = Guid.Empty;
        version = "1.0";
        try
        {
            var hresult = LoadTypeLibEx(path, RegKindNone, out var library);
            if (hresult != 0)
            {
                return false;
            }

            try
            {
                library.GetLibAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<System.Runtime.InteropServices.ComTypes.TYPELIBATTR>(
                        attributes);
                    libraryId = attribute.guid;
                    version = attribute.wMajorVerNum + "." + attribute.wMinorVerNum;
                    return true;
                }
                finally
                {
                    library.ReleaseTLibAttr(attributes);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(library);
            }
        }
        catch (COMException)
        {
            return false;
        }
    }

    // REGKIND_NONE: die Bibliothek wird gelesen, nicht registriert.
    private const int RegKindNone = 2;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out System.Runtime.InteropServices.ComTypes.ITypeLib typeLibrary);

    private static System.Collections.Immutable.ImmutableArray<ComClassIdentity> ReadComClasses(
        string assemblyPath)
    {
        // Metadata, never an execution load: a legacy .vbp defaults to x86 while vb6c runs as x64,
        // and loading such an assembly for execution fails outright with "The assembly architecture
        // is not compatible with the current process architecture". Every ActiveX DLL asking for a
        // manifest died there, with an unhandled exception rather than a diagnostic.
        var resolver = new PathAssemblyResolver(
            Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")
                .Concat(Directory.EnumerateFiles(
                    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "*.dll"))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        using var context = new MetadataLoadContext(resolver);
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                var details = string.Join(
                    Environment.NewLine,
                    exception.LoaderExceptions
                        .Where(error => error is not null)
                        .Select(error => error!.Message));
                throw new ManagedArtifactException(
                    $"Could not inspect COM classes in '{assemblyPath}'. {details}");
            }

            var identities = System.Collections.Immutable.ImmutableArray.CreateBuilder<ComClassIdentity>();
            foreach (var type in types
                         .Where(type => type.IsClass && !type.IsAbstract &&
                                        type.Namespace == "VB6.Generated")
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                // Attribute *data*, not attribute instances: a MetadataLoadContext never runs the
                // assembly, so it cannot construct one.
                var attributes = CustomAttributeData.GetCustomAttributes(type);
                var comVisible = attributes.FirstOrDefault(attribute =>
                    attribute.AttributeType.FullName == typeof(ComVisibleAttribute).FullName);
                if (comVisible?.ConstructorArguments is not [{ Value: true }])
                {
                    continue;
                }

                var guid = attributes.FirstOrDefault(attribute =>
                    attribute.AttributeType.FullName == typeof(GuidAttribute).FullName);
                if (guid?.ConstructorArguments is not [{ Value: string guidText }] ||
                    !Guid.TryParse(guidText, out var classId))
                {
                    throw new ManagedArtifactException(
                        $"ComVisible class '{type.FullName}' does not have a valid GuidAttribute.");
                }

                var progId = attributes
                    .FirstOrDefault(attribute =>
                        attribute.AttributeType.FullName == typeof(ProgIdAttribute).FullName)
                    ?.ConstructorArguments is [{ Value: string progIdText }]
                    ? progIdText
                    : null;
                identities.Add(new ComClassIdentity(classId, progId));
            }

            return identities.ToImmutable();
        }
    }

    private static Version NormalizeVersion(Version? version)
    {
        version ??= new Version(1, 0, 0, 0);
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }

    private sealed record ComClassIdentity(Guid ClassId, string? ProgId);
}
