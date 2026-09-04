namespace VB6.Compiler.Tests;

/// <summary>
/// A member of an IUnknown-derived interface. Such an interface answers no <c>IDispatch</c>, so its
/// members exist only as vtable slots — a call reported 438 although the type library describes the
/// member. <c>stdole.IFont</c> is the measurable case: its properties have a dispatch twin in
/// <c>IFontDisp</c>, its methods do not.
/// </summary>
[TestClass]
public sealed class ComVTableExecutionTests
{
    private const string StdOleLibraryId = "{00020430-0000-0000-C000-000000000046}";

    [TestMethod]
    public void Analyze_ReportsAVTableMemberWithAnOutParameter()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Type-library import requires Windows.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ComVTableOut",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Aus.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Aus"
                Reference=*\G{StdOleLibraryId}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);

            // IFont.Clone traegt PARAMFLAG_FOUT, nicht FRETVAL: Sein letzter Parameter ist ein
            // ByRef-Argument, in das der Server schreibt. Diese Form nimmt der vtable-Weg bewusst
            // nicht -- und sie auf dem Dispatchweg zu lassen hiesse 438 zu melden, "Member nicht
            // gefunden", fuer einen Member, den die Bibliothek beschreibt.
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim f As stdole.IFont
                    Set f = New stdole.StdFont
                    Dim g As stdole.IFont
                    f.Clone g
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            var diagnostics = analysis.Units.SelectMany(unit => unit.Analysis.Diagnostics).ToArray();

            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(
                diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0075"),
                string.Join(" | ", diagnostics.Select(diagnostic => diagnostic.ToString())));
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
    public void EmitManagedApplication_CallsAVTableOnlyInterfaceMember()
    {
        if (!OperatingSystem.IsWindows() ||
            Type.GetTypeFromProgID("StdFont", throwOnError: false) is null)
        {
            Assert.Inconclusive("The registered StdFont fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ComVTable",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VTable.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="VTable"
                Reference=*\G{StdOleLibraryId}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim f As stdole.IFont
                    Set f = New stdole.StdFont

                    ' Eine Eigenschaft: die gibt es auch auf IFontDisp, sie ginge auch ueber
                    ' IDispatch.
                    f.Name = "Courier New"
                    Debug.Print f.Name

                    ' SetRatio steht nur in der vtable von IFont. Vor dieser Karte meldete der
                    ' Aufruf 438, obwohl die Typbibliothek den Member beschreibt.
                    On Error Resume Next
                    Err.Clear
                    f.SetRatio 2540, 1440
                    Debug.Print Err.Number
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Courier New", "0" },
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
}
