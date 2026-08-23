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
        Assert.AreEqual(4, VBStrings.Len(42f));
        Assert.AreEqual(8, VBStrings.Len(42d));
        Assert.AreEqual(2, VBStrings.Len(true));
        Assert.AreEqual(8, VBStrings.Len(VBConversions.CCur(42m)));
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
    public void Chr_RejectsExtendedAnsiUntilCodePageSemanticsAreModeled()
    {
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Chr(-1));
        Assert.ThrowsException<NotSupportedException>(() => VBStrings.Chr(128));
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
    public void String_RepeatsNumericAndStringCharacters()
    {
        Assert.AreEqual("xxx", VBStrings.String(3, "x"));
        Assert.AreEqual("AAA", VBStrings.String(3, 65));
        Assert.AreEqual("\0\0", VBStrings.String(2, 0));
    }
}
