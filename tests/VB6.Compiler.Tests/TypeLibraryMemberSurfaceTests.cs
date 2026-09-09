using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// The automation surface a generated class publishes in its type library.
///
/// A VB6 property is one member with two invoke kinds sharing a DISPID -- not two functions. The
/// emitter has to publish a real CLR property for that, and the accessor methods have to disappear
/// behind it; otherwise the library carries <c>Wert</c>, <c>get_Wert</c> and <c>set_Wert</c> side by
/// side, and oleaut32 refuses the whole library with TYPE_E_AMBIGUOUSNAME.
/// </summary>
[TestClass]
public sealed class TypeLibraryMemberSurfaceTests
{
    private const int InvokeFunc = 1;
    private const int InvokePropertyGet = 2;
    private const int InvokePropertyPut = 4;

    private sealed record Member(string Name, int InvokeKind, int MemberId, int ParameterCount);

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TypeLibrary_DescribesTheWholeAutomationSurfaceOfAClass()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6TypeLibSurface", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Halterei.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Halterei
                Class=Halter; Halter.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Halter.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Halter"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Feld As Long
                Private m As Long
                Private mTexte(1 To 3) As String
                Private mObj As Object

                Public Property Get Wert() As Long
                    Wert = m
                End Property

                Public Property Let Wert(ByVal Neu As Long)
                    m = Neu
                End Property

                Public Property Get NurLesen() As Long
                    NurLesen = 7
                End Property

                Public Property Get Text(ByVal Index As Long) As String
                    Text = mTexte(Index)
                End Property

                Public Property Let Text(ByVal Index As Long, ByVal Neu As String)
                    mTexte(Index) = Neu
                End Property

                Public Property Get Ziel() As Object
                    Set Ziel = mObj
                End Property

                Public Property Set Ziel(ByVal Neu As Object)
                    Set mObj = Neu
                End Property

                Public Function Summe(ByVal A As Long) As Long
                    Summe = A + m
                End Function
                """);

            var assemblyPath = Path.Combine(directory, "Halterei.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var typeLibraryPath = ManagedTypeLibraryWriter.Create(assemblyPath, ManagedPlatform.X86);
            var members = ReadMembers(typeLibraryPath, "_Halter");

            // Eine Methode bleibt eine Methode.
            Assert.AreEqual(1, members.Count(member => member.Name == "Summe" && member.InvokeKind == InvokeFunc));

            // Die Accessoren duerfen *nicht* zusaetzlich als Funktionen dastehen -- genau daran
            // scheiterte die Bibliothek vorher.
            Assert.IsFalse(members.Any(member => member.Name.StartsWith("get_", StringComparison.Ordinal)));
            Assert.IsFalse(members.Any(member => member.Name.StartsWith("set_", StringComparison.Ordinal)));

            // Get und Let sind ein Mitglied mit einer DISPID und zwei Aufrufarten.
            AssertPair(members, "Wert", indexParameters: 0);

            // Ein Public-Feld ist in VB6 dasselbe Paar, obwohl der Emitter ein CLR-Feld daraus macht.
            AssertPair(members, "Feld", indexParameters: 0);

            // Property Set ist der Setter derselben Property; VB6 trennt nach Objektwert, COM nicht.
            AssertPair(members, "Ziel", indexParameters: 0);

            // Eine indizierte Property tragen beide Aufrufarten: der Setter mit dem Wert dahinter.
            AssertPair(members, "Text", indexParameters: 1);

            // Nur-Lesend hat keinen Setter und bleibt trotzdem eine Property.
            var readOnly = members.Where(member => member.Name == "NurLesen").ToList();
            Assert.AreEqual(1, readOnly.Count);
            Assert.AreEqual(InvokePropertyGet, readOnly[0].InvokeKind);

            // Jede DISPID gehoert genau einem Namen.
            foreach (var group in members.GroupBy(member => member.MemberId))
            {
                Assert.AreEqual(1, group.Select(member => member.Name).Distinct(StringComparer.Ordinal).Count());
            }
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    /// <summary>
    /// An <c>Optional</c> parameter and the library version are facts a foreign early-bound client
    /// reads before it ever calls: without PARAMFLAG_FOPT every argument is mandatory to it, and a
    /// library version invented next to the assembly version is exactly the disagreement this
    /// contract forbids.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TypeLibrary_CarriesOptionalParametersAndTheProjectVersion()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6TypeLibOptional", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Melderei.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Melderei
                Class=Melder; Melder.cls
                MajorVer=3
                MinorVer=7
                RevisionVer=2
                """);
            File.WriteAllText(Path.Combine(directory, "Melder.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Melder"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Sub Melde(ByVal Pflicht As Long, Optional ByVal Text As String = "hallo", Optional ByVal Zahl As Long = 4)
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "Melderei.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var typeLibraryPath = ManagedTypeLibraryWriter.Create(assemblyPath, ManagedPlatform.X86);
            Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
            try
            {
                // Die Bibliothek nennt die Version des Projekts, nicht eine eigene.
                library!.GetLibAttr(out var libraryAttributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPELIBATTR>(libraryAttributes);
                    Assert.AreEqual(3, attribute.wMajorVerNum);
                    Assert.AreEqual(7, attribute.wMinorVerNum);
                }
                finally
                {
                    library.ReleaseTLibAttr(libraryAttributes);
                }

                var parameters = ReadParameters(library, "_Melder", "Melde", out var optionalCount);

                // cParamsOpt zaehlt die auslassbare Reihe am Ende.
                Assert.AreEqual(2, optionalCount);
                Assert.AreEqual(3, parameters.Count);
                Assert.AreEqual(ParameterFlagNone, parameters[0].Flags);
                Assert.IsNull(parameters[0].DefaultValue);
                Assert.AreEqual(ParameterFlagOptional | ParameterFlagHasDefault, parameters[1].Flags);
                Assert.AreEqual("hallo", parameters[1].DefaultValue);
                Assert.AreEqual(ParameterFlagOptional | ParameterFlagHasDefault, parameters[2].Flags);
                Assert.AreEqual(4, parameters[2].DefaultValue);
            }
            finally
            {
                Marshal.ReleaseComObject(library!);
            }
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    /// <summary>
    /// An implemented interface belongs to the coclass, not onto the class's own default
    /// interface. A client that wants <c>IZaehler</c> asks for it by IID; an
    /// <c>IZaehler_Zaehle</c> function on <c>_Melder</c> would be a second, wrong way in -- and
    /// the interface itself would be missing from the library entirely.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TypeLibrary_PublishesAnImplementedInterfaceOnTheCoclass()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6TypeLibInterface", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Zaehlerei.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Zaehlerei
                Class=Melder; Melder.cls
                Class=IZaehler; IZaehler.cls
                """);
            File.WriteAllText(Path.Combine(directory, "IZaehler.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "IZaehler"
                Attribute VB_Creatable = False
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Sub Zaehle(ByVal Um As Long)
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Melder.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Melder"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Implements IZaehler

                Private mStand As Long

                Private Sub IZaehler_Zaehle(ByVal Um As Long)
                    mStand = mStand + Um
                End Sub

                Public Sub Melde()
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "Zaehlerei.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var typeLibraryPath = ManagedTypeLibraryWriter.Create(assemblyPath, ManagedPlatform.X86);

            // Die Schnittstelle steht als eigener Typ in der Bibliothek.
            var interfaceMembers = ReadMembers(typeLibraryPath, "IZaehler");
            Assert.AreEqual(1, interfaceMembers.Count);
            Assert.AreEqual("Zaehle", interfaceMembers[0].Name);

            // Und ihre Mitglieder stehen *nicht* zusaetzlich auf der Standardschnittstelle.
            var classMembers = ReadMembers(typeLibraryPath, "_Melder");
            Assert.IsFalse(classMembers.Any(member =>
                member.Name.Contains('_', StringComparison.Ordinal)));
            Assert.AreEqual(1, classMembers.Count(member => member.Name == "Melde"));

            // Die Coclass nennt beide, die eigene als Standard.
            var implemented = ReadImplementedTypes(typeLibraryPath, "Melder");
            CollectionAssert.AreEqual(new[] { "_Melder", "IZaehler" }, implemented.Select(entry => entry.Name).ToArray());
            Assert.AreEqual(ImplTypeFlagDefault, implemented[0].Flags & ImplTypeFlagDefault);

            // Die IID der Bibliothek ist die der Assembly -- QueryInterface antwortet auf diese.
            Assert.AreEqual(ReadAssemblyInterfaceId(assemblyPath, "IZaehler"), ReadTypeIdentity(typeLibraryPath, "IZaehler"));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    /// <summary>
    /// An event is a contract of the *client*: the sink implements the source interface. Without
    /// that interface in the library nothing tells a foreign client what to implement, and the
    /// events of the class are invisible to it -- the connection point would be there with nobody
    /// able to describe what arrives through it.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TypeLibrary_PublishesTheEventSourceInterface()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6TypeLibEvents", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Melderei.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Melderei
                Class=Melder; Melder.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Melder.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Melder"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Event Fertig(ByVal Stand As Long)
                Public Event Abbruch()

                Public Sub Melde()
                    RaiseEvent Fertig(3)
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "Melderei.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var typeLibraryPath = ManagedTypeLibraryWriter.Create(assemblyPath, ManagedPlatform.X86);

            // VB6 nennt die Ereignisquelle __Klasse; ihre Mitglieder sind die Ereignisse mit
            // ihren Argumenten.
            var events = ReadMembers(typeLibraryPath, "__Melder");
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(0, events.Single(member => member.Name == "Abbruch").ParameterCount);
            Assert.AreEqual(1, events.Single(member => member.Name == "Fertig").ParameterCount);

            // Ein Ereignis ist kein Mitglied der Standardschnittstelle.
            var members = ReadMembers(typeLibraryPath, "_Melder");
            Assert.AreEqual(1, members.Count);
            Assert.AreEqual("Melde", members[0].Name);

            // Die Coclass fuehrt sie als Quelle: FSOURCE sagt, dass der Client sie implementiert.
            var implemented = ReadImplementedTypes(typeLibraryPath, "Melder");
            var source = implemented.Single(entry => entry.Name == "__Melder");
            Assert.AreEqual(ImplTypeFlagSource, source.Flags & ImplTypeFlagSource);
            Assert.AreEqual(ImplTypeFlagDefault, source.Flags & ImplTypeFlagDefault);
            Assert.AreEqual(0, implemented.Single(entry => entry.Name == "_Melder").Flags & ImplTypeFlagSource);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private const int ImplTypeFlagSource = 0x0002;
    private const int ImplTypeFlagDefault = 0x0001;

    private static Guid ReadAssemblyInterfaceId(string assemblyPath, string interfaceName)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new System.Reflection.PortableExecutable.PEReader(stream);
        var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(reader);
        foreach (var handle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(handle);
            if (metadata.GetString(definition.Name) != "__vb6_interface_" + interfaceName)
            {
                continue;
            }

            foreach (var attributeHandle in definition.GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(attributeHandle);
                var blob = metadata.GetBlobReader(attribute.Value);
                blob.ReadUInt16();
                var text = blob.ReadSerializedString();
                if (Guid.TryParse(text, out var identity))
                {
                    return identity;
                }
            }
        }

        Assert.Fail("The assembly has no interface " + interfaceName + ".");
        return Guid.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static Guid ReadTypeIdentity(string typeLibraryPath, string typeName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
        try
        {
            for (var index = 0; index < library!.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                info.GetTypeAttr(out var attributes);
                try
                {
                    return Marshal.PtrToStructure<TYPEATTR>(attributes).guid;
                }
                finally
                {
                    info.ReleaseTypeAttr(attributes);
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library!);
        }

        Assert.Fail("The type library does not contain " + typeName + ".");
        return Guid.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static List<(string Name, int Flags)> ReadImplementedTypes(string typeLibraryPath, string typeName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
        try
        {
            for (var index = 0; index < library!.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                info.GetTypeAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                    var result = new List<(string, int)>();
                    for (var slot = 0; slot < attribute.cImplTypes; slot++)
                    {
                        info.GetRefTypeOfImplType(slot, out var reference);
                        info.GetRefTypeInfo(reference, out var implemented);
                        implemented.GetDocumentation(-1, out var implementedName, out _, out _, out _);
                        info.GetImplTypeFlags(slot, out var flags);
                        result.Add((implementedName, (int)flags));
                        Marshal.ReleaseComObject(implemented);
                    }

                    return result;
                }
                finally
                {
                    info.ReleaseTypeAttr(attributes);
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library!);
        }

        Assert.Fail("The type library does not contain " + typeName + ".");
        return new List<(string, int)>();
    }

    private const int ParameterFlagNone = 0;
    private const int ParameterFlagOptional = 0x0004;
    private const int ParameterFlagHasDefault = 0x0020;

    private sealed record Parameter(int Flags, object? DefaultValue);

    /// <summary>
    /// Reads the parameter descriptors of one member. <c>ELEMDESC</c> ends in a union, so
    /// <c>wParamFlags</c> and the default value are read at hand-computed offsets rather than
    /// through the marshalled structure -- the same rule the type library importer follows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static List<Parameter> ReadParameters(
        ITypeLib library,
        string typeName,
        string memberName,
        out int optionalCount)
    {
        for (var index = 0; index < library.GetTypeInfoCount(); index++)
        {
            library.GetDocumentation(index, out var name, out _, out _, out _);
            if (!string.Equals(name, typeName, StringComparison.Ordinal))
            {
                continue;
            }

            library.GetTypeInfo(index, out var info);
            try
            {
                info.GetTypeAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                    for (var function = 0; function < attribute.cFuncs; function++)
                    {
                        info.GetFuncDesc(function, out var descriptor);
                        try
                        {
                            var func = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                            var buffer = new string[1];
                            info.GetNames(func.memid, buffer, 1, out _);
                            if (!string.Equals(buffer[0], memberName, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            optionalCount = func.cParamsOpt;
                            var elementSize = Marshal.SizeOf<ELEMDESC>();
                            var parameters = new List<Parameter>();
                            for (var parameter = 0; parameter < func.cParams; parameter++)
                            {
                                var element = IntPtr.Add(func.lprgelemdescParam, elementSize * parameter);
                                var paramdesc = IntPtr.Add(element, IntPtr.Size * 2);
                                var value = Marshal.ReadIntPtr(paramdesc);
                                var flags = Marshal.ReadInt16(paramdesc, IntPtr.Size);
                                parameters.Add(new Parameter(
                                    flags,
                                    value == IntPtr.Zero
                                        ? null
                                        : Marshal.GetObjectForNativeVariant(IntPtr.Add(value, 8))));
                            }

                            return parameters;
                        }
                        finally
                        {
                            info.ReleaseFuncDesc(descriptor);
                        }
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

        Assert.Fail("The type library does not contain " + typeName + "." + memberName + ".");
        optionalCount = 0;
        return new List<Parameter>();
    }

    private static void AssertPair(List<Member> members, string name, int indexParameters)
    {
        var getter = members.SingleOrDefault(member => member.Name == name && member.InvokeKind == InvokePropertyGet);
        var setter = members.SingleOrDefault(member => member.Name == name && member.InvokeKind == InvokePropertyPut);
        Assert.IsNotNull(getter, name + " has no propget.");
        Assert.IsNotNull(setter, name + " has no propput.");
        Assert.AreEqual(indexParameters, getter.ParameterCount, name + " propget arity");
        Assert.AreEqual(indexParameters + 1, setter.ParameterCount, name + " propput arity");
        Assert.AreEqual(getter.MemberId, setter.MemberId, name + " does not share one DISPID.");
    }

    [SupportedOSPlatform("windows")]
    private static List<Member> ReadMembers(string typeLibraryPath, string typeName)
    {
        var hresult = LoadTypeLibEx(typeLibraryPath, 2, out var library);
        Marshal.ThrowExceptionForHR(hresult);
        try
        {
            for (var index = 0; index < library!.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                try
                {
                    info.GetTypeAttr(out var attributes);
                    try
                    {
                        var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                        var members = new List<Member>();
                        for (var function = 0; function < attribute.cFuncs; function++)
                        {
                            info.GetFuncDesc(function, out var descriptor);
                            try
                            {
                                var func = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                                var buffer = new string[1];
                                info.GetNames(func.memid, buffer, 1, out _);
                                members.Add(new Member(buffer[0], (int)func.invkind, func.memid, func.cParams));
                            }
                            finally
                            {
                                info.ReleaseFuncDesc(descriptor);
                            }
                        }

                        return members;
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

            Assert.Fail("The type library does not contain " + typeName + ".");
            return new List<Member>();
        }
        finally
        {
            Marshal.ReleaseComObject(library!);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            Thread.Sleep(100);
        }
    }

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib? typeLibrary);
}
