using VB6.Semantics;

namespace VB6.Compiler.Tests;

/// <summary>
/// What a VB6 project gets from an <c>Object=</c> line. The GUID there is the component's **type
/// library** id, not a CLSID, and an installed OCX registers it under <c>TypeLib\</c> — so looking
/// it up under <c>CLSID\</c> never found anything, and the project fell back to a handful of
/// control names catalogued by hand. Everything else in the library was missing: MSCOMCTL alone
/// carries 42 enums, and a legacy project that writes <c>ccOrientationVertical</c> did not compile.
/// </summary>
[TestClass]
public sealed class RegisteredControlLibraryTests
{
    private const string MSComctlLibraryId = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}";

    private static bool RequireNativeOcx =>
        string.Equals(
            Environment.GetEnvironmentVariable("VB6_REQUIRE_NATIVE_OCX"),
            "1",
            StringComparison.Ordinal);

    [TestMethod]
    public void Analyze_ImportsTheEnumConstantsOfAReferencedControlLibrary()
    {
        if (!OperatingSystem.IsWindows() ||
            Type.GetTypeFromProgID("MSComctlLib.TreeCtrl.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Control-library import validation requires a registered MSCOMCTL.OCX.");
            }

            Assert.Inconclusive("The registered MSCOMCTL.OCX fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6RegisteredControlLibrary",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Probe.vbp");

            // The version the project pins is 2.0; what is installed is 2.1. A minor version is
            // upward compatible in COM, and VB6 binds to it -- stopping at the exact match left
            // every such reference unresolved.
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Probe"
                Object={MSComctlLibraryId}#2.0#0; MSCOMCTL.OCX
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim orientation As MSComctlLib.OrientationConstants
                    orientation = ccOrientationVertical
                    Debug.Print orientation
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();

            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Analyze_MapsImportedScalarsToTheTypesVB6Has()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type-library import requires Windows.");
            return;
        }

        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "stdole2.tlb");
        if (!File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The registered Windows stdole2.tlb fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ImportedScalars",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Skalare.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Skalare"
                Reference=*\G{00020430-0000-0000-C000-000000000046}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);

            // OLE_HANDLE is VT_INT and used to arrive unmapped, so it answered VarType 0 and
            // printed nothing at all. OLE_COLOR is VT_UI4; VB6 has no unsigned 32-bit type and
            // maps it to Long. Arriving as the UInteger extension of this repository would
            // answer VarType 20, which a VB6 program reads as vbLongLong.
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim color As stdole.OLE_COLOR
                    Dim handle As stdole.OLE_HANDLE
                    Dim size As stdole.FONTSIZE
                    Dim cancel As stdole.OLE_CANCELBOOL
                    Debug.Print VarType(color)
                    Debug.Print VarType(handle)
                    Debug.Print VarType(size)
                    Debug.Print VarType(cancel)
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "3", "3", "6", "11" },
                VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void EmitManagedApplication_UsesAFixedCArrayOfAnImportedRecord()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type-library import requires Windows.");
            return;
        }

        var typeLibraryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "stdole2.tlb");
        if (!File.Exists(typeLibraryPath))
        {
            Assert.Inconclusive("The registered Windows stdole2.tlb fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ImportedCArray",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Feld.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Feld"
                Reference=*\G{00020430-0000-0000-C000-000000000046}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);

            // stdole.GUID.Data4 is Data4(0 To 7) As Byte -- a fixed C array. It used to arrive as
            // a bare Object, so the first indexed read tore the program down with a
            // NullReferenceException instead of answering a value.
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim g As stdole.GUID
                    Debug.Print TypeName(g.Data4(0))
                    Debug.Print LBound(g.Data4)
                    Debug.Print UBound(g.Data4)
                    g.Data4(3) = 200
                    Debug.Print g.Data4(3)

                    ' Eine UDT-Wertkopie kopiert auch ihre Arrays -- auch bei einem importierten
                    ' Record, dessen Grenzen aus der Typbibliothek stammen.
                    Dim h As stdole.GUID
                    h = g
                    h.Data4(3) = 5
                    Debug.Print g.Data4(3)
                    Debug.Print h.Data4(3)
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Byte", "0", "7", "200", "200", "5" },
                VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Analyze_StillRejectsANameTheControlLibraryDoesNotDefine()
    {
        if (!OperatingSystem.IsWindows() ||
            Type.GetTypeFromProgID("MSComctlLib.TreeCtrl.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Control-library import validation requires a registered MSCOMCTL.OCX.");
            }

            Assert.Inconclusive("The registered MSCOMCTL.OCX fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6RegisteredControlLibrary",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Probe.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Probe"
                Object={MSComctlLibraryId}#2.0#0; MSCOMCTL.OCX
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim missing As MSComctlLib.GibtsNicht
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            var diagnostics = analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics).ToArray();

            // Importing the whole library must not turn the library prefix into a wildcard --
            // otherwise the gain would be an unnoticed loss of every misspelling.
            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(
                diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0003"),
                string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Code)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
