namespace VB6.Compiler.Tests;

/// <summary>
/// Fehlernummern, die VB6 ausdruecklich vergibt und die vorher unter dem Sammelwert 5 lagen.
/// Die 5 ist in <c>VBErrors.Set</c> der Rueckfall fuer jede nicht zugeordnete Ausnahme, deshalb
/// sieht ein falsches 5 wie ein Ergebnis aus. Jeder Fall hier ist gemessen, keiner hergeleitet.
/// </summary>
[TestClass]
public sealed class DocumentedErrorNumberExecutionTests
{
    /// <summary>
    /// Ein Mitgliedszugriff auf eine nicht gesetzte Objektvariable ist Fehler 91. Der frueh
    /// gebundene Pfad ruft dabei auf null, der spaet gebundene wirft in <c>RequireTarget</c>;
    /// beide Wege muessen dieselbe Nummer liefern.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ReportsObjectVariableNotSet()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim c As Collection
                Dim o As Object

                On Error Resume Next

                c.Add "x"
                Debug.Print Err.Number

                Err.Clear
                Debug.Print c.Count
                Debug.Print Err.Number

                Err.Clear
                Set c = New Collection
                Set c = Nothing
                c.Add "x"
                Debug.Print Err.Number

                Err.Clear
                o.Irgendwas
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(new[] { "91", "91", "91", "91" }, output);
    }

    /// <summary>
    /// Ein fehlender Pfad ist Fehler 53. Zwei der vier Faelle waren vorher nicht einmal Fehler:
    /// <c>File.Delete</c> loescht eine fehlende Datei geraeuschlos, und
    /// <c>File.GetLastWriteTime</c> liefert fuer sie einen 1601er-Platzhalter statt zu werfen.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_ReportsFileNotFound()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim missing As String
                missing = "vb6-gibt-es-nicht-4711.txt"

                On Error Resume Next

                Open missing For Input As #1
                Debug.Print Err.Number

                Err.Clear
                Debug.Print FileLen(missing)
                Debug.Print Err.Number

                Err.Clear
                Kill missing
                Debug.Print Err.Number

                Err.Clear
                Debug.Print FileDateTime(missing)
                Debug.Print Err.Number
            End Sub
            """);

        // Nur vier Zeilen: Unter Resume Next bricht die ganze Debug.Print-Anweisung ab, der
        // fehlgeschlagene Aufruf gibt also gar nichts aus.
        CollectionAssert.AreEqual(
            new[] { "53", "53", "53", "53" },
            output);
    }

    /// <summary>
    /// VB6 trennt die beiden Fehlschlaege einer Collection: eine Position ausserhalb der
    /// Sammlung ist 9 (Subscript out of range), ein unbekannter Schluessel dagegen 5. Die
    /// Position von <c>Add</c>s Before/After bleibt bewusst 5 -- dort ist sie ein ungueltiges
    /// Argument an Add, kein Subscript.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_SeparatesCollectionIndexFromKeyFailures()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim c As New Collection
                c.Add "x", "k"

                On Error Resume Next

                Debug.Print c(0)
                Debug.Print Err.Number

                Err.Clear
                Debug.Print c(5)
                Debug.Print Err.Number

                Err.Clear
                c.Remove 5
                Debug.Print Err.Number

                Err.Clear
                Debug.Print c("fehlt")
                Debug.Print Err.Number

                Err.Clear
                c.Add "y", , 9
                Debug.Print Err.Number

                Err.Clear
                c.Add "z", "k"
                Debug.Print Err.Number
            End Sub
            """);

        // 457 am Ende ist die Gegenprobe: ein doppelter Schluessel behaelt seine eigene Nummer.
        CollectionAssert.AreEqual(
            new[] { "9", "9", "9", "5", "5", "457" },
            output);
    }

    /// <summary>
    /// Gegenproben: Diese Faelle melden 5 zu Recht und duerfen nicht mitgezogen werden, wenn
    /// eine neue Zuordnung dazukommt. Ohne sie sieht jede Verschiebung nach 91 oder 53 wie ein
    /// Fortschritt aus.
    /// </summary>
    [TestMethod]
    public void EmitManagedApplication_KeepsUnmappedFailuresAtFive()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                On Error Resume Next

                Debug.Print Left("abc", -1)
                Debug.Print Err.Number

                Err.Clear
                Debug.Print Mid("abc", 0)
                Debug.Print Err.Number

                Err.Clear
                Debug.Print Sqr(-1)
                Debug.Print Err.Number

                Err.Clear
                Debug.Print Log(0)
                Debug.Print Err.Number
            End Sub
            """);

        CollectionAssert.AreEqual(
            new[] { "5", "5", "5", "5" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_SeparatesEndOfFileFromAClosedChannel()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim f As Integer
                Dim s As String
                Dim l As Long

                On Error Resume Next

                f = FreeFile
                Open "kanaele.txt" For Output As #f
                Print #f, "eine"
                Close #f

                f = FreeFile
                Open "kanaele.txt" For Input As #f
                Line Input #f, s
                Debug.Print Err.Number
                Line Input #f, s
                Debug.Print Err.Number
                Close #f

                Err.Clear
                Print #97, "x"
                Debug.Print Err.Number
                Err.Clear
                Line Input #96, s
                Debug.Print Err.Number
                Err.Clear
                Get #95, 1, l
                Debug.Print Err.Number
                Err.Clear
                l = LOF(94)
                Debug.Print Err.Number
                Err.Clear
                Close #93
                Debug.Print Err.Number

                Kill "kanaele.txt"
            End Sub
            """);

        // Lesen ueber das Dateiende ist 62, ein nicht geoeffneter Kanal 52. Beide fielen vorher
        // in den Sammelwert 5 und waren damit von jedem anderen nicht zugeordneten Fehler
        // ununterscheidbar. Close auf einen ungeoeffneten Kanal bleibt geraeuschlos, wie in VB6.
        CollectionAssert.AreEqual(
            new[] { "0", "62", "52", "52", "52", "52", "0" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsBadRecordLength()
    {
        var output = VB6TestProgram.RunLines("""
            Type Satz
                Nummer As Long
                Name As String * 6
                Flag As Boolean
            End Type

            Sub Main()
                Dim f As Integer
                Dim s As Satz
                Dim t As Satz

                On Error Resume Next
                s.Nummer = 7
                s.Name = "abc"
                s.Flag = True

                f = FreeFile
                Open "satz.dat" For Random As #f Len = 12
                Put #f, 1, s
                Get #f, 1, t
                Debug.Print Err.Number & " " & LOF(f) & " " & t.Nummer & " [" & t.Name & "] " & t.Flag
                Close #f

                Err.Clear
                f = FreeFile
                Open "kurz.dat" For Random As #f Len = 4
                Put #f, 1, s
                Debug.Print Err.Number
                Close #f

                Kill "satz.dat"
                Kill "kurz.dat"
            End Sub
            """);

        // Eine Satzlaenge, die den Wert nicht fasst, ist VB6-Fehler 59. Vorher warf die Runtime
        // dafuer eine generische Ausnahme ohne Nummer, die im Sammelwert 5 landete. Der passende
        // Fall darueber zeigt, dass ein Satz mit genau passender Laenge unveraendert durchlaeuft
        // -- einschliesslich der auf sechs Zeichen aufgefuellten festen Zeichenkette.
        CollectionAssert.AreEqual(
            new[] { "0 12 7 [abc   ] True", "59" },
            output);
    }

    [TestMethod]
    public void EmitManagedApplication_ReportsTheDocumentedNumbersOfTheIntrinsicArgumentContracts()
    {
        var output = VB6TestProgram.RunLines("""
            Sub Main()
                Dim s As String
                Dim i As Long
                Dim v As Variant

                On Error Resume Next

                i = Asc("")
                Debug.Print Err.Number
                Err.Clear
                s = Left("abc", -1)
                Debug.Print Err.Number
                Err.Clear
                s = Mid("abc", 0)
                Debug.Print Err.Number
                Err.Clear
                s = Space(-1)
                Debug.Print Err.Number
                Err.Clear
                s = StrConv("abc", 99)
                Debug.Print Err.Number
                Err.Clear
                i = InStr(0, "abc", "b")
                Debug.Print Err.Number
                Err.Clear
                v = Sqr(-1)
                Debug.Print Err.Number
                Err.Clear
                v = Log(0)
                Debug.Print Err.Number

                Err.Clear
                v = CInt("keine Zahl")
                Debug.Print Err.Number
                Err.Clear
                v = CLng(99999999999#)
                Debug.Print Err.Number
                Err.Clear
                i = 1 \ 0
                Debug.Print Err.Number
                Err.Clear
                i = 1 Mod 0
                Debug.Print Err.Number

                ' Ohne Fehler: Choose und Switch liefern Null, Format rundet bankerskonform.
                Err.Clear
                v = Choose(0, "a", "b")
                Debug.Print Err.Number & " " & IsNull(v)
                Err.Clear
                v = Switch(False, "a")
                Debug.Print Err.Number & " " & IsNull(v)
                Err.Clear
                Debug.Print Format(1.5, "###") & " " & Err.Number
            End Sub
            """);

        // Ein ungueltiges Argument ist in VB6 Fehler 5 -- hier die dokumentierte 5, nicht der
        // Sammelwert in Verkleidung. Eine nicht konvertierbare Zeichenkette ist 13, ein Ueberlauf
        // 6 und eine Division durch null 11. Ein Index ausserhalb von Choose und ein Switch ohne
        // Treffer sind kein Fehler, sondern Null. Ein Breitendurchgang hat alle diese Verhalten
        // als korrekt gemessen und zugleich als voellig ungetestet vorgefunden.
        CollectionAssert.AreEqual(
            new[]
            {
                "5", "5", "5", "5", "5", "5", "5", "5",
                "13", "6", "11", "11",
                "0 True", "0 True", "2 0"
            },
            output);
    }
}
