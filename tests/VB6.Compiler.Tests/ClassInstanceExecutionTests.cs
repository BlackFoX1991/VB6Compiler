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
    public void EmitManagedApplication_AllowsWithOverIndexedClassProperty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerIndexedWithTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "IndexedWith.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="IndexedWith"
                Class=Item; Item.cls
                Class=Items; Items.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Item.cls"), """
                Option Explicit

                Private current As Long

                Public Property Get Value() As Long
                    Value = current
                End Property

                Public Property Let Value(ByVal newValue As Long)
                    current = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "Items.cls"), """
                Option Explicit

                Private value As Item

                Private Sub Class_Initialize()
                    Set value = New Item
                End Sub

                Public Property Get Item(ByVal index As Long) As Item
                    Set Item = value
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim items As Items
                    Set items = New Items
                    With items.Item(1)
                        .Value = 42
                        Debug.Print .Value
                    End With
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            CollectionAssert.AreEqual(
                new[] { "42" },
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

    [TestMethod]
    public void EmitManagedApplication_UsesItemAsAnImplicitDefaultProperty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "DefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="DefaultProperty"
                Class=Bucket; Bucket.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Bucket.cls"), """
                Private stored As String

                Public Property Get Item(ByVal index As Long) As String
                    Item = stored
                End Property

                Public Property Let Item(ByVal index As Long, ByVal newValue As String)
                    stored = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim bucket As Bucket
                    Set bucket = New Bucket
                    bucket(2) = "hello"
                    Debug.Print bucket(2)
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(new[] { "hello" }, VB6TestProgram.SplitLines(standardOutput));
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
    public void EmitManagedApplication_UsesVBUserMemIdForANamedDefaultProperty()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerNamedDefaultPropertyTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "NamedDefaultProperty.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="NamedDefaultProperty"
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

                Public Property Get Text(ByVal index As Long) As String
                    Text = stored
                End Property

                Public Property Let Text(ByVal index As Long, ByVal newValue As String)
                    stored = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Public Sub Main()
                    Dim bucket As Bucket
                    Set bucket = New Bucket
                    bucket(3) = "metadata"
                    Debug.Print bucket(3)
                End Sub
                """);

            var standardOutput = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(new[] { "metadata" }, VB6TestProgram.SplitLines(standardOutput));
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
    public void EmitManagedProject_SeparatesLetFromSetAndReportsTheVb6TypeName()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerClassLetSetTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "LetSet.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="LetSet"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                Option Explicit

                Private stored As Long

                Public Property Get Value() As Long
                    Value = stored
                End Property

                Public Property Let Value(ByVal newValue As Long)
                    stored = newValue
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim a As Box
                    Dim b As Box
                    Dim v As Variant

                    Set a = New Box
                    a.Value = 5
                    Set b = a
                    b.Value = 9

                    Debug.Print a.Value
                    Debug.Print (a Is b)

                    Set v = a
                    Debug.Print VarType(v)
                    Debug.Print TypeName(v)
                    Debug.Print TypeName(a)

                    v = a.Value
                    Debug.Print VarType(v)
                    Debug.Print v
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            // Set teilt die Referenz -- die Schreibung ueber b ist durch a sichtbar und
            // "Is" bestaetigt dieselbe Instanz. Let kopiert dagegen den Wert: v traegt
            // danach Long (3), nicht mehr das Objekt (9).
            //
            // TypeName muss den VB6-Namen liefern. Der Emitter praefixt jeden erzeugten Typ
            // (__vb6_class_Box), damit VB6-Namen nicht kollidieren; ohne Ruecknahme des
            // Praefixes wird sein Namensschema zu beobachtbarem Programmverhalten.
            CollectionAssert.AreEqual(
                new[] { "9", "True", "9", "Box", "Box", "3", "9" },
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
    public void EmitManagedProject_ReadsAndWritesPublicClassFieldsAcrossModules()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerPublicFieldTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "Fields.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="Fields"
                Class=IShape; IShape.cls
                Class=Square; Square.cls
                Class=Bag; Bag.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "IShape.cls"), """
                Option Explicit

                Public Function Area() As Long
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Square.cls"), """
                Option Explicit

                Implements IShape

                Public Side As Long

                Private Function IShape_Area() As Long
                    IShape_Area = Side * Side
                End Function
                """);
            File.WriteAllText(Path.Combine(directory, "Bag.cls"), """
                Option Explicit

                Public Label As String
                Public Count As Long
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private Type Point
                    X As Long
                    Y As Long
                End Type

                Sub Main()
                    Dim s As Square
                    Dim bag As Bag
                    Dim shape As IShape
                    Dim p As Point

                    Set bag = New Bag
                    bag.Label = "hallo"
                    bag.Count = 3
                    Debug.Print bag.Label
                    Debug.Print bag.Count

                    Set s = New Square
                    s.Side = 4
                    Set shape = s
                    Debug.Print shape.Area
                    Debug.Print s.Side

                    p.X = 3
                    p.Y = 4
                    Debug.Print p.X + p.Y
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            // Ein Klassenempfaenger ist bereits eine Referenz: ldfld/stfld brauchen das
            // Objekt selbst. Vorher lud der Emitter die Adresse des Slots und las am
            // falschen Offset -- der Zugriff endete in einer Zugriffsverletzung, sobald das
            // Feld ueberhaupt sichtbar war. Der UDT-Fall am Ende deckt die Gegenrichtung ab:
            // ein Werttyp braucht weiterhin die Adresse.
            CollectionAssert.AreEqual(
                new[] { "hallo", "3", "16", "4", "7" },
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
    public void EmitManagedProject_WritesBackByRefThroughAPublicClassField()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerFieldByRefTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "ByRef.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="ByRefFields"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                Option Explicit

                Public N As Long
                Public V As Variant

                Private guarded As Long

                Public Property Get Computed() As Long
                    Computed = guarded
                End Property

                Public Property Let Computed(ByVal newValue As Long)
                    guarded = newValue
                End Property

                Public Sub BumpMine()
                    Bump Me.N
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private Type Pt
                    X As Long
                End Type

                Public Sub Bump(ByRef value As Long)
                    value = value + 1
                End Sub

                Public Sub BumpVariant(ByRef value As Variant)
                    value = value + 1
                End Sub

                Sub Main()
                    Dim b As Box
                    Dim p As Pt

                    Set b = New Box

                    b.N = 5
                    Bump b.N
                    Debug.Print b.N

                    b.N = 5
                    b.BumpMine
                    Debug.Print b.N

                    b.V = 5
                    BumpVariant b.V
                    Debug.Print b.V

                    b.Computed = 5
                    Bump b.Computed
                    Debug.Print b.Computed

                    p.X = 5
                    Bump p.X
                    Debug.Print p.X
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            // Ein Public-Feld einer Klasse ist echter Speicher und muss das
            // ByRef-Rueckschreiben empfangen -- von aussen wie ueber Me. Vorher wurde es
            // still verworfen, weil der Binder den Zugriff wie eine Property behandelte und
            // einen Temp anlegte: das Ergebnis war 5 statt 6, ohne jede Diagnose.
            //
            // Die beiden letzten Zeilen sind die Gegenprobe: Ein echtes Property Get/Let
            // besitzt keinen Speicherplatz und behaelt den Temp (5), ein UDT-Member
            // schreibt wie bisher zurueck (6).
            CollectionAssert.AreEqual(
                new[] { "6", "6", "6", "5", "6" },
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
    public void EmitManagedProject_AssignsObjectsToPublicClassFieldsWithSet()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerFieldSetTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "FieldSet.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="FieldSet"
                Class=Src; Src.cls
                Class=Node; Node.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Src.cls"), """
                Option Explicit

                Public Event Fired()

                Public Sub Go()
                    RaiseEvent Fired
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "Node.cls"), """
                Option Explicit

                Public NextNode As Node
                Public Bag As Object
                Public Payload As Variant
                Public Tag As String

                Private WithEvents watched As Src

                Public Sub Link()
                    Set Me.NextNode = New Node
                    Me.NextNode.Tag = "verlinkt"
                End Sub

                Public Sub Watch()
                    Set watched = New Src
                    watched.Go
                End Sub

                Private Sub watched_Fired()
                    Debug.Print "gehoert"
                End Sub
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim head As Node

                    Set head = New Node

                    Set head.NextNode = New Node
                    head.NextNode.Tag = "zwei"
                    Debug.Print head.NextNode.Tag
                    Debug.Print (head.NextNode Is Nothing)

                    Set head.Bag = New Collection
                    head.Bag.Add "x"
                    Debug.Print head.Bag.Count

                    Set head.Payload = New Collection
                    Debug.Print TypeName(head.Payload)

                    Set head.NextNode = Nothing
                    Debug.Print (head.NextNode Is Nothing)

                    head.Link
                    Debug.Print head.NextNode.Tag

                    head.Watch
                End Sub
                """);

            var analysis = VBProjectCompilation.Create(projectPath).Analyze();
            Assert.IsTrue(
                analysis.Success,
                string.Join(
                    Environment.NewLine,
                    analysis.ProjectDiagnostics.Select(diagnostic => diagnostic.ToString())
                        .Concat(analysis.Diagnostics.Select(diagnostic => diagnostic.ToString()))));

            // Ein Feld, das eine Objektreferenz tragen kann, wird in VB6 mit Set zugewiesen.
            // Vorher meldete der Binder VB6S0064, weil die synthetisierte Property nur Get
            // und Let besass -- obwohl echter Speicher dahinterliegt.
            //
            // Die letzte Zeile ist die Gegenprobe: Eine WithEvents-Variable bekommt bewusst
            // KEINEN Set-Accessor. Sonst bindet schon "Set watched = New Src" innerhalb der
            // Klasse an die Property und umgeht die Verdrahtung der Ereignishandler -- der
            // Handler feuerte dann nicht mehr.
            CollectionAssert.AreEqual(
                new[] { "zwei", "False", "1", "Collection", "True", "verlinkt", "gehoert" },
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
