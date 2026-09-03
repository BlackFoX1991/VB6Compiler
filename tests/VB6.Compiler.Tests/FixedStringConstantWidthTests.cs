namespace VB6.Compiler.Tests;

/// <summary>
/// The width of a <c>String * n</c> may be a named constant, exactly as an array bound of a UDT
/// member may. Both declaration forms go through the same folder now — a width that folded in a
/// UDT member but not in a Dim would make the same source mean two different things depending on
/// where it is written.
/// </summary>
[TestClass]
public sealed class FixedStringConstantWidthTests
{
    [TestMethod]
    public void EmitManagedApplication_AcceptsANamedConstantAsTheFixedWidth()
    {
        var output = VB6TestProgram.RunLines("""
            Private Const BREITE As Long = 5
            Private Const DOPPELT As Long = BREITE * 2

            Private Type Satz
                Feld As String * BREITE
                Weit As String * DOPPELT
            End Type

            Sub Main()
                Dim lokal As String * BREITE
                Dim s As Satz

                ' Anfangswert: n Leerzeichen, in beiden Deklarationsformen.
                Debug.Print Len(lokal)
                Debug.Print Len(s.Feld)
                Debug.Print Len(s.Weit)

                ' Abschneiden beim Überschreiten.
                lokal = "abcdefgh"
                s.Feld = "abcdefgh"
                Debug.Print lokal
                Debug.Print s.Feld

                ' Auffüllen beim Unterschreiten.
                lokal = "xy"
                s.Feld = "xy"
                Debug.Print Len(lokal)
                Debug.Print Len(s.Feld)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "5", "5", "10", "abcde", "abcde", "5", "5" },
            output);
    }

    [TestMethod]
    public void Compile_StillRejectsAWidthThatDoesNotFold()
    {
        // Eine Laufzeitgröße ist keine Breite. Sie zu akzeptieren hieße, den Speicher erst zur
        // Laufzeit festzulegen -- und genau das kann ein festes Layout nicht.
        var result = VBCompilation.Create("""
            Sub Main()
                Dim n As Long
                n = 4
                Dim lokal As String * n
            End Sub
            """).Analyze();

        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0043"),
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
    }
}
