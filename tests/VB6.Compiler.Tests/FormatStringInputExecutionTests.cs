namespace VB6.Compiler.Tests;

[TestClass]
public sealed class FormatStringInputExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_FormatsANumericStringThroughItsFormat()
    {
        // Der Alltagsfall: Was aus einem Textfeld kommt, ist eine Zeichenkette, und das Muster
        // entscheidet trotzdem. Vorher lieferte das 0.00 -- der Wert verschwand still.
        var lines = VB6TestProgram.RunLines(
            """
            Sub Main()
                Debug.Print Format("12", "0.00")
                Debug.Print Format("1234.5", "#,##0.00")
                Debug.Print Format("12", "Currency")
                Debug.Print Format("abc", "0.00")
                Debug.Print Format("abc", "#,##0")
                Debug.Print Format("2026-03-04", "mmmm")
                Debug.Print Format("AB", "@@@")
            End Sub
            """);

        CollectionAssert.AreEqual(
            // Der letzte Fall trägt in der Runtime einen führenden Abstand; die E2E-Helfer
            // trimmen bewusst, deshalb steht die Spaltenform in FormatStringInputTests.
            new[] { "12.00", "1,234.50", "$12.00", "abc", "abc", "March", "AB" },
            lines);
    }
}
