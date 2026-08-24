using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

[STATestClass]
public sealed class WinFormsHostTests
{
    [TestMethod]
    public void GeneratedRunnerRejectsMissingAssembly()
    {
        Assert.ThrowsException<FileNotFoundException>(() =>
            GeneratedApplicationRunner.Run(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
    }

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
        Assert.IsTrue(host.TrySetMember(control!, "Appearance", Array.Empty<object?>(), 0));
        Assert.IsTrue(host.TrySetMember(control!, "Tag", Array.Empty<object?>(), "command"));
        Assert.IsTrue(host.TrySetMember(control!, "ToolTipText", Array.Empty<object?>(), "Run it"));
        Assert.IsTrue(host.TrySetMember(control!, "AutoRedraw", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(control!, "FillStyle", Array.Empty<object?>(), 1));
        Assert.IsTrue(host.TrySetMember(control!, "MousePointer", Array.Empty<object?>(), 2));
        Assert.IsTrue(host.TrySetMember(control!, "ScaleMode", Array.Empty<object?>(), 3));
        Assert.IsTrue(host.TrySetMember(control!, "Picture", Array.Empty<object?>(), CreateFrxBitmapValue()));
        Assert.IsNotNull(((Button)control!).BackgroundImage);

        Assert.IsTrue(host.TrySetMember(owner, "Caption", Array.Empty<object?>(), "Main window"));
        Assert.IsTrue(host.TrySetMember(owner, "BorderStyle", Array.Empty<object?>(), 1));
        Assert.IsTrue(host.TrySetMember(owner, "ControlBox", Array.Empty<object?>(), false));
        Assert.IsTrue(host.TrySetMember(owner, "StartUpPosition", Array.Empty<object?>(), 2));

        Assert.IsTrue(host.TryGetMember(control!, "Text", Array.Empty<object?>(), out var text));
        Assert.AreEqual("Run", text);
        Assert.IsTrue(host.TryGetMember(control!, "Caption", Array.Empty<object?>(), out var caption));
        Assert.AreEqual("Run", caption);
        Assert.IsTrue(host.TryGetMember(control!, "Left", Array.Empty<object?>(), out var left));
        Assert.AreEqual(1440, left);
        Assert.IsTrue(host.TryGetMember(control!, "Appearance", Array.Empty<object?>(), out var appearance));
        Assert.AreEqual(0, appearance);
        Assert.IsTrue(host.TryGetMember(control!, "Tag", Array.Empty<object?>(), out var tag));
        Assert.AreEqual("command", tag);
        Assert.IsTrue(host.TryGetMember(control!, "ToolTipText", Array.Empty<object?>(), out var toolTip));
        Assert.AreEqual("Run it", toolTip);
        Assert.IsTrue(host.TryGetMember(control!, "AutoRedraw", Array.Empty<object?>(), out var autoRedraw));
        Assert.AreEqual(true, autoRedraw);
        Assert.IsTrue(host.TryGetMember(owner, "Caption", Array.Empty<object?>(), out var formCaption));
        Assert.AreEqual("Main window", formCaption);
        Assert.IsTrue(host.TryGetMember(owner, "BorderStyle", Array.Empty<object?>(), out var formBorderStyle));
        Assert.AreEqual(1, formBorderStyle);
        Assert.IsTrue(host.TryGetMember(owner, "ControlBox", Array.Empty<object?>(), out var controlBox));
        Assert.AreEqual(false, controlBox);
        Assert.IsTrue(host.TryGetMember(owner, "StartUpPosition", Array.Empty<object?>(), out var startUpPosition));
        Assert.AreEqual(2, startUpPosition);
        Assert.IsTrue(host.TryGetMember(owner, "Button1", Array.Empty<object?>(), out var named));
        Assert.AreSame(control, named);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostEmbedsGeneratedUserControlClassesAsDesignerComponents()
    {
        using var host = new WinFormsHost();
        var owner = new UserControlOwner();
        host.Load(owner);
        Assert.IsTrue(host.TrySetMember(owner, "Width", Array.Empty<object?>(), 1440));

        var typeName = typeof(GeneratedUserControlStub).FullName!;
        var generated = host.CreateControl(owner, "Widget1", typeName);

        Assert.IsInstanceOfType<GeneratedUserControlStub>(generated);
        var generatedStub = (GeneratedUserControlStub)generated!;
        Assert.AreEqual(1, generatedStub.InitializeCount);
        Assert.AreEqual(1, generatedStub.ReadPropertiesCount);
        Assert.AreEqual("persisted", generatedStub.ReadPropertyValue);
        Assert.IsTrue(host.TryGetMember(owner, "Widget1", Array.Empty<object?>(), out var named));
        Assert.AreSame(generated, named);
        Assert.IsTrue(host.TrySetMember(generated!, "Width", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TryGetMember(generated!, "Width", Array.Empty<object?>(), out var width));
        Assert.IsTrue((int)width! > 0);
        Assert.IsTrue(host.TrySetMember(generated!, "Visible", Array.Empty<object?>(), false));
        Assert.IsTrue(host.TryGetMember(generated!, "Visible", Array.Empty<object?>(), out var visible));
        Assert.AreEqual(false, visible);
        Assert.AreEqual(1, host.EnumerateControls(owner)!.OfType<Form>().Count());

        host.Unload(owner);
        Assert.AreEqual(1, generatedStub.WritePropertiesCount);
        Assert.AreEqual("persisted", generatedStub.WritePropertyValue);
        Assert.AreEqual(1, generatedStub.TerminateCount);
    }

    [STATestMethod]
    public void HostShowsStartupFormThroughInteractionDispatch()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);

        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        Assert.IsTrue(host.TryGetMember(owner, "Visible", Array.Empty<object?>(), out var visible));
        Assert.AreEqual(true, visible);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRendersShapeAndLineDesignerControls()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var shape = (Control)host.CreateControl(owner, "Shape1", "Shape")!;
        Assert.AreEqual("ShapeControl", shape.GetType().Name);
        Assert.IsTrue(host.TrySetMember(shape, "Width", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TrySetMember(shape, "Height", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TrySetMember(shape, "BackColor", Array.Empty<object?>(), ColorTranslator.ToOle(Color.Red)));
        Assert.IsTrue(host.TrySetMember(shape, "Shape", Array.Empty<object?>(), 2));
        Assert.IsTrue(host.TrySetMember(shape, "BorderColor", Array.Empty<object?>(), ColorTranslator.ToOle(Color.Blue)));

        using var shapeBitmap = new Bitmap(96, 96);
        shape.DrawToBitmap(shapeBitmap, new Rectangle(0, 0, 96, 96));
        var shapePixel = shapeBitmap.GetPixel(48, 48);
        Assert.IsTrue(shapePixel.R > 150 && shapePixel.G < 120);

        var line = (Control)host.CreateControl(owner, "Line1", "Line")!;
        Assert.AreEqual("LineControl", line.GetType().Name);
        line.Dock = DockStyle.None;
        line.Size = new Size(96, 96);
        Assert.IsTrue(host.TrySetMember(line, "X1", Array.Empty<object?>(), 0));
        Assert.IsTrue(host.TrySetMember(line, "Y1", Array.Empty<object?>(), 0));
        Assert.IsTrue(host.TrySetMember(line, "X2", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TrySetMember(line, "Y2", Array.Empty<object?>(), 1440));
        Assert.IsTrue(host.TrySetMember(line, "BorderColor", Array.Empty<object?>(), ColorTranslator.ToOle(Color.Blue)));

        using var lineBitmap = new Bitmap(96, 96);
        line.DrawToBitmap(lineBitmap, new Rectangle(0, 0, 96, 96));
        var linePixel = lineBitmap.GetPixel(48, 48);
        Assert.IsTrue(linePixel.B > 120 && linePixel.R < 150);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRendersGraphicsLineOnFormSurface()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            Assert.IsTrue(host.TrySetMember(owner, "Width", Array.Empty<object?>(), 4320));
            Assert.IsTrue(host.TrySetMember(owner, "Height", Array.Empty<object?>(), 2880));

            VBInteraction.GraphicsLine(
                0,
                0,
                1440,
                1440,
                ColorTranslator.ToOle(Color.Red),
                false,
                false,
                false);

            Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var image));
            Assert.IsInstanceOfType<Bitmap>(image);
            using var snapshot = new Bitmap((Bitmap)image!);
            var diagonal = snapshot.GetPixel(48, 48);
            Assert.IsTrue(diagonal.R > 180 && diagonal.G < 120 && diagonal.B < 120);

            VBInteraction.GraphicsLine(
                1440,
                0,
                2880,
                1440,
                ColorTranslator.ToOle(Color.Blue),
                false,
                true,
                true);
            using var filledSnapshot = new Bitmap((Bitmap)image);
            var filled = filledSnapshot.GetPixel(144, 48);
            Assert.IsTrue(filled.B > 150 && filled.R < 120);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostRendersPaintPictureOnFormSurface()
    {
        using var host = new WinFormsHost();
        using var source = new Bitmap(2, 2);
        using var sourceGraphics = Graphics.FromImage(source);
        sourceGraphics.Clear(Color.Lime);
        var owner = new object();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            Assert.IsTrue(host.TrySetMember(owner, "Width", Array.Empty<object?>(), 4320));
            Assert.IsTrue(host.TrySetMember(owner, "Height", Array.Empty<object?>(), 2880));

            VBInteraction.PaintPicture(source, 1440, 720, 1440, 720);

            Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var image));
            Assert.IsInstanceOfType<Bitmap>(image);
            using var snapshot = new Bitmap((Bitmap)image!);
            var painted = snapshot.GetPixel(144, 72);
            Assert.IsTrue(painted.G > 180 && painted.R < 120 && painted.B < 120);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostRendersQualifiedPaintPictureOnPictureBoxTarget()
    {
        using var host = new WinFormsHost();
        using var source = new Bitmap(2, 2);
        using var sourceGraphics = Graphics.FromImage(source);
        sourceGraphics.Clear(Color.Magenta);
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;
        pictureBox.Size = new Size(240, 120);

        Assert.IsTrue(host.TryInvokeMember(
            pictureBox,
            "PaintPicture",
            new object?[] { source, 1440f, 720f, 1440f, 720f },
            out _));
        Assert.IsNotNull(pictureBox.Image);
        using var pictureSnapshot = new Bitmap(pictureBox.Image!);
        var painted = pictureSnapshot.GetPixel(144, 72);
        Assert.IsTrue(painted.R > 180 && painted.B > 180 && painted.G < 120);
        Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var formPicture));
        Assert.IsNull(formPicture);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRendersQualifiedGraphicsLineOnPictureBoxTarget()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;
        pictureBox.Size = new Size(240, 120);

        host.GraphicsLine(
            pictureBox,
            new VBGraphicsLine(
                1440,
                720,
                2880,
                1440,
                ColorTranslator.ToOle(Color.Red),
                false,
                false,
                false));

        Assert.IsNotNull(pictureBox.Image);
        using var pictureSnapshot = new Bitmap(pictureBox.Image!);
        var painted = pictureSnapshot.GetPixel(144, 72);
        Assert.IsTrue(painted.R > 180 && painted.G < 120 && painted.B < 120);
        Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var formPicture));
        Assert.IsNull(formPicture);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostDecodesFrxPicturePayloadsForPictureBoxes()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var picture = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;

        Assert.IsTrue(host.TrySetMember(picture, "Picture", Array.Empty<object?>(), CreateFrxBitmapValue()));
        Assert.IsNotNull(picture.Image);
        Assert.AreEqual(2, picture.Image!.Width);
        Assert.AreEqual(2, picture.Image.Height);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostDecodesFrxIconPayloadsForForms()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        Assert.IsTrue(host.TrySetMember(owner, "Icon", Array.Empty<object?>(), CreateFrxIconValue()));
        Assert.IsTrue(host.TryGetMember(owner, "Icon", Array.Empty<object?>(), out var icon));
        Assert.IsNotNull(icon);

        host.Unload(owner);
    }

    private static string CreateFrxBitmapValue()
    {
        using var bitmap = new Bitmap(2, 2);
        using var imageStream = new MemoryStream();
        bitmap.Save(imageStream, ImageFormat.Bmp);
        var payload = imageStream.ToArray();
        var resource = new byte[24 + payload.Length];
        resource[16] = 0x6C;
        resource[17] = 0x74;
        BitConverter.GetBytes(payload.Length).CopyTo(resource, 20);
        payload.CopyTo(resource, 24);
        return "__VB6_FRX_BASE64__" + Convert.ToBase64String(resource);
    }

    private static string CreateFrxIconValue()
    {
        using var icon = (Icon)SystemIcons.Application.Clone();
        using var iconStream = new MemoryStream();
        icon.Save(iconStream);
        var payload = iconStream.ToArray();
        var resource = new byte[8 + payload.Length];
        resource[0] = 0x6C;
        resource[1] = 0x74;
        BitConverter.GetBytes(payload.Length).CopyTo(resource, 4);
        payload.CopyTo(resource, 8);
        return "__VB6_FRX_BASE64__" + Convert.ToBase64String(resource);
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
    public void HostAdaptsRichTextBoxSelectionRtfAndFileMembers()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        var filePath = Path.Combine(Path.GetTempPath(), "vb6-richtext-" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            host.Load(owner);
            var richTextBox = (RichTextBox)host.CreateControl(
                owner,
                "Editor",
                "RichTextLib.RichTextBox")!;
            richTextBox.Text = "first\r\nsecond";

            Assert.IsTrue(host.TrySetMember(richTextBox, "SelStart", Array.Empty<object?>(), 6));
            Assert.IsTrue(host.TrySetMember(richTextBox, "SelLength", Array.Empty<object?>(), 6));
            Assert.IsTrue(host.TrySetMember(
                richTextBox,
                "SelColor",
                Array.Empty<object?>(),
                ColorTranslator.ToOle(Color.Red)));
            Assert.IsTrue(host.TrySetMember(richTextBox, "SelBold", Array.Empty<object?>(), true));
            Assert.IsTrue(host.TrySetMember(richTextBox, "SelItalic", Array.Empty<object?>(), true));

            Assert.IsTrue(host.TryGetMember(richTextBox, "SelText", Array.Empty<object?>(), out var selected));
            Assert.AreEqual("second", selected);
            Assert.IsTrue(host.TryGetMember(richTextBox, "SelBold", Array.Empty<object?>(), out var bold));
            Assert.AreEqual(true, bold);
            Assert.IsTrue(host.TryGetMember(richTextBox, "SelItalic", Array.Empty<object?>(), out var italic));
            Assert.AreEqual(true, italic);
            Assert.IsTrue(host.TryGetMember(richTextBox, "TextRTF", Array.Empty<object?>(), out var rtf));
            StringAssert.StartsWith((string)rtf!, "{\\rtf");
            Assert.IsTrue(host.TrySetMember(richTextBox, "TextRTF", Array.Empty<object?>(), string.Empty));
            Assert.AreEqual(string.Empty, richTextBox.Text);
            Assert.IsTrue(host.TrySetMember(richTextBox, "Text", Array.Empty<object?>(), "first\r\nsecond"));

            Assert.IsTrue(host.TryInvokeMember(
                richTextBox,
                "GetLineFromChar",
                new object?[] { 7 },
                out var line));
            Assert.AreEqual(1, line);

            Assert.IsTrue(host.TryInvokeMember(
                richTextBox,
                "SaveFile",
                new object?[] { filePath, 1 },
                out _));
            Assert.IsTrue(host.TryGetMember(richTextBox, "FileName", Array.Empty<object?>(), out var fileName));
            Assert.AreEqual(filePath, fileName);
            Assert.IsTrue(host.TryGetMember(richTextBox, "Modified", Array.Empty<object?>(), out var modified));
            Assert.AreEqual(false, modified);

            richTextBox.Text = string.Empty;
            Assert.IsTrue(host.TryInvokeMember(
                richTextBox,
                "LoadFile",
                new object?[] { filePath, 1 },
                out _));
            Assert.IsTrue(host.TryGetMember(richTextBox, "Text", Array.Empty<object?>(), out var text));
            Assert.AreEqual("first\r\nsecond", text);
        }
        finally
        {
            host.Unload(owner);
            File.Delete(filePath);
        }
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
            Assert.IsTrue(host.TrySetMember(imageList, "ImageWidth", Array.Empty<object?>(), 16));
            Assert.IsTrue(host.TrySetMember(imageList, "ImageHeight", Array.Empty<object?>(), 24));
            Assert.IsTrue(host.TrySetMember(imageList, "ListImage1.Picture", Array.Empty<object?>(), CreateFrxBitmapValue()));
            Assert.IsTrue(host.TrySetMember(imageList, "ListImage1.Key", Array.Empty<object?>(), "Folder"));
            Assert.IsTrue(host.TryGetMember(imageList, "ImageWidth", Array.Empty<object?>(), out var imageWidth));
            Assert.AreEqual(16, imageWidth);
            Assert.IsTrue(host.TryGetMember(imageList, "ImageHeight", Array.Empty<object?>(), out var imageHeight));
            Assert.AreEqual(24, imageHeight);
            var listImages = VBDynamicDispatch.GetMember(imageList, "ListImages")!;
            Assert.AreEqual(1, VBDynamicDispatch.GetMember(listImages, "Count"));
            var designerImage = (ListImageProxy)VBDynamicDispatch.GetIndexedMember(listImages, "Item", Arguments(1))!;
            Assert.AreEqual("Folder", designerImage.Key);
            Assert.IsInstanceOfType<Image>(designerImage.Picture);
            VBDynamicDispatch.InvokeMember(listImages, "Clear", Arguments());
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
    public void HostBuildsMenuHierarchyAndConnectsMenuHandlers()
    {
        using var host = new WinFormsHost();
        var owner = new MenuEventSink();

        host.Load(owner);
        var file = (ToolStripMenuItem)host.CreateControl(owner, "mnuFile", "Menu")!;
        var open = (ToolStripMenuItem)host.CreateControl(owner, "mnuFile.cmdOpen", "Menu")!;

        Assert.IsInstanceOfType<MenuStrip>(file.Owner);
        Assert.AreSame(file, open.OwnerItem);
        Assert.AreSame(open, file.DropDownItems[0]);
        Assert.IsTrue(host.TrySetMember(file, "Caption", Array.Empty<object?>(), "File"));
        Assert.IsTrue(host.TrySetMember(open, "Caption", Array.Empty<object?>(), "Open"));
        Assert.IsTrue(host.TrySetMember(open, "Checked", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(open, "Enabled", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySubscribeEvent(open, "Click", owner, "Open_Click"));
        Assert.IsTrue(host.TryInvokeMember(open, "PerformClick", Array.Empty<object?>(), out _));

        Assert.AreEqual("File", file.Text);
        Assert.AreEqual("Open", open.Text);
        Assert.IsTrue(open.Checked);
        Assert.AreEqual(1, owner.OpenCount);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostShowsPopupMenuWithoutDetachingOriginalMenu()
    {
        using var host = new WinFormsHost();
        var owner = new MenuEventSink();
        var previousHost = VBInteraction.Host;

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            var file = (ToolStripMenuItem)host.CreateControl(owner, "mnuFile", "Menu")!;
            var open = (ToolStripMenuItem)host.CreateControl(owner, "mnuFile.Open", "Menu")!;
            Assert.IsTrue(host.TrySetMember(open, "Caption", Array.Empty<object?>(), "Open"));
            Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

            VBInteraction.PopupMenu(file, 0, 1440, 1440);

            Assert.AreSame(file, open.OwnerItem);
            Assert.IsInstanceOfType<MenuStrip>(file.Owner);

            var popupItem = (ToolStripMenuItem)typeof(WinFormsHost)
                .GetMethod("ClonePopupItem", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { open })!;
            Assert.AreNotSame(open, popupItem);
            Assert.AreEqual("Open", popupItem.Text);
            Assert.IsTrue(host.TryInvokeMember(open, "PerformClick", Array.Empty<object?>(), out _));
            Assert.AreEqual(1, owner.OpenCount);
            using var popup = new ContextMenuStrip();
            popup.Items.Add(popupItem);
            typeof(ToolStripMenuItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(popupItem, new object[] { EventArgs.Empty });
            Assert.AreEqual(2, owner.OpenCount);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
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

    [STATestMethod]
    public void HostMapsConventionalFormLifecycleEvents()
    {
        using var host = new WinFormsHost();
        var owner = new FormLifecycleEventSink();
        using var form = new Form();

        host.Register(owner, form);
        host.Load(owner);

        typeof(Form).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { EventArgs.Empty });
        typeof(Form).GetMethod("OnDeactivate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { EventArgs.Empty });

        var closing = new FormClosingEventArgs(CloseReason.UserClosing, cancel: false);
        typeof(Form).GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { closing });
        var closed = new FormClosedEventArgs(CloseReason.UserClosing);
        typeof(Form).GetMethod("OnFormClosed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { closed });

        Assert.AreEqual(1, owner.ActivateCount);
        Assert.AreEqual(1, owner.DeactivateCount);
        Assert.AreEqual(1, owner.QueryUnloadCount);
        Assert.AreEqual(1, owner.UnloadCount);
        Assert.IsTrue(closing.Cancel);
        Assert.AreEqual(0, owner.UnloadMode);

        host.Unload(owner);
    }

    private sealed class EventSink
    {
        public int ChangeCount { get; private set; }

        private void Text1_Change() => ChangeCount++;
    }

    private sealed class MenuEventSink
    {
        public int OpenCount { get; private set; }

        private void Open_Click() => OpenCount++;
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

    private sealed class FormLifecycleEventSink
    {
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public int QueryUnloadCount { get; private set; }
        public int UnloadCount { get; private set; }
        public short UnloadMode { get; private set; }

        private void Form_Activate() => ActivateCount++;

        private void Form_Deactivate() => DeactivateCount++;

        private void Form_QueryUnload(ref short cancel, ref short unloadMode)
        {
            QueryUnloadCount++;
            cancel = 1;
            UnloadMode = unloadMode;
        }

        private void Form_Unload(ref short cancel)
        {
            _ = cancel;
            UnloadCount++;
        }
    }

    private sealed class TimerEventSink
    {
        public int TickCount { get; private set; }

        private void Timer1_Timer() => TickCount++;
    }

    private sealed class UserControlOwner
    {
    }

    private sealed class GeneratedUserControlStub
    {
        public int InitializeCount { get; private set; }
        public int TerminateCount { get; private set; }

        public int ReadPropertiesCount { get; private set; }
        public int WritePropertiesCount { get; private set; }
        public object? ReadPropertyValue { get; private set; }
        public object? WritePropertyValue { get; private set; }

        private void UserControl_Initialize() => InitializeCount++;

        private void UserControl_ReadProperties(object propertyBag)
        {
            ReadPropertiesCount++;
            var bag = (VBPropertyBag)propertyBag;
            bag.WriteProperty("Caption", "persisted");
            ReadPropertyValue = bag.ReadProperty("Caption");
        }

        private void UserControl_WriteProperties(object propertyBag)
        {
            WritePropertiesCount++;
            WritePropertyValue = ((VBPropertyBag)propertyBag).ReadProperty("Caption");
        }

        private void UserControl_Terminate() => TerminateCount++;
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
