namespace VB6.Compiler.Tests;

[TestClass]
public sealed class AsNewExecutionTests
{
    [TestMethod]
    public void EmitManagedProject_InitializesLocalAsNewClassDeclarators()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNew.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNew"
                Class=Counter; Counter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private currentValue As Long

                Private Sub Class_Initialize()
                    currentValue = 7
                End Sub

                Public Property Get Value() As Long
                    Value = currentValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As New Counter
                    Debug.Print item.Value
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            var output = VB6TestProgram.RunProject(projectPath);
            Assert.AreEqual("7", output.Trim());
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
