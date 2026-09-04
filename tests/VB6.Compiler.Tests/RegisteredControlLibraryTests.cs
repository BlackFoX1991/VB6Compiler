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
