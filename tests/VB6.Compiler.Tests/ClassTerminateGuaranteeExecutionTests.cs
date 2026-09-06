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

    [TestMethod]
    public void EmitManagedApplication_KeepsAWithEventsSinkAliveUntilItsSourceDetaches()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateWithEvents", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "WithEvents.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="WithEvents"
                Class=Quelle; Quelle.cls
                Class=Senke; Senke.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Quelle.cls"), """
                Option Explicit

                Public Event Signal()

                Public Sub Ausloesen()
                    RaiseEvent Signal
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate Quelle"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Senke.cls"), """
                Option Explicit

                Private WithEvents aktuelleQuelle As Quelle

                Public Sub Verbinde(ByVal value As Quelle)
                    Set aktuelleQuelle = value
                End Sub

                Private Sub aktuelleQuelle_Signal()
                    Set aktuelleQuelle = aktuelleQuelle
                    Debug.Print "Signal"
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate Senke"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim quelle As Quelle
                    Dim senke As Senke
                    Set quelle = New Quelle
                    Set senke = New Senke
                    senke.Verbinde quelle

                    Set senke = Nothing
                    Debug.Print "Nur Ereignis besitzt Senke"
                    quelle.Ausloesen
                    Debug.Print "Vor Programmende"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Nur Ereignis besitzt Senke", "Signal", "Vor Programmende",
                    "Terminate Senke", "Terminate Quelle"
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
    public void EmitManagedApplication_TracksGeneratedObjectsInCollections()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateCollection", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Collection.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Collection"
                Class=C; C.cls
                Class=Bag; Bag.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Private Sub Class_Terminate()
                    Debug.Print "Terminate C"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Bag.cls"), """
                Option Explicit

                Private entries As Collection

                Private Sub Class_Initialize()
                    Set entries = New Collection
                End Sub

                Public Sub AddChild()
                    entries.Add New C
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate Bag"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim entries As Collection
                    Dim child As C
                    Set entries = New Collection
                    Set child = New C
                    entries.Add child
                    Set child = Nothing
                    Debug.Print "Collection besitzt"
                    entries.Remove 1
                    Debug.Print "Collection entfernt"

                    entries.Add New C
                    Debug.Print "New uebernommen"
                    entries.Remove 1
                    Debug.Print "New entfernt"

                    entries.Add New C
                    Walk entries
                    Debug.Print "For Each beendet"
                    entries.Remove 1
                    Debug.Print "For Each entfernt"

                    Dim bag As Bag
                    Set bag = New Bag
                    bag.AddChild
                    Debug.Print "Bag besitzt"
                    Set bag = Nothing
                    Debug.Print "Bag entfernt"
                End Sub

                Sub Walk(ByVal values As Collection)
                    Dim entry As Variant
                    For Each entry In values
                        Debug.Print "For Each besitzt"
                    Next entry
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Collection besitzt", "Terminate C", "Collection entfernt",
                    "New uebernommen", "Terminate C", "New entfernt",
                    "For Each besitzt", "For Each beendet", "Terminate C", "For Each entfernt",
                    "Bag besitzt", "Terminate Bag", "Terminate C", "Bag entfernt"
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
    public void EmitManagedApplication_DoesNotRunClassTerminateAfterEnd()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateEnd", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "End.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="End"
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
                    Dim value As C
                    Set value = New C
                    Debug.Print "Vor End"
                    End
                End Sub
                """);

            // End ends the process, so the execution helper observes the actual ProcessExit
            // path rather than an in-process host approximation.
            CollectionAssert.AreEqual(
                new[] { "Vor End" },
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
    public void EmitManagedApplication_TracksObjectsAcrossVariantAndArrayParameterBoundaries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateParameterStorage", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ParameterStorage.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ParameterStorage"
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

                Sub ClearVariant(ByRef value As Variant)
                    Set value = Nothing
                End Sub

                Sub InspectVariant(ByVal value As Variant)
                    Debug.Print "ByVal besitzt"
                End Sub

                Sub ClearArrayElement(ByRef values() As C)
                    Set values(0) = Nothing
                End Sub

                Function CreateArray() As Variant
                    Dim values As Variant
                    values = Array(Empty)
                    Set values(0) = New C
                    values(0).Label = "Rueckgabe"
                    CreateArray = values
                End Function

                Sub Main()
                    Dim value As Variant
                    Set value = New C
                    value.Label = "ByRef Variant"
                    ClearVariant value
                    Debug.Print "ByRef frei"

                    Set value = New C
                    value.Label = "ByVal Variant"
                    InspectVariant value
                    Debug.Print "Caller besitzt"
                    Set value = Nothing
                    Debug.Print "ByVal frei"

                    Dim returned As Variant
                    returned = CreateArray()
                    Debug.Print "Rueckgabe besitzt"
                    Set returned(0) = Nothing
                    Debug.Print "Rueckgabe frei"

                    Dim direct() As C
                    ReDim direct(0 To 0)
                    Set direct(0) = New C
                    direct(0).Label = "ByRef Array"
                    ClearArrayElement direct
                    Debug.Print "Array frei"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Terminate ByRef Variant", "ByRef frei",
                    "ByVal besitzt", "Caller besitzt", "Terminate ByVal Variant", "ByVal frei",
                    "Rueckgabe besitzt", "Terminate Rueckgabe", "Rueckgabe frei",
                    "Terminate ByRef Array", "Array frei"
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
    public void EmitManagedApplication_ReleasesObjectsAfterAHandledError()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateHandledError", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "HandledError.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="HandledError"
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

                Sub Recover()
                    Dim local As C
                    Set local = New C
                    On Error GoTo Failed
                    Err.Raise 5
                    Debug.Print "Nicht erreicht"
                    Exit Sub
                Failed:
                    Debug.Print "Handler"
                End Sub

                Sub Main()
                    Recover
                    Debug.Print "Danach"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Handler", "Terminate", "Danach" },
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
    public void EmitManagedApplication_TerminatesAFieldReferenceCycleOnceAtProgramEnd()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateCycle", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Cycle.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Cycle"
                Class=Node; Node.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Node.cls"), """
                Option Explicit

                Public Label As String
                Public NextNode As Node

                Private Sub Class_Terminate()
                    Debug.Print "Terminate " & Label
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim first As Node
                    Dim second As Node
                    Set first = New Node
                    first.Label = "first"
                    Set second = New Node
                    second.Label = "second"
                    Set first.NextNode = second
                    Set second.NextNode = first
                    Set first = Nothing
                    Set second = Nothing
                    Debug.Print "Keine externen Referenzen"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[] { "Keine externen Referenzen", "Terminate second", "Terminate first" },
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
    public void EmitManagedApplication_TracksDeferredAsNewStorage()
    {
        var directory = Path.Combine(Path.GetTempPath(), "VB6TerminateAsNew", Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNew.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNew"
                Class=C; C.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "C.cls"), """
                Option Explicit

                Public Sub Touch()
                End Sub

                Private Sub Class_Terminate()
                    Debug.Print "Terminate"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Public Shared As New C

                Sub Main()
                    Dim local As New C
                    local.Touch
                    Shared.Touch
                    Debug.Print "Aktiviert"
                    Set local = Nothing
                    Set Shared = Nothing
                    Debug.Print "Freigegeben"
                    local.Touch
                    Shared.Touch
                    Debug.Print "Reaktiviert"
                End Sub
                """);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Aktiviert", "Terminate", "Terminate", "Freigegeben",
                    "Reaktiviert", "Terminate", "Terminate"
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
}
