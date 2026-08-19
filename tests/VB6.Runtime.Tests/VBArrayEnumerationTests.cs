using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBArrayEnumerationTests
{
    [TestMethod]
    public void EnumerateValues_UsesRightmostDimensionFirstAndReturnsValues()
    {
        var array = new VBArray<int>(
            new VBArrayBound(1, 2),
            new VBArrayBound(5, 6));
        array[1, 5] = 15;
        array[1, 6] = 16;
        array[2, 5] = 25;
        array[2, 6] = 26;

        CollectionAssert.AreEqual(
            new[] { 15, 16, 25, 26 },
            array.EnumerateValues().ToArray());
    }

    [TestMethod]
    public void EnumerateValues_DoesNotExposeByRefArraySlots()
    {
        var array = new VBArray<MutableValue>(new VBArrayBound(1, 1));
        array[1] = new MutableValue { Number = 10 };

        var value = array.EnumerateValues().Single();
        value = new MutableValue { Number = 20 };

        Assert.AreEqual(10, array[1].Number);
        Assert.AreEqual(20, value.Number);
    }

    private sealed class MutableValue
    {
        public int Number { get; set; }
    }
}
