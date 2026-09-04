namespace VB6.Compiler.Tests;

/// <summary>
/// <c>New Scripting.Dictionary</c> is ordinary VB6, and it used to fail at emit time with
/// <c>VB6E0001: Class 'Scripting.Dictionary' has no managed constructor</c> — the backend has no
/// constructor for a contract it did not emit, and nobody had told it to activate the registered
/// coclass instead. Every <c>New</c> on an imported class was a compile failure.
/// </summary>
[TestClass]
public sealed class ImportedCoclassExecutionTests
{
    private const string ScriptingLibraryId = "{420B2830-E718-11CF-893D-00A0C9054228}";
    private const string StdOleLibraryId = "{00020430-0000-0000-C000-000000000046}";

    private static bool IsRegistered(string progId) =>
        OperatingSystem.IsWindows() && Type.GetTypeFromProgID(progId, throwOnError: false) is not null;

    [TestMethod]
    public void EmitManagedApplication_CreatesAndUsesAnImportedCoclass()
    {
        if (!IsRegistered("Scripting.Dictionary"))
        {
            Assert.Inconclusive("The registered Windows Scripting Runtime fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ImportedCoclass",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Neu.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Neu"
                Reference=*\G{ScriptingLibraryId}#1.0#0#scrrun.dll#Scripting
                Module=Main; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim d As Scripting.Dictionary
                    Set d = New Scripting.Dictionary
                    d.Add "a", 1
                    d.Add "b", 2
                    Debug.Print d.Count
                    Debug.Print d.Item("b")
                    Debug.Print d.Exists("a")

                    Dim fso As Scripting.FileSystemObject
                    Set fso = New Scripting.FileSystemObject
                    Debug.Print fso.GetExtensionName("C:\pfad\datei.txt")
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "2", "2", "True", "txt" },
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
    public void EmitManagedApplication_PassesACurrencyValueAcrossTheComBoundary()
    {
        if (!IsRegistered("StdFont"))
        {
            Assert.Inconclusive("The registered StdFont fixture is not available.");
            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6ComCurrency",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Schrift.vbp");
            File.WriteAllText(projectPath, $"""
                Type=Exe
                Startup="Sub Main"
                Name="Schrift"
                Reference=*\G{StdOleLibraryId}#2.0#0#stdole2.tlb#stdole
                Module=Main; Main.bas
                """);

            // stdole.FONTSIZE is VT_CY, so Size carries a VBCurrency -- a struct of this runtime
            // that cannot go into a VARIANT as it stands. The marshaller answered "cannot be
            // marshalled to a Variant. Type library is not registered", which reads like a
            // registration problem and is not one.
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Option Explicit

                Sub Main()
                    Dim f As stdole.StdFont
                    Set f = New stdole.StdFont
                    f.Name = "Courier New"
                    f.Size = 9
                    f.Bold = True
                    Debug.Print f.Name
                    Debug.Print f.Size
                    Debug.Print f.Bold
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Courier New", "9", "True" },
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
