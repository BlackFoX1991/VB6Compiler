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
    public void Array_IndexerCanBePassedByReference()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 2));
        array[1] = 10;

        Increment(ref array[1]);

        Assert.AreEqual(11, array[1]);
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
