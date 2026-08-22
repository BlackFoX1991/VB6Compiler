namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ClassInstanceExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesClassFieldsMethodsPropertiesAndInitialize()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerClassInstanceTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ClassInstance.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ClassInstance"
                Class=Counter; Counter.cls
                Class=Observer; Observer.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private current As Long
                Public Event Changed(ByVal value As Long)

                Private Sub Class_Initialize()
                    current = 7
                    RaiseEvent Changed(current)
                End Sub

                Private Sub Class_Terminate()
                    current = 0
                End Sub

                Public Property Get Value() As Long
                    Value = current
                End Property

                Public Property Let Value(ByVal newValue As Long)
                    current = newValue
                    RaiseEvent Changed(current)
                End Property

                Public Function Add(ByVal amount As Long) As Long
                    current = current + amount
                    Add = current
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Observer.cls"), """
                Option Explicit

                Private WithEvents source As Counter

                Public Sub Run()
                    Set source = New Counter
                    source.Value = 22
                End Sub

                Private Sub source_Changed(ByVal value As Long)
                    Debug.Print value
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As Counter
                    Dim other As Counter
                    Set item = New Counter
                    Set other = New Counter
                    Debug.Print item.Value
                    item.Value = 10
                    Debug.Print other.Value
                    Debug.Print item.Add(5)
                    Debug.Print item.Value
                    Debug.Print TypeOf item Is Counter
                    Debug.Print item Is item
                    Debug.Print item Is other
                    Dim observer As Observer
                    Set observer = New Observer
                    observer.Run
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "7", "7", "15", "15", "True", "True", "False", "22" },
                standardOutput.Trim().Split(Environment.NewLine).Select(line => line.Trim()).ToArray());
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
