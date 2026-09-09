using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using VB6.Emit.Managed;

namespace VB6.Compiler;

/// <summary>
/// Writes a real <c>.tlb</c> for an emitted VB6 library through <c>ICreateTypeLib2</c>.
///
/// The interfaces are declared here rather than taken from a library because .NET ships only the
/// reading half of the type-library API. Only the members this writer actually calls carry real
/// signatures; the rest exist to hold their vtable slot, and calling one of those would be a bug,
/// not a limitation -- which is why they take no arguments and are never referenced.
///
/// The shape is the one VB6 produces for a class module: a dispinterface carrying the members, and
/// a coclass that names it as its default. Late-bound clients need neither, but an early-bound one
/// -- VB6, VBA, C++ -- cannot see the class at all without it.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ManagedTypeLibraryWriter
{
    private const int SysWin32 = 1;
    private const int SysWin64 = 3;

    private const int TypeFlagDispatchable = 0x1000;   // TYPEFLAG_FDISPATCHABLE
    private const int TypeFlagCanCreate = 0x0002;      // TYPEFLAG_FCANCREATE
    private const int ImplTypeFlagDefault = 0x0001;    // IMPLTYPEFLAG_FDEFAULT

    private const int InvokeFunc = 1;
    private const int InvokePropertyGet = 2;
    private const int InvokePropertyPut = 4;
    private const int InvokePropertyPutRef = 8;

    private const int ParameterFlagNone = 0;           // PARAMFLAG_NONE
    private const int ParameterFlagOptional = 0x0004;  // PARAMFLAG_FOPT
    private const int ParameterFlagHasDefault = 0x0020; // PARAMFLAG_FHASDEFAULT

    // Ein VARIANT ist auf x64 24 Bytes, nicht 16 -- seine Union traegt BRECORD mit zwei Zeigern.
    private static readonly int VariantSize = IntPtr.Size == 8 ? 24 : 16;

    public static string Create(string managedAssemblyPath, ManagedPlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedAssemblyPath);
        if (!OperatingSystem.IsWindows())
        {
            throw new ManagedArtifactException("Type library generation is supported only on Windows.");
        }

        var assemblyPath = Path.GetFullPath(managedAssemblyPath);
        if (!File.Exists(assemblyPath))
        {
            throw new ManagedArtifactException(
                $"Cannot create a type library because '{assemblyPath}' does not exist.");
        }

        var libraryName = Path.GetFileNameWithoutExtension(assemblyPath);
        var outputPath = Path.Combine(Path.GetDirectoryName(assemblyPath)!, libraryName + ".tlb");
        var (version, classes, interfaces) = ReadComAssembly(assemblyPath);
        if (classes.Count == 0 && interfaces.Count == 0)
        {
            throw new ManagedArtifactException(
                $"The managed assembly '{assemblyPath}' contains no ComVisible classes with COM identities.");
        }

        // A rewritten library would otherwise be merged with whatever is already there.
        File.Delete(outputPath);

        var hresult = CreateTypeLib2(
            platform == ManagedPlatform.X64 ? SysWin64 : SysWin32,
            outputPath,
            out var library);
        Marshal.ThrowExceptionForHR(hresult);

        try
        {
            library.SetName(libraryName);
            library.SetGuid(DeriveIdentity(libraryName, "library", libraryName));
            // Dieselben Zahlen wie die Assembly -- ein fruehgebundener Client bindet an sie, und
            // zwei getrennt erfundene Versionen sind genau die Abweichung, die diese Karte verbietet.
            library.SetVersion((ushort)version.Major, (ushort)version.Minor);
            library.SetLcid(0);

            // Die Schnittstellen zuerst und bis zum Ende offen: Eine Coclass verweist ueber
            // AddRefTypeInfo auf ihre ITypeInfo, und wer die vorher freigibt, trennt den RCW --
            // derselbe Grund, aus dem eine Klasse ihre eigene Dispinterface festhaelt.
            var written = new Dictionary<string, ICreateTypeInfo2>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var comInterface in interfaces)
                {
                    written.Add(comInterface.Name, WriteInterface(library, comInterface));
                }

                foreach (var comClass in classes)
                {
                    WriteClass(library, libraryName, comClass, written);
                }

                library.SaveAllChanges();
            }
            finally
            {
                foreach (var info in written.Values)
                {
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        return outputPath;
    }

    /// <summary>
    /// Writes one <c>Implements</c>-able VB6 class as a dispinterface.
    ///
    /// It carries the plain name rather than an underscored one: the emitter maps a
    /// PublicNotCreatable class module to a CLR interface with no coclass beside it, so this type
    /// *is* what a client names in <c>Dim x As IZaehler</c>. Its GUID is the one the assembly
    /// carries, not a derived one -- QueryInterface answers for that IID, and a library naming a
    /// different one would describe an interface nobody can reach.
    /// </summary>
    private static ICreateTypeInfo2 WriteInterface(ICreateTypeLib2 library, ComInterface comInterface)
    {
        library.CreateTypeInfo(comInterface.Name, TypeKind.TKIND_DISPATCH, out var info);
        var interfaceId = comInterface.InterfaceId;
        info.SetGuid(ref interfaceId);
        info.SetTypeFlags(TypeFlagDispatchable);
        AddMembers(info, comInterface.Members);
        info.LayOut();
        return info;
    }

    /// <summary>
    /// Adds the members of one type, giving every name a single DISPID. A Get/Let pair is one
    /// member with two invoke kinds, so both accessors share it; two DISPIDs for one name are
    /// ambiguous to oleaut32. The function index of AddFuncDesc counts entries and is independent
    /// of the DISPID.
    /// </summary>
    private static void AddMembers(ICreateTypeInfo2 info, List<ComMember> members)
    {
        var dispIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var nextDispId = 1;
        var index = 0;
        foreach (var member in members)
        {
            if (!dispIds.TryGetValue(member.Name, out var dispId))
            {
                dispId = nextDispId++;
                dispIds[member.Name] = dispId;
            }

            AddMember(info, member, index++, dispId);
        }
    }

    private static void WriteClass(
        ICreateTypeLib2 library,
        string libraryName,
        ComClass comClass,
        Dictionary<string, ICreateTypeInfo2> interfaces)
    {
        // VB6 names the members interface after the class with a leading underscore, and the
        // coclass keeps the plain name so that a client writes New Klasse rather than New _Klasse.
        var interfaceName = "_" + comClass.Name;
        library.CreateTypeInfo(interfaceName, TypeKind.TKIND_DISPATCH, out var dispatchInfo);
        try
        {
            dispatchInfo.SetGuid(DeriveIdentity(libraryName, "interface", comClass.Name));
            dispatchInfo.SetTypeFlags(TypeFlagDispatchable);

            AddMembers(dispatchInfo, comClass.Members);
            dispatchInfo.LayOut();

            // The coclass points at the interface, so the interface has to stay alive until the
            // reference is established -- releasing it first separates the RCW and the next call
            // through it fails.
            library.CreateTypeInfo(comClass.Name, TypeKind.TKIND_COCLASS, out var coClassInfo);
            try
            {
                coClassInfo.SetGuid(comClass.ClassId);
                coClassInfo.SetTypeFlags(TypeFlagCanCreate);
                coClassInfo.AddRefTypeInfo((ITypeInfo)dispatchInfo, out var reference);
                coClassInfo.AddImplType(0, reference);
                coClassInfo.SetImplTypeFlags(0, ImplTypeFlagDefault);

                // Was die Klasse implementiert, gehoert an die Coclass, nicht auf ihre
                // Standardschnittstelle. Ein Client, der IZaehler will, fragt danach per
                // QueryInterface -- IZaehler_Zaehle auf _Melder waere ein zweiter, falscher Weg.
                var slot = 1u;
                foreach (var implemented in comClass.Interfaces)
                {
                    if (!interfaces.TryGetValue(implemented, out var implementedInfo))
                    {
                        continue;
                    }

                    coClassInfo.AddRefTypeInfo((ITypeInfo)implementedInfo, out var implementedReference);
                    coClassInfo.AddImplType(slot, implementedReference);
                    slot++;
                }

                coClassInfo.LayOut();
            }
            finally
            {
                Marshal.ReleaseComObject(coClassInfo);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dispatchInfo);
        }
    }

    private static void AddMember(ICreateTypeInfo2 info, ComMember member, int index, int dispId)
    {
        var parameterCount = member.Parameters.Count;
        var elementSize = Marshal.SizeOf<ELEMDESC>();
        var parameters = parameterCount == 0
            ? IntPtr.Zero
            : Marshal.AllocCoTaskMem(elementSize * parameterCount);
        var descriptor = Marshal.AllocCoTaskMem(Marshal.SizeOf<FUNCDESC>());
        var names = new string[parameterCount + 1];
        var defaults = new List<IntPtr>();
        names[0] = member.Name;

        try
        {
            for (var parameter = 0; parameter < parameterCount; parameter++)
            {
                var described = member.Parameters[parameter];
                var flags = described.IsOptional ? ParameterFlagOptional : ParameterFlagNone;
                var defaultValue = IntPtr.Zero;
                if (described.IsOptional && described.DefaultValue is not null)
                {
                    defaultValue = AllocateParameterDefault(described.DefaultValue);
                    defaults.Add(defaultValue);
                    flags |= ParameterFlagHasDefault;
                }

                var element = new ELEMDESC
                {
                    tdesc = new TYPEDESC { lpValue = IntPtr.Zero, vt = described.VariantType },
                    desc = new ELEMDESC.DESCUNION
                    {
                        paramdesc = new PARAMDESC
                        {
                            lpVarValue = defaultValue,
                            wParamFlags = (PARAMFLAG)flags
                        }
                    }
                };
                Marshal.StructureToPtr(element, IntPtr.Add(parameters, elementSize * parameter), false);
                names[parameter + 1] = described.Name;
            }

            // cParamsOpt zaehlt die zusammenhaengende Reihe auslassbarer Argumente am Ende. Ein
            // Client liest daran ab, wie viele er weglassen darf; steht hier 0, sind alle Pflicht.
            var optional = 0;
            for (var parameter = parameterCount - 1; parameter >= 0 && member.Parameters[parameter].IsOptional; parameter--)
            {
                optional++;
            }

            var function = new FUNCDESC
            {
                memid = dispId,
                funckind = FUNCKIND.FUNC_DISPATCH,
                invkind = (INVOKEKIND)member.InvokeKind,
                callconv = CALLCONV.CC_STDCALL,
                cParams = (short)parameterCount,
                cParamsOpt = (short)optional,
                oVft = 0,
                cScodes = 0,
                lprgelemdescParam = parameters,
                elemdescFunc = new ELEMDESC
                {
                    tdesc = new TYPEDESC { lpValue = IntPtr.Zero, vt = member.ReturnType }
                },
                wFuncFlags = 0
            };
            Marshal.StructureToPtr(function, descriptor, false);

            info.AddFuncDesc((uint)index, descriptor);

            // Der Wertparameter eines propput hat in COM keinen eigenen Namen: er ist die rechte
            // Seite der Zuweisung. Wird er trotzdem benannt, meldet SetFuncAndParamNames
            // TYPE_E_ELEMENTNOTFOUND -- der Name gehoert zu keinem benennbaren Element.
            var nameCount = member.InvokeKind is InvokePropertyPut or InvokePropertyPutRef
                ? names.Length - 1
                : names.Length;
            info.SetFuncAndParamNames((uint)index, names, (uint)nameCount);
        }
        finally
        {
            Marshal.FreeCoTaskMem(descriptor);
            if (parameters != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(parameters);
            }

            foreach (var defaultValue in defaults)
            {
                FreeParameterDefault(defaultValue);
            }
        }
    }

    /// <summary>
    /// Allocates the <c>PARAMDESCEX</c> a PARAMFLAG_FHASDEFAULT parameter points at: a byte count
    /// followed by the default as a VARIANT.
    ///
    /// The VARIANT starts at offset 8 in both bit widths -- its union carries a <c>double</c>, so
    /// the field is 8-byte aligned and the 4-byte count is padded out. Written by hand rather than
    /// as a struct because .NET has no PARAMDESCEX, and the value has to survive until LayOut.
    /// </summary>
    private static IntPtr AllocateParameterDefault(object value)
    {
        var variantOffset = 8;
        var size = variantOffset + VariantSize;
        var block = Marshal.AllocCoTaskMem(size);
        for (var offset = 0; offset < size; offset++)
        {
            Marshal.WriteByte(block, offset, 0);
        }

        Marshal.WriteInt32(block, 0, size);
        Marshal.GetNativeVariantForObject(value, IntPtr.Add(block, variantOffset));
        return block;
    }

    private static void FreeParameterDefault(IntPtr block)
    {
        if (block == IntPtr.Zero)
        {
            return;
        }

        VariantClear(IntPtr.Add(block, 8));
        Marshal.FreeCoTaskMem(block);
    }

    /// <summary>
    /// The same derivation the emitter uses for a class identity, so the coclass in the library
    /// and the GuidAttribute in the assembly agree without either having to read the other.
    /// </summary>
    private static Guid DeriveIdentity(string assemblyName, string kind, string name)
    {
        var identity = assemblyName + "\0" + kind + "\0" + name;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    /// <summary>
    /// Reads the COM surface of the emitted assembly as **metadata**, never by loading it for
    /// execution. Two reasons, and the second is fatal on its own: an execution load keeps the file
    /// locked past this method, so the caller cannot replace its own build output; and a legacy
    /// <c>.vbp</c> defaults to x86 while <c>vb6c</c> runs as x64, which makes such a load fail
    /// outright with "The assembly architecture is not compatible with the current process
    /// architecture". Every ActiveX DLL built with COM hosting died there, with an unhandled
    /// exception rather than a diagnostic.
    /// </summary>
    private static (Version Version, List<ComClass> Classes, List<ComInterface> Interfaces) ReadComAssembly(
        string assemblyPath)
    {
        var resolver = new PathAssemblyResolver(
            Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll")
                .Concat(Directory.EnumerateFiles(
                    Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "*.dll"))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        using var context = new MetadataLoadContext(resolver);
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var classes = new List<ComClass>();

            // Ein PublicNotCreatable-Klassenmodul wird zu einer CLR-Schnittstelle; ihre Mitglieder
            // tragen das __vb6_-Praefix, das der Lowerer setzt, damit sie nicht mit einem
            // gleichnamigen Klassenmitglied zusammenfallen.
            var interfaces = new List<ComInterface>();
            foreach (var type in assembly.GetTypes()
                         .Where(type => type.IsInterface && type.Namespace == "VB6.Generated")
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                if (!TryReadComIdentity(type, out var interfaceId))
                {
                    continue;
                }

                var interfaceMembers = new List<ComMember>();
                foreach (var method in type
                             .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(method => !method.IsSpecialName)
                             .OrderBy(method => method.Name, StringComparer.Ordinal))
                {
                    interfaceMembers.Add(new ComMember(
                        StripPrefix(method.Name, "__vb6_"),
                        InvokeFunc,
                        ToVariantType(method.ReturnType),
                        method.GetParameters().Select(ToComParameter).ToList()));
                }

                interfaces.Add(new ComInterface(
                    StripPrefix(type.Name, "__vb6_interface_"),
                    interfaceId,
                    interfaceMembers));
            }

            foreach (var type in assembly.GetTypes()
                         .Where(type => type.IsClass && !type.IsAbstract && type.Namespace == "VB6.Generated")
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                // Metadata, not instantiated attributes: a MetadataLoadContext never runs the
                // assembly, so an attribute object cannot be constructed from it.
                if (!TryReadComIdentity(type, out var classId))
                {
                    continue;
                }

                // Was eine implementierte Schnittstelle traegt, gehoert an sie -- nicht als
                // Iface_Member noch einmal auf die Standardschnittstelle der Klasse.
                var implemented = type.GetInterfaces()
                    .Where(candidate => candidate.Namespace == "VB6.Generated")
                    .Select(candidate => StripPrefix(candidate.Name, "__vb6_interface_"))
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToList();

                var members = new List<ComMember>();
                foreach (var method in type
                             .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(method => !method.IsSpecialName)
                             .Where(method => !IsInterfaceForwarder(method.Name, implemented))
                             .OrderBy(method => method.Name, StringComparer.Ordinal))
                {
                    members.Add(new ComMember(
                        method.Name,
                        InvokeFunc,
                        ToVariantType(method.ReturnType),
                        method.GetParameters().Select(ToComParameter).ToList()));
                }

                // Ein Public-Feld ist in VB6 ein Get/Let-Paar, im Emitter aber ein CLR-Feld mit
                // Assembly-Sichtbarkeit -- dieselbe Regel, nach der VBDynamicDispatch es findet.
                // Ohne diesen Durchgang fehlt es in der Bibliothek, obwohl der Server es bedient.
                foreach (var field in type
                             .GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .Where(field => field.IsAssembly)
                             .OrderBy(field => field.Name, StringComparer.Ordinal))
                {
                    members.Add(new ComMember(
                        field.Name,
                        InvokePropertyGet,
                        ToVariantType(field.FieldType),
                        new List<ComParameter>()));
                    members.Add(new ComMember(
                        field.Name,
                        InvokePropertyPut,
                        (short)VarEnum.VT_VOID,
                        new List<ComParameter>
                        {
                            new("value", ToVariantType(field.FieldType), false, null)
                        }));
                }

                foreach (var property in type
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    // Eine indizierte Property tragen beide Aufrufarten: der Getter die Indizes,
                    // der Setter die Indizes und dahinter den Wert.
                    var indices = property.GetIndexParameters().Select(ToComParameter).ToList();

                    if (property.CanRead)
                    {
                        members.Add(new ComMember(
                            property.Name,
                            InvokePropertyGet,
                            ToVariantType(property.PropertyType),
                            indices));
                    }

                    if (property.CanWrite)
                    {
                        members.Add(new ComMember(
                            property.Name,
                            InvokePropertyPut,
                            (short)VarEnum.VT_VOID,
                            new List<ComParameter>(indices)
                            {
                                new("value", ToVariantType(property.PropertyType), false, null)
                            }));
                    }
                }

                classes.Add(new ComClass(
                    StripPrefix(type.Name, "__vb6_class_"),
                    classId,
                    members,
                    implemented));
            }

            return (assembly.GetName().Version ?? new Version(1, 0, 0, 0), classes, interfaces);
        }
    }

    /// <summary>
    /// Reads ComVisible and Guid from the type's metadata. <see cref="MetadataLoadContext"/> hands
    /// out attribute *data*, never an attribute instance, because it never runs the assembly.
    /// </summary>
    private static bool TryReadComIdentity(Type type, out Guid classId)
    {
        classId = Guid.Empty;
        var attributes = CustomAttributeData.GetCustomAttributes(type);

        var comVisible = attributes.FirstOrDefault(attribute =>
            attribute.AttributeType.FullName == typeof(ComVisibleAttribute).FullName);
        if (comVisible?.ConstructorArguments is not [{ Value: true }])
        {
            return false;
        }

        var guid = attributes.FirstOrDefault(attribute =>
            attribute.AttributeType.FullName == typeof(GuidAttribute).FullName);
        return guid?.ConstructorArguments is [{ Value: string text }] &&
               Guid.TryParse(text, out classId);
    }

    /// <summary>
    /// The VARIANT type a CLR type appears as in the library. Anything this writer cannot describe
    /// becomes VT_VARIANT, which stays callable rather than being silently dropped.
    /// </summary>
    private static short ToVariantType(Type type) => (short)(Type.GetTypeCode(type) switch
    {
        TypeCode.Empty => VarEnum.VT_VOID,
        TypeCode.Boolean => VarEnum.VT_BOOL,
        TypeCode.Byte => VarEnum.VT_UI1,
        TypeCode.SByte => VarEnum.VT_I1,
        TypeCode.Int16 => VarEnum.VT_I2,
        TypeCode.UInt16 => VarEnum.VT_UI2,
        TypeCode.Int32 => VarEnum.VT_I4,
        TypeCode.UInt32 => VarEnum.VT_UI4,
        TypeCode.Int64 => VarEnum.VT_I8,
        TypeCode.UInt64 => VarEnum.VT_UI8,
        TypeCode.Single => VarEnum.VT_R4,
        TypeCode.Double => VarEnum.VT_R8,
        TypeCode.Decimal => VarEnum.VT_DECIMAL,
        TypeCode.DateTime => VarEnum.VT_DATE,
        TypeCode.String => VarEnum.VT_BSTR,
        _ when type == typeof(void) => VarEnum.VT_VOID,
        _ => VarEnum.VT_VARIANT
    });

    private static string StripPrefix(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;

    /// <summary>
    /// True for the method a class carries for one member of an interface it implements. VB6 names
    /// it <c>Interface_Member</c>, and it belongs to that interface rather than to the class's own
    /// default interface.
    /// </summary>
    private static bool IsInterfaceForwarder(string methodName, List<string> implemented)
    {
        foreach (var name in implemented)
        {
            if (methodName.Length > name.Length + 1 &&
                methodName.StartsWith(name, StringComparison.OrdinalIgnoreCase) &&
                methodName[name.Length] == '_')
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ComClass(
        string Name,
        Guid ClassId,
        List<ComMember> Members,
        List<string> Interfaces);

    private sealed record ComInterface(string Name, Guid InterfaceId, List<ComMember> Members);

    private sealed record ComMember(
        string Name,
        int InvokeKind,
        short ReturnType,
        List<ComParameter> Parameters);

    private sealed record ComParameter(
        string Name,
        short VariantType,
        bool IsOptional,
        object? DefaultValue);

    /// <summary>
    /// One reflected parameter as the library describes it. The optionality comes from the
    /// metadata the emitter writes, so an <c>Optional</c> in VB6 reaches a foreign client as
    /// PARAMFLAG_FOPT rather than as a required argument.
    /// </summary>
    private static ComParameter ToComParameter(ParameterInfo parameter) => new(
        parameter.Name ?? "value",
        ToVariantType(parameter.ParameterType),
        parameter.IsOptional,
        parameter.HasDefaultValue ? parameter.RawDefaultValue : null);

    private sealed class TypeLibraryAssemblyLoadContext : System.Runtime.Loader.AssemblyLoadContext
    {
        private readonly System.Runtime.Loader.AssemblyDependencyResolver _resolver;

        public TypeLibraryAssemblyLoadContext(string assemblyPath)
            : base(isCollectible: true) =>
            _resolver = new System.Runtime.Loader.AssemblyDependencyResolver(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int CreateTypeLib2(
        int syskind,
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        [MarshalAs(UnmanagedType.Interface)] out ICreateTypeLib2 ppctlib);

    private enum TypeKind
    {
        TKIND_ENUM = 0,
        TKIND_RECORD = 1,
        TKIND_MODULE = 2,
        TKIND_INTERFACE = 3,
        TKIND_DISPATCH = 4,
        TKIND_COCLASS = 5,
        TKIND_ALIAS = 6,
        TKIND_UNION = 7
    }

    [ComImport]
    [Guid("0002040F-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateTypeLib2
    {
        void CreateTypeInfo(
            [MarshalAs(UnmanagedType.LPWStr)] string szName,
            TypeKind tkind,
            [MarshalAs(UnmanagedType.Interface)] out ICreateTypeInfo2 ppCTInfo);

        void SetName([MarshalAs(UnmanagedType.LPWStr)] string szName);

        void SetVersion(ushort wMajorVerNum, ushort wMinorVerNum);

        void SetGuid(ref Guid guid);

        void SetDocString([MarshalAs(UnmanagedType.LPWStr)] string szDoc);

        void SetHelpFileName([MarshalAs(UnmanagedType.LPWStr)] string szHelpFileName);

        void SetHelpContext(uint dwHelpContext);

        void SetLcid(uint lcid);

        void SetLibFlags(uint uLibFlags);

        void SaveAllChanges();

        // ICreateTypeLib2 continues here. The slots are held so the vtable stays correct; calling
        // one of them is a defect, which is why none of them takes a usable argument.
        void DeleteTypeInfo();

        void SetCustData();

        void SetHelpStringContext();

        void SetHelpStringDll();
    }

    [ComImport]
    [Guid("0002040E-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICreateTypeInfo2
    {
        void SetGuid(ref Guid guid);

        void SetTypeFlags(uint uTypeFlags);

        void SetDocString([MarshalAs(UnmanagedType.LPWStr)] string pStrDoc);

        void SetHelpContext(uint dwHelpContext);

        void SetVersion(ushort wMajorVerNum, ushort wMinorVerNum);

        void AddRefTypeInfo(
            [MarshalAs(UnmanagedType.Interface)] ITypeInfo pTInfo,
            out uint phRefType);

        void AddFuncDesc(uint index, IntPtr pFuncDesc);

        void AddImplType(uint index, uint hRefType);

        void SetImplTypeFlags(uint index, int implTypeFlags);

        void SetAlignment(ushort cbAlignment);

        void SetSchema([MarshalAs(UnmanagedType.LPWStr)] string pStrSchema);

        void AddVarDesc(uint index, IntPtr pVarDesc);

        void SetFuncAndParamNames(
            uint index,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] rgszNames,
            uint cNames);

        void SetVarName(uint index, [MarshalAs(UnmanagedType.LPWStr)] string szName);

        void SetTypeDescAlias(IntPtr pTDescAlias);

        void DefineFuncAsDllEntry();

        void SetFuncDocString();

        void SetVarDocString();

        void SetFuncHelpContext();

        void SetVarHelpContext();

        void SetMops();

        void SetTypeIdldesc();

        void LayOut();
    }
}
