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
    /// The pairs that differ today, with the original's answer. Empty is the goal, and since
    /// <c>r1-division-result-type</c> was closed it **is** empty.
    ///
    /// It held three entries, all of them <c>/</c> with Integer operands: the original answers
    /// <c>Double</c>, this compiler answered <c>Single</c>.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal);

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
            "CByte(1) / CByte(3)",
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

    /// <summary>
    /// The result type of <c>/</c> over the **complete** cross product of its operand types --
    /// 121 pairs, every one of them asked of the original.
    ///
    /// The whole surface rather than a sample, because a sample is what got this wrong twice. The
    /// diagonal alone passes under three different rules: "both operands narrow", the documented
    /// "one side Single and the other not Long/Currency/Decimal", and the one that actually holds.
    /// Only the mixed pairs separate them -- <c>Single / Double</c> and <c>Single / Date</c> are
    /// <c>Double</c>, <c>Single / Boolean</c> is <c>Single</c>.
    ///
    /// The Variant operands are there for a second reason: their subtype decides at runtime, so
    /// they are the only cases that exercise <c>VBVariantArithmetic</c> rather than the binder.
    /// </summary>
    [TestMethod]
    public void Oracle_AgreesOnEveryDivisionResultType()
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

        // Die Operanden sind Modulvariablen und keine Ausdruecke, weil ein Variant seinen Subtyp
        // sonst gar nicht tragen koennte: CInt(3) inline waere ein statisch typisierter Integer.
        var operands = new (string Name, string Declaration)[]
        {
            ("oByte", "Byte"),
            ("oInteger", "Integer"),
            ("oLong", "Long"),
            ("oSingle", "Single"),
            ("oDouble", "Double"),
            ("oCurrency", "Currency"),
            ("oBoolean", "Boolean"),
            ("oDate", "Date"),
            ("oVariantInteger", "Variant"),
            ("oVariantDouble", "Variant"),
            ("oString", "String")
        };

        var declarations = string.Join(
            Environment.NewLine,
            operands.Select(operand => $"Private {operand.Name} As {operand.Declaration}"));

        var assignments = operands.Select(operand => operand.Name switch
        {
            "oVariantInteger" => "    oVariantInteger = CInt(3)",
            "oVariantDouble" => "    oVariantDouble = CDbl(3)",
            "oString" => "    oString = \"3\"",
            var name => $"    {name} = 3"
        });

        var questions =
            from left in operands
            from right in operands
            select $"    Vb6OracleType \"{left.Name} / {right.Name}\", {left.Name} / {right.Name}";

        OracleComparison comparison;
        try
        {
            comparison = VB6Oracle.Ask(
                string.Join(Environment.NewLine, assignments.Concat(questions)),
                declarations);
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
        Assert.AreEqual(
            operands.Length * operands.Length,
            comparison.Original.Values.Count,
            "Das Original hat nicht alle Paare beantwortet.");
        Assert.AreEqual(0, comparison.Differences.Count, comparison.Describe());

        // Drei gemessene Antworten ausdruecklich, damit ein Fehler, den beide Seiten teilen, hier
        // nicht als Erfolg durchgeht -- je eine pro Zweig der Regel.
        CollectionAssert.AreEqual(
            new[] { "Double", "Single", "Double" },
            new[]
            {
                comparison.Original.Values.GetValueOrDefault("oInteger / oInteger", "(fehlt)"),
                comparison.Original.Values.GetValueOrDefault("oSingle / oBoolean", "(fehlt)"),
                comparison.Original.Values.GetValueOrDefault("oSingle / oDouble", "(fehlt)")
            },
            comparison.Describe());
    }

    /// <summary>
    /// The type name was never the point -- the precision was. <c>CInt(1) / CInt(3)</c> came out
    /// as <c>0.3333333</c> here and as fifteen digits in the original, and a table of
    /// <c>TypeName</c> answers alone would not have said whether the value followed the type.
    ///
    /// The decimal separator is normalised away on both sides, deliberately and visibly: the
    /// original writes a comma under the system LCID and we write a point, which is
    /// <c>r1-cstr-locale</c> and has its own case. What is compared here is the digits.
    /// </summary>
    [TestMethod]
    public void Oracle_ComputesDivisionWithThePrecisionOfItsResultType()
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

        var cases = new[]
        {
            "1 / 3",
            "CInt(1) / CInt(3)",
            "CSng(1) / CSng(3)",
            "CSng(1) / CInt(3)",
            "CDbl(1) / CInt(3)",
            "CCur(1) / CInt(3)",
            "CLng(7) / CInt(3)",
            "CInt(-1) / CInt(3)",
            "CLng(1000000) / CInt(3)",
            "CInt(1) / CInt(4)"
        };

        var body = string.Join(
            Environment.NewLine,
            cases.Select(expression =>
                $"    Vb6OracleSay \"{expression.Replace("\"", "\"\"")}\", " +
                $"Replace(CStr({expression}), \",\", \".\")"));

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
        Assert.AreEqual(0, comparison.Differences.Count, comparison.Describe());

        // Zwei gemessene Werte ausdruecklich, damit ein Fehler, den beide Seiten teilen, hier
        // nicht als Erfolg durchgeht: der Double-Fall mit seinen fuenfzehn Stellen und der
        // Single-Fall mit seinen sieben.
        Assert.AreEqual(
            "0.333333333333333",
            comparison.Original.Values.GetValueOrDefault("CInt(1) / CInt(3)", "(fehlt)"));
        Assert.AreEqual(
            "0.3333333",
            comparison.Original.Values.GetValueOrDefault("CSng(1) / CInt(3)", "(fehlt)"));
    }
}
