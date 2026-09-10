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
    /// What differs today, with the original's answer. Empty is the goal.
    ///
    /// One entry out of twelve, and the eleven that agree carry weight: Integer, Long, Byte,
    /// Boolean, Single, Double, Currency and Date come out byte-identical, in both signs where a
    /// sign exists. The numeric layouts were right all along; the string layout is wrong twice
    /// over.
    ///
    /// <c>Put #f, 1, "ABC"</c> in Binary mode gives <c>414243</c> in the original -- three ANSI
    /// bytes, no prefix -- and <c>0300410042004300</c> here: a two-byte length followed by UTF-16.
    /// The length prefix belongs to **Random** mode, and the encoding is wrong regardless of mode.
    /// A data file written by a VB6 program is therefore unreadable by ours and the other way
    /// round, which is the exact failure this compiler exists to prevent. Open as
    /// <c>r1-put-binary-string-layout</c>; Random mode is a separate question and was not measured
    /// here.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal)
    {
        ["String variabler Laenge"] = "414243"
    };

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
            ("String variabler Laenge", "String", "\"ABC\"")

            // `String * 6` fehlt hier bewusst. Unser Compiler meldet dafuer VB6S0058 -- "Put of
            // type 'String * 6' is not implemented yet" -- und das ist das gewuenschte Verhalten
            // fuer eine Luecke, nicht ein Defekt. Es heisst aber, dass die ganze Sonde nicht
            // uebersetzt, denn ein Vergleich braucht zwei laufende Programme. Der Fall steht
            // deshalb als eigene Karte (r1-put-fixed-string) und kommt hierher zurueck, sobald
            // Put ihn traegt.
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
}
