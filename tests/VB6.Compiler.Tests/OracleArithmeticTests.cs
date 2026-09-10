namespace VB6.Compiler.Tests;

/// <summary>
/// The result types of VB6 arithmetic, asked of the original compiler.
///
/// This is the first case built on <see cref="VB6Oracle"/>, and it exists because the answer was a
/// surprise. The promotion table had been recorded as fully correct across 49 measured operand
/// pairs -- measured against this project's own understanding, which is a regression proof and not
/// a contract proof. The original disagrees in one rule: <c>/</c> computes in <c>Double</c> unless
/// both operands are <c>Single</c>, while this compiler promotes Integer operands to
/// <c>Single</c>. That is not a cosmetic difference in a type name; the value loses precision,
/// and <c>CStr(1 / 3)</c> comes out as <c>0.3333333</c> instead of fifteen digits.
///
/// The case is written as a **table with a known remainder** rather than as a passing assertion.
/// Every pair the original and this compiler agree on is locked in; the three that differ are
/// listed by name, so the day the defect is fixed this test fails and forces the list to shrink.
/// A test that simply skipped the known-bad entries would let the fix pass unnoticed and a
/// regression pass too.
/// </summary>
[TestClass]
public sealed class OracleArithmeticTests
{
    /// <summary>
    /// The pairs that differ today, with the original's answer. Empty is the goal;
    /// <c>r1-division-result-type</c> is the card that empties it.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal)
    {
        ["1 / 3"] = "Double",
        ["CInt(1) / CInt(3)"] = "Double",
        ["CInt(7) / CInt(1)"] = "Double"
    };

    [TestMethod]
    public void Oracle_AgreesOnEveryArithmeticResultTypeExceptTheKnownDivisionDefect()
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

        // Die Faelle deckizen die Operatoren ueber die Zahlentypen ab, plus die Formen, an denen
        // VB6 bekannt eigenwillig ist: ganzzahlige Division, Potenz, Verkettung und And auf
        // Boolean. Ueberlaufende Ausdruecke stehen bewusst nicht drin -- 2000 * 365 waere ein
        // Laufzeitfehler und wuerde die Messung abbrechen statt sie zu beantworten.
        var cases = new[]
        {
            "1 / 3",
            "CInt(1) / CInt(3)",
            "CInt(7) / CInt(1)",
            "CSng(1) / CSng(3)",
            "CDbl(1) / CDbl(3)",
            "CCur(1) / CCur(3)",
            "CLng(1) / CLng(3)",
            "1 \\ 3",
            "2 ^ 3",
            "1 + 1",
            "CInt(1) + CInt(1)",
            "CSng(1) + CSng(1)",
            "CDbl(1) + CDbl(1)",
            "CLng(1) + CLng(1)",
            "CCur(1) + CCur(1)",
            "CByte(1) + CByte(1)",
            "1 * 3",
            "CSng(1) * CSng(3)",
            "1 - 3",
            "1 Mod 3",
            "1 & 2",
            "True And 1",
            "True Or 1",
            "-CInt(1)",
            "Not CInt(1)"
        };

        var body = string.Join(
            Environment.NewLine,
            cases.Select(expression =>
                $"    Vb6OracleType \"{expression.Replace("\"", "\"\"")}\", {expression}"));

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(body);
        }
        catch (OracleNeedsElevationException exception)
        {
            // Erreichbar, aber nicht erreichbar *von hier*. Ein erzwungener Lauf soll das melden,
            // ein gewoehnlicher ueberspringt -- dieselbe Regel wie beim nativen OCX-Pfad.
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

        // Neue Abweichungen sind der eigentliche Zweck dieses Tests: Sie bedeuten, dass eine
        // Zusage bricht, die bisher nur gegen das eigene Verstaendnis geprueft war.
        var unerwartet = differences
            .Where(pair => !KnownDeviations.ContainsKey(pair.Key))
            .Select(pair => $"{pair.Key}: VB6={pair.Value} wir={comparison.Ours.Values[pair.Key]}")
            .ToArray();
        Assert.AreEqual(
            0,
            unerwartet.Length,
            "Neue Abweichung vom Original: " + string.Join(" | ", unerwartet));

        // Und die Gegenrichtung, die den Fix erzwingt: Ein bekannter Befund, der verschwunden
        // ist, gehoert aus der Liste gestrichen -- sonst deckt sie spaeter eine Regression.
        var behoben = KnownDeviations.Keys
            .Where(label => !differences.ContainsKey(label))
            .ToArray();
        Assert.AreEqual(
            0,
            behoben.Length,
            "Diese Abweichung besteht nicht mehr und gehört aus KnownDeviations entfernt: " +
            string.Join(", ", behoben));

        // Die bekannten Befunde tragen die Antwort des Originals mit, damit die Karte nicht auf
        // eine Erinnerung angewiesen ist.
        foreach (var (label, expected) in KnownDeviations)
        {
            Assert.AreEqual(
                expected,
                differences[label],
                $"Das Original antwortet für '{label}' inzwischen anders.");
        }
    }
}
