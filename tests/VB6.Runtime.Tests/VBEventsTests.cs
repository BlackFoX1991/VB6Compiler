using System.Reflection;
using System.Runtime.InteropServices;
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

    [TestMethod]
    public void ComEventDelegateType_UsesAutomationMarshallingForVariantValues()
    {
        var method = typeof(AutomationEventTarget).GetMethod(
            "OnAutomation",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var delegateType = VBEvents.GetComEventDelegateType(method);
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters();

        Assert.AreSame(delegateType, VBEvents.GetComEventDelegateType(method));
        Assert.AreEqual(typeof(object).MakeByRefType(), parameters[0].ParameterType);
        Assert.AreEqual(UnmanagedType.Struct, parameters[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.VariantBool, parameters[1].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(UnmanagedType.BStr, parameters[2].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.IsNull(parameters[3].GetCustomAttribute<MarshalAsAttribute>());
    }

    [TestMethod]
    public void ComEventDelegateType_UsesTypedSafeArrayForAutomationArrays()
    {
        var method = typeof(SafeArrayEventTarget).GetMethod(
            "OnValues",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var delegateType = VBEvents.GetComEventDelegateType(method);
        var parameter = delegateType.GetMethod("Invoke")!.GetParameters().Single();
        var marshal = parameter.GetCustomAttribute<MarshalAsAttribute>();

        Assert.AreEqual(typeof(Array).MakeByRefType(), parameter.ParameterType);
        Assert.AreEqual(UnmanagedType.SafeArray, marshal?.Value);
        Assert.AreEqual(VarEnum.VT_I4, marshal?.SafeArraySubType);
    }

    [TestMethod]
    public void VBArray_ConvertsToClrArrayWithBoundsAndElementOrder()
    {
        var source = new VBArray<int>(new VBArrayBound(-2, 1), new VBArrayBound(3, 4));
        source[-2, 3] = 10;
        source[-2, 4] = 20;
        source[1, 3] = 30;
        source[1, 4] = 40;

        var clr = VBArrayOperations.ToClrArray(source)!;
        Assert.AreEqual(typeof(int), clr.GetType().GetElementType());
        Assert.AreEqual(2, clr.Rank);
        Assert.AreEqual(-2, clr.GetLowerBound(0));
        Assert.AreEqual(3, clr.GetLowerBound(1));
        Assert.AreEqual(10, clr.GetValue(-2, 3));
        Assert.AreEqual(40, clr.GetValue(1, 4));

        var roundTrip = VBArrayOperations.FromObject<int>(clr)!;
        Assert.AreEqual(-2, roundTrip.LBound(1));
        Assert.AreEqual(3, roundTrip.LBound(2));
        Assert.AreEqual(30, roundTrip[1, 3]);
    }

    [TestMethod]
    public void ComEventAdapter_ConvertsSafeArrayAndWritesBackReplacement()
    {
        var method = typeof(SafeArrayEventTarget).GetMethod(
            "OnValues",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var target = new SafeArrayEventTarget();
        var native = Array.CreateInstance(typeof(int), new[] { 2 }, new[] { -1 });
        native.SetValue(7, -1);
        native.SetValue(8, 0);
        var arguments = new object?[] { native };

        VBEvents.CreateComEventDelegate(target, method).DynamicInvoke(arguments);

        var replacement = (Array)arguments[0]!;
        Assert.AreEqual(4, replacement.GetLowerBound(0));
        Assert.AreEqual(5, replacement.GetUpperBound(0));
        Assert.AreEqual(42, replacement.GetValue(4));
    }

    [TestMethod]
    public void SubscribeMethod_DoesNotBindComProvidersToWrapperClrEvents()
    {
        var source = new ComProviderEventSource();
        var target = new ComProviderEventTarget();

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        source.Raise();
        Assert.AreEqual(0, target.CallCount);

        VBEvents.Raise(source, "Changed", Array.Empty<object?>());
        Assert.AreEqual(1, target.CallCount);
        VBEvents.UnsubscribeMethod(source, "Changed", target, "OnChanged");
    }

    [TestMethod]
    public void UnsubscribeMethod_RemovesTheRequestedMethodSubscription()
    {
        var source = new ClrEventSource();
        var target = new ClrEventTarget();

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        VBEvents.UnsubscribeMethod(source, "Changed", target, "OnChanged");
        source.Raise(42, "removed");

        Assert.AreEqual(0, target.CallCount);

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        VBEvents.UnsubscribeMethod(null, "Changed", target, "OnChanged");
        source.Raise(99, "removed-all");

        Assert.AreEqual(0, target.CallCount);
    }

    [TestMethod]
    public void UnsubscribeObject_RemovesSubscriptionsBySourceOrTarget()
    {
        var source = new ClrEventSource();
        var target = new ClrEventTarget();

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        VBEvents.UnsubscribeObject(source);
        source.Raise(1, "source");
        Assert.AreEqual(0, target.CallCount);

        VBEvents.SubscribeMethod(source, "Changed", target, "OnChanged");
        VBEvents.UnsubscribeObject(target);
        source.Raise(2, "target");
        Assert.AreEqual(0, target.CallCount);
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

    private sealed class ComProviderEventSource : IVBComObjectProvider
    {
        public object? ComObject => null;

        public event Action? Changed;

        public void Raise() => Changed?.Invoke();
    }

    private sealed class ComProviderEventTarget
    {
        public int CallCount { get; private set; }

        private void OnChanged() => CallCount++;
    }

    private sealed class AutomationEventTarget
    {
        private void OnAutomation(
            [MarshalAs(UnmanagedType.Struct)] ref object? value,
            bool flag,
            string text,
            object? source)
        {
            _ = value;
            _ = flag;
            _ = text;
            _ = source;
        }
    }

    private sealed class SafeArrayEventTarget
    {
        private void OnValues(ref VBArray<int> values)
        {
            values = new VBArray<int>(new VBArrayBound(4, 5));
            values[4] = 42;
        }
    }

    private delegate void ByRefChangedHandler(ref int value);
}
