using System.Reflection;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class StringIntrinsicRuntimeTests
{
    [TestMethod]
    public void Len_ReturnsCharacterCountForStringsAndZeroForEmpty()
    {
        Assert.AreEqual(0, VBStrings.Len(null));
        Assert.AreEqual(0, VBStrings.Len(string.Empty));
        Assert.AreEqual(5, VBStrings.Len("Hello"));
        Assert.AreEqual(3, VBStrings.Len("äöü"));
    }

    [TestMethod]
    public void Len_ReturnsVb6StorageSizeForSupportedScalarVariants()
    {
        Assert.AreEqual(1, VBStrings.Len((byte)42));
        Assert.AreEqual(2, VBStrings.Len((short)42));
        Assert.AreEqual(4, VBStrings.Len(42));
        Assert.AreEqual(8, VBStrings.Len(42L));
        Assert.AreEqual(2, VBStrings.Len((ushort)42));
        Assert.AreEqual(4, VBStrings.Len(42u));
        Assert.AreEqual(8, VBStrings.Len(42UL));
        Assert.AreEqual(IntPtr.Size, VBStrings.Len(new IntPtr(42)));
        Assert.AreEqual(4, VBStrings.Len(42f));
        Assert.AreEqual(8, VBStrings.Len(42d));
        Assert.AreEqual(2, VBStrings.Len(true));
        Assert.AreEqual(8, VBStrings.Len(new VBDateValue(43832d)));
        Assert.AreEqual(8, VBStrings.Len(VBConversions.CCur(42m)));
    }

    [TestMethod]
    public void LenAndLenB_TreatDateTimeAsEightByteOleDateVariant()
    {
        var dateTime = new DateTime(2020, 1, 2, 3, 4, 5);
        var dateValue = new VBDateValue(dateTime.ToOADate());

        Assert.AreEqual(VBStrings.Len(dateValue), VBStrings.Len(dateTime));
        Assert.AreEqual(VBStrings.LenB(dateValue), VBStrings.LenB(dateTime));
        Assert.AreEqual(8, VBStrings.Len(dateTime));
        Assert.AreEqual(8, VBStrings.LenB(dateTime));
    }

    [TestMethod]
    public void Len_RejectsUnsupportedClrPayloads()
    {
        Assert.ThrowsException<InvalidCastException>(() => VBStrings.Len(new object()));
    }

    [TestMethod]
    public void Mid_UsesOneBasedPositionsAndClipsLength()
    {
        Assert.AreEqual("bcd", VBStrings.Mid("abcdef", 2, 3));
        Assert.AreEqual("ef", VBStrings.Mid("abcdef", 5, 20));
        Assert.AreEqual(string.Empty, VBStrings.Mid("abcdef", 9, 3));
        Assert.AreEqual(string.Empty, VBStrings.Mid("abcdef", 2, 0));
    }

    [TestMethod]
    public void Mid_RejectsInvalidStartOrLength()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBStrings.Mid("abc", 0, 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBStrings.Mid("abc", 1, -1));
    }

    [TestMethod]
    public void Chr_ReturnsReachableAsciiCharacters()
    {
        Assert.AreEqual("\0", VBStrings.Chr(0));
        Assert.AreEqual("\"", VBStrings.Chr(34));
        Assert.AreEqual("A", VBStrings.Chr(65));
        Assert.AreEqual(((char)127).ToString(), VBStrings.Chr(127));
    }

    [TestMethod]
    public void Chr_UsesDeterministicWindows1252ForExtendedByteValues()
    {
        var originalCulture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ja-JP");
        try
        {
            Assert.AreEqual("€", VBStrings.Chr(128));
            Assert.AreEqual("‚", VBStrings.Chr(130));
            Assert.AreEqual("ÿ", VBStrings.Chr(255));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void ChrWAndAscW_RoundTripUnicodeCodeUnits()
    {
        Assert.AreEqual("\u20AC", VBStrings.ChrW(0x20AC));
        Assert.AreEqual("\uFFFF", VBStrings.ChrW(-1));
        Assert.AreEqual((short)0x20AC, VBStrings.AscW("\u20AC"));
        Assert.AreEqual((short)-1, VBStrings.AscW("\uFFFF"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBStrings.ChrW(-32769));
        Assert.ThrowsException<ArgumentException>(() => VBStrings.AscW(string.Empty));
    }

    [TestMethod]
    public void Chr_RejectsOutOfRangeAndUndefinedWindows1252Values()
    {
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Chr(-1));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Chr(256));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Chr(129));
    }

    [TestMethod]
    public void Val_ReadsDecimalAndPrefixedNumericPrefixes()
    {
        Assert.AreEqual(-12.5d, VBStrings.Val("  -12.5 points"));
        Assert.AreEqual(255d, VBStrings.Val("&HFF"));
        Assert.AreEqual(8d, VBStrings.Val("&O10"));
        Assert.AreEqual(0d, VBStrings.Val("not a number"));
    }

    [TestMethod]
    public void Hex_UsesUppercaseLongRepresentation()
    {
        Assert.AreEqual("FF", VBStrings.Hex(255));
        Assert.AreEqual("FFFFFFFF", VBStrings.Hex(-1));
    }

    [TestMethod]
    public void Str_ReservesLeadingSignSpaceForNonnegativeNumbers()
    {
        Assert.AreEqual(" 459", VBStrings.Str(459));
        Assert.AreEqual("-459.65", VBStrings.Str(-459.65));
        Assert.AreEqual(" 459.001", VBStrings.Str(459.001));
        Assert.AreEqual(" 0", VBStrings.Str(null));
        Assert.ThrowsException<InvalidCastException>(() => VBStrings.Str("459"));
    }

    [TestMethod]
    public void ObjectVariants_ResolveDefaultValuesAcrossStringIntrinsics()
    {
        var number = new NumericDefaultObject();
        var character = new CharacterDefaultObject();

        Assert.AreEqual(4, VBStrings.Len(number));
        Assert.AreEqual(4, VBStrings.LenB(number));
        Assert.AreEqual("7", VBStrings.Hex(number));
        Assert.AreEqual("7", VBStrings.Oct(number));
        Assert.AreEqual(" 7", VBStrings.Str(number));
        Assert.AreEqual("7", VBStrings.FormatValue(number, "0", 0, 0));
        Assert.IsTrue(VBStrings.IsNumeric(number));
        Assert.IsTrue(VBStrings.Like(number, "7", textCompare: false));
        Assert.AreEqual("AA", VBStrings.String(2, character));
    }

    [DefaultMember(nameof(Value))]
    private sealed class NumericDefaultObject
    {
        public int Value => 7;
    }

    [DefaultMember(nameof(Value))]
    private sealed class CharacterDefaultObject
    {
        public int Value => 65;
    }

    [TestMethod]
    public void Oct_UsesVb6LongRepresentationAndPreservesNull()
    {
        Assert.AreEqual("10", VBStrings.Oct(8));
        Assert.AreEqual("713", VBStrings.Oct(459));
        Assert.AreEqual("37777777777", VBStrings.Oct(-1));
        Assert.IsTrue(VBVariants.IsNull(VBStrings.Oct(VBVariants.NullValue())));
        Assert.AreEqual("0", VBStrings.Oct(VBVariants.EmptyValue()));
    }

    [TestMethod]
    public void CVar_PreservesVariantStateAndSubtype()
    {
        var date = new VBDateValue(43832d);

        Assert.AreSame(date, VBConversions.CVar(date));
        Assert.IsTrue(VBVariants.IsNull(VBConversions.CVar(VBVariants.NullValue())));
        Assert.AreEqual((short)3, VBVariants.VarType(VBConversions.CVar(42)));
    }

    [TestMethod]
    public void CStr_FormatsErrorVariantsAndRejectsNull()
    {
        Assert.AreEqual("Error 11", VBConversions.CStr(new VBErrorValue(11)));
        Assert.ThrowsException<InvalidCastException>(() => VBConversions.CStr(VBVariants.NullValue()));
    }

    [TestMethod]
    public void String_RepeatsNumericAndStringCharacters()
    {
        Assert.AreEqual("xxx", VBStrings.String(3, "x"));
        Assert.AreEqual("AAA", VBStrings.String(3, 65));
        Assert.AreEqual("\0\0", VBStrings.String(2, 0));
    }

    [TestMethod]
    public void FormatValue_FormatsSupportedNumericMasksInvariantly()
    {
        Assert.AreEqual("5,459.40", VBStrings.FormatValue(5459.4d, "##,##0.00", 0, 0));
        Assert.AreEqual("500.00%", VBStrings.FormatValue(5, "0.00%", 0, 0));
        Assert.AreEqual("$1,234.50", VBStrings.FormatValue(1234.5m, "Currency", 0, 0));
    }

    [TestMethod]
    public void FormatValue_FormatsSupportedStringCases()
    {
        Assert.AreEqual("hello", VBStrings.FormatValue("HELLO", "<", 0, 0));
        Assert.AreEqual("HELLO", VBStrings.FormatValue("hello", ">", 0, 0));
        Assert.AreEqual("unchanged", VBStrings.FormatValue("unchanged", string.Empty, 0, 0));
    }

    [TestMethod]
    public void FormatValue_FormatsSupportedDateAndTimeMasks()
    {
        Assert.AreEqual("2020-01-02", VBStrings.FormatValue(new VBDateValue(43832), "yyyy-mm-dd", 0, 0));
        Assert.AreEqual(
            "2020-01-02",
            VBStrings.FormatValue(new DateTime(2020, 1, 2), "yyyy-mm-dd", 0, 0));
        Assert.AreEqual("12:00:00", VBStrings.FormatValue(new VBDateValue(0.5), "hh:nn:ss", 0, 0));
        Assert.AreEqual("Thursday, 02 January 2020", VBStrings.FormatValue(new VBDateValue(43832), "dddd, dd mmmm yyyy", 0, 0));
        Assert.AreEqual("12:00 PM", VBStrings.FormatValue(new VBDateValue(0.5), "h:nn AM/PM", 0, 0));
    }

    [TestMethod]
    public void FormatValue_UsesWeekdayWeekQuarterAndWeekArguments()
    {
        var date = new VBDateValue(43835); // Sunday, 5 January 2020.

        Assert.AreEqual("1 2 1 5", VBStrings.FormatValue(date, "w ww q y", 1, 1));
        Assert.AreEqual("1 2 1 5", VBStrings.FormatValue(date, "W WW Q Y", 1, 1));
        Assert.AreEqual("7 1 1 5", VBStrings.FormatValue(date, "w ww q y", 2, 2));
    }

    [TestMethod]
    public void FormatValue_RejectsUnsupportedDateMasks()
    {
        Assert.ThrowsException<NotSupportedException>(() =>
            VBStrings.FormatValue(new VBDateValue(45292), "zzz", 0, 0));
    }
}
