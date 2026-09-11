namespace VB6.Compiler.Tests;

/// <summary>
/// How VB6 reads and advances dates, asked of the original compiler.
///
/// The two cards behind this case came out of a different measurement: <c>Write #</c> started
/// writing a Date as a date literal instead of as its serial number, and the date part of a
/// time-only value became visible for the first time. As a number among numbers nobody had looked
/// at it -- which is the recurring shape of the findings here, and the reason this surface got its
/// own file rather than another entry in the number one.
///
/// Every answer travels as <c>Str$</c> of the OLE serial. That keeps the comparison invariant on
/// both sides: a date rendered as text would differ by the system LCID and turn one deviation into
/// two dozen.
/// </summary>
[TestClass]
public sealed class OracleDateTimeTests
{
    /// <summary>
    /// What differs today, with the original's answer. Empty is the goal, and since
    /// <c>r1-cdate-time-only</c> and <c>r1-dateadd-fractional-interval</c> were closed on
    /// 2026-09-11 it **is** empty.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal);

    /// <summary>
    /// <c>CDate</c> on a string, which is where both of its own rules live.
    ///
    /// A time without a date keeps the OLE epoch day rather than today's -- we produced a value
    /// that changed every morning for the same input. And the text is read under the system LCID:
    /// <c>03.01.2020</c> is the third of January, not the first of March.
    /// </summary>
    [TestMethod]
    public void Oracle_ReadsTheSameDateFromEveryTextForm()
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

        var cases = new (string Label, string Expression)[]
        {
            ("CDate Zeit", "CDate(\"18:30:45\")"),
            ("CDate Zeit kurz", "CDate(\"18:30\")"),
            ("CDate Zeit AM", "CDate(\"6:30:45 AM\")"),
            ("CDate Datum", "CDate(\"2020-01-03\")"),
            ("CDate Datum Zeit", "CDate(\"2020-01-03 18:30:45\")"),

            // Die Ortsform. Unter deutscher LCID ist das der dritte Januar; invariant gelesen
            // waere es der erste Maerz, und genau das stand bei uns.
            ("CDate Ortsform", "CDate(\"03.01.2020\")"),

            ("TimeValue", "TimeValue(\"18:30:45\")"),
            ("DateValue", "DateValue(\"2020-01-03\")"),
            ("TimeValue aus Datum", "TimeValue(\"2020-01-03 18:30:45\")"),
            ("DateValue aus Zeit", "DateValue(\"2020-01-03 18:30:45\")"),
            ("TimeSerial", "TimeSerial(18, 30, 45)"),
            ("DateSerial", "DateSerial(2020, 1, 3)"),
            ("Zeitliteral", "#6:30:45 PM#")
        };

        Compare(
            cases.Select(item =>
                $"    Vb6OracleSay \"{item.Label}\", Str$(CDbl({item.Expression}))"));
    }

    /// <summary>
    /// <c>DateAdd</c> truncates a fractional interval toward zero.
    ///
    /// All ten interval keys against 1.6, -1.6, 0.4, -0.4, 1 and 2.5 -- sixty cases, because a
    /// single key would not have separated the three candidate rules. Rounding and truncation
    /// agree on 2.5, and truncation and <c>Int</c> agree on every positive value; only the
    /// negative fractions tell all three apart.
    /// </summary>
    [TestMethod]
    public void Oracle_TruncatesEveryFractionalDateAddInterval()
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

        string[] intervals = ["yyyy", "q", "m", "y", "d", "w", "ww", "h", "n", "s"];
        string[] amounts = ["1.6", "-1.6", "0.4", "-0.4", "1", "2.5"];

        Compare(
            from interval in intervals
            from amount in amounts
            select $"    Vb6OracleSay \"DateAdd {interval} {amount}\", " +
                $"Str$(CDbl(DateAdd(\"{interval}\", {amount}, CDate(43832))))");
    }

    /// <summary>Puts the lines to both compilers and insists the answers match.</summary>
    private static void Compare(IEnumerable<string> body)
    {
        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(string.Join(Environment.NewLine, body));
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
    }
}
