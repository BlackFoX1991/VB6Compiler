namespace VB6.Compiler.Tests;

/// <summary>
/// Module variables outlive procedure calls and therefore need storage initialized before any
/// generated module code observes them. CLR static fields already cover numeric zero values, but
/// VB6 Strings start as "" and fixed arrays exist with their declared bounds from startup.
/// </summary>
[TestClass]
public sealed class ModuleStorageInitializationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ModuleStringStartsAsEmptyString()
    {
        var lines = VB6TestProgram.RunLines("""
            Private Label As String

            Sub Main()
                Debug.Print "[" & UCase(Label) & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "[]" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_ModuleFixedArrayExistsBeforeMain()
    {
        var lines = VB6TestProgram.RunLines("""
            Public Values(1 To 2) As Long
            Private Names(-1 To 0) As String

            Sub Main()
                Debug.Print Values(1)
                Values(2) = 42
                Debug.Print Values(2)
                Debug.Print LBound(Names)
                Debug.Print UBound(Names)
                Debug.Print "[" & UCase(Names(-1)) & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "0", "42", "-1", "0", "[]" }, lines);
    }

    [TestMethod]
    public void EmitManagedApplication_InitializesPublicModuleStorageInItsDeclaringModule()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerModuleStorageTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var projectPath = Path.Combine(directory, "ModuleStorage.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ModuleStorage"
                Module=StateModule; State.bas
                Module=MainModule; Main.bas
                """);
            File.WriteAllText(Path.Combine(directory, "State.bas"), """
                Public Values(3 To 4) As Long
                Public Caption As String
                """);
            File.WriteAllText(Path.Combine(directory, "Main.bas"), """
                Sub Main()
                    Debug.Print LBound(Values)
                    Debug.Print UBound(Values)
                    Values(4) = 7
                    Debug.Print Values(4)
                    Debug.Print "[" & UCase(Caption) & "]"
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "3", "4", "7", "[]" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray(),
                standardOutput);
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
