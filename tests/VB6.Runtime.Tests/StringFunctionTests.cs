namespace VB6.Runtime.Tests;

/// <summary>
/// The VB6 string functions, where they differ from their .NET lookalikes.
/// </summary>
[TestClass]
public sealed class StringFunctionTests
{
    /// <summary>VB6 lets the length be omitted, which then runs to the end of the string.</summary>
    [TestMethod]
    public void Mid_WithoutALengthRunsToTheEnd()
    {
        Assert.AreEqual("cdef", VBStrings.Mid("abcdef", 3));
        Assert.AreEqual("abcdef", VBStrings.Mid("abcdef", 1));
        Assert.AreEqual(string.Empty, VBStrings.Mid("abcdef", 7), "A start past the end returns an empty string.");
        Assert.AreEqual("cd", VBStrings.Mid("abcdef", 3, 2));
    }

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

    [TestMethod]
    public void SearchFunctions_UseOneBasedPositionsAndOptionalComparison()
    {
        Assert.AreEqual(2, VBStrings.InStr(1, "abc", "b", 0));
        Assert.AreEqual(2, VBStrings.InStr(1, "abc", "B", 1));
        Assert.AreEqual(4, VBStrings.InStrRev("abca", "a", -1, 0));
        Assert.AreEqual(4, VBStrings.InStrRev("abca", "A", -1, 1));
        Assert.AreEqual(0, VBStrings.InStr(3, "abc", "b", 0));
    }

    [TestMethod]
    public void Replace_SupportsStartCountAndTextComparison()
    {
        Assert.AreEqual("a-x-x", VBStrings.Replace("a-b-b", "b", "x", 1, -1, 0));
        Assert.AreEqual("a-x-B", VBStrings.Replace("a-b-B", "b", "x", 3, 1, 1));
        Assert.AreEqual("a-b-b", VBStrings.Replace("a-b-b", "b", "x", 1, 0, 0));
    }

    [TestMethod]
    public void SpaceAndStrConv_ProducePortableScalarResults()
    {
        Assert.AreEqual("   ", VBStrings.Space(3));
        Assert.AreEqual("ABC", VBStrings.StrConv("aBc", 1, 0));
        Assert.AreEqual("abc", VBStrings.StrConv("aBc", 2, 0));
        Assert.AreEqual("Hello World", VBStrings.StrConv("hello world", 3, 0));
    }

    [TestMethod]
    public void FormatValue_SupportsStringPlaceholdersAndEmptySections()
    {
        Assert.AreEqual(" AB", VBStrings.FormatValue("AB", "@@@", 0, 0));
        Assert.AreEqual("AB ", VBStrings.FormatValue("AB", "!@@@", 0, 0));
        Assert.AreEqual("AB", VBStrings.FormatValue("AB", "&&&", 0, 0));
        Assert.AreEqual("empty", VBStrings.FormatValue(string.Empty, "@@;empty", 0, 0));
        Assert.AreEqual("null", VBStrings.FormatValue(VBVariants.NullValue(), "@@;null", 0, 0));
    }

    [TestMethod]
    public void Split_PreservesEmptyFieldsAndHonorsLimit()
    {
        var values = VBStrings.Split("a,,B", ",", -1, 1);
        Assert.AreEqual(0, values.LBound());
        Assert.AreEqual(2, values.UBound());
        Assert.AreEqual("a", values[0]);
        Assert.AreEqual(string.Empty, values[1]);
        Assert.AreEqual("B", values[2]);

        var limited = VBStrings.Split("a,b,c", ",", 2, 0);
        Assert.AreEqual(1, limited.UBound());
        Assert.AreEqual("b,c", limited[1]);
    }

    [TestMethod]
    public void JoinAndFilter_PreserveOrderAndComparisonMode()
    {
        var values = VBStrings.Split("alpha,beta,BETA,gamma", ",", -1, 0);

        Assert.AreEqual("alpha-beta-BETA-gamma", VBStrings.Join(values, "-"));
        Assert.AreEqual("alpha beta BETA gamma", VBStrings.Join(values, " "));

        var binary = VBStrings.Filter(values, "beta", true, 0);
        Assert.AreEqual(0, binary.UBound());
        Assert.AreEqual("beta", binary[0]);

        var excluded = VBStrings.Filter(values, "beta", false, 1);
        Assert.AreEqual(1, excluded.UBound());
        Assert.AreEqual("alpha", excluded[0]);
        Assert.AreEqual("gamma", excluded[1]);

        var empty = VBStrings.Filter(values, "missing", true, 0);
        Assert.AreEqual(-1, empty.UBound());
        Assert.AreEqual(string.Empty, VBStrings.Join(empty, ","));
    }
}
