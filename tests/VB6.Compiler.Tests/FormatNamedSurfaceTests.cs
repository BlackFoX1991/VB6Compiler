namespace VB6.Compiler.Tests;

/// <summary>
/// The documented named formats of <c>Format</c>, measured rather than assumed. The one that was
/// wrong is <c>General Number</c>: it showed a Double's conversion remainder because it asked for
/// 29 significant digits, which VB6 never prints.
/// </summary>
[TestClass]
public sealed class FormatNamedSurfaceTests
{
    [TestMethod]
    public void EmitManagedApplication_AppliesTheDocumentedNamedNumberFormats()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Format(1234.567, "General Number")
                Debug.Print Format(1234.567, "Fixed")
                Debug.Print Format(1234.567, "Standard")
                Debug.Print Format(0.567, "Percent")
                Debug.Print Format(1234.567, "Scientific")
                Debug.Print Format(0, "Yes/No") & "," & Format(1, "Yes/No")
                Debug.Print Format(0, "True/False") & "," & Format(1, "True/False")
                Debug.Print Format(0, "On/Off") & "," & Format(1, "On/Off")

                ' Ohne Formatangabe gilt dieselbe Regel wie für General Number.
                Debug.Print Format(1234.567)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[]
            {
                "1234.567",
                "1234.57",
                "1,234.57",
                "56.70%",
                "1.23E+03",
                "No,Yes",
                "False,True",
                "Off,On",
                "1234.567"
            },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_AppliesCustomNumericSections()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Format(1234.567, "#,##0.00")
                Debug.Print Format(1234.567, "0.0")
                Debug.Print Format(0.5, "0%")
                Debug.Print Format(12, "00000")

                ' Zwei Abschnitte: der zweite gilt negativen Werten.
                Debug.Print Format(-5, "0;(0)")

                ' Groß-/Kleinschreibung und Platzhalter auf Zeichenketten.
                Debug.Print Format("abc", ">") & "," & Format("ABC", "<")
                Debug.Print "[" & Format("12345", "@@@@@@@") & "]"
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "1,234.57", "1234.6", "50%", "00012", "(5)", "ABC,abc", "[  12345]" },
            output);
    }
}
