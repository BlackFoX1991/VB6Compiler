using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBCollectionTests
{
    [TestMethod]
    public void Collection_SupportsOneBasedAndKeyedLookup()
    {
        var collection = new VBCollection();
        collection.Add("first", "one", VBVariants.MissingValue(), VBVariants.MissingValue());
        collection.Add("second", "two", VBVariants.MissingValue(), VBVariants.MissingValue());

        Assert.AreEqual(2, collection.Count);
        Assert.AreEqual("first", collection.Item(1));
        Assert.AreEqual("second", collection.Item("two"));
    }

    [TestMethod]
    public void Collection_AddBeforeAndRemoveRebuildsKeyPositions()
    {
        var collection = new VBCollection();
        collection.Add("first", "one", VBVariants.MissingValue(), VBVariants.MissingValue());
        collection.Add("third", "three", VBVariants.MissingValue(), VBVariants.MissingValue());
        collection.Add("second", "two", 2, VBVariants.MissingValue());

        Assert.AreEqual("second", collection.Item(2));
        collection.Remove("one");
        Assert.AreEqual("second", collection.Item(1));
        Assert.AreEqual("third", collection.Item("three"));
    }
}
