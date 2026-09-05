namespace VB6.Compiler.Tests;

/// <summary>
/// <c>Property Get</c>, <c>Let</c> and <c>Set</c> declared in a standard module.
///
/// A class reaches its properties through its instance; a module has none, so the class path could
/// never answer for one. The result was worse than a plain rejection: with <c>Option Explicit</c>
/// the reference reported <c>VB6S0001</c>, and without it an implicit local of the same name was
/// created instead, so <c>Value = 5</c> wrote to a local and <c>Debug.Print Value</c> read it back.
/// The program ran and printed a plausible number that had never been through the property.
///
/// A module-level accessor is an ordinary procedure of that module, so the binder resolves a read
/// to a call of the Get and an assignment to a call of the Let. Nothing below the binder needed to
/// learn a new shape.
/// </summary>
[TestClass]
public sealed class ModulePropertyExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_RoutesAnAssignmentThroughTheModulePropertyLet()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Backing As Long

            Public Property Get Value() As Long
                Value = Backing
            End Property

            Public Property Let Value(ByVal newValue As Long)
                Backing = newValue * 2
            End Property

            Public Sub Main()
                Value = 5
                Debug.Print "property|" & Value
                Debug.Print "backing|" & Backing
            End Sub
            """);

        // The doubling is what proves the Let ran: a local would have kept the 5.
        CollectionAssert.AreEqual(new[] { "property|10", "backing|10" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReadsAModulePropertyGetWithoutALet()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Backing As Long

            Public Property Get Tripled() As Long
                Tripled = Backing * 3
            End Property

            Public Sub Main()
                Backing = 4
                Debug.Print "get|" & Tripled
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "get|12" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_LetsAVariableWinOverASameNamedModuleProperty()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Backing As Long

            Public Property Get Value() As Long
                Value = 99
            End Property

            Public Sub Main()
                Dim Value As Long
                Value = 1
                Debug.Print "local|" & Value
                Debug.Print "backing|" & Backing
            End Sub
            """);

        // A declared variable outranks everything else in name resolution, property included.
        CollectionAssert.AreEqual(new[] { "local|1", "backing|0" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_UsesAModulePropertyDeclaredBelowItsUse()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Sub Main()
                Later = 6
                Debug.Print "below|" & Later
            End Sub

            Private Backing As Long

            Public Property Get Later() As Long
                Later = Backing + 1
            End Property

            Public Property Let Later(ByVal newValue As Long)
                Backing = newValue
            End Property
            """);

        // Accessors are collected before any body is bound, so source order does not matter.
        CollectionAssert.AreEqual(new[] { "below|7" }, output);
    }
}
