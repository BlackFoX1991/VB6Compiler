namespace VB6.Compiler.Tests;

[TestClass]
public sealed class ReplaceAndSplitShapeExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ShapesReplaceAndSplitResultsLikeVB6()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Debug.Print Replace("aXbXc", "X", "-", 3)
                Debug.Print "[" & Replace("aXbXc", "X", "-", 6) & "]"

                Dim leer As Variant
                leer = Split("", ",")
                Debug.Print LBound(leer) & "/" & UBound(leer)

                Dim eins As Variant
                eins = Split("a", ",")
                Debug.Print LBound(eins) & "/" & UBound(eins)

                ' Die Zeile, die jeder Anrufer als nächstes schreibt.
                Dim i As Long
                Dim treffer As Long
                For i = LBound(leer) To UBound(leer)
                    treffer = treffer + 1
                Next i
                Debug.Print treffer
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "b-c", "[]", "0/-1", "0/0", "0" },
            output);
    }
}
