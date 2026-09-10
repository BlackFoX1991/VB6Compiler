namespace VB6.Compiler.Tests;

/// <summary>
/// Which error VB6 raises, asked of the original compiler.
///
/// Two of this project's own notes make this the surface with the most to lose. Error number 5 is
/// the catch-all -- <c>VBErrors.Set</c> maps every unmapped exception to it -- so a wrong 5 looks
/// like a result rather than like a gap. And a measurement that only asks known failure cases
/// misses the worse class entirely: <c>Kill</c> on a missing file and <c>FileDateTime</c> on one
/// reported *nothing at all*, because .NET hides the failure rather than the number. Every case
/// below therefore also answers the question "does it report anything?", and the expected answer
/// is sometimes 0.
///
/// The last case is the documented invariant itself: <c>2000 * 365</c> overflows Integer even
/// though the target is Long. It has never been checked against the original.
/// </summary>
[TestClass]
public sealed class OracleErrorNumberTests
{
    /// <summary>
    /// What differs today, with the original's answer. Empty is the goal.
    ///
    /// One entry out of twenty-six, and the twenty-five that agree are the point of this list as
    /// much as the one that does not. Every catch-all-5 suspect matches, and so do <c>Kill</c> and
    /// <c>FileDateTime</c> on a missing file -- the two that used to report nothing at all. This
    /// surface came out of R1 in good shape; the remaining case is a *different* wrong number
    /// rather than a missing one.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal)
    {
        // 76 ist 'Path not found', 53 ist 'File not found'. Ein fehlendes Verzeichnis ist kein
        // fehlende Datei, und ein VB6-Programm, das auf 76 verzweigt, sieht unsere 53 nie.
        ["ChDir fehlendes Verzeichnis"] = "76"
    };

    [TestMethod]
    public void Oracle_AgreesOnWhichErrorNumberEachFailureRaises()
    {
        if (!VB6Oracle.IsAvailable(out var reason))
        {
            if (VB6Oracle.IsRequired)
            {
                Assert.Fail(reason);
            }

            Assert.Inconclusive(reason);
            return;
        }

        // Jeder Fall raeumt erst Err auf, fuehrt dann seine Anweisung aus und meldet die Nummer.
        // Ohne das Err.Clear traegt ein Fall die Nummer des vorigen weiter, und eine Null waere
        // nicht von 'hat nichts gemeldet' zu unterscheiden.
        var cases = new (string Label, string Statement)[]
        {
            ("Division durch Null", "vb6Wert = 1 / 0"),
            ("Ganzzahldivision durch Null", "vb6Wert = 1 \\ 0"),
            ("Mod durch Null", "vb6Wert = 1 Mod 0"),
            ("Overflow CInt", "vb6Wert = CInt(40000)"),
            ("Overflow CLng", "vb6Wert = CLng(3000000000#)"),
            ("Overflow CByte", "vb6Wert = CByte(300)"),
            ("Type Mismatch CInt", "vb6Wert = CInt(\"abc\")"),
            ("Type Mismatch CDate", "vb6Wert = CDate(\"kein Datum\")"),
            ("Null in CInt", "vb6Wert = CInt(Null)"),
            ("Null in Abs", "vb6Wert = Abs(Null)"),
            ("Objekt nicht gesetzt", "vb6Objekt.Irgendwas"),
            ("Sqr negativ", "vb6Wert = Sqr(-1)"),
            ("Log Null", "vb6Wert = Log(0)"),
            ("Log negativ", "vb6Wert = Log(-1)"),
            ("Left negativ", "vb6Wert = Left$(\"abc\", -1)"),
            ("Mid Null", "vb6Wert = Mid$(\"abc\", 0)"),
            ("String negativ", "vb6Wert = String(-1, \"x\")"),
            ("Chr zu klein", "vb6Wert = Chr(-1)"),
            ("Space negativ", "vb6Wert = Space(-1)"),
            ("Open fehlende Datei", "Open \"gibtesnicht.txt\" For Input As #9"),
            ("Kill fehlende Datei", "Kill \"gibtesnicht.txt\""),
            ("FileLen fehlende Datei", "vb6Wert = FileLen(\"gibtesnicht.txt\")"),
            ("FileDateTime fehlende Datei", "vb6Wert = FileDateTime(\"gibtesnicht.txt\")"),
            ("Dir fehlende Datei", "vb6Wert = Dir(\"gibtesnicht.txt\")"),
            ("ChDir fehlendes Verzeichnis", "ChDir \"C:\\gibtesnicht_xyz\""),
            ("Integerueberlauf im Ausdruck", "vb6Lang = 2000 * 365")
        };

        var body =
            "    On Error Resume Next" + Environment.NewLine +
            string.Join(
                Environment.NewLine,
                cases.Select(item => string.Join(
                    Environment.NewLine,
                    "    Err.Clear",
                    $"    {item.Statement}",
                    $"    Vb6OracleSay \"{item.Label}\", Err.Number")));

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(
                body,
                declarations: string.Join(
                    Environment.NewLine,
                    "Private vb6Wert As Variant",
                    "Private vb6Lang As Long",
                    "Private vb6Objekt As Object"));
        }
        catch (OracleNeedsElevationException exception)
        {
            if (VB6Oracle.IsRequired)
            {
                Assert.Fail(exception.Message);
            }

            Assert.Inconclusive(exception.Message);
            return;
        }

        Assert.IsTrue(comparison.BothRan, comparison.Describe());
        Assert.AreEqual(
            0,
            comparison.MissingFromOurs.Count,
            "Unsere Ausgabe fehlt für: " + string.Join(", ", comparison.MissingFromOurs));

        var differences = comparison.Differences.ToDictionary(
            difference => difference.Label,
            difference => difference.Expected,
            StringComparer.Ordinal);

        var unerwartet = differences
            .Where(pair => !KnownDeviations.ContainsKey(pair.Key))
            .Select(pair => $"{pair.Key}: VB6={pair.Value} wir={comparison.Ours.Values[pair.Key]}")
            .ToArray();
        Assert.AreEqual(
            0,
            unerwartet.Length,
            "Neue Abweichung vom Original: " + string.Join(" | ", unerwartet));

        var behoben = KnownDeviations.Keys
            .Where(label => !differences.ContainsKey(label))
            .ToArray();
        Assert.AreEqual(
            0,
            behoben.Length,
            "Diese Abweichung besteht nicht mehr und gehört aus KnownDeviations entfernt: " +
            string.Join(", ", behoben));

        foreach (var (label, expected) in KnownDeviations)
        {
            Assert.AreEqual(
                expected,
                differences[label],
                $"Das Original antwortet für '{label}' inzwischen anders.");
        }
    }
}
