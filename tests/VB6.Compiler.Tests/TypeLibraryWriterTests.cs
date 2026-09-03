using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Runtime.InteropServices.ComTypes;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// Writing a type library and reading it back with the importer this project already has. The
/// round trip is the point: a .tlb nobody can load is worth nothing, and only the reader proves
/// the writer produced a real one.
/// </summary>
[TestClass]
public sealed class TypeLibraryWriterTests
{
    [TestMethod]
    public void CreateTypeLibrary_WritesACoclassAndItsDispatchInterface()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6TypeLib", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "Rechner.vbp");
            File.WriteAllText(projectPath, """
                Type=OleDll
                Name=Rechner
                Class=Addierer; Addierer.cls
                Class=Intern; Intern.cls
                """);
            File.WriteAllText(Path.Combine(directory, "Addierer.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Addierer"
                Attribute VB_Creatable = True
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = True
                Option Explicit

                Public Function Summe(ByVal Links As Long, ByVal Rechts As Long) As Long
                    Summe = Links + Rechts
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Intern.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1  'True
                END
                Attribute VB_Name = "Intern"
                Attribute VB_Creatable = False
                Attribute VB_PredeclaredId = False
                Attribute VB_Exposed = False
                Option Explicit

                Public Function Geheim() As Long
                    Geheim = 1
                End Function
                """);

            var assemblyPath = Path.Combine(directory, "Rechner.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var typeLibraryPath = ManagedTypeLibraryWriter.Create(assemblyPath, ManagedPlatform.X86);
            Assert.IsTrue(File.Exists(typeLibraryPath));

            var names = ReadTypeNames(typeLibraryPath);

            // Die Form, die VB6 fuer ein Klassenmodul erzeugt: die Mitglieder liegen auf einer
            // Dispinterface mit fuehrendem Unterstrich, die Coclass traegt den blanken Namen.
            CollectionAssert.Contains(names, "Addierer");
            CollectionAssert.Contains(names, "_Addierer");

            // Eine Private-Klasse gehoert nicht in die Typbibliothek -- genau wie in VB6.
            CollectionAssert.DoesNotContain(names, "Intern");
            CollectionAssert.DoesNotContain(names, "_Intern");

            CollectionAssert.Contains(ReadMemberNames(typeLibraryPath, "_Addierer"), "Summe");
        }
        finally
        {
            // Das Auslesen der Klassen laedt die emittierte Assembly in einen einsammelbaren
            // Kontext. Dessen Entladen ist asynchron, die Datei bleibt also noch kurz gesperrt --
            // das ist kein Befund über die Typbibliothek und darf den Test nicht faerben.
            TryDeleteDirectory(directory);
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

    [SupportedOSPlatform("windows")]
    private static List<string> ReadTypeNames(string typeLibraryPath)
    {
        var library = LoadTypeLibrary(typeLibraryPath);
        try
        {
            var names = new List<string>();
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                names.Add(name);
            }

            return names;
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> ReadMemberNames(string typeLibraryPath, string typeName)
    {
        var library = LoadTypeLibrary(typeLibraryPath);
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var typeInfo);
                try
                {
                    typeInfo.GetTypeAttr(out var attributes);
                    try
                    {
                        var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                        var members = new List<string>();
                        for (var function = 0; function < attribute.cFuncs; function++)
                        {
                            typeInfo.GetFuncDesc(function, out var descriptor);
                            try
                            {
                                var func = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                                var buffer = new string[1];
                                typeInfo.GetNames(func.memid, buffer, 1, out _);
                                members.Add(buffer[0]);
                            }
                            finally
                            {
                                typeInfo.ReleaseFuncDesc(descriptor);
                            }
                        }

                        return members;
                    }
                    finally
                    {
                        typeInfo.ReleaseTypeAttr(attributes);
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(typeInfo);
                }
            }

            Assert.Fail("The type library does not contain " + typeName + ".");
            return new List<string>();
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }
    }

    [SupportedOSPlatform("windows")]
    private static ITypeLib LoadTypeLibrary(string path)
    {
        // REGKIND_NONE: die Bibliothek wird geprueft, nicht auf der Maschine registriert.
        var hresult = LoadTypeLibEx(path, 2, out var library);
        Marshal.ThrowExceptionForHR(hresult);
        return library!;
    }

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode)]
    private static extern int LoadTypeLibEx(
        [MarshalAs(UnmanagedType.LPWStr)] string szFile,
        int regKind,
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib? typeLibrary);
}
