namespace VB6.Compiler.Tests;

/// <summary>
/// The bytes VB6 writes, asked of the original compiler.
///
/// This is the surface that has waited longest for an oracle, and the roadmap says so in as many
/// words: the <c>Get</c>/<c>Put</c> contracts rest on named VBA documentation, "a contract proof,
/// not an original VB6 run", and the expectations therefore stayed <c>documented-verified</c>
/// rather than <c>oracle-verified</c>. R1 also recorded the right way to check them -- against raw
/// bytes, never against a self round trip, because a <c>Put</c>/<c>Get</c> pair only ever confirms
/// itself.
///
/// So each case writes with <c>Put</c> and reads the file back as raw bytes, and the comparison is
/// over those bytes. A layout difference shows up as a different hex string, which is the one form
/// that cannot be explained away.
/// </summary>
[TestClass]
public sealed class OracleFileLayoutTests
{
    /// <summary>
    /// What differs today, with the original's answer. Empty is the goal, and since
    /// <c>r1-put-binary-string-layout</c> was closed it **is** empty.
    ///
    /// It held one entry: <c>Put #f, 1, "ABC"</c> in Binary mode gave <c>414243</c> in the
    /// original -- three ANSI bytes, no prefix -- and <c>0300410042004300</c> here, a two-byte
    /// length followed by UTF-16. The prefix belongs to Random mode and the encoding was wrong in
    /// both. The entry disappearing is what made this test fail and forced the list to shrink,
    /// which is the whole reason it is written as a table with a known remainder rather than as a
    /// set of skipped cases.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal);

    [TestMethod]
    public void Oracle_WritesTheSameBytesForEveryRandomAccessValue()
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

        // Jeder Fall schreibt genau einen Wert in eine eigene Datei und liest sie als Rohbytes
        // zurueck. Binary statt Random, damit kein Satzlaengen-Padding die Bytes ueberdeckt --
        // gefragt ist die Kodierung des Werts, nicht die Satzverwaltung.
        var cases = new (string Label, string Declaration, string Value)[]
        {
            ("Integer positiv", "Integer", "CInt(258)"),
            ("Integer negativ", "Integer", "CInt(-2)"),
            ("Long positiv", "Long", "CLng(16909060)"),
            ("Long negativ", "Long", "CLng(-2)"),
            ("Byte", "Byte", "CByte(200)"),
            ("Boolean wahr", "Boolean", "True"),
            ("Boolean falsch", "Boolean", "False"),
            ("Single", "Single", "CSng(1.5)"),
            ("Double", "Double", "CDbl(1.5)"),
            ("Currency", "Currency", "CCur(1.5)"),
            ("Date", "Date", "CDate(\"1999-12-31\")"),
            ("String variabler Laenge", "String", "\"ABC\""),

            // Dieser Fall fehlte hier, solange unser Compiler dafuer VB6S0058 meldete -- "Put of
            // type 'String * 6' is not implemented yet". Das war das gewuenschte Verhalten fuer
            // eine Luecke, hiess aber, dass die ganze Sonde nicht uebersetzt, denn ein Vergleich
            // braucht zwei laufende Programme. `r1-put-fixed-string` hat sie geschlossen: die
            // deklarierte Breite ist die Laenge, das Original schreibt 41 42 20 20 20 20.
            ("String fester Laenge", "String * 6", "\"AB\"")
        };

        var declarations = string.Join(
            Environment.NewLine,
            cases.Select((item, index) => $"Private vb6Wert{index} As {item.Declaration}"));

        var body = string.Join(
            Environment.NewLine,
            cases.Select((item, index) => string.Join(
                Environment.NewLine,
                $"    vb6Wert{index} = {item.Value}",
                $"    vb6OracleBytes = vb6OraclePath & \"bytes{index}.bin\"",
                "    vb6OracleUnit = FreeFile",
                "    Open vb6OracleBytes For Binary As #vb6OracleUnit",
                $"    Put #vb6OracleUnit, 1, vb6Wert{index}",
                "    Close #vb6OracleUnit",
                $"    Vb6OracleSay \"{item.Label}\", Vb6OracleHex(vb6OracleBytes)")));

        // Der Leser gehoert in die Sonde und nicht in den Vergleich: Was verglichen wird, sind die
        // Bytes, die das jeweilige Programm selbst geschrieben und selbst gelesen hat.
        var helper = string.Join(
            Environment.NewLine,
            "Private vb6OracleBytes As String",
            "Private vb6OracleUnit As Integer",
            string.Empty,
            "Public Function Vb6OracleHex(ByVal Pfad As String) As String",
            "    Dim h As Integer",
            "    Dim b As Byte",
            "    Dim s As String",
            "    Dim i As Long",
            "    h = FreeFile",
            "    Open Pfad For Binary As #h",
            "    For i = 1 To LOF(h)",
            "        Get #h, i, b",
            "        s = s & Right$(\"0\" & Hex$(b), 2)",
            "    Next i",
            "    Close #h",
            "    Vb6OracleHex = s",
            "End Function");

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(
                body,
                declarations: declarations + Environment.NewLine + helper);
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

    /// <summary>
    /// The mode is what decides whether a String carries a descriptor -- for a variable-length one.
    /// A <c>String * n</c> does not take part in that distinction, and this case is the measurement
    /// that says so rather than the assumption: the same six bytes in Random mode as in Binary.
    ///
    /// It matters because the pair beside it *does* differ by mode, and a contract that reads "in
    /// both modes" is worth exactly as much as the run behind it.
    /// </summary>
    [TestMethod]
    public void Oracle_WritesAFixedStringWithoutADescriptorInRandomMode()
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

        var body = string.Join(
            Environment.NewLine,
            "    vb6Fest = \"AB\"",
            "    vb6OracleBytes = vb6OraclePath & \"fest.bin\"",
            "    vb6OracleUnit = FreeFile",
            "    Open vb6OracleBytes For Random As #vb6OracleUnit Len = 6",
            "    Put #vb6OracleUnit, 1, vb6Fest",
            "    Close #vb6OracleUnit",
            "    vb6OracleUnit = FreeFile",
            "    Open vb6OracleBytes For Random As #vb6OracleUnit Len = 6",
            "    Get #vb6OracleUnit, 1, vb6Zurueck",
            "    Close #vb6OracleUnit",
            "    Vb6OracleSay \"Random Bytes\", Vb6OracleHex(vb6OracleBytes)",
            "    Vb6OracleSay \"Random gelesen\", \"[\" & vb6Zurueck & \"]\"");

        var helper = string.Join(
            Environment.NewLine,
            "Private vb6Fest As String * 6",
            "Private vb6Zurueck As String * 6",
            "Private vb6OracleBytes As String",
            "Private vb6OracleUnit As Integer",
            string.Empty,
            "Public Function Vb6OracleHex(ByVal Pfad As String) As String",
            "    Dim h As Integer",
            "    Dim b As Byte",
            "    Dim s As String",
            "    Dim i As Long",
            "    h = FreeFile",
            "    Open Pfad For Binary As #h",
            "    For i = 1 To LOF(h)",
            "        Get #h, i, b",
            "        s = s & Right$(\"0\" & Hex$(b), 2)",
            "    Next i",
            "    Close #h",
            "    Vb6OracleHex = s",
            "End Function");

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(body, declarations: helper);
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
        Assert.AreEqual(0, comparison.Differences.Count, comparison.Describe());

        // Nicht nur "gleich wie das Original", sondern die gemessene Form selbst -- sonst wuerde
        // ein Fehler, den beide Seiten teilen, hier als Erfolg durchgehen.
        CollectionAssert.AreEqual(
            new[] { "414220202020", "[AB    ]" },
            new[]
            {
                comparison.Original.Values.GetValueOrDefault("Random Bytes", "(fehlt)"),
                comparison.Original.Values.GetValueOrDefault("Random gelesen", "(fehlt)")
            },
            comparison.Describe());
    }
}
