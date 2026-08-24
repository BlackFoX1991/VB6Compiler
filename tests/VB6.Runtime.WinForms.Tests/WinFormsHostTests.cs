using System.Reflection;
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
    public void HostSupportsCommonListAndTextSelectionMembers()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);
        var list = (ListBox)host.CreateControl(owner, "List1", "ListBox")!;
        Assert.IsTrue(host.TryInvokeMember(list, "AddItem", new object?[] { "first" }, out _));
        Assert.IsTrue(host.TryInvokeMember(list, "AddItem", new object?[] { "second" }, out _));
        Assert.IsTrue(host.TryInvokeMember(list, "AddItem", new object?[] { "inserted", 1 }, out _));

        Assert.IsTrue(host.TryGetMember(list, "ListCount", Array.Empty<object?>(), out var count));
        Assert.AreEqual(3, count);
        Assert.IsTrue(host.TryGetMember(list, "List", new object?[] { 1 }, out var item));
        Assert.AreEqual("inserted", item);
        Assert.IsTrue(host.TrySetMember(list, "ListIndex", Array.Empty<object?>(), 1));
        Assert.AreEqual(1, list.SelectedIndex);
        Assert.IsTrue(host.TrySetMember(list, "List", new object?[] { 1 }, "changed"));
        Assert.AreEqual("changed", list.Items[1]);
        Assert.IsTrue(host.TryInvokeMember(list, "RemoveItem", new object?[] { 0 }, out _));
        Assert.AreEqual(2, list.Items.Count);
        Assert.IsTrue(host.TryInvokeMember(list, "Clear", Array.Empty<object?>(), out _));
        Assert.AreEqual(0, list.Items.Count);

        var textBox = (TextBox)host.CreateControl(owner, "Text1", "TextBox")!;
        textBox.Text = "abcdef";
        Assert.IsTrue(host.TrySetMember(textBox, "SelStart", Array.Empty<object?>(), 2));
        Assert.IsTrue(host.TrySetMember(textBox, "SelLength", Array.Empty<object?>(), 2));
        Assert.IsTrue(host.TryGetMember(textBox, "SelText", Array.Empty<object?>(), out var selected));
        Assert.AreEqual("cd", selected);
        Assert.IsTrue(host.TrySetMember(textBox, "SelText", Array.Empty<object?>(), "XY"));
        Assert.AreEqual("abXYef", textBox.Text);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostCreatesCommonDialogAsNonVisualComponent()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);
        var dialog = host.CreateControl(owner, "Dialog1", "MSComDlg.CommonDialog");

        Assert.IsInstanceOfType<CommonDialogProxy>(dialog);
        var commonDialog = (CommonDialogProxy)dialog!;
        commonDialog.Filter = "Text files (*.txt)|*.txt";
        commonDialog.FileName = "sample.txt";
        commonDialog.FilterIndex = 1;
        commonDialog.CancelError = true;

        Assert.IsTrue(host.TryGetMember(owner, "Dialog1", Array.Empty<object?>(), out var named));
        Assert.AreSame(commonDialog, named);
        Assert.AreEqual(0, host.EnumerateControls(owner)!.Count());

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostAdaptsTreeViewNodesToVb6OneBasedLateBoundContract()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            var tree = (TreeView)host.CreateControl(owner, "Tree1", "MSComctlLib.TreeView")!;
            var nodes = VBDynamicDispatch.GetMember(tree, "Nodes")!;
            var root = (TreeNodeProxy)VBDynamicDispatch.InvokeMember(
                nodes,
                "Add",
                Arguments(null, null, "Root", "Root", "Folder"))!;
            VBDynamicDispatch.InvokeMember(
                nodes,
                "Add",
                Arguments("Root", 4, "Child", "Child", "Item"));

            Assert.AreEqual(2, VBDynamicDispatch.GetMember(nodes, "Count"));
            var child = (TreeNodeProxy)VBDynamicDispatch.GetIndexedMember(nodes, "Item", Arguments(2))!;
            Assert.AreEqual("Child", child.Text);
            Assert.AreEqual("Folder", root.Image);

            VBDynamicDispatch.SetMember(child, "Text", "Changed");
            Assert.AreEqual("Changed", tree.Nodes[0].Nodes[0].Text);
            VBDynamicDispatch.InvokeMember(nodes, "Remove", Arguments(1));
            Assert.AreEqual(0, tree.Nodes.Count);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostAdaptsImageListAndImageComboCollections()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            var imageList = host.CreateControl(owner, "Images", "MSComctlLib.ImageList")!;
            var listImages = VBDynamicDispatch.GetMember(imageList, "ListImages")!;
            VBDynamicDispatch.InvokeMember(listImages, "Add", Arguments(null, "Folder", "folder.bmp"));
            Assert.AreEqual(1, VBDynamicDispatch.GetMember(listImages, "Count"));
            var image = (ListImageProxy)VBDynamicDispatch.GetIndexedMember(listImages, "Item", Arguments(1))!;
            Assert.AreEqual("Folder", image.Key);
            Assert.AreEqual("folder.bmp", ((VBPicture)image.Picture!).FileName);

            var combo = (ImageComboControl)host.CreateControl(owner, "Combo", "MSComctlLib.ImageCombo")!;
            VBDynamicDispatch.SetMember(combo, "ImageList", imageList);
            var comboItems = VBDynamicDispatch.GetMember(combo, "ComboItems")!;
            VBDynamicDispatch.InvokeMember(comboItems, "Add", Arguments(null, "Root", "Root", 1));
            var item = (ComboItemProxy)VBDynamicDispatch.GetIndexedMember(comboItems, "Item", Arguments(1))!;
            item.Selected = true;

            Assert.AreSame(imageList, VBDynamicDispatch.GetMember(combo, "ImageList"));
            Assert.AreEqual("Root", item.Text);
            Assert.IsTrue(item.Selected);
            Assert.AreEqual(1, combo.Items.Count);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostCreatesTimerControlsAndConnectsTimerHandlers()
    {
        using var host = new WinFormsHost();
        var owner = new TimerEventSink();

        host.Load(owner);
        var timer = host.CreateControl(owner, "Timer1", "Timer")!;
        Assert.IsTrue(host.TrySetMember(timer, "Interval", Array.Empty<object?>(), 250));
        Assert.IsTrue(host.TrySetMember(timer, "Enabled", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TryGetMember(timer, "Interval", Array.Empty<object?>(), out var interval));
        Assert.AreEqual(250, interval);
        Assert.IsTrue(host.TryGetMember(timer, "Enabled", Array.Empty<object?>(), out var enabled));
        Assert.AreEqual(true, enabled);

        timer.GetType().GetMethod("RaiseTick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(timer, null);
        Assert.AreEqual(1, owner.TickCount);

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

    [STATestMethod]
    public void HostMapsMouseAndKeyboardEventsToVb6Arguments()
    {
        using var host = new WinFormsHost();
        var owner = new InputEventSink();

        host.Load(owner);
        var textBox = (TextBox)host.CreateControl(owner, "Input", "TextBox")!;
        Assert.IsTrue(host.TrySubscribeEvent(textBox, "MouseDown", owner, "OnMouseDown"));
        Assert.IsTrue(host.TrySubscribeEvent(textBox, "KeyDown", owner, "OnKeyDown"));
        Assert.IsTrue(host.TrySubscribeEvent(textBox, "KeyPress", owner, "OnKeyPress"));

        typeof(Control).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, new object[]
            {
                new MouseEventArgs(
                    MouseButtons.Left | MouseButtons.Right,
                    1,
                    10,
                    20,
                    0)
            });
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, new object[] { new KeyEventArgs(Keys.A | Keys.Shift | Keys.Control) });
        typeof(Control).GetMethod("OnKeyPress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, new object[] { new KeyPressEventArgs('x') });

        Assert.AreEqual(3, owner.MouseButton);
        Assert.AreEqual((short)0, owner.MouseShift);
        Assert.AreEqual(10f * 1440f / textBox.DeviceDpi, owner.MouseX);
        Assert.AreEqual(20f * 1440f / textBox.DeviceDpi, owner.MouseY);
        Assert.AreEqual(65, owner.KeyCode);
        Assert.AreEqual((short)3, owner.KeyShift);
        Assert.AreEqual((short)'x', owner.KeyAscii);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostConnectsConventionalFormResizeHandler()
    {
        using var host = new WinFormsHost();
        var owner = new InputEventSink();
        using var form = new Form();

        host.Register(owner, form);
        host.Load(owner);

        typeof(Control).GetMethod("OnResize", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { EventArgs.Empty });

        Assert.AreEqual(1, owner.FormResizeCount);
        host.Unload(owner);
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

    private sealed class InputEventSink
    {
        public int MouseButton { get; private set; }
        public short MouseShift { get; private set; }
        public float MouseX { get; private set; }
        public float MouseY { get; private set; }
        public int KeyCode { get; private set; }
        public short KeyShift { get; private set; }
        public short KeyAscii { get; private set; }
        public int FormResizeCount { get; private set; }

        private void OnMouseDown(short button, short shift, float x, float y)
        {
            MouseButton = button;
            MouseShift = shift;
            MouseX = x;
            MouseY = y;
        }

        private void OnKeyDown(short keyCode, short shift)
        {
            KeyCode = keyCode;
            KeyShift = shift;
        }

        private void OnKeyPress(short keyAscii) => KeyAscii = keyAscii;

        private void Form_Resize() => FormResizeCount++;
    }

    private sealed class TimerEventSink
    {
        public int TickCount { get; private set; }

        private void Timer1_Timer() => TickCount++;
    }

    private static VBArray<object> Arguments(params object?[] values)
    {
        var arguments = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            arguments[index] = values[index]!;
        }

        return arguments;
    }
}
