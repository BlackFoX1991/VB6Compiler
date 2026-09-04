namespace VB6.Runtime.Tests;

/// <summary>
/// <c>Val</c> strips blanks, tabs and line feeds from the whole argument before it reads a number,
/// not just from the front. That is deliberate in VB6 rather than incidental: the function was
/// built to read numbers out of fixed-width text, where the digits of one field can be spaced
/// apart. Trimming only the front made <c>Val(" 1 2 ")</c> answer 1 instead of 12.
/// </summary>
[TestClass]
public sealed class ValWhitespaceTests
{
    [TestMethod]
    public void Val_JoinsDigitsSeparatedByWhitespace()
    {
        Assert.AreEqual(12d, VBStrings.Val("  1 2  "));
        Assert.AreEqual(123d, VBStrings.Val("1 2 3"));
        Assert.AreEqual(123d, VBStrings.Val("1\t2\n3"));
    }

    [TestMethod]
    public void Val_StillStopsAtTheFirstCharacterThatIsNotPartOfANumber()
    {
        // Das Beispiel aus der VB6-Dokumentation selbst.
        Assert.AreEqual(24d, VBStrings.Val("24 and 57"));
        Assert.AreEqual(-12.5d, VBStrings.Val("  -12.5 points"));
        Assert.AreEqual(0d, VBStrings.Val("not a number"));
    }

    [TestMethod]
    public void Val_KeepsTheRadixPrefixesAndTheExponent()
    {
        Assert.AreEqual(255d, VBStrings.Val("&HFF"));
        Assert.AreEqual(255d, VBStrings.Val(" & H F F "));
        Assert.AreEqual(8d, VBStrings.Val("&O10"));
        Assert.AreEqual(150d, VBStrings.Val("1.5e2"));
    }

    [TestMethod]
    public void Val_AnswersZeroForNothingButWhitespace()
    {
        Assert.AreEqual(0d, VBStrings.Val(string.Empty));
        Assert.AreEqual(0d, VBStrings.Val("   "));
        Assert.AreEqual(0d, VBStrings.Val("\t\r\n"));
    }
}
