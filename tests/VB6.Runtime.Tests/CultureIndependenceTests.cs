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

    private static void UnderCommaDecimalCulture(Action action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(CommaDecimalCulture);

        try
        {
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
