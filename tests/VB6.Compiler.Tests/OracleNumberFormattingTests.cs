namespace VB6.Compiler.Tests;

/// <summary>
/// How VB6 turns a number into text, asked of the original compiler.
///
/// This surface is measured second because it is where the first oracle finding landed: the
/// project's notes justified the G7/G15/G29 staircase with the sentence "<c>1 / 3</c> is a Single
/// in VB6", and the original says it is a <c>Double</c>. The staircase itself may still be right --
/// its example was not, and a rule whose stated reason is false has never really been checked.
///
/// The comparison runs against <c>--compatibility vb6-sp6</c>, so the decimal separator is the
/// system's on both sides. A comma against a point would otherwise show up as two dozen defects
/// that are in fact one decided difference between the profiles.
/// </summary>
[TestClass]
public sealed class OracleNumberFormattingTests
{
    /// <summary>
    /// What differs today, with the original's answer. Empty is the goal, and since the four
    /// output cards were closed on 2026-09-11 it **is** empty.
    ///
    /// It held twelve entries from four causes, and the first measurement here refuted the obvious
    /// reading of the list. A comma against a point looks like the decided profile difference and
    /// is not: this comparison already runs in the <c>vb6-sp6</c> profile, and <c>Format</c>
    /// answered with a comma in the same run. So <c>CStr</c> ignoring the locale was a defect, not
    /// a decision.
    ///
    /// <list type="bullet">
    /// <item><c>r1-cstr-locale</c>: <c>CStr</c> kept the invariant separator where the profile asks
    /// for the system's. <c>Str</c> was correctly invariant -- the original itself answers
    /// <c>.3333333</c> with a point while <c>CStr</c> answers with a comma.</item>
    /// <item><c>r1-number-notation-threshold</c>: the original writes 0,00001 in full where we
    /// switched to 1E-05.</item>
    /// <item><c>r1-format-general-single</c>: <c>Format(…, "General Number")</c> on a Single gives
    /// seven significant digits in VB6 and gave fifteen here -- the very staircase whose stated
    /// reason was falsified.</item>
    /// <item><c>r1-str-leading-zero</c>: <c>Str</c> drops the leading zero below one.</item>
    /// </list>
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal);

    [TestMethod]
    public void Oracle_AgreesOnHowNumbersBecomeText()
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

        // Jede Zeile fragt CStr fuer einen Wert, dessen Typ feststeht. Der Punkt ist die Zahl der
        // signifikanten Stellen: Ein Single mit fuenfzehn auszugeben zeigt seine Umrechnungsreste
        // als waeren sie Werte, ein Double mit sieben verliert echte Stellen.
        var cases = new (string Label, string Expression)[]
        {
            ("CStr Single Drittel", "CStr(CSng(1) / CSng(3))"),
            ("CStr Double Drittel", "CStr(CDbl(1) / CDbl(3))"),
            ("CStr Single klein", "CStr(CSng(0.1))"),
            ("CStr Double klein", "CStr(CDbl(0.1))"),
            ("CStr Single gross", "CStr(CSng(123456789))"),
            ("CStr Double gross", "CStr(CDbl(123456789012345#))"),
            ("CStr Currency", "CStr(CCur(1.2345))"),
            ("CStr Currency gerundet", "CStr(CCur(1.23455))"),
            ("CStr Integer", "CStr(CInt(-42))"),
            ("CStr Long", "CStr(CLng(-2147483648#))"),
            ("CStr Byte", "CStr(CByte(255))"),
            ("CStr Boolean wahr", "CStr(True)"),
            ("CStr Boolean falsch", "CStr(False)"),
            ("CStr negativ Single", "CStr(CSng(-1) / CSng(3))"),
            ("CStr Null Single", "CStr(CSng(0))"),
            ("CStr Exponent klein", "CStr(CDbl(0.00001))"),
            ("CStr Exponent gross", "CStr(CDbl(100000000000000000#))"),
            ("Format General Single", "Format(CSng(1) / CSng(3), \"General Number\")"),
            ("Format General Double", "Format(CDbl(1) / CDbl(3), \"General Number\")"),
            ("Format zwei Stellen", "Format(CDbl(1) / CDbl(3), \"0.00\")"),
            ("Format Tausender", "Format(CDbl(1234567.891), \"#,##0.00\")"),
            ("Format Prozent", "Format(CDbl(0.1234), \"0.00%\")"),
            ("Str Single", "Str(CSng(1) / CSng(3))"),
            ("Str Double", "Str(CDbl(1) / CDbl(3))"),
            ("Str positiv", "Str(CLng(42))"),
            ("Str negativ", "Str(CLng(-42))"),

            // Die Schwelle zur Exponentialschreibweise, von beiden Seiten und in beiden Typen.
            // Sie ist die Stelle, an der .NETs G-Spezifizierer und VB6 auseinandergehen, und ein
            // einzelner Wert haette sie nicht festgelegt: Ein Single mit 1E-5 wird ausgeschrieben,
            // derselbe Exponent mit sieben Mantissenstellen nicht.
            ("Schwelle Double 1e-15", "CStr(CDbl(0.000000000000001))"),
            ("Schwelle Double 1e-16", "CStr(CDbl(0.0000000000000001))"),
            ("Schwelle Double 1e14", "CStr(CDbl(100000000000000#))"),
            ("Schwelle Double 1e15", "CStr(CDbl(1000000000000000#))"),
            ("Schwelle Double 15 Neunen", "CStr(CDbl(999999999999999#))"),
            ("Schwelle Double 16 Stellen", "CStr(CDbl(1999999999999999#))"),
            ("Schwelle Single 1e-7", "CStr(CSng(0.0000001))"),
            ("Schwelle Single 1e-8", "CStr(CSng(0.00000001))"),
            ("Schwelle Single 1e6", "CStr(CSng(1000000))"),
            ("Schwelle Single 1e7", "CStr(CSng(10000000))"),
            ("Schwelle Single krumm klein", "CStr(CSng(0.00000123))"),
            ("Schwelle Single knapp klein", "CStr(CSng(0.0000012))"),
            ("Schwelle Single sieben Stellen", "CStr(CSng(1234567))"),
            ("Schwelle Single acht Stellen", "CStr(CSng(12345678))"),
            ("Schwelle Double gemischt", "CStr(CDbl(12345.6789012345))"),
            ("Schwelle Double dreistellig", "CStr(CDbl(0.0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001))"),

            // Str, die drei Regeln einzeln: keine Null vor dem Trenner, invariante Ziffern trotz
            // SP6-Profil, und die Vorzeichenspalte nur fuer Zahlen.
            ("Str Currency", "Str(CCur(0.25))"),
            ("Str negativ klein", "Str(CDbl(-0.00001))"),
            ("Str Null", "Str(CDbl(0))"),
            ("Str Boolean", "Str(True)"),
            ("Str Exponent", "Str(CSng(0.00000001))"),

            // General Number ist gemessen dasselbe wie CStr -- auch an den Schwellen.
            ("Format General Schwelle", "Format(CDbl(0.00001), \"General Number\")"),
            ("Format General Single gross", "Format(CSng(123456789), \"General Number\")"),
            ("Format General Currency", "Format(CCur(1.2345), \"General Number\")")
        };

        var body = string.Join(
            Environment.NewLine,
            cases.Select(item =>
                $"    Vb6OracleSay \"{item.Label}\", {item.Expression}"));

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(body);
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
            .Select(pair => $"{pair.Key}: VB6='{pair.Value}' wir='{comparison.Ours.Values[pair.Key]}'")
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

        // Der Wert des Originals gehört mit in die Liste, damit die Karten nicht auf eine
        // Erinnerung angewiesen sind -- und damit auffällt, wenn das Original selbst eine andere
        // Antwort gibt, etwa unter einer anderen System-LCID.
        foreach (var (label, expected) in KnownDeviations)
        {
            Assert.AreEqual(
                expected,
                differences[label],
                $"Das Original antwortet für '{label}' inzwischen anders.");
        }
    }
}
