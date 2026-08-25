using System.Globalization;

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
