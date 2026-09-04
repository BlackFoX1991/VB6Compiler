using System.Globalization;

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

    [TestMethod]
    public void RightAlignFixedString_PadsLeftAndKeepsLeftmostCharacters()
    {
        Assert.AreEqual("   Hi", VBTypeStorage.RightAlignFixedString("Hi", 5));
        Assert.AreEqual("ABCDE", VBTypeStorage.RightAlignFixedString("ABCDEFGH", 5));
        Assert.AreEqual(string.Empty, VBTypeStorage.RightAlignFixedString("value", 0));
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
    public void VariantStringFunctions_PropagateNull()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBStrings.Mid(nullValue, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.Mid(nullValue, 1, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.Left(nullValue, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.Right(nullValue, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.UCase(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.LCase(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.Trim(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.LTrim(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.RTrim(nullValue)));
    }

    [TestMethod]
    public void Asc_ReturnsWindows1252CodesAndRejectsUnrepresentableCharacters()
    {
        Assert.AreEqual(65, VBStrings.Asc("ABC"));
        Assert.AreEqual(128, VBStrings.Asc("€"));
        Assert.AreEqual(228, VBStrings.Asc("ä"));
        Assert.AreEqual(255, VBStrings.Asc("ÿ"));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Asc("Ā"));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Asc("\u0081"));
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
    public void StrComp_ReturnsNormalizedOrderingForBinaryAndTextComparison()
    {
        Assert.AreEqual(1, VBStrings.StrComp("a", "B", 0));
        Assert.AreEqual(0, VBStrings.StrComp("a", "A", 1));
        Assert.AreEqual(1, VBStrings.StrComp("b", "A", 1));
        Assert.AreEqual(0, VBStrings.StrComp("same", "same", 0));
    }

    [TestMethod]
    public void Replace_SupportsStartCountAndTextComparison()
    {
        Assert.AreEqual("a-x-x", VBStrings.Replace("a-b-b", "b", "x", 1, -1, 0));
        // Das Ergebnis beginnt bei Start -- "a-" gehört nicht mehr dazu.
        Assert.AreEqual("x-B", VBStrings.Replace("a-b-B", "b", "x", 3, 1, 1));
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
    public void StrReverseAndFormatHelpers_RespectVb6ArgumentConventions()
    {
        Assert.AreEqual("cba", VBStrings.StrReverse("abc"));
        Assert.AreEqual(
            "1,234.50",
            VBStrings.FormatNumber(1234.5d, -1, -2, -2, -2, VBCompatibilityProfile.Deterministic));
        Assert.AreEqual(
            ".50",
            VBStrings.FormatNumber(0.5d, 2, 0, 0, 0, VBCompatibilityProfile.Deterministic));
        Assert.AreEqual(
            "(1,234.50)",
            VBStrings.FormatNumber(-1234.5d, 2, -1, -1, -1, VBCompatibilityProfile.Deterministic));
        Assert.AreEqual(
            "$12.5",
            VBStrings.FormatCurrency(12.5d, 1, -2, -2, -2, VBCompatibilityProfile.Deterministic));
        Assert.AreEqual(
            "12.5%",
            VBStrings.FormatPercent(0.125d, 1, -2, -2, -2, VBCompatibilityProfile.Deterministic));
        Assert.AreEqual(
            "2020-01-02",
            VBStrings.FormatDateTime(new DateTime(2020, 1, 2), 2, VBCompatibilityProfile.Deterministic));
    }

    [TestMethod]
    public void Partition_ProducesFixedWidthRangesAndValidatesItsBounds()
    {
        Assert.AreEqual("10:19", VBStrings.Partition(17, 0, 99, 10));
        Assert.AreEqual("  :-1", VBStrings.Partition(-1, 0, 99, 10));
        Assert.AreEqual("100:  ", VBStrings.Partition(100, 0, 99, 10));
        Assert.ThrowsException<VB6RuntimeErrorException>(() => VBStrings.Partition(1, -1, 9, 1));
        Assert.ThrowsException<VB6RuntimeErrorException>(() => VBStrings.Partition(1, 0, 9, 0));
    }

    [TestMethod]
    public void StrConv_SupportsJapaneseWidthAndKanaFlags()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");

            Assert.AreEqual("ＡＢＣ　１２３", VBStrings.StrConv("ABC 123", 4, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("ガ", VBStrings.StrConv("ｶﾞ", 4, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("ABC 123", VBStrings.StrConv("ＡＢＣ　１２３", 8, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("ｶﾀｶﾅ", VBStrings.StrConv("カタカナ", 8, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("カタカナ", VBStrings.StrConv("かたかな", 16, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("かたかな", VBStrings.StrConv("カタカナ", 32, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual("ＡＢＣ", VBStrings.StrConv("abc", 1 | 4, 0, VBCompatibilityProfile.VB6Sp6));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void StrConv_RejectsLocaleGatedFlagsOutsideApplicableLocales()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.ThrowsException<InvalidOperationException>(() =>
                VBStrings.StrConv("ABC", 4, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.ThrowsException<InvalidOperationException>(() =>
                VBStrings.StrConv("かな", 16, 0, VBCompatibilityProfile.VB6Sp6));
            Assert.ThrowsException<ArgumentException>(() => VBStrings.StrConv("ABC", 64 | 128, 0));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void StrConv_UsesExplicitLcidInVb6Profile()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.AreEqual(
                "İ",
                VBStrings.StrConv("i", 1, 1055, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "Ａ",
                VBStrings.StrConv("A", 4, 1041, VBCompatibilityProfile.VB6Sp6));
            Assert.AreEqual(
                "I",
                VBStrings.StrConv("i", 1, 1055, VBCompatibilityProfile.Deterministic));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
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
