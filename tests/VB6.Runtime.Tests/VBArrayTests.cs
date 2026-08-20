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

    [TestMethod]
    public void Array_ResizePreserveCopiesOverlappingValues()
    {
        var array = new VBArray<int>(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 6));
        array[1, 5] = 15;
        array[2, 6] = 26;

        var resized = array.ResizePreserve(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 8));

        Assert.AreEqual(15, resized[1, 5]);
        Assert.AreEqual(26, resized[2, 6]);
        Assert.AreEqual(0, resized[2, 8]);
        Assert.AreEqual(8, resized.UBound(2));
    }

    [TestMethod]
    public void Array_ResizePreserveRejectsChangingEarlierDimensions()
    {
        var array = new VBArray<int>(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 6));

        Assert.ThrowsException<InvalidOperationException>(() =>
            array.ResizePreserve(
                new VBArrayBound(0, 2),
                new VBArrayBound(5, 8)));
    }

    [TestMethod]
    public void Array_ValuesEnumeratesStoredItems()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 3));
        array[1] = 10;
        array[2] = 20;
        array[3] = 30;

        CollectionAssert.AreEqual(
            new[] { 10, 20, 30 },
            array.Values().ToArray());
    }

    [TestMethod]
    public void Array_FromValuesCreatesZeroBasedArrayAndSupportsEmpty()
    {
        var filled = VBArray<int>.FromValues(10, 20);

        Assert.AreEqual(1, filled.Rank);
        Assert.AreEqual(2, filled.Length);
        Assert.AreEqual(0, filled.LBound());
        Assert.AreEqual(1, filled.UBound());
        CollectionAssert.AreEqual(new[] { 10, 20 }, filled.Values().ToArray());

        var empty = VBArray<int>.FromValues();

        Assert.AreEqual(1, empty.Rank);
        Assert.AreEqual(0, empty.Length);
        Assert.AreEqual(0, empty.LBound());
        Assert.AreEqual(-1, empty.UBound());
        Assert.AreEqual(0, empty.Values().Count());
        Assert.ThrowsException<IndexOutOfRangeException>(() => _ = empty[0]);
    }

    [TestMethod]
    public void Array_ElementReturnsMutableReference()
    {
        var array = new VBArray<int>(new VBArrayBound(1, 2));
        array[1] = 10;

        ref var item = ref array.Element(1);
        item = 25;

        Assert.AreEqual(25, array[1]);
    }

    [TestMethod]
    public void Array_ClonePreservesBoundsAndCopiesValues()
    {
        var array = new VBArray<int>(new VBArrayBound(5, 6));
        array[5] = 50;
        array[6] = 60;

        var copy = array.Clone();
        array[5] = 99;

        Assert.AreEqual(5, copy.LBound());
        Assert.AreEqual(6, copy.UBound());
        Assert.AreEqual(50, copy[5]);
        Assert.AreEqual(60, copy[6]);
    }

    [TestMethod]
    public void Array_CloneUsesElementCloner()
    {
        var array = new VBArray<Box>(new VBArrayBound(1, 1));
        array[1] = new Box { Value = 10 };

        var copy = array.Clone(static box => new Box { Value = box.Value });
        array[1].Value = 99;

        Assert.AreEqual(10, copy[1].Value);
    }

    private sealed class Box
    {
        public int Value { get; set; }
    }
}
