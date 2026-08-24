using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
}
