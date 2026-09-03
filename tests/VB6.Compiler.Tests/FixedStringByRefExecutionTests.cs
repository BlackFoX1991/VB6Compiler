namespace VB6.Compiler.Tests;

/// <summary>
/// VB6 passes a <c>String * n</c> to a <c>ByRef s As String</c> by copy-in/copy-out: the callee
/// sees an ordinary string of the declared width, and whatever it leaves behind is written back at
/// the declared width. Refusing this would be the stricter answer and the wrong one — legacy code
/// does exactly this, and the acceptance criterion is that such code compiles unchanged.
/// </summary>
[TestClass]
public sealed class FixedStringByRefExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_PassesAFixedStringByRefWithCopyInAndCopyOut()
    {
        var output = VB6TestProgram.RunLines("""
            Private Type Satz
                Feld As String * 5
            End Type

            Sub Laenge(ByRef s As String)
                Debug.Print Len(s)
            End Sub

            Sub Aendere(ByRef s As String)
                s = "xy"
            End Sub

            Sub Main()
                Dim lokal As String * 5
                Dim rec As Satz
                lokal = "abcde"
                rec.Feld = "abcde"

                ' Copy-in: der Aufgerufene sieht die volle Breite.
                Laenge lokal

                ' Copy-out: was er zurückgibt, wird wieder auf die Breite gebracht.
                Aendere lokal
                Debug.Print "[" & lokal & "]"
                Debug.Print Len(lokal)

                Aendere rec.Feld
                Debug.Print "[" & rec.Feld & "]"
                Debug.Print Len(rec.Feld)
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "5", "[xy   ]", "5", "[xy   ]", "5" },
            output);
    }

    [TestMethod]
    public void Compile_StillRejectsAByRefArgumentOfAGenuinelyWrongType()
    {
        // Die typstrenge ByRef-Regel bleibt: Sie gilt einer Variablen des falschen Typs, nicht
        // einer Zeichenkette fester Breite, für die VB6 einen eigenen Weg hat.
        var result = VBCompilation.Create("""
            Sub Aendere(ByRef s As String)
            End Sub

            Sub Main()
                Dim zahl As Long
                Aendere zahl
            End Sub
            """).Analyze();

        Assert.IsTrue(
            result.Diagnostics.Any(diagnostic => diagnostic.Code == "VB6S0008"),
            string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code)));
    }
}
