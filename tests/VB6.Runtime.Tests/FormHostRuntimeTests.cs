namespace VB6.Runtime.Tests;

[TestClass]
public sealed class FormHostRuntimeTests
{
    [TestMethod]
    public void DynamicDispatch_ForwardsFormMembersToConfiguredHost()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();
        try
        {
            VBInteraction.Host = host;

            VBDynamicDispatch.SetMember(form, "Caption", "Main window");
            Assert.AreEqual("Main window", VBDynamicDispatch.GetMember(form, "Caption"));
            VBDynamicDispatch.InvokeMember(form, "Show", Arguments());

            Assert.AreEqual("Main window", host.Properties["Caption"]);
            Assert.AreEqual(1, host.ShowCount);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void InteractionLifecycle_UsesConfiguredHost()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();
        try
        {
            VBInteraction.Host = host;

            VBInteraction.Load(form);
            VBInteraction.DoEvents();
            VBInteraction.Show(form);
            VBInteraction.Unload(form);

            Assert.AreEqual(1, host.LoadCount);
            Assert.AreEqual(1, host.DoEventsCount);
            Assert.AreEqual(1, host.ShowCount);
            Assert.AreEqual(1, host.UnloadCount);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void SendKeys_ForwardsKeyboardInputToConfiguredHost()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        try
        {
            VBInteraction.Host = host;

            VBInteraction.SendKeys("{DOWN}", wait: true);

            Assert.AreEqual("{DOWN}", host.SentKeys);
            Assert.IsTrue(host.WaitForKeys);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void DesignerControlCreation_UsesHostAndHeadlessProxyFallback()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();
        try
        {
            VBInteraction.Host = host;
            var hosted = VBInteraction.CreateControl(form, "Button1", "CommandButton");
            Assert.AreSame(host.CreatedControl, hosted);

            VBInteraction.Host = null;
            var proxy = VBInteraction.CreateControl(form, "Text1", "TextBox");
            Assert.IsInstanceOfType<VBControlProxy>(proxy);
            Assert.AreEqual("Text1", ((VBControlProxy)proxy).Name);
            Assert.AreEqual("TextBox", ((VBControlProxy)proxy).TypeName);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    private static VBArray<object> Arguments(params object?[] values)
    {
        var result = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = values[index]!;
        }

        return result;
    }

    [TestMethod]
    public void LoadControlArrayElement_GrowsTheArrayAndClonesTheDesignerElement()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();

        try
        {
            VBInteraction.Host = host;
            var designerElement = new VBControlProxy("ctlButton(0)", "CommandButton", form);
            var array = new VBArray<object?>(new VBArrayBound(0, 0));
            array[0] = designerElement;

            var grown = VBInteraction.LoadControlArrayElement(array, 1, "ctlButton", form);

            Assert.IsNotNull(grown);
            Assert.AreEqual(0, grown!.LBound());
            Assert.AreEqual(1, grown.UBound(), "Load must grow the array to reach the new index.");
            Assert.IsNotNull(grown[1]);
            Assert.AreSame(designerElement, grown[0], "The designer element must survive the growth.");

            // VB6 clones the lowest existing element, which is the one the designer created.
            Assert.AreEqual(1, host.LoadedElements.Count);
            Assert.AreEqual(("ctlButton", 1, (object?)designerElement), host.LoadedElements[0]);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void LoadControlArrayElement_RejectsAnExistingOrNegativeIndex()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();

        try
        {
            VBInteraction.Host = host;
            var array = new VBArray<object?>(new VBArrayBound(0, 0));
            array[0] = new VBControlProxy("ctlButton(0)", "CommandButton", form);

            var alreadyLoaded = Assert.ThrowsException<VB6RaisedError>(() =>
                VBInteraction.LoadControlArrayElement(array, 0, "ctlButton", form));
            Assert.AreEqual(360, alreadyLoaded.Number, "VB6 reports Object already loaded.");

            var outOfRange = Assert.ThrowsException<VB6RaisedError>(() =>
                VBInteraction.LoadControlArrayElement(array, -1, "ctlButton", form));
            Assert.AreEqual(9, outOfRange.Number, "VB6 reports Subscript out of range below LBound.");
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void UnloadControlArrayElement_ClearsTheSlotButKeepsTheBounds()
    {
        var previousHost = VBInteraction.Host;
        var host = new TestHost();
        var form = new object();

        try
        {
            VBInteraction.Host = host;
            var loaded = new VBControlProxy("ctlButton(1)", "CommandButton", form);
            var array = new VBArray<object?>(new VBArrayBound(0, 1));
            array[0] = new VBControlProxy("ctlButton(0)", "CommandButton", form);
            array[1] = loaded;

            var result = VBInteraction.UnloadControlArrayElement(array, 1, "ctlButton", form);

            Assert.IsNotNull(result);
            Assert.IsNull(result![1], "Unload clears the slot.");
            Assert.AreEqual(1, result.UBound(), "VB6 keeps the bounds so the index stays addressable.");
            Assert.AreEqual(1, host.UnloadedElements.Count);
            Assert.AreEqual(("ctlButton", 1, (object?)loaded), host.UnloadedElements[0]);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    private sealed class TestHost : IVB6Host
    {
        public Dictionary<string, object?> Properties { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public int DoEventsCount { get; private set; }

        public int LoadCount { get; private set; }

        public int UnloadCount { get; private set; }

        public int ShowCount { get; private set; }

        public string? SentKeys { get; private set; }

        public bool WaitForKeys { get; private set; }

        public object? CreatedControl { get; private set; }

        public void DoEvents() => DoEventsCount++;

        public void SendKeys(string keys, bool wait)
        {
            SentKeys = keys;
            WaitForKeys = wait;
        }

        public void Load(object target) => LoadCount++;

        public void Unload(object target) => UnloadCount++;

        public object? CreateControl(object owner, string name, string typeName)
        {
            CreatedControl = new VBControlProxy(name, typeName, owner);
            return CreatedControl;
        }

        public List<(string Name, int Index, object? Template)> LoadedElements { get; } = new();

        public List<(string Name, int Index, object? Element)> UnloadedElements { get; } = new();

        public object? LoadControlArrayElement(object owner, string name, int index, object? template)
        {
            LoadedElements.Add((name, index, template));
            return new VBControlProxy($"{name}({index})", "CommandButton", owner);
        }

        public void UnloadControlArrayElement(object owner, string name, int index, object? element) =>
            UnloadedElements.Add((name, index, element));

        public bool TryGetMember(object target, string memberName, object?[] arguments, out object? value)
        {
            if (Properties.TryGetValue(memberName, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public bool TrySetMember(object target, string memberName, object?[] arguments, object? value)
        {
            Properties[memberName] = value;
            return true;
        }

        public bool TryInvokeMember(object target, string memberName, object?[] arguments, out object? result)
        {
            result = null;
            if (string.Equals(memberName, "Show", StringComparison.OrdinalIgnoreCase))
            {
                ShowCount++;
                return true;
            }

            return false;
        }

        public IEnumerable<object?>? EnumerateControls(object? target) => null;
    }
}
