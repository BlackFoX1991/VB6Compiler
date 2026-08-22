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
                Private retained As Counter

                Public Sub Run()
                    Set source = New Counter
                    source.Value = 22
                    Set retained = source
                    Set source = New Counter
                    retained.Value = 44
                    source.Value = 33
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
                new[] { "7", "7", "15", "15", "True", "True", "False", "22", "33" },
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

    [TestMethod]
    public void EmitManagedApplication_PreservesIndexedPropertyArgumentsForReadsAndWrites()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerIndexedPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "IndexedProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="IndexedProperty"
                Class=Bag; Bag.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bag.cls"), """
                Option Explicit

                Private values(0 To 3) As Long

                Public Property Get Item(ByVal index As Long) As Long
                    Item = values(index)
                End Property

                Public Property Let Item(ByVal index As Long, ByVal newValue As Long)
                    values(index) = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim bag As Bag
                    Set bag = New Bag
                    bag.Item(2) = 41
                    Debug.Print bag.Item(2)
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
                new[] { "41" },
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

    [TestMethod]
    public void AnalyzeProject_ResolvesImplementsContractsAndPrefixedMembers()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerImplementsTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Implements.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Implements"
                Class=IWorker; IWorker.cls
                Class=Worker; Worker.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "IWorker.cls"), """
                Option Explicit

                Public Sub Run(ByVal value As Long)
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Worker.cls"), """
                Option Explicit

                Implements IWorker

                Private Sub IWorker_Run(ByVal value As Long)
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));
            var worker = analysis.SemanticModel!.ClassTypes.Single(type => type.Name == "Worker");
            CollectionAssert.AreEqual(
                new[] { "IWorker" },
                worker.ImplementedInterfaces.Select(type => type.Name).ToArray());
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
    public void EmitManagedApplication_DispatchesImplementsCallThroughInterface()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerInterfaceDispatchTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "InterfaceDispatch.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="InterfaceDispatch"
                Class=IWorker; IWorker.cls
                Class=Worker; Worker.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "IWorker.cls"), """
                Option Explicit

                Public Function Run(ByVal value As Long) As Long
                End Function

                Public Property Get Value() As Long
                End Property

                Public Property Let Value(ByVal newValue As Long)
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "Worker.cls"), """
                Option Explicit

                Implements IWorker

                Private current As Long

                Private Function IWorker_Run(ByVal value As Long) As Long
                    IWorker_Run = value + 5
                End Function

                Private Property Get IWorker_Value() As Long
                    IWorker_Value = current
                End Property

                Private Property Let IWorker_Value(ByVal newValue As Long)
                    current = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim contract As IWorker
                    Set contract = New Worker
                    Debug.Print contract.Run(7)
                    contract.Value = 19
                    Debug.Print contract.Value
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
                new[] { "12", "19" },
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

    [TestMethod]
    public void AnalyzeProjectReportsMissingImplementsMember()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerMissingImplementsTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "MissingImplements.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="MissingImplements"
                Class=IWorker; IWorker.cls
                Class=Worker; Worker.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "IWorker.cls"), """
                Public Sub Run()
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Worker.cls"), """
                Implements IWorker
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Sub Main()
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsFalse(analysis.Success);
            Assert.IsTrue(analysis.ProjectDiagnostics.Any(diagnostic => diagnostic.Code == "VB6PRJ0012"));
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
