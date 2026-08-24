using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

[STATestClass]
public sealed class WinFormsHostTests
{
    [STATestMethod]
    public void HostCreatesDesignerControlsAndMapsVb6Properties()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);
        var control = host.CreateControl(owner, "Button1", "CommandButton");

        Assert.IsInstanceOfType<Button>(control);
        Assert.IsTrue(host.TrySetMember(control!, "Caption", Array.Empty<object?>(), "Run"));
        Assert.IsTrue(host.TrySetMember(control!, "Left", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TrySetMember(control!, "Width", Array.Empty<object?>(), 2880));

        Assert.IsTrue(host.TryGetMember(control!, "Text", Array.Empty<object?>(), out var text));
        Assert.AreEqual("Run", text);
        Assert.IsTrue(host.TryGetMember(control!, "Caption", Array.Empty<object?>(), out var caption));
        Assert.AreEqual("Run", caption);
        Assert.IsTrue(host.TryGetMember(control!, "Left", Array.Empty<object?>(), out var left));
        Assert.AreEqual(1440, left);
        Assert.IsTrue(host.TryGetMember(owner, "Button1", Array.Empty<object?>(), out var named));
        Assert.AreSame(control, named);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostConnectsConventionalVb6ControlHandlers()
    {
        using var host = new WinFormsHost();
        var owner = new EventSink();

        host.Load(owner);
        var textBox = (TextBox)host.CreateControl(owner, "Text1", "TextBox")!;
        textBox.Text = "changed";

        Assert.AreEqual(1, owner.ChangeCount);
        host.Unload(owner);
    }

    [STATestMethod]
    public void HostPlacesQualifiedDesignerControlsInsideTheirParent()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);
        var frame = (GroupBox)host.CreateControl(owner, "Frame1", "Frame")!;
        var textBox = (TextBox)host.CreateControl(owner, "Frame1.Text1", "TextBox")!;

        Assert.AreSame(frame, textBox.Parent);
        Assert.AreSame(textBox, frame.Controls[0]);
        Assert.IsTrue(host.TryGetMember(owner, "Text1", Array.Empty<object?>(), out var named));
        Assert.AreSame(textBox, named);
        Assert.AreEqual(1, frame.Controls.Count);

        host.Unload(owner);
    }

    [STATestMethod]
    public void VbEventSubscriptionsUseTheWinFormsEventBridge()
    {
        using var host = new WinFormsHost();
        var owner = new ExplicitEventSink();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            var textBox = (TextBox)host.CreateControl(owner, "Input", "TextBox")!;
            VBEvents.SubscribeMethod(textBox, "TextChanged", owner, "OnChanged");
            textBox.Text = "changed";
            VBEvents.SubscribeMethod(null, "TextChanged", owner, "OnChanged");
            textBox.Text = "detached";

            Assert.AreEqual(1, owner.ChangeCount);
            host.Unload(owner);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    private sealed class EventSink
    {
        public int ChangeCount { get; private set; }

        private void Text1_Change() => ChangeCount++;
    }

    private sealed class ExplicitEventSink
    {
        public int ChangeCount { get; private set; }

        private void OnChanged() => ChangeCount++;
    }
}
