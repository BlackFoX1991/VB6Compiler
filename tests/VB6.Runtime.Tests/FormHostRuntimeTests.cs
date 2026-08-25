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
