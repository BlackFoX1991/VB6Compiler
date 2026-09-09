using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VB6.Compiler;

/// <summary>
/// VB6 Binary Compatibility: the identities and member ids a previously built component published.
///
/// A client that was built against that component holds a CLSID, an IID and a DISPID -- never a
/// name. Deriving fresh ones on every build therefore orphans it silently: it still compiles, it
/// still registers, and the old client fails at activation with "class not registered" or calls a
/// member that has moved. So the identities of the old component win over the derivation, and a
/// member it published must still be there.
///
/// The old component is read through its type library, which is where those identities live. VB6
/// points at the component itself (<c>CompatibleEXE32</c>); a library beside it is used when the
/// file carries none.
/// </summary>
internal static class VBBinaryCompatibility
{
    /// <summary>VB6 Instancing of the project setting: 0 none, 1 project, 2 binary.</summary>
    private const string BinaryCompatibleMode = "2";

    /// <summary>Key of the library identity inside the identity map.</summary>
    public const string LibraryKey = "library\0";

    /// <summary>
    /// What the old component published. <paramref name="MemberIds"/> is keyed
    /// <c>type\0member</c> and holds the DISPID: a client calls by that number, so a member that
    /// keeps its name but changes its id is just as broken as one that disappeared.
    /// </summary>
    public sealed record Surface(
        Guid? LibraryId,
        ImmutableDictionary<string, Guid> Identities,
        ImmutableDictionary<string, ImmutableArray<string>> Members,
        ImmutableDictionary<string, int> MemberIds)
    {
        public static readonly Surface Empty = new(
            null,
            ImmutableDictionary<string, Guid>.Empty,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty,
            ImmutableDictionary<string, int>.Empty);

        public bool IsEmpty => Identities.IsEmpty && LibraryId is null;
    }

    /// <summary>
    /// Reads the compatible component named by the project, or <see cref="Surface.Empty"/> when
    /// the project asks for no binary compatibility or the component is not there. A missing file
    /// is not an error here: VB6 builds the first version of a component before there is anything
    /// to stay compatible with.
    /// </summary>
    public static Surface Read(VB6.ProjectSystem.VBProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!OperatingSystem.IsWindows())
        {
            return Surface.Empty;
        }

        if (!string.Equals(ReadProperty(project, "CompatibleMode"), BinaryCompatibleMode, StringComparison.Ordinal))
        {
            return Surface.Empty;
        }

        var reference = ReadProperty(project, "CompatibleEXE32");
        if (string.IsNullOrWhiteSpace(reference))
        {
            return Surface.Empty;
        }

        // VB6 bettet die Typbibliothek in die Komponente ein, der eigene Writer legt sie als
        // eigene Datei daneben. Beide Orte werden probiert, und dass die Komponente selbst keine
        // traegt, ist kein Fehler -- nur eine Datei ohne Aussage.
        var path = Path.GetFullPath(Path.Combine(project.ProjectDirectory, reference));
        foreach (var candidate in new[] { path, Path.ChangeExtension(path, ".tlb") })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return ReadTypeLibrary(candidate);
            }
            catch (COMException)
            {
            }
        }

        return Surface.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static Surface ReadTypeLibrary(string path)
    {
        var hresult = LoadTypeLibEx(path, RegKindNone, out var library);
        Marshal.ThrowExceptionForHR(hresult);
        try
        {
            var identities = ImmutableDictionary.CreateBuilder<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var members = ImmutableDictionary.CreateBuilder<string, ImmutableArray<string>>(
                StringComparer.OrdinalIgnoreCase);
            var memberIds = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.OrdinalIgnoreCase);

            library.GetLibAttr(out var libraryAttributes);
            Guid libraryId;
            try
            {
                libraryId = Marshal.PtrToStructure<TYPELIBATTR>(libraryAttributes).guid;
            }
            finally
            {
                library.ReleaseTLibAttr(libraryAttributes);
            }

            // Die Bibliotheks-Id steht unter demselben Schluesselschema wie die Typen, damit
            // Emitter und Writer dieselbe Abbildung benutzen koennen.
            identities[LibraryKey] = libraryId;

            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                library.GetTypeInfo(index, out var info);
                try
                {
                    info.GetTypeAttr(out var attributes);
                    try
                    {
                        var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                        switch (attribute.typekind)
                        {
                            case TYPEKIND.TKIND_COCLASS:
                                identities["class\0" + name] = attribute.guid;
                                break;

                            // Die Standardschnittstelle einer Klasse heisst _Klasse, die
                            // Ereignisquelle __Klasse. Beide gehoeren zu derselben Klasse und
                            // tragen eigene IIDs, an denen ein Client haengt.
                            case TYPEKIND.TKIND_DISPATCH or TYPEKIND.TKIND_INTERFACE:
                                identities["interface\0" + name] = attribute.guid;
                                var published = ReadMembers(info, attribute.cFuncs);
                                members[name] = published.Select(entry => entry.Key).ToImmutableArray();
                                foreach (var entry in published)
                                {
                                    memberIds[name + "\0" + entry.Key] = entry.Value;
                                }

                                break;
                        }
                    }
                    finally
                    {
                        info.ReleaseTypeAttr(attributes);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(info);
                }
            }

            return new Surface(
                libraryId,
                identities.ToImmutable(),
                members.ToImmutable(),
                memberIds.ToImmutable());
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }
    }

    /// <summary>
    /// The published members of one type in declaration order, with their DISPIDs. A property
    /// appears twice -- propget and propput share one id -- and is listed once.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static List<KeyValuePair<string, int>> ReadMembers(ITypeInfo info, int count)
    {
        var members = new List<KeyValuePair<string, int>>(count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < count; index++)
        {
            info.GetFuncDesc(index, out var descriptor);
            try
            {
                var function = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                var buffer = new string[1];
                info.GetNames(function.memid, buffer, 1, out _);
                if (!string.IsNullOrEmpty(buffer[0]) && seen.Add(buffer[0]))
                {
                    members.Add(new KeyValuePair<string, int>(buffer[0], function.memid));
                }
            }
            finally
            {
                info.ReleaseFuncDesc(descriptor);
            }
        }

        return members;
    }

    private static string? ReadProperty(VB6.ProjectSystem.VBProject project, string name)
    {
        foreach (var property in project.Properties)
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    // REGKIND_NONE: die Bibliothek wird gelesen, nicht auf der Maschine registriert.
    private const int RegKindNone = 2;

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib typeLibrary);
}
