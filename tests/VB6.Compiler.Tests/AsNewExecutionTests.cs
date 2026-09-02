namespace VB6.Compiler.Tests;

[TestClass]
public sealed class AsNewExecutionTests
{
    [TestMethod]
    public void EmitManagedProject_DefersAsNewClassActivationUntilTheFirstRead()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNewDeferred.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNewDeferred"
                Class=Counter; Counter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private currentValue As Long

                Private Sub Class_Initialize()
                    currentValue = 7
                    Debug.Print "initialized"
                End Sub

                Public Property Get Value() As Long
                    Value = currentValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As New Counter
                    Debug.Print "before"
                    Debug.Print item.Value
                    Debug.Print item.Value
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "before", "initialized", "7", "7" },
                VB6TestProgram.SplitLines(output),
                output);
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
    public void EmitManagedProject_ReactivatesLocalAsNewAfterSetNothing()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNewReactivate.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNewReactivate"
                Class=Counter; Counter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private currentValue As Long

                Private Sub Class_Initialize()
                    currentValue = 7
                    Debug.Print "initialized"
                End Sub

                Public Property Get Value() As Long
                    Value = currentValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As New Counter
                    Debug.Print "before"
                    Set item = Nothing
                    Debug.Print item.Value
                    Set item = Nothing
                    Debug.Print item.Value
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "before", "initialized", "7", "initialized", "7" },
                VB6TestProgram.SplitLines(output),
                output);
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
    public void EmitManagedProject_ActivatesLocalAsNewBeforePassingItByRef()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNewByRef.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNewByRef"
                Class=Counter; Counter.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Counter.cls"), """
                Option Explicit

                Private currentValue As Long

                Private Sub Class_Initialize()
                    currentValue = 7
                    Debug.Print "initialized"
                End Sub

                Public Property Get Value() As Long
                    Value = currentValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private Sub Observe(ByRef item As Counter)
                    Debug.Print item.Value
                End Sub

                Sub Main()
                    Dim item As New Counter
                    Debug.Print "before"
                    Observe item
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[] { "before", "initialized", "7" },
                VB6TestProgram.SplitLines(output),
                output);
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
