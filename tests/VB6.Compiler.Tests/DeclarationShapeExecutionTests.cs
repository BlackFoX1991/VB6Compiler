namespace VB6.Compiler.Tests;

/// <summary>
/// The remaining declaration shapes of the grammar inventory.
///
/// <c>Property Set</c> in a standard module is the one that changed behavior here, and it changed
/// for an uncomfortable reason: once a module <c>Property Get</c> became resolvable, <c>Set x =
/// value</c> started resolving its target through the read path and handed the lowerer an
/// invocation where an assignment target belongs. The result was an internal failure, not a
/// diagnostic — a fix creating a worse failure mode than the gap it closed. The Set accessor is
/// now answered before the target is bound at all.
/// </summary>
[TestClass]
public sealed class DeclarationShapeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RoutesSetThroughTheModulePropertySet()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Backing As Collection

            Public Property Set Items(ByVal newValue As Collection)
                Set Backing = newValue
            End Property

            Public Property Get Items() As Collection
                Set Items = Backing
            End Property

            Public Sub Main()
                Dim c As Collection
                Set c = New Collection
                c.Add "x"
                c.Add "y"

                Set Items = c
                Debug.Print "count|" & Items.Count
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "count|2" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LeavesAPropertyEarlyOnExitProperty()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Property Get Early() As Long
                Early = 1
                Exit Property
                Early = 99
            End Property

            Public Sub Main()
                Debug.Print "exit|" & Early
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "exit|1" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_StopsTheProgramAtEnd()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Sub Main()
                Debug.Print "before"
                End
                Debug.Print "after"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "before" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_AcceptsEveryConstDeclarationForm()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Const ModuleWide As Long = 10
            Private Const Inferred = 2.5
            Private Const Suffixed& = 7

            Public Sub Main()
                Const Local As String = "abc"
                Debug.Print "const|" & ModuleWide & "|" & Inferred & "|" & Suffixed & "|" & Local
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "const|10|2.5|7|abc" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LeavesOnlyTheInnerLoopOnExitFor()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Sub Main()
                Dim i As Long
                Dim j As Long
                Dim hits As Long

                For i = 1 To 3
                    For j = 1 To 3
                        If j = 2 Then Exit For
                        hits = hits + 1
                    Next j
                Next i

                Debug.Print "exit-for|" & hits
            End Sub
            """);

        // Three outer rounds, one inner hit each: Exit For leaves the inner loop only.
        CollectionAssert.AreEqual(new[] { "exit-for|3" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_RejectsAnUndeclaredNameUnderOptionExplicit()
    {
        var compilation = VBCompilation.Create("""
            Option Explicit

            Public Sub Main()
                undeclared = 1
            End Sub
            """, "Module1.bas");

        var analysis = compilation.Analyze();

        Assert.IsTrue(
            analysis.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0001"),
            "Option Explicit muss eine nicht deklarierte Zuweisung melden.");
    }
}
