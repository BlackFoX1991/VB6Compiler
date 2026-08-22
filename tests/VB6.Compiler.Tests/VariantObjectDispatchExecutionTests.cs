namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantObjectDispatchExecutionTests
{
    [TestMethod]
    public void EmitManagedProject_DispatchesVariantPropertiesAndMethods()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantObjectDispatchTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantObjectDispatch.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantObjectDispatch"
                Class=Widget; Widget.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Widget.cls"), """
                Option Explicit

                Private current As Long

                Private Sub Class_Initialize()
                    current = 7
                End Sub

                Public Property Get Value() As Long
                    Value = current
                End Property

                Public Property Let Value(ByVal newValue As Long)
                    current = newValue
                End Property

                Public Function Add(ByVal amount As Long) As Long
                    current = current + amount
                    Add = current
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim value As Variant
                    Set value = New Widget

                    Debug.Print value.Value
                    value.Value = 10
                    Debug.Print value.Add(5)
                    Debug.Print value.Value
                End Sub
                """);

            var compilation = VBProjectCompilation.Create(projectPath);
            var analysis = compilation.Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "7", "15", "15" },
                VB6TestProgram.RunProjectLines(projectPath));
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
