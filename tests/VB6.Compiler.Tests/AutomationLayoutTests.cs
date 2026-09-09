using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using VB6.Emit.Managed;
using VB6.Semantics;

namespace VB6.Compiler.Tests;

/// <summary>
/// The raw Automation layouts a type library can describe: aliases, records, C arrays, nested
/// pointers and SAFEARRAYs.
///
/// Every case is measured against a registered library rather than against a fixture written for
/// the occasion -- a fixture would only prove that the importer agrees with itself. The expected
/// values are read from the same library in the same test wherever the library states them, so a
/// changed library shows up as a failure instead of as a silently outdated constant.
/// </summary>
[TestClass]
public sealed class AutomationLayoutTests
{
    private const string StdOleLibraryId = "{00020430-0000-0000-C000-000000000046}";
    private static readonly string StdOlePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "stdole2.tlb");

    /// <summary>
    /// An alias is a name for another type. VB6 resolves it away: <c>OLE_COLOR</c> is a Long, and
    /// a member typed with it takes a Long. Leaving it opaque would make every stock control
    /// property a Variant.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ImportedAliases_ResolveToTheTypeTheLibraryNames()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(StdOlePath))
        {
            Assert.Inconclusive("stdole2.tlb is not available.");
            return;
        }

        var imported = VBTypeLibraryImporter.Import(StdOlePath, "stdole", controlLibrary: false);
        var native = ReadNativeAliases(StdOlePath);
        Assert.IsTrue(native.Count > 10, "The library declares no aliases; the probe is worthless.");

        var checkedAliases = 0;
        foreach (var (name, variantType) in native)
        {
            if (ExpectedAliasType(variantType) is not { } expected)
            {
                continue;
            }

            Assert.IsTrue(
                imported.Aliases.TryGetValue("stdole." + name, out var actual),
                "The importer dropped the alias " + name + ".");
            Assert.AreEqual(expected, actual!.Name, name + " (vt " + variantType + ")");
            checkedAliases++;
        }

        // stdole carries scalar aliases of six different VARTYPEs; anything less means the probe
        // stopped measuring what it claims to measure.
        Assert.IsTrue(checkedAliases >= 15, "Only " + checkedAliases + " scalar aliases were checked.");
    }

    /// <summary>
    /// A record has a fixed layout, and an imported one has to match the layout of the library it
    /// came from -- byte for byte, because a <c>Declare</c> or a COM call writes into it.
    ///
    /// x86 is the platform this contract is defined for: a legacy project defaults to it, and the
    /// native OCX path is bound to it. On x64 the same record deviates for two separate reasons --
    /// VB6 packs a UDT to 4 while the native x64 ABI aligns to 8, and a pointer member that VB6
    /// calls Long is 8 bytes there. Both are decisions about the modern extension, not about VB6.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ImportedRecord_HasTheNativeLayoutOnX86()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(StdOlePath))
        {
            Assert.Inconclusive("stdole2.tlb is not available.");
            return;
        }

        var host = X86DotnetHost();
        if (host is null)
        {
            Assert.Inconclusive("The x86 .NET host required by the x86 layout contract is unavailable.");
            return;
        }

        // Die Erwartung kommt aus den Feldern, die die Bibliothek nennt, nicht aus einer Zahl im
        // Test: Jede VARTYPE traegt ihre x86-Breite, und die Ausrichtung ist die von VB6 (4).
        var expected = ExpectedX86RecordSize(StdOlePath, "EXCEPINFO");
        Assert.AreEqual(32, expected, "The library no longer describes the EXCEPINFO this test measures.");

        var directory = Path.Combine(Path.GetTempPath(), "VB6Layout", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var projectPath = Path.Combine(directory, "Layout.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Layout"
                Reference=*\G{StdOleLibraryId}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim e As stdole.EXCEPINFO
                    Dim g As stdole.GUID
                    Debug.Print LenB(e)
                    Debug.Print LBound(g.Data4) & ":" & UBound(g.Data4)
                End Sub
                """);

            var assemblyPath = Path.Combine(directory, "Layout.dll");
            var emit = VBProjectCompilation.Create(projectPath).EmitManagedApplication(
                assemblyPath,
                new ManagedEmitOptions(assemblyPath)
                {
                    OutputKind = ManagedOutputKind.Library,
                    Platform = ManagedPlatform.X86
                });
            Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, emit.Lowering.Analysis.Diagnostics));

            var output = RunOnHost(host, assemblyPath, directory);
            CollectionAssert.AreEqual(
                new[] { expected.ToString(), "0:7" },
                VB6TestProgram.SplitLines(output),
                output);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    /// <summary>
    /// A pointer parameter is unwrapped exactly one level: <c>IFont**</c> is an <c>IFont</c> the
    /// caller passes ByRef. A second level is an opaque native pointer and must not be guessed as
    /// a VB scalar -- calling it as one hands the server a number where it expects an address.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void NestedPointerParameter_UnwrapsOneLevelAndStopsAtTheSecond()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(StdOlePath))
        {
            Assert.Inconclusive("stdole2.tlb is not available.");
            return;
        }

        var imported = VBTypeLibraryImporter.Import(StdOlePath, "stdole", controlLibrary: false);
        Assert.IsTrue(imported.Aliases.TryGetValue("stdole.IFont", out var fontType));
        var font = (ClassTypeSymbol)fontType!;
        Assert.IsTrue(font.TryGetProcedure("Clone", out var clone));

        // Eine Zeigerebene aufgeloest: IFont** wird ein ByRef-IFont, nicht ein Long und nicht ein
        // Object. Genau daran haengt, dass `f.Clone g` bindet.
        Assert.AreEqual(1, clone.Parameters.Length);
        Assert.AreEqual(ParameterPassingMode.ByRef, clone.Parameters[0].PassingMode);
        Assert.AreSame(font, clone.Parameters[0].Type);
    }

    /// <summary>
    /// A SAFEARRAY through a real out-of-process server, in both directions.
    ///
    /// Ownership is the point: the server copies what it is given rather than taking it, so the
    /// caller's own array is still intact afterwards, and what comes back is a VB6 array with its
    /// bounds -- not an opaque Variant. The Task Scheduler service is used because its
    /// <c>IEmailAction.Attachments</c> is one of the few SAFEARRAY properties a normal user can
    /// reach without installing anything.
    /// </summary>
    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void SafeArrayProperty_RoundTripsThroughARegisteredServer()
    {
        if (!OperatingSystem.IsWindows() ||
            Type.GetTypeFromProgID("Schedule.Service", throwOnError: false) is null)
        {
            Assert.Inconclusive("The registered Task Scheduler service is not available.");
            return;
        }

        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim dienst As Object
                Dim aufgabe As Object
                Dim aktion As Object
                Dim hinein(1 To 2) As Variant
                Dim heraus As Variant
                Set dienst = CreateObject("Schedule.Service")
                dienst.Connect
                Set aufgabe = dienst.NewTask(0)
                Set aktion = aufgabe.Actions.Create(6)
                hinein(1) = "eins"
                hinein(2) = "zwei"
                aktion.Attachments = hinein
                heraus = aktion.Attachments
                Debug.Print VarType(heraus)
                Debug.Print LBound(heraus) & ":" & UBound(heraus)
                Debug.Print heraus(LBound(heraus)) & "," & heraus(UBound(heraus))
                Debug.Print hinein(1) & "," & hinein(2)
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "8204",             // vbArray + vbVariant: eine echte VB6-Array-Variant
                "1:2",              // die Grenzen des Aufrufers ueberleben beide Richtungen
                "eins,zwei",        // und die Werte kommen unveraendert zurueck
                "eins,zwei",        // das eigene Array ist danach unberuehrt: der Server kopiert
                "0"
            },
            output,
            string.Join(Environment.NewLine, output));
    }

    /// <summary>The x86 width of one VARTYPE, and null for a type this probe does not measure.</summary>
    private static int? X86Width(short variantType) => variantType switch
    {
        2 or 18 or 11 => 2,          // VT_I2, VT_UI2, VT_BOOL
        3 or 19 or 22 or 23 => 4,    // VT_I4, VT_UI4, VT_INT, VT_UINT
        10 => 4,                     // VT_ERROR
        4 => 4,                      // VT_R4
        5 or 6 or 7 => 8,            // VT_R8, VT_CY, VT_DATE
        8 => 4,                      // VT_BSTR is a pointer
        26 => 4,                     // VT_PTR is a pointer
        _ => null
    };

    private static string? ExpectedAliasType(short variantType) => variantType switch
    {
        2 or 18 => "Integer",
        11 => "Boolean",
        3 or 19 or 22 or 23 or 10 => "Long",
        4 => "Single",
        5 => "Double",
        6 => "Currency",
        7 => "Date",
        8 => "String",
        17 => "Byte",
        _ => null
    };

    [SupportedOSPlatform("windows")]
    private static List<(string Name, short VariantType)> ReadNativeAliases(string path)
    {
        var aliases = new List<(string, short)>();
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(path, 2, out var library));
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                library.GetTypeInfo(index, out var info);
                info.GetTypeAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                    if (attribute.typekind == TYPEKIND.TKIND_ALIAS)
                    {
                        aliases.Add((name, attribute.tdescAlias.vt));
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

        return aliases;
    }

    /// <summary>
    /// The size the named record occupies on x86, computed from the fields the library declares
    /// and the VB6 packing rule of four. Reading the size the library reports would give the size
    /// for the *loading* process, which is x64 here -- and that is the number this test must not
    /// use.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static int ExpectedX86RecordSize(string path, string recordName)
    {
        Marshal.ThrowExceptionForHR(LoadTypeLibEx(path, 2, out var library));
        try
        {
            for (var index = 0; index < library.GetTypeInfoCount(); index++)
            {
                library.GetDocumentation(index, out var name, out _, out _, out _);
                if (!string.Equals(name, recordName, StringComparison.Ordinal))
                {
                    continue;
                }

                library.GetTypeInfo(index, out var info);
                info.GetTypeAttr(out var attributes);
                try
                {
                    var attribute = Marshal.PtrToStructure<TYPEATTR>(attributes);
                    var offset = 0;
                    for (var variable = 0; variable < attribute.cVars; variable++)
                    {
                        info.GetVarDesc(variable, out var descriptor);
                        try
                        {
                            var vardesc = Marshal.PtrToStructure<VARDESC>(descriptor);
                            var width = X86Width(vardesc.elemdescVar.tdesc.vt)
                                ?? throw new InvalidOperationException(
                                    $"{recordName} has a field this probe cannot size: vt {vardesc.elemdescVar.tdesc.vt}.");
                            var alignment = Math.Min(width, 4);
                            offset = (offset + alignment - 1) / alignment * alignment;
                            offset += width;
                        }
                        finally
                        {
                            info.ReleaseVarDesc(descriptor);
                        }
                    }

                    return (offset + 3) / 4 * 4;
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

        throw new InvalidOperationException("The library does not declare " + recordName + ".");
    }

    private static string? X86DotnetHost()
    {
        var root = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var host = Path.Combine(root, "dotnet", "dotnet.exe");
        return File.Exists(host) ? host : null;
    }

    private static string RunOnHost(string host, string assemblyPath, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo(host)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assemblyPath);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The x86 layout probe could not start.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, standardError);
        return standardOutput;
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
