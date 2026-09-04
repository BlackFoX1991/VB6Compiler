namespace VB6.Runtime.Tests;

/// <summary>
/// Two shapes that a breadth measurement of the string surface caught, both about what comes back
/// rather than what gets computed.
/// </summary>
[TestClass]
public sealed class ReplaceAndSplitShapeTests
{
    [TestMethod]
    public void Replace_ReturnsTheStringFromTheStartPositionOnward()
    {
        // Start is not "begin searching here while keeping the rest" -- the return value itself
        // begins at Start. Keeping the prefix made Replace look like an in-place edit.
        Assert.AreEqual("b-c", VBStrings.Replace("aXbXc", "X", "-", 3, -1, 0));
        Assert.AreEqual("a-b-c", VBStrings.Replace("aXbXc", "X", "-", 1, -1, 0));
        Assert.AreEqual("x-B", VBStrings.Replace("a-b-B", "b", "x", 3, 1, 1));
    }

    [TestMethod]
    public void Replace_ReturnsAZeroLengthStringWhenStartIsPastTheEnd()
    {
        Assert.AreEqual(string.Empty, VBStrings.Replace("aXbXc", "X", "-", 6, -1, 0));
        Assert.AreEqual(string.Empty, VBStrings.Replace("aXbXc", "X", "-", 9, -1, 0));
    }

    [TestMethod]
    public void Replace_KeepsTheRemainderWhenThereIsNothingToReplace()
    {
        Assert.AreEqual("bXc", VBStrings.Replace("aXbXc", "X", "-", 3, 0, 0));
        Assert.AreEqual("bXc", VBStrings.Replace("aXbXc", string.Empty, "-", 3, -1, 0));
    }

    [TestMethod]
    public void Split_ReturnsNoElementsForAZeroLengthExpression()
    {
        // The caller's next line is For i = 0 To UBound(parts). It has to run zero times.
        var parts = VBStrings.Split(string.Empty, ",", -1, 0);

        Assert.AreEqual(0, parts.LBound(1));
        Assert.AreEqual(-1, parts.UBound(1));
        Assert.AreEqual(0, parts.Length);
    }

    [TestMethod]
    public void Split_StillReturnsOneElementWhenNoDelimiterOccurs()
    {
        var parts = VBStrings.Split("a", ",", -1, 0);

        Assert.AreEqual(0, parts.UBound(1));
        Assert.AreEqual("a", parts[0]);
    }

    [TestMethod]
    public void Split_KeepsAnEmptyFieldBetweenTwoDelimiters()
    {
        var parts = VBStrings.Split("a,,b", ",", -1, 0);

        Assert.AreEqual(2, parts.UBound(1));
        Assert.AreEqual(string.Empty, parts[1]);
    }
}
