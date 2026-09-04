namespace VB6.Runtime.Tests;

/// <summary>
/// <c>Format</c> chooses its formatter from the format, not from how the expression happens to be
/// stored. A numeric picture applied to a numeric string formats the number — <c>Format(txt.Text,
/// "0.00")</c> is everyday VB6 — and a string that carries no number comes back untouched.
///
/// Before this card every string went to the string formatter: <c>Format("12", "0.00")</c> gave
/// <c>0.00</c>, losing the value, and <c>Format("abc", "#,##0")</c> gave back the picture itself.
/// </summary>
[TestClass]
public sealed class FormatStringInputTests
{
    [TestMethod]
    public void FormatValue_FormatsANumericStringWithANumericFormat()
    {
        Assert.AreEqual("12.00", VBStrings.FormatValue("12", "0.00", 0, 0));
        Assert.AreEqual("1,234.50", VBStrings.FormatValue("1234.5", "#,##0.00", 0, 0));
        Assert.AreEqual("-7.00", VBStrings.FormatValue("-7", "0.00", 0, 0));
        Assert.AreEqual("0012", VBStrings.FormatValue(" 12 ", "0000", 0, 0));
    }

    [TestMethod]
    public void FormatValue_FormatsANumericStringWithANamedNumericFormat()
    {
        Assert.AreEqual("$12.00", VBStrings.FormatValue("12", "Currency", 0, 0));
        Assert.AreEqual("50.00%", VBStrings.FormatValue("0.5", "Percent", 0, 0));
        Assert.AreEqual("1,234.57", VBStrings.FormatValue("1234.5678", "Standard", 0, 0));
    }

    [TestMethod]
    public void FormatValue_ReturnsAStringThatCarriesNoNumberUnchanged()
    {
        // Inventing a zero for it would be worse than doing nothing -- the value the program
        // meant to show would silently disappear.
        Assert.AreEqual("abc", VBStrings.FormatValue("abc", "0.00", 0, 0));
        Assert.AreEqual("abc", VBStrings.FormatValue("abc", "#,##0", 0, 0));
        Assert.AreEqual("abc", VBStrings.FormatValue("abc", "Currency", 0, 0));
        Assert.AreEqual("abc", VBStrings.FormatValue("abc", "yyyy-mm-dd", 0, 0));
    }

    [TestMethod]
    public void FormatValue_FormatsADateStringWithADateFormat()
    {
        Assert.AreEqual("2026-03-04", VBStrings.FormatValue("2026-03-04", "yyyy-mm-dd", 0, 0));
        Assert.AreEqual("March", VBStrings.FormatValue("2026-03-04", "mmmm", 0, 0));
    }

    [TestMethod]
    public void FormatValue_StillRoutesAStringFormatToTheStringFormatter()
    {
        // A picture made of @, &, <, > or ! addresses characters, so even a numeric string takes
        // this route.
        Assert.AreEqual(" AB", VBStrings.FormatValue("AB", "@@@", 0, 0));
        Assert.AreEqual("AB ", VBStrings.FormatValue("AB", "!@@@", 0, 0));
        Assert.AreEqual("AB", VBStrings.FormatValue("AB", "&&&", 0, 0));
        Assert.AreEqual("AB-X", VBStrings.FormatValue("ab", ">!@@\"-x\"", 0, 0));
        Assert.AreEqual("@AB", VBStrings.FormatValue("AB", "\"@\"@@", 0, 0));
        Assert.AreEqual(" 12", VBStrings.FormatValue("12", "@@@", 0, 0));
        Assert.AreEqual("unchanged", VBStrings.FormatValue("unchanged", string.Empty, 0, 0));
    }

    [TestMethod]
    public void FormatValue_DoesNotLetAQuotedPlaceholderDecideTheFormatKind()
    {
        // The @ here is literal text inside a numeric picture, so this stays a numeric format.
        Assert.AreEqual("12.00@", VBStrings.FormatValue("12", "0.00\"@\"", 0, 0));
        Assert.AreEqual("12.00@", VBStrings.FormatValue("12", "0.00\\@", 0, 0));
    }
}
