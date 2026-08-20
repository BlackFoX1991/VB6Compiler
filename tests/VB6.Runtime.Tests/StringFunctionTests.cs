namespace VB6.Runtime.Tests;

/// <summary>
/// The VB6 string functions, where they differ from their .NET lookalikes.
/// </summary>
[TestClass]
public sealed class StringFunctionTests
{
    [TestMethod]
    public void LeftAndRight_ClipInsteadOfFailing()
    {
        Assert.AreEqual("ab", VBStrings.Left("abcdef", 2));
        Assert.AreEqual("ef", VBStrings.Right("abcdef", 2));

        // A length beyond the end returns the whole string; Substring would throw here.
        Assert.AreEqual("abc", VBStrings.Left("abc", 10));
        Assert.AreEqual("abc", VBStrings.Right("abc", 10));
        Assert.AreEqual(string.Empty, VBStrings.Left("abc", 0));
    }

    /// <summary>VB6 Trim removes spaces only, so a tab survives it.</summary>
    [TestMethod]
    public void Trim_RemovesSpacesButNotOtherWhitespace()
    {
        Assert.AreEqual("a", VBStrings.Trim("  a  "));
        Assert.AreEqual("a\t", VBStrings.Trim(" a\t"));
        Assert.AreEqual("a  ", VBStrings.LTrim("  a  "));
        Assert.AreEqual("  a", VBStrings.RTrim("  a  "));
    }

    [TestMethod]
    public void UCaseAndLCase_AreCultureIndependent()
    {
        Assert.AreEqual("ABC", VBStrings.UCase("aBc"));
        Assert.AreEqual("abc", VBStrings.LCase("aBc"));
    }

    [TestMethod]
    public void Asc_ReturnsTheCharacterCodeAndRejectsNonAscii()
    {
        Assert.AreEqual(65, VBStrings.Asc("ABC"));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Asc("ä"));
        Assert.ThrowsException<ArgumentException>(() => VBStrings.Asc(string.Empty));
    }

    [TestMethod]
    public void IsNumeric_AcceptsNumbersAndNumericStrings()
    {
        Assert.IsTrue(VBStrings.IsNumeric(42));
        Assert.IsTrue(VBStrings.IsNumeric(VBConversions.CCur(1.5m)));
        Assert.IsTrue(VBStrings.IsNumeric(" 2.5 "));
        Assert.IsFalse(VBStrings.IsNumeric("abc"));
        Assert.IsFalse(VBStrings.IsNumeric(null), "Empty is not numeric.");
    }

    /// <summary>The same invariant rule the conversions follow: no locale in the compiled program.</summary>
    [TestMethod]
    public void IsNumeric_IgnoresTheAmbientCulture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            Assert.IsTrue(VBStrings.IsNumeric("2.5"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}
