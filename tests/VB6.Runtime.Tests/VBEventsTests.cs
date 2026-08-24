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

    [TestMethod]
    public void SubscribeMethod_ConnectsAndDisconnectsClrEvents()
    {
        var source = new ClrEventSource();
        var target = new ClrEventTarget();

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        source.Raise(42, "first");
        Assert.AreEqual(1, target.CallCount);
        Assert.AreEqual(42, target.Value);
        Assert.AreEqual("first", target.Text);

        VBEvents.SubscribeMethod(null, "Changed", target, "OnChanged");
        source.Raise(99, "second");
        Assert.AreEqual(1, target.CallCount);

        var byRefSource = new ByRefEventSource();
        var byRefTarget = new ByRefEventTarget();
        VBEvents.SubscribeMethod(byRefSource, "Changed", byRefTarget, "OnChanged");
        var value = 10;
        byRefSource.Raise(ref value);
        Assert.AreEqual(15, value);
    }

    private sealed class ClrEventSource
    {
        public event EventHandler<ChangedEventArgs>? Changed;

        public void Raise(int value, string text) => Changed?.Invoke(this, new ChangedEventArgs(value, text));
    }

    private sealed class ClrEventTarget
    {
        public int CallCount { get; private set; }
        public int Value { get; private set; }
        public string Text { get; private set; } = string.Empty;

        private void OnChanged(object? sender, ChangedEventArgs arguments)
        {
            _ = sender;
            CallCount++;
            Value = arguments.Value;
            Text = arguments.Text;
        }
    }

    private sealed class ChangedEventArgs : EventArgs
    {
        public ChangedEventArgs(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public int Value { get; }
        public string Text { get; }
    }

    private sealed class ByRefEventSource
    {
        public event ByRefChangedHandler? Changed;

        public void Raise(ref int value) => Changed?.Invoke(ref value);
    }

    private sealed class ByRefEventTarget
    {
        private void OnChanged(ref int value) => value += 5;
    }

    private delegate void ByRefChangedHandler(ref int value);
}
