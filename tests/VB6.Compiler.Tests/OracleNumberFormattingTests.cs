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
    /// What differs today, with the original's answer. Empty is the goal.
    ///
    /// Twelve entries, four causes -- and the first measurement here refuted the obvious reading
    /// of the list. A comma against a point looks like the decided profile difference and is not:
    /// this comparison already runs in the <c>vb6-sp6</c> profile, and <c>Format</c> below does
    /// answer with a comma. So <c>CStr</c> ignoring the locale is a defect, not a decision.
    ///
    /// <list type="bullet">
    /// <item><c>r1-cstr-locale</c>: <c>CStr</c> keeps the invariant separator where the profile
    /// asks for the system's. <c>Str</c> is correctly invariant -- the original itself answers
    /// <c>.3333333</c> with a point while <c>CStr</c> answers with a comma.</item>
    /// <item><c>r1-number-notation-threshold</c>: the original writes 0,00001 in full where we
    /// switch to 1E-05.</item>
    /// <item><c>r1-format-general-single</c>: <c>Format(…, "General Number")</c> on a Single gives
    /// seven significant digits in VB6 and fifteen here -- the very staircase whose stated reason
    /// was falsified.</item>
    /// <item><c>r1-str-leading-zero</c>: <c>Str</c> drops the leading zero below one.</item>
    /// </list>
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal)
    {
        ["CStr Single Drittel"] = "0,3333333",
        ["CStr Double Drittel"] = "0,333333333333333",
        ["CStr Single klein"] = "0,1",
        ["CStr Double klein"] = "0,1",
        ["CStr Single gross"] = "1,234568E+08",
        ["CStr Currency"] = "1,2345",
        ["CStr Currency gerundet"] = "1,2346",
        ["CStr negativ Single"] = "-0,3333333",
        ["CStr Exponent klein"] = "0,00001",
        ["Format General Single"] = "0,3333333",
        ["Str Single"] = ".3333333",
        ["Str Double"] = ".333333333333333"
    };

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
            ("Str negativ", "Str(CLng(-42))")
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
