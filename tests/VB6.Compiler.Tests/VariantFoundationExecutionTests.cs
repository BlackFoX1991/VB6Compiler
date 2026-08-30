namespace VB6.Compiler.Tests;

[TestClass]
public sealed class VariantFoundationExecutionTests
{
    [TestMethod]
    public void EmitManagedApplication_ExecutesVariantStorageAndScalarConversions()
    {
        var compilation = VBCompilation.Create("""
            Sub Consume(ByVal value As Variant)
                Debug.Print value
            End Sub

            Sub Main()
                Dim value As Variant
                Dim number As Long
                Dim values(1 To 2) As Variant

                Debug.Print value
                value = 42
                number = value
                Debug.Print number

                value = "hello"
                Debug.Print value

                values(1) = 7
                Debug.Print values(1)
                Consume 9
            End Sub
            """, "Module1.bas");
        var standardOutput = VB6TestProgram.Run(compilation);
        var lines = standardOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n')
            .Select(line => line.Trim())
            .ToArray();
        CollectionAssert.AreEqual(
            new[] { string.Empty, "42", "hello", "7", "9" },
            lines);
    }

    [TestMethod]
    public void EmitManagedApplication_RejectsNullConversionsWithVbError94()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant
                Dim typed As Long
                v = Null

                On Error Resume Next
                Debug.Print CInt(v)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print CDbl(v)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print CStr(v)
                Debug.Print Err.Number
                Err.Clear
                Debug.Print CBool(v)
                Debug.Print Err.Number
                Err.Clear
                typed = v
                Debug.Print Err.Number
            End Sub
            """);

        // Die letzte Zeile deckt den impliziten Pfad ab: Eine Zuweisung an eine
        // typisierte Variable meldet dieselbe 94 wie die ausdrueckliche Konvertierung.
        CollectionAssert.AreEqual(new[] { "94", "94", "94", "94", "94" }, output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsNullPropagationThroughOperators()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant
                v = Null

                Debug.Print IsNull(v + 1)
                Debug.Print IsNull(v * 2)
                Debug.Print IsNull(-v)
                Debug.Print IsNull(v = 1)
                Debug.Print IsNull(v < 1)
                Debug.Print IsNull(v & "x")
                If v Then
                    Debug.Print "then"
                Else
                    Debug.Print "else"
                End If
            End Sub
            """);

        // Operatoren reichen Null weiter -- ausser "&", das Null wie einen Leerstring
        // behandelt, und "If", das Null als False wertet.
        CollectionAssert.AreEqual(
            new[] { "True", "True", "True", "True", "True", "False", "else" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_PreservesVariantSubtypeTagsAcrossAssignment()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant
                Dim d As Date
                Dim c As Currency

                Debug.Print VarType(v)
                v = Null
                Debug.Print VarType(v)
                d = CDate("2026-08-30")
                v = d
                Debug.Print VarType(v)
                c = 1.2345
                v = c
                Debug.Print VarType(v)
                v = CDec("1.5")
                Debug.Print VarType(v)
                v = CVErr(13)
                Debug.Print VarType(v)
                v = Empty
                Debug.Print VarType(v)
            End Sub
            """);

        // vbEmpty, vbNull, vbDate, vbCurrency, vbDecimal, vbError, vbEmpty
        CollectionAssert.AreEqual(
            new[] { "0", "1", "7", "6", "14", "10", "0" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_KeepsDocumentedRoundingAndOverflowOnVariantConversions()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim v As Variant

                Debug.Print CLng(2.5)
                Debug.Print CLng(3.5)
                Debug.Print CLng(-2.5)
                Debug.Print CCur(2.00005)
                Debug.Print CCur(2.00015)

                On Error Resume Next
                v = CCur(1E15)
                Debug.Print Err.Number
                Err.Clear
                v = CInt(40000)
                Debug.Print Err.Number
                Err.Clear
                v = CDate("kein Datum")
                Debug.Print Err.Number
                Err.Clear
                v = CInt("keine Zahl")
                Debug.Print Err.Number
            End Sub
            """);

        // Banker's Rounding auf beiden Ebenen: CLng rundet zur geraden Zahl, Currency
        // rundet auf vier Nachkommastellen ebenso. Ueberlauf meldet 6, ein nicht
        // interpretierbares Datum 13.
        CollectionAssert.AreEqual(
            new[] { "2", "4", "-2", "2", "2.0002", "6", "6", "13", "13" },
            output);
    }
}
