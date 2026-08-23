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

    [TestMethod]
    public void EmitManagedProject_UsesItemDefaultPropertyThroughVariant()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantDefaultItemTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantDefaultItem.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantDefaultItem"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal index As Long) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal index As Long, ByVal value As String)
                    stored = value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value(2) = "through Variant"
                    Debug.Print value(2)
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
                new[] { "through Variant" },
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

    [TestMethod]
    public void EmitManagedProject_PreservesStringKeysForVariantDefaultProperties()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantStringKeyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantStringKey.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantStringKey"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal key As String) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value("legacy") = "default"
                    Debug.Print value("legacy")
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
                new[] { "legacy:default" },
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

    [TestMethod]
    public void EmitManagedProject_UsesDefaultPropertyThroughObject()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerObjectDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ObjectDefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ObjectDefaultProperty"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal key As String) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Object
                    Set value = New Bucket
                    value("object") = "default"
                    Debug.Print value("object")
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
                new[] { "object:default" },
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

    [TestMethod]
    public void EmitManagedProject_UsesNamedDefaultPropertyThroughVariant()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerVariantNamedDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantNamedDefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantNamedDefaultProperty"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                VERSION 1.0 CLASS
                BEGIN
                  MultiUse = -1
                END
                Attribute VB_Name = "Bucket"
                Attribute Text.VB_UserMemId = 0

                Private stored As String

                Public Property Get Text(ByVal key As String) As String
                    Text = stored
                End Property

                Public Property Let Text(ByVal key As String, ByVal value As String)
                    stored = key & ":" & value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim value As Variant
                    Set value = New Bucket
                    value("named") = "default"
                    Debug.Print value("named")
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
                new[] { "named:default" },
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
