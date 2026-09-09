using VB6.IR;

namespace VB6.Compiler.Tests;

[TestClass]
public sealed class AsNewExecutionTests
{
    [TestMethod]
    public void Lower_PutsTheAsNewEnsureInFrontOfAFieldAccess()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNewFieldIr.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNewFieldIr"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                Option Explicit

                Public N As Long
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Sub Main()
                    Dim item As New Box
                    item.N = 3
                End Sub
                """);

            // Die Uebersetzungsentscheidung, nicht die Ausgabe: Der Feldzugriff steht auf einem
            // Platz, und davor muss die Ensure-Form stehen. Ein Ausfuehrungstest allein wuerde
            // auch dann gruen, wenn die Instanz an einer ganz anderen Stelle entstuende.
            var program = VB6TestIr.LowerProject(projectPath);
            var main = VB6TestIr.Procedures(program)
                .Single(procedure => procedure.Name.Contains("Main", StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(
                VB6TestIr.Expressions(program).OfType<IrEnsureLocalClassExpression>().Any(),
                "Der Feldzugriff auf eine As-New-Variable traegt keine Ensure-Form.");
            Assert.IsTrue(
                main.Blocks.SelectMany(block => block.Instructions)
                    .OfType<IrEvaluateInstruction>()
                    .Any(instruction => instruction.Expression is IrEnsureLocalClassExpression),
                "Die Ensure-Form steht nicht als eigene Anweisung vor dem Zugriff.");
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
    public void EmitManagedProject_CreatesTheAsNewInstanceOnAFieldAccessToo()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerAsNewTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(directory);
            var projectPath = Path.Combine(directory, "AsNewField.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Sub Main"
                Name="AsNewField"
                Class=Box; Box.cls
                Module=MainModule; MainModule.bas
                """);

            // Vier Zugriffsformen loesen As New aus: Werteverwendung, Methodenaufruf,
            // Property Get und Feldzugriff. Die ersten drei senken ihren Empfaenger als Ausdruck
            // und kamen deshalb schon immer an der Nachinstanziierung vorbei; der Feldzugriff
            // braucht einen Platz und ging daran vorbei. Jede Form steht hier, weil der Fall
            // genau daran unentdeckt geblieben ist: Alle Tests dieser Datei benutzten ein
            // Property Get, keiner ein nacktes Public-Feld.
            File.WriteAllText(Path.Combine(directory, "Box.cls"), """
                Option Explicit

                Public N As Long
                Public Nums(2) As Long

                Private Sub Class_Initialize()
                    Debug.Print "initialized"
                End Sub

                Public Sub Bump()
                    N = N + 1
                End Sub

                Public Property Get P() As Long
                    P = 42
                End Property
                """);
            File.WriteAllText(Path.Combine(directory, "MainModule.bas"), """
                Option Explicit

                Private m As New Box

                Sub Main()
                    Dim schreiben As New Box
                    Debug.Print "before"
                    schreiben.N = 3
                    Debug.Print schreiben.N

                    Dim lesen As New Box
                    Debug.Print lesen.N

                    Dim feldArray As New Box
                    feldArray.Nums(1) = 5
                    Debug.Print feldArray.Nums(1)

                    m.N = 7
                    Debug.Print m.N

                    Dim gemischt As New Box
                    gemischt.N = 1
                    gemischt.Bump
                    Debug.Print gemischt.N
                    Debug.Print gemischt.P
                End Sub
                """);

            var output = VB6TestProgram.RunProject(projectPath);
            CollectionAssert.AreEqual(
                new[]
                {
                    "before",       // die Deklaration allein erzeugt nichts
                    "initialized",  // erst der schreibende Feldzugriff
                    "3",            // und er trifft dieselbe Instanz, die er erzeugt hat
                    "initialized",
                    "0",            // ein lesender Feldzugriff erzeugt ebenfalls
                    "initialized",
                    "5",            // ein Arrayfeld laeuft ueber denselben Weg
                    "initialized",
                    "7",            // dasselbe fuer eine Modulvariable
                    "initialized",
                    "2",            // Feld und Methode sehen dieselbe Instanz
                    "42"
                },
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
