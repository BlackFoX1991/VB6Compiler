using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBEventsTests
{
    [TestMethod]
    public void Raise_DispatchesToCurrentReferenceAndUnsubscribeStopsDispatch()
    {
        var source = new object();
        var calls = new List<object?[]>();
        Action<object?[]> handler = arguments => calls.Add(arguments);

        VBEvents.Subscribe(source, "Changed", handler);
        VBEvents.Raise(source, "Changed", new object?[] { 42, "ok" });
        VBEvents.Unsubscribe(source, "Changed", handler);
        VBEvents.Raise(source, "Changed", new object?[] { 99 });

        Assert.AreEqual(1, calls.Count);
        CollectionAssert.AreEqual(new object?[] { 42, "ok" }, calls[0]);
    }

    [TestMethod]
    public void Raise_DoesNotCrossReferenceIdentities()
    {
        var first = new object();
        var second = new object();
        var firstCalls = 0;
        var secondCalls = 0;
        Action<object?[]> firstHandler = _ => firstCalls++;
        Action<object?[]> secondHandler = _ => secondCalls++;

        VBEvents.Subscribe(first, "Changed", firstHandler);
        VBEvents.Subscribe(second, "Changed", secondHandler);
        VBEvents.Raise(first, "Changed", Array.Empty<object?>());

        Assert.AreEqual(1, firstCalls);
        Assert.AreEqual(0, secondCalls);
        VBEvents.Unsubscribe(first, "Changed", firstHandler);
        VBEvents.Unsubscribe(second, "Changed", secondHandler);
    }
}
