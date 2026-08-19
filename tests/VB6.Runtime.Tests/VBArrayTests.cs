using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBArrayTests
{
    [TestMethod]
    public void Array_PreservesNonZeroLowerBounds()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 3));

        Assert.AreEqual(1, array.Rank);
        Assert.AreEqual(3, array.Length);
        Assert.AreEqual(1, array.LBound());
        Assert.AreEqual(3, array.UBound());

        array[1] = 10;
        array[3] = 30;
        Assert.AreEqual(10, array[1]);
        Assert.AreEqual(30, array[3]);
    }

    [TestMethod]
    public void Array_PreservesMultipleDimensions()
    {
        var array = new VBArray<string>(
            new VBArrayBound(0, 1),
            new VBArrayBound(5, 7));

        Assert.AreEqual(2, array.Rank);
        Assert.AreEqual(6, array.Length);
        Assert.AreEqual(0, array.LBound(1));
        Assert.AreEqual(1, array.UBound(1));
        Assert.AreEqual(5, array.LBound(2));
        Assert.AreEqual(7, array.UBound(2));

        array[1, 6] = "ok";
        Assert.AreEqual("ok", array[1, 6]);
    }

    [TestMethod]
    public void Array_StringElementsUseVbEmptyStringDefault()
    {
        var array = new VBArray<string>(new VBArrayBound(1, 2));

        Assert.AreEqual(string.Empty, array[1]);
        Assert.AreEqual(string.Empty, array[2]);
    }

    [TestMethod]
    public void Array_ClearResetsElementsAndPreservesBounds()
    {
        var array = new VBArray<int>(
            new VBArrayBound(-1, 1),
            new VBArrayBound(4, 5));
        array[-1, 4] = 10;
        array[1, 5] = 20;

        array.Clear();

        Assert.AreEqual(-1, array.LBound(1));
        Assert.AreEqual(1, array.UBound(1));
        Assert.AreEqual(4, array.LBound(2));
        Assert.AreEqual(5, array.UBound(2));
        Assert.AreEqual(0, array[-1, 4]);
        Assert.AreEqual(0, array[1, 5]);
    }

    [TestMethod]
    public void Array_ClearRestoresVbStringDefaults()
    {
        var array = new VBArray<string>(new VBArrayBound(0, 1));
        array[0] = "first";
        array[1] = "second";

        array.Clear();

        Assert.AreEqual(string.Empty, array[0]);
        Assert.AreEqual(string.Empty, array[1]);
    }

    [TestMethod]
    public void Array_IndexerCanBePassedByReference()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 2));
        array[1] = 10;

        Increment(ref array[1]);

        Assert.AreEqual(11, array[1]);
    }

    [TestMethod]
    public void Array_ReDimPreserveCanGrowLastDimension()
    {
        var array = new VBArray<int>(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 6));
        array[1, 5] = 15;
        array[1, 6] = 16;
        array[2, 5] = 25;
        array[2, 6] = 26;

        var resized = array.ReDimPreserve(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 8));

        Assert.AreEqual(8, resized.Length);
        Assert.AreEqual(15, resized[1, 5]);
        Assert.AreEqual(16, resized[1, 6]);
        Assert.AreEqual(25, resized[2, 5]);
        Assert.AreEqual(26, resized[2, 6]);
        Assert.AreEqual(0, resized[1, 7]);
        Assert.AreEqual(0, resized[2, 8]);
    }

    [TestMethod]
    public void Array_ReDimPreserveCanShrinkLastDimension()
    {
        var array = new VBArray<int>(new VBArrayBound(-1, 2));
        array[-1] = 10;
        array[0] = 20;
        array[1] = 30;
        array[2] = 40;

        var resized = array.ReDimPreserve(new VBArrayBound(-1, 0));

        Assert.AreEqual(-1, resized.LBound());
        Assert.AreEqual(0, resized.UBound());
        Assert.AreEqual(10, resized[-1]);
        Assert.AreEqual(20, resized[0]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = resized[1]);
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsRankChange()
    {
        var array = new VBArray<int>(new VBArrayBound(0, 2));

        Assert.ThrowsException<ArgumentException>(() => array.ReDimPreserve(
            new VBArrayBound(0, 2),
            new VBArrayBound(0, 2)));
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsEarlierDimensionChange()
    {
        var array = new VBArray<int>(
            new VBArrayBound(0, 1),
            new VBArrayBound(0, 2));

        Assert.ThrowsException<ArgumentException>(() => array.ReDimPreserve(
            new VBArrayBound(0, 2),
            new VBArrayBound(0, 3)));
    }

    [TestMethod]
    public void Array_ReDimPreserveRejectsLowerBoundChange()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 3));

        Assert.ThrowsException<ArgumentException>(() =>
            array.ReDimPreserve(new VBArrayBound(0, 3)));
    }

    [TestMethod]
    public void Array_RejectsOutOfRangeSubscriptsAndDimensions()
    {
        var array = new VBArray<int>(new VBArrayBound(-2, 2));

        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = array[3]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = array[0, 0]);
        Assert.ThrowsException<IndexOutOfRangeException>(() => array.LBound(2));
    }

    [TestMethod]
    public void Array_RejectsInvalidBounds()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            new VBArray<int>(new VBArrayBound(5, 4)));
        Assert.ThrowsException<ArgumentException>(() => new VBArray<int>());
    }

    private static void Increment(ref int value) => value++;
}
