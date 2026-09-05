namespace VB6.Compiler.Tests;

/// <summary>
/// The declaration forms the grammar inventory had not yet executed.
///
/// All of them measured correct on the first run, which is the usual outcome here — the deliverable
/// is the running program, not a change. They are worth having because each is a spelling that a
/// refactor could quietly drop: a <c>Friend</c> that stops being callable, a bracketed reserved
/// word that starts being a keyword again, an <c>Enum</c> member that stops continuing from the
/// previous value.
/// </summary>
[TestClass]
public sealed class RemainingDeclarationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_AcceptsGlobalAndFriendDeclarations()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Global Wide As Long

            Friend Function Helper(ByVal value As Long) As Long
                Helper = value + 1
            End Function

            Public Sub Main()
                Wide = 4
                Debug.Print "global|" & Wide
                Debug.Print "friend|" & Helper(1)
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "global|4", "friend|2" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_TreatsABracketedReservedWordAsAnIdentifier()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Sub Main()
                Dim [Select] As Long
                [Select] = 3
                Debug.Print "bracketed|" & [Select]
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "bracketed|3" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_ContinuesImplicitEnumValuesAndKeepsFixedMemberWidth()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Public Enum Colour
                Red = 1
                Green
            End Enum

            Private Type Point
                X As Long
                Label As String * 4
            End Type

            Public Sub Main()
                Dim p As Point
                p.X = 2
                p.Label = "ab"
                Debug.Print "enum|" & Red & "," & Green
                Debug.Print "type|" & p.X & "|[" & p.Label & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "enum|1,2", "type|2|[ab  ]" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_CollectsParamArrayArgumentsAndAppliesOptionalDefaults()
    {
        var output = VB6TestProgram.RunLines("""
            Option Explicit

            Private Function Total(ParamArray values() As Variant) As Long
                Dim i As Long
                For i = LBound(values) To UBound(values)
                    Total = Total + values(i)
                Next i
            End Function

            Private Function WithDefault(Optional ByVal value As Long = 7) As Long
                WithDefault = value
            End Function

            Public Sub Main()
                Debug.Print "paramarray|" & Total(1, 2, 3)
                Debug.Print "empty|" & Total()
                Debug.Print "optional|" & WithDefault() & "," & WithDefault(2)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "paramarray|6", "empty|0", "optional|7,2" },
            output);
    }
}
