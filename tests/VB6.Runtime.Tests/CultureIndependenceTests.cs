using System.Globalization;
using System.Text;

namespace VB6.Runtime.Tests;

/// <summary>
/// VB6.Runtime converts between strings and numbers with the invariant culture so that a compiled
/// program behaves identically everywhere. These tests run under a comma-decimal culture on
/// purpose: CI runs on en-US, where an accidental return to CultureInfo.CurrentCulture would pass
/// unnoticed while breaking every machine with a different locale.
/// </summary>
[TestClass]
public sealed class CultureIndependenceTests
{
    private const string CommaDecimalCulture = "de-DE";

    [TestMethod]
    public void Conversions_IgnoreTheAmbientCulture()
    {
        UnderCommaDecimalCulture(() =>
        {
            Assert.AreEqual(2.5d, VBConversions.CDbl("2.5"));
            Assert.AreEqual(2.5f, VBConversions.CSng("2.5"));
            Assert.AreEqual("2.5", VBConversions.CStr(2.5d));
        });
    }

    [TestMethod]
    public void StrConv_UsesAmbientCultureOnlyForExplicitVb6Profile()
    {
        UnderCulture("tr-TR", () =>
        {
            Assert.AreEqual(
                "I",
                VBStrings.StrConv("i", 1, 0),
                "The compatibility-free overload remains invariant.");
            Assert.AreEqual(
                "İ",
                VBStrings.StrConv("i", 1, 0, VBCompatibilityProfile.VB6Sp6),
                "VB6Sp6 follows the active system culture for casing.");
        });
    }

    [TestMethod]
    public void StringByteIntrinsics_UseTheSelectedAnsiProfile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var systemAnsi = Encoding.GetEncoding(
            0,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
        var sample = "ä";
        var expectedLength = systemAnsi.GetByteCount(sample);

        Assert.AreEqual(
            expectedLength,
            Convert.ToInt32(VBStrings.LenB(sample, VBCompatibilityProfile.VB6Sp6), CultureInfo.InvariantCulture));

        var bytes = systemAnsi.GetBytes(sample);
        if (bytes.Length == 1)
        {
            Assert.AreEqual(bytes[0], VBStrings.Asc(sample, VBCompatibilityProfile.VB6Sp6));
        }

        Assert.AreEqual("A", VBStrings.Chr(65, VBCompatibilityProfile.VB6Sp6));
    }

    [TestMethod]
    public void Format_UsesAmbientLocaleOnlyForExplicitVb6Profile()
    {
        UnderCulture("de-DE", () =>
        {
            Assert.AreEqual(
                "1,234.50",
                VBStrings.FormatValue(1234.5d, "Standard", 1, 1),
                "The compatibility-free overload remains invariant.");
            Assert.AreEqual(
                "1.234,50",
                VBStrings.FormatValue(1234.5d, "Standard", 1, 1, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "January",
                VBStrings.FormatValue(new DateTime(2020, 1, 2), "mmmm", 1, 1),
                "Deterministic date names remain invariant.");
            Assert.AreEqual(
                "Januar",
                VBStrings.FormatValue(new DateTime(2020, 1, 2), "mmmm", 1, 1, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "1.234,50 €",
                VBStrings.FormatValue(1234.5d, "Currency", 1, 1, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "02.01.2020",
                VBStrings.FormatValue(new DateTime(2020, 1, 2), "Short Date", 1, 1, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "02.01.2020 17:04:23",
                VBStrings.FormatValue(
                    new DateTime(2020, 1, 2, 17, 4, 23),
                    "dd/mm/yyyy hh:nn:ss",
                    1,
                    1,
                    VBCompatibilityProfile.VB6Sp6));
        });
    }

    [TestMethod]
    public void VariantMultiply_IgnoresTheAmbientCulture()
    {
        UnderCommaDecimalCulture(() =>
            Assert.AreEqual(5d, VBOperators.MultiplyInteger((object?)"2.5", (object?)(short)2)));
    }

    [TestMethod]
    public void CurrencyFormatting_IgnoresTheAmbientCulture()
    {
        UnderCommaDecimalCulture(() =>
            Assert.AreEqual("1.5", VBConversions.CCur(1.5m).ToString()));
    }

    [TestMethod]
    public void DebugPrint_IgnoresTheAmbientCulture()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            UnderCommaDecimalCulture(() => VBDebug.Print(2.5d));
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.AreEqual("2.5", writer.ToString().Trim());
    }

    [TestMethod]
    public void DebugPrint_UsesVb6ScalarFormatting()
    {
        Assert.AreEqual(" 42", VBDebug.Format(42));
        Assert.AreEqual("-42", VBDebug.Format(-42));
        Assert.AreEqual("True", VBDebug.Format(true));
        Assert.AreEqual(string.Empty, VBDebug.Format(null));
        Assert.AreEqual("Null", VBDebug.Format(VBVariants.NullValue()));
        Assert.AreEqual("Error 2001", VBDebug.Format(new VBErrorValue(2001)));
        Assert.AreEqual(" 1.23456789012346", VBDebug.Format(1.234567890123456d));
    }

    /// <summary>
    /// vbUseSystem is the one sanctioned exception to the invariant-culture rule: the caller
    /// explicitly asks for the system setting, so the ambient culture is the requested value and
    /// not an accidental leak. de-DE starts its week on Monday, en-US on Sunday.
    /// </summary>
    [TestMethod]
    public void FirstDayOfWeek_FollowsTheAmbientCultureOnlyForUseSystem()
    {
        // OLE date 43832 is Thursday, 2 January 2020.
        const double thursday = 43832d;

        Assert.AreEqual(
            (short)5,
            UnderCulture("en-US", () => VBDateTime.Weekday(thursday, 0)),
            "en-US starts the week on Sunday, so Thursday is the fifth day.");
        Assert.AreEqual(
            (short)4,
            UnderCulture("de-DE", () => VBDateTime.Weekday(thursday, 0)),
            "de-DE starts the week on Monday, so Thursday is the fourth day.");

        // An explicit constant is a value, not a question to the system: it stays invariant.
        foreach (var culture in new[] { "en-US", "de-DE" })
        {
            Assert.AreEqual(
                (short)5,
                UnderCulture(culture, () => VBDateTime.Weekday(thursday, 1)),
                $"vbSunday must not depend on {culture}.");
            Assert.AreEqual(
                (short)4,
                UnderCulture(culture, () => VBDateTime.Weekday(thursday, 2)),
                $"vbMonday must not depend on {culture}.");
        }
    }

    [TestMethod]
    public void DateTimeNamesAndParsing_UseAmbientLocaleOnlyForVb6Profile()
    {
        UnderCulture("de-DE", () =>
        {
            Assert.AreEqual("Thursday", VBDateTime.WeekdayName(4, false, 2));
            Assert.AreEqual("January", VBDateTime.MonthName(1));
            Assert.AreEqual("Donnerstag", VBDateTime.WeekdayName(4, false, 2, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("Do", VBDateTime.WeekdayName(4, true, 2, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("Januar", VBDateTime.MonthName(1, false, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("Jan", VBDateTime.MonthName(1, true, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                43832d,
                VBDateTime.DateValue("02.01.2020", VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                0.75d,
                VBDateTime.TimeValue("18:00:00", VBCompatibilityProfile.VB6Sp6));
        });
    }

    [TestMethod]
    public void VariantPredicates_UseAmbientLocaleOnlyForVb6Profile()
    {
        UnderCulture("de-DE", () =>
        {
            Assert.IsFalse(VBStrings.IsNumeric("1.234,5"));
            Assert.IsTrue(VBStrings.IsNumeric("1.234,5", VBCompatibilityProfile.VB6Sp6));
            Assert.IsTrue(VBVariants.IsDate("02.01.2020", VBCompatibilityProfile.VB6Sp6));
        });
    }

    [TestMethod]
    public void DatePart_UsesSystemWeekRuleOnlyForUseSystem()
    {
        const double newYear = 44197d; // Friday, 1 January 2021.

        var unitedStates = UnderCulture(
            "en-US",
            () => VBDateTime.DatePart("ww", newYear, 0, 0));
        var germany = UnderCulture(
            "de-DE",
            () => VBDateTime.DatePart("ww", newYear, 0, 0));
        Assert.AreNotEqual(unitedStates, germany);

        var explicitUnitedStates = UnderCulture(
            "en-US",
            () => VBDateTime.DatePart("ww", newYear, 1, 1));
        var explicitGermany = UnderCulture(
            "de-DE",
            () => VBDateTime.DatePart("ww", newYear, 1, 1));
        Assert.AreEqual(explicitUnitedStates, explicitGermany);
    }

    /// <summary>
    /// Format's week token uses the same contract, through VBStrings rather than VBDateTime.
    /// Both resolvers must agree about what vbUseSystem means.
    /// </summary>
    [TestMethod]
    public void FormatWeekTokens_FollowTheAmbientCultureOnlyForUseSystem()
    {
        // OLE date 44197 is Friday, 1 January 2021: en-US counts it as week 1, while de-DE's
        // four-day-week rule still assigns it to the final week of 2020.
        var newYear = DateTime.FromOADate(44197d);

        var systemUnitedStates = UnderCulture("en-US", () => VBStrings.FormatValue(newYear, "ww", 0, 0));
        var systemGermany = UnderCulture("de-DE", () => VBStrings.FormatValue(newYear, "ww", 0, 0));
        Assert.AreNotEqual(
            systemUnitedStates,
            systemGermany,
            "vbUseSystem must follow the ambient culture for both the first day and the week rule.");

        var explicitUnitedStates = UnderCulture("en-US", () => VBStrings.FormatValue(newYear, "ww", 1, 1));
        var explicitGermany = UnderCulture("de-DE", () => VBStrings.FormatValue(newYear, "ww", 1, 1));
        Assert.AreEqual(
            explicitUnitedStates,
            explicitGermany,
            "Explicit vbSunday/vbFirstJan1 must produce the same week everywhere.");
    }

    private static void UnderCommaDecimalCulture(Action action) =>
        UnderCulture(CommaDecimalCulture, action);

    private static void UnderCulture(string culture, Action action) =>
        UnderCulture(culture, () =>
        {
            action();
            return true;
        });

    private static T UnderCulture<T>(string culture, Func<T> action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);

        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
