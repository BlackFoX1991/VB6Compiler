using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using VB6.Emit.Managed;

namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 Binary Compatibility. A client built against a component holds a CLSID, an IID and a
/// DISPID -- never a name. A rebuild that derives fresh identities therefore orphans it silently:
/// it compiles, it registers, and the old client fails at activation. So the identities of the
/// component named by <c>CompatibleEXE32</c> win over the derivation, and a member it published
/// has to still be there.
/// </summary>
[TestClass]
public sealed class BinaryCompatibilityTests
{
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void CompatibleBuild_KeepsTheIdentitiesOfThePreviousComponent()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6Compat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = Build(directory, "alt", """
                Public Function Summe(ByVal A As Long) As Long
                    Summe = A
                End Function
                """);

            // Eine vertraegliche Aenderung: ein Mitglied kommt dazu, keines faellt weg.
            var second = Build(directory, "neu", """
                Public Function Summe(ByVal A As Long) As Long
                    Summe = A
                End Function

                Public Function Differenz(ByVal A As Long) As Long
                    Differenz = -A
                End Function
                """, compatibleWith: first.AssemblyPath);

            Assert.AreEqual(ReadIdentity(first.TypeLibraryPath, "Rechner"), ReadIdentity(second.TypeLibraryPath, "Rechner"));
            Assert.AreEqual(ReadIdentity(first.TypeLibraryPath, "_Rechner"), ReadIdentity(second.TypeLibraryPath, "_Rechner"));

            // Und die DISPID bleibt: Ein Client ruft die Nummer auf, nicht den Namen. Ohne diese
            // Regel schiebt das neue, alphabetisch fruehere Differenz die Summe auf 2.
            Assert.AreEqual(
                ReadMemberId(first.TypeLibraryPath, "_Rechner", "Summe"),
                ReadMemberId(second.TypeLibraryPath, "_Rechner", "Summe"));
            Assert.AreNotEqual(
                ReadMemberId(second.TypeLibraryPath, "_Rechner", "Summe"),
                ReadMemberId(second.TypeLibraryPath, "_Rechner", "Differenz"));

            // Ohne Binary Compatibility wird dieselbe Klasse aus dem Namen abgeleitet -- und weil
            // der Assemblyname hier ein anderer ist, ist es eine andere CLSID. Genau das ist der
            // Fall, den die Einstellung verhindert.
            var third = Build(directory, "ohne", """
                Public Function Summe(ByVal A As Long) As Long
                    Summe = A
                End Function
                """);
            Assert.AreNotEqual(ReadIdentity(first.TypeLibraryPath, "Rechner"), ReadIdentity(third.TypeLibraryPath, "Rechner"));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void IncompatibleBuild_ReportsTheMemberThatWentAway()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type libraries are a Windows contract.");
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "VB6Compat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = Build(directory, "alt", """
                Public Function Summe(ByVal A As Long) As Long
                    Summe = A
                End Function

                Public Function Differenz(ByVal A As Long) As Long
                    Differenz = -A
                End Function
                """);

            var project = WriteProject(directory, "kaputt", """
                Public Function Summe(ByVal A As Long) As Long
                    Summe = A
                End Function
                """, compatibleWith: first.AssemblyPath);

            var assemblyPath = Path.Combine(directory, "kaputt.dll");
            var emit = VBProjectCompilation.Create(project).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });

            Assert.IsFalse(emit.Success);
            var diagnostics = emit.BackendResult?.Diagnostics ?? throw new AssertFailedException("No backend result.");
            Assert.IsTrue(
                diagnostics.Any(diagnostic =>
                    diagnostic.Code == "VB6E0004" &&
                    diagnostic.Message.Contains("Differenz", StringComparison.Ordinal)),
                string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Code + ": " + diagnostic.Message)));

            // Und die Ausgabe entsteht gar nicht erst: Eine inkompatible Komponente unter
            // denselben Identitaeten auszuliefern waere schlimmer als sie nicht zu bauen.
            Assert.IsFalse(File.Exists(assemblyPath));
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static string WriteProject(string root, string name, string body, string? compatibleWith = null)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        var compatibility = compatibleWith is null
            ? string.Empty
            : "CompatibleMode=2\nCompatibleEXE32=" + compatibleWith + "\n";
        File.WriteAllText(
            Path.Combine(directory, name + ".vbp"),
            "Type=OleDll\nName=" + name + "\nClass=Rechner; Rechner.cls\n" + compatibility);
        File.WriteAllText(Path.Combine(directory, "Rechner.cls"), """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Rechner"
            Attribute VB_Creatable = True
            Attribute VB_PredeclaredId = False
            Attribute VB_Exposed = True
            Option Explicit

            """ + body + Environment.NewLine);
        return Path.Combine(directory, name + ".vbp");
    }

    private static (string AssemblyPath, string TypeLibraryPath) Build(
        string root,
        string name,
        string body,
        string? compatibleWith = null)
    {
        var projectPath = WriteProject(root, name, body, compatibleWith);
        var assemblyPath = Path.Combine(Path.GetDirectoryName(projectPath)!, name + ".dll");
        var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
            assemblyPath,
            new ManagedEmitOptions(assemblyPath) { EnableComHosting = true });
        Assert.IsTrue(
            emit.Success,
            string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics) +
            string.Join(Environment.NewLine, emit.BackendResult?.Diagnostics.Select(d => d.Code + ": " + d.Message) ?? Array.Empty<string>()));
        return (assemblyPath, Path.ChangeExtension(assemblyPath, ".tlb"));
    }

    [SupportedOSPlatform("windows")]
    private static Guid ReadIdentity(string typeLibraryPath, string typeName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
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
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail("The type library does not contain " + typeName + ".");
        return Guid.Empty;
    }

    [SupportedOSPlatform("windows")]
    private static int ReadMemberId(string typeLibraryPath, string typeName, string memberName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(typeLibraryPath, 2, out var library));
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
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
                    for (var function = 0; function < attribute.cFuncs; function++)
                    {
                        info.GetFuncDesc(function, out var descriptor);
                        try
                        {
                            var func = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                            var buffer = new string[1];
                            info.GetNames(func.memid, buffer, 1, out _);
                            if (string.Equals(buffer[0], memberName, StringComparison.Ordinal))
                            {
                                return func.memid;
                            }
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
                    Marshal.ReleaseComObject(info);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(library);
        }

        Assert.Fail("The type library does not contain " + typeName + "." + memberName + ".");
        return 0;
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
        [MarshalAs(UnmanagedType.Interface)] out ITypeLib typeLibrary);
}
