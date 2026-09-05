namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Class_Terminate</c> must follow the ownership of generated local storage rather than the
/// collector's schedule. The shutdown register remains a fallback for boundaries not yet counted,
/// but a normal Set-to-Nothing and a procedure return are observable at their actual boundaries.
/// </summary>
[TestClass]
public sealed class ClassTerminateGuaranteeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RunsEveryClassTerminate()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateRuns", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Leben.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Leben"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Public Etikett As String

                Private Sub Class_Terminate()
                    Debug.Print "Terminate " & Etikett
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim x As C
                    Set x = New C
                    x.Etikett = "eins"

                    Dim alias As C
                    Set alias = x
                    Set x = Nothing
                    Debug.Print "Alias lebt"
                    Set alias = alias
                    Debug.Print "Selbst lebt"
                    Set alias = Nothing

                    Dim y As C
                    Set y = New C
                    y.Etikett = "zwei"

                    Debug.Print "Ende"
                End Sub
                """);

            var lines = VB6TestProgram.SplitLines(VB6TestProgram.RunProject(projectPath));

            // The alias keeps x alive after its first slot is cleared; self-assignment must also
            // retain before release. y remains alive until Main releases its local storage.
            CollectionAssert.AreEqual(
                new[] { "Alias lebt", "Selbst lebt", "Terminate eins", "Ende", "Terminate zwei" },
                lines);
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
    public void EmitManagedApplication_DoesNotTerminateAClassWhoseInitializerFailed()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateInit", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Init.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Init"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);

            // In VB6 gilt ein Objekt, dessen Class_Initialize einen Fehler auslöst, als nie
            // erzeugt -- es bekommt kein Terminate. Deshalb meldet der Konstruktor die Instanz
            // erst an, nachdem der Initialisierer durch ist.
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Initialize()
                    Err.Raise 5
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    On Error Resume Next
                    Dim x As C
                    Set x = New C
                    Debug.Print Err.Number
                    Debug.Print "Ende"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "5", "Ende" },
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
    public void EmitManagedApplication_ReleasesAClassFieldAfterItsOwnerTerminates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateFields", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Fields.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Fields"
                Class=Child; Child.cls
                Class=Holder; Holder.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Child.cls"), """
                Option Explicit

                Public Name As String

                Private Sub Class_Terminate()
                    Debug.Print "Terminate child " & Name
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Holder.cls"), """
                Option Explicit

                Public Current As Child

                Private Sub Class_Terminate()
                    Debug.Print "Terminate holder"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim holder As Holder
                    Dim child As Child
                    Set holder = New Holder
                    Set child = New Child
                    child.Name = "eins"
                    Set holder.Current = child
                    Set holder.Current = holder.Current
                    Set child = Nothing
                    Debug.Print "Holder besitzt child"
                    Set holder = Nothing
                    Debug.Print "Danach"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Holder besitzt child",
                    "Terminate holder",
                    "Terminate child eins",
                    "Danach"
                },
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
    public void EmitManagedApplication_TerminatesAClassWhenItsGlobalSlotIsCleared()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateGlobal", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Global.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Global"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Public Stored As C

                Sub Main()
                    Set Stored = New C
                    Debug.Print "Gespeichert"
                    Set Stored = Nothing
                    Debug.Print "Geloescht"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Gespeichert", "Terminate", "Geloescht" },
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
    public void EmitManagedApplication_TransfersAClassFunctionResultIntoItsCaller()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateReturn", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Return.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Return"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Function Create() As C
                    Dim local As C
                    Set local = New C
                    Set Create = local
                End Function

                Sub Main()
                    Dim received As C
                    Set received = Create()
                    Debug.Print "Erhalten"
                    Set received = Nothing
                    Debug.Print "Geloescht"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Erhalten", "Terminate", "Geloescht" },
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
    public void EmitManagedApplication_SeparatesByRefAndByValClassOwnership()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateParameters", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Parameters.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Parameters"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Public Label As String

                Private Sub Class_Terminate()
                    Debug.Print "Terminate " & Label
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub ClearByRef(ByRef value As C)
                    Set value = Nothing
                End Sub

                Sub ClearByVal(ByVal value As C)
                    Set value = Nothing
                    Debug.Print "ByVal frei"
                End Sub

                Sub Main()
                    Dim byRefValue As C
                    Set byRefValue = New C
                    byRefValue.Label = "ByRef"
                    ClearByRef byRefValue
                    Debug.Print "Nach ByRef"

                    Dim byValValue As C
                    Set byValValue = New C
                    byValValue.Label = "ByVal"
                    ClearByVal byValValue
                    Debug.Print "Caller lebt"
                    Set byValValue = Nothing
                    Debug.Print "Nach ByVal"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Terminate ByRef",
                    "Nach ByRef",
                    "ByVal frei",
                    "Caller lebt",
                    "Terminate ByVal",
                    "Nach ByVal"
                },
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
    public void EmitManagedApplication_TracksTypedClassArrayElements()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateArray", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Array.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Array"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim values() As C
                    ReDim values(1 To 1)
                    Set values(1) = New C
                    Set values(1) = values(1)
                    Debug.Print "Array besitzt"
                    Set values(1) = Nothing
                    Debug.Print "Array geloescht"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Array besitzt", "Terminate", "Array geloescht" },
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
    public void EmitManagedApplication_TracksGeneratedObjectsInVariantStorage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateVariant", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Variant.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Variant"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim value As Variant
                    Set value = New C
                    Set value = value
                    Debug.Print "Variant besitzt"
                    Set value = Nothing
                    Debug.Print "Variant geloescht"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Variant besitzt", "Terminate", "Variant geloescht" },
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
    public void EmitManagedApplication_TracksGeneratedObjectsInVariantArrayElements()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateVariantArray", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantArray.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantArray"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim typed() As Variant
                    ReDim typed(0 To 0)
                    Set typed(0) = New C
                    Set typed(0) = typed(0)
                    Set typed(0) = Nothing

                    Dim holder As Variant
                    holder = Array(Empty)
                    Set holder(0) = New C
                    Set holder(0) = holder(0)
                    Set holder(0) = Nothing
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Terminate", "Terminate" },
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
    public void EmitManagedApplication_ReleasesClassArrayElementsOnReDimAndErase()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateArrayStorage", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ArrayStorage.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ArrayStorage"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim values() As C
                    ReDim values(0 To 0)
                    Set values(0) = New C
                    Debug.Print "Vor ReDim"
                    ReDim values(0 To 1)
                    Debug.Print "Nach ReDim"

                    Set values(0) = New C
                    Debug.Print "Vor Erase"
                    Erase values
                    Debug.Print "Nach Erase"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Vor ReDim", "Terminate", "Nach ReDim",
                    "Vor Erase", "Terminate", "Nach Erase"
                },
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
    public void EmitManagedApplication_ReleasesClassArrayElementsWhenAVariantIsReplaced()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateVariantArrayStorage", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantArrayStorage.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantArrayStorage"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim holder As Variant
                    holder = Array(Empty)
                    Set holder(0) = New C
                    Debug.Print "Variant besitzt"
                    holder = Empty
                    Debug.Print "Variant ersetzt"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Variant besitzt", "Terminate", "Variant ersetzt" },
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
    public void EmitManagedApplication_ReleasesClassArrayFieldsAfterTheirOwnerTerminates()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateArrayField", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ArrayField.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ArrayField"
                Class=C; C.cls
                Class=Bag; Bag.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate child"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Bag.cls"), """
                Option Explicit

                Public Items(1 To 1) As Variant

                Public Sub SetChild()
                    Set Items(1) = New C
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate holder"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim bag As Bag
                    Set bag = New Bag
                    bag.SetChild
                    Debug.Print "Bag besitzt child"
                    Set bag = Nothing
                    Debug.Print "Danach"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Bag besitzt child", "Terminate holder", "Terminate child", "Danach"
                },
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
    public void EmitManagedApplication_TransfersAClassHeldByAVariantFunctionResult()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateVariantReturn", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "VariantReturn.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="VariantReturn"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Function Create() As Variant
                    Dim local As C
                    Set local = New C
                    Set Create = local
                End Function

                Sub Main()
                    Dim value As Variant
                    Set value = Create()
                    Debug.Print "Erhalten"
                    Set value = Nothing
                    Debug.Print "Geloescht"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Erhalten", "Terminate", "Geloescht" },
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
