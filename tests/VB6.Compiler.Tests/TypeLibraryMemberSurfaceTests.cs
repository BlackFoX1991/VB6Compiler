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
