namespace VB6.Compiler.Tests;

[TestClass]
public sealed class DynamicForEachProjectCompilationTests
{
    [TestMethod]
    public void EmitManagedApplication_LowersDynamicArrayForEachInsideVbpProject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDynamicForEachProjectTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DynamicForEachProject.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="DynamicForEachProject"
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                    Dim item
                    Dim values() As Long
                    ReDim values(-1 To 1)
                    values(-1) = 7
                    values(0) = 8
                    values(1) = 9

                    For Each item In values
                        Debug.Print item
                    Next item
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "7", "8", "9" },
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
