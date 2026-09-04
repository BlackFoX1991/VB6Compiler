using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VB6.Compiler;
using VB6.Runtime;
using VB6.Runtime.WinForms;

namespace VB6.Runtime.WinForms.Tests;

[STATestClass]
public sealed class WinFormsHostTests
{
    private const uint WindowMessageLeftButtonDown = 0x0201;
    private const uint WindowMessageLeftButtonUp = 0x0202;
    private const uint WindowMessageChar = 0x0102;
    private const uint WindowMessageLeftButtonDoubleClick = 0x0203;

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    private static bool RequireNativeOcx =>
        string.Equals(
            Environment.GetEnvironmentVariable("VB6_REQUIRE_NATIVE_OCX"),
            "1",
            StringComparison.Ordinal);

    [TestMethod]
    public void GeneratedRunnerRejectsMissingAssembly()
    {
        Assert.ThrowsException<FileNotFoundException>(() =>
            GeneratedApplicationRunner.Run(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe")));
    }

    [STATestMethod]
    public void GeneratedWinFormsHostStartsAndCleansUpWithoutAForm()
    {
        var previousHost = VBInteraction.Host;
        VBInteraction.Host = null;
        try
        {
            VBInteraction.StartWinFormsHost();
            Assert.IsInstanceOfType<WinFormsHost>(VBInteraction.Host);
            Assert.AreEqual(0, VBInteraction.RunWinFormsMessageLoop());
            Assert.AreSame(previousHost, VBInteraction.Host);
        }
        finally
        {
            if (VBInteraction.Host is WinFormsHost)
            {
                VBInteraction.RunWinFormsMessageLoop();
            }

            VBInteraction.Host = previousHost;
        }
    }

    [STATestMethod]
    public void HostCarriesSelectedCompatibilityProfile()
    {
        using var host = new WinFormsHost(
            preferNativeActiveX: true,
            compatibilityProfile: VBCompatibilityProfile.VB6Sp6);

        Assert.AreEqual(VBCompatibilityProfile.VB6Sp6, host.CompatibilityProfile);
        Assert.AreEqual(VBCompatibilityProfile.VB6Sp6, ((IVB6Host)host).CompatibilityProfile);
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
    public void HostLoadsPictureFileThroughLoadPictureIntoPictureBox()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            "VB6CompilerPicture_" + Guid.NewGuid().ToString("N") + ".png");
        try
        {
            using (var source = new Bitmap(3, 2))
            {
                source.SetPixel(0, 0, Color.Red);
                source.Save(filePath, ImageFormat.Png);
            }

            using var host = new WinFormsHost();
            var owner = new object();
            host.Load(owner);
            var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;

            Assert.IsTrue(host.TrySetMember(
                pictureBox,
                "Picture",
                Array.Empty<object?>(),
                VBInteraction.LoadPicture(filePath)));
            Assert.IsNotNull(pictureBox.Image);
            Assert.AreEqual(3, pictureBox.Image!.Width);
            Assert.AreEqual(2, pictureBox.Image.Height);

            host.Unload(owner);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [STATestMethod]
    public void HostLoadsAndUnloadsExistingControlWithoutCreatingSyntheticFormBinding()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        var control = (Button)host.CreateControl(owner, "Button1", "CommandButton")!;
        control.Visible = false;

        host.Load(control);
        Assert.IsTrue(control.Visible);
        Assert.IsFalse(control.IsDisposed);

        host.Unload(control);
        Assert.IsFalse(control.Visible);
        Assert.IsFalse(control.IsDisposed);

        host.Load(control);
        Assert.IsTrue(control.Visible);
        Assert.IsFalse(control.IsDisposed);

        host.Unload(owner);
        Assert.IsTrue(control.IsDisposed);
    }

    [STATestMethod]
    public void HostDispatchesMembersThroughNativeActiveXComObjectWhenX86OcxIsAvailable()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.ListViewCtrl.2", throwOnError: false) is null)
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);

        var control = host.CreateControl(owner, "List1", "MSComctlLib.ListView")!;
        Assert.IsInstanceOfType<AxHost>(control);
        Assert.IsInstanceOfType<IVBComObjectProvider>(control);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        ((Control)control).CreateControl();
        Assert.IsNotNull(((IVBComObjectProvider)control).ComObject);

        Assert.IsTrue(host.TrySetMember(control, "View", Array.Empty<object?>(), (short)1));
        Assert.IsTrue(host.TryGetMember(control, "View", Array.Empty<object?>(), out var view));
        Assert.AreEqual(1, view);

        if (Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is not null)
        {
            var richText = host.CreateControl(owner, "Editor", "RichTextLib.RichTextBox")!;
            if (richText is AxHost)
            {
                Assert.IsInstanceOfType<IVBComObjectProvider>(richText);
                const string rtf = "{\\rtf1\\ansi Native OCX}";
                Assert.IsTrue(host.TrySetMember(richText, "TextRTF", Array.Empty<object?>(), rtf));
                Assert.IsTrue(host.TryGetMember(richText, "TextRTF", Array.Empty<object?>(), out var textRtf));
                StringAssert.Contains((string)textRtf!, "Native OCX");
            }
        }

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostBridgesNativeRichTextChangeEventThroughComConnectionPointInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native RichTextBox OCX validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var richText = host.CreateControl(owner, "Editor", "RichTextLib.RichTextBox")!;
        Assert.IsInstanceOfType<AxHost>(richText);
        ((Control)richText).CreateControl();
        var comObject = ((IVBComObjectProvider)richText).ComObject;
        Assert.IsNotNull(comObject);

        var sink = new NativeRichTextEventSink();
        VBEvents.SubscribeMethod(richText, "Change", sink, "OnChange");
        try
        {
            Assert.IsTrue(host.TrySetMember(richText, "Text", Array.Empty<object?>(), "native event"));
            Application.DoEvents();
            Assert.AreEqual(1, sink.ChangeCount);
        }
        finally
        {
            VBEvents.UnsubscribeMethod(richText, "Change", sink, "OnChange");
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void NativeComSubscriptionUnsubscribesThroughTheConnectedRcwAfterProviderResetInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native RichTextBox OCX validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var richText = host.CreateControl(owner, "Editor", "RichTextLib.RichTextBox")!;
        Assert.IsInstanceOfType<AxHost>(richText);
        var control = (Control)richText;
        control.CreateControl();
        var connectedComObject = ((IVBComObjectProvider)richText).ComObject;
        Assert.IsNotNull(connectedComObject);

        var provider = new MutableComObjectProvider(connectedComObject);
        var sink = new NativeRichTextEventSink();
        Assert.IsTrue(VBEvents.TrySubscribeComMethod(provider, "Change", sink, "OnChange"));
        try
        {
            Assert.IsTrue(host.TrySetMember(richText, "Text", Array.Empty<object?>(), "first"));
            Application.DoEvents();
            Assert.AreEqual(1, sink.ChangeCount);

            // The wrapper has lost its current COM reference, but the connection point was
            // installed on the RCW returned during subscription.
            provider.ComObject = null;
            VBEvents.UnsubscribeMethod(provider, "Change", sink, "OnChange");

            Assert.IsTrue(host.TrySetMember(richText, "Text", Array.Empty<object?>(), "second"));
            Application.DoEvents();
            Assert.AreEqual(1, sink.ChangeCount);
        }
        finally
        {
            VBEvents.UnsubscribeObject(provider);
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostBridgesNativeRichTextKeyPressByRefEventThroughConventionalHostHookInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native RichTextBox OCX validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var richText = host.CreateControl(owner, "Editor", "RichTextLib.RichTextBox")!;
        Assert.IsInstanceOfType<AxHost>(richText);
        var control = (Control)richText;
        control.CreateControl();
        control.Focus();
        Assert.IsTrue(host.TrySetMember(richText, "Text", Array.Empty<object?>(), string.Empty));

        var sink = new NativeRichTextKeyPressEventSink();
        Assert.IsTrue(host.TrySubscribeEvent(richText, "KeyPress", sink, "OnKeyPress"));
        try
        {
            _ = SendMessage(control.Handle, WindowMessageChar, (IntPtr)'x', IntPtr.Zero);
            Application.DoEvents();

            Assert.AreEqual(1, sink.KeyPressCount);
            Assert.AreEqual((short)'x', sink.OriginalKeyAscii);
            Assert.IsTrue(host.TryGetMember(richText, "Text", Array.Empty<object?>(), out var text));
            Assert.AreEqual("y", text);
        }
        finally
        {
            VBEvents.UnsubscribeMethod(richText, "KeyPress", sink, "OnKeyPress");
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void CompiledLegacyFormBindsNativeOcxHandlerThroughDesignerConventionInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native RichTextBox OCX validation requires a registered 32-bit control.");
            }

            return;
        }

        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6RuntimeWinFormsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousHost = VBInteraction.Host;

        try
        {
            var typeLibraryPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "SysWow64",
                "RICHTX32.OCX");
            var projectPath = Path.Combine(directory, "NativeDesignerEvents.vbp");
            File.WriteAllText(projectPath, $$"""
                Type=Exe
                Startup="Main"
                Name="NativeDesignerEvents"
                Object={3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0; RICHTX32.OCX
                Reference=*\G{3B7C8863-D78F-101B-B9B5-04021C009402}#1.2#0#{{typeLibraryPath}}#RichTextLib
                Form=Main.frm
                """);
            File.WriteAllText(Path.Combine(directory, "Main.frm"), """
                VERSION 5.00
                Begin VB.Form Main
                   Begin RichTextLib.RichTextBox editor
                   End
                   Begin VB.TextBox sink
                   End
                End
                Attribute VB_Name = "Main"
                Attribute VB_PredeclaredId = True
                Option Explicit

                Private WithEvents source As RichTextLib.RichTextBox
                Private changeCount As Integer
                Private designerChangeCount As Integer
                Private gotFocusCount As Integer
                Private lostFocusCount As Integer
                Private dblClickCount As Integer
                Private formInitialized As Boolean
                Private sourceKeyValue As Integer
                Private observedKey As Integer

                Private Sub Form_Load()
                    Set source = editor
                    formInitialized = True
                End Sub

                Private Sub source_Change()
                    changeCount = changeCount + 1
                End Sub

                Private Sub source_KeyPress(KeyAscii As Integer)
                    sourceKeyValue = KeyAscii
                End Sub

                Private Sub Editor_KeyPress(KeyAscii As Integer)
                    observedKey = KeyAscii
                    KeyAscii = Asc("y")
                End Sub

                Private Sub Editor_Change()
                    designerChangeCount = designerChangeCount + 1
                End Sub

                Private Sub Editor_GotFocus()
                    gotFocusCount = gotFocusCount + 1
                End Sub

                Private Sub Editor_LostFocus()
                    lostFocusCount = lostFocusCount + 1
                End Sub

                Private Sub Editor_DblClick()
                    dblClickCount = dblClickCount + 1
                End Sub

                Public Property Get DesignerChange() As Integer
                    DesignerChange = designerChangeCount
                End Property

                Public Property Get DesignerGotFocus() As Integer
                    DesignerGotFocus = gotFocusCount
                End Property

                Public Property Get DesignerLostFocus() As Integer
                    DesignerLostFocus = lostFocusCount
                End Property

                Public Property Get DesignerDblClick() As Integer
                    DesignerDblClick = dblClickCount
                End Property

                Public Property Get ObservedChange() As Integer
                    ObservedChange = changeCount
                End Property

                Public Property Get FormLoaded() As Boolean
                    FormLoaded = formInitialized
                End Property

                Public Property Get LastKey() As Integer
                    LastKey = observedKey
                End Property

                Public Property Get SourceKey() As Integer
                    SourceKey = sourceKeyValue
                End Property
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "NativeDesignerEvents.dll"));
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));
            Assert.IsNotNull(result.AssemblyPath);

            using var host = new WinFormsHost(preferNativeActiveX: true);
            VBInteraction.Host = host;
            var assembly = Assembly.Load(File.ReadAllBytes(result.AssemblyPath!));
            var formType = assembly.GetType("VB6.Generated.__vb6_class_Main", throwOnError: true)!;
            var form = Activator.CreateInstance(formType)!;
            host.Load(form);
            Assert.IsTrue(host.TryInvokeMember(form, "Show", Array.Empty<object?>(), out _));
            Assert.IsTrue(host.TryGetMember(form, "Editor", Array.Empty<object?>(), out var editor));
            Assert.IsInstanceOfType<AxHost>(editor);
            var formLoadedGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("FormLoaded", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(true, formLoadedGetter.Invoke(form, null));

            var control = (Control)editor!;
            control.CreateControl();
            control.Focus();
            Assert.IsTrue(host.TrySetMember(editor, "Text", Array.Empty<object?>(), "probe"));
            var observedChangeGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("ObservedChange", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual((short)1, observedChangeGetter.Invoke(form, null));

            // The designer convention has to reach the native OCX too, not just WithEvents. This
            // is what the VB6 event names are for: the COM connection point knows "Change", never
            // the WinForms name "TextChanged".
            var designerChangeGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("DesignerChange", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(
                (short)1,
                designerChangeGetter.Invoke(form, null),
                "Editor_Change did not fire through the native connection point.");

            // GotFocus, LostFocus and DblClick go through the same name translation as Change.
            // Focus already moved onto the control above; moving it away produces LostFocus.
            AssertDesignerHandlerRan(formType, form, "DesignerGotFocus", "Editor_GotFocus");

            Assert.IsTrue(host.TryGetMember(form, "Sink", Array.Empty<object?>(), out var sink));
            var sinkControl = (Control)sink!;
            sinkControl.CreateControl();
            sinkControl.Focus();
            Application.DoEvents();
            AssertDesignerHandlerRan(formType, form, "DesignerLostFocus", "Editor_LostFocus");

            control.Focus();
            Application.DoEvents();
            _ = SendMessage(control.Handle, WindowMessageLeftButtonDown, IntPtr.Zero, IntPtr.Zero);
            _ = SendMessage(control.Handle, WindowMessageLeftButtonUp, IntPtr.Zero, IntPtr.Zero);
            _ = SendMessage(control.Handle, WindowMessageLeftButtonDoubleClick, IntPtr.Zero, IntPtr.Zero);
            _ = SendMessage(control.Handle, WindowMessageLeftButtonUp, IntPtr.Zero, IntPtr.Zero);
            Application.DoEvents();
            AssertDesignerHandlerRan(formType, form, "DesignerDblClick", "Editor_DblClick");

            Assert.IsTrue(host.TrySetMember(editor, "Text", Array.Empty<object?>(), string.Empty));

            _ = SendMessage(control.Handle, WindowMessageChar, (IntPtr)'x', IntPtr.Zero);
            Application.DoEvents();

            var sourceKeyGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("SourceKey", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual((short)120, sourceKeyGetter.Invoke(form, null));

            var lastKeyGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("LastKey", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual((short)120, lastKeyGetter.Invoke(form, null));
            Assert.IsTrue(host.TryGetMember(editor, "Text", Array.Empty<object?>(), out var text));
            Assert.AreEqual("y", text);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [STATestMethod]
    public void CompiledLegacyFormBindsIntrinsicControlArrayIndex()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "VB6RuntimeWinFormsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousHost = VBInteraction.Host;

        try
        {
            var projectPath = Path.Combine(directory, "ControlArrayEvents.vbp");
            File.WriteAllText(projectPath, """
                Type=Exe
                Startup="Main"
                Name="ControlArrayEvents"
                Form=Main.frm
                """);
            File.WriteAllText(Path.Combine(directory, "Main.frm"), """
                VERSION 5.00
                Begin VB.Form Main
                   Begin VB.CommandButton Buttons
                      Index = 0
                      Caption = "First"
                   End
                   Begin VB.CommandButton Buttons
                      Index = 2
                      Caption = "Second"
                   End
                End
                Attribute VB_Name = "Main"
                Attribute VB_PredeclaredId = True
                Option Explicit

                Private observedIndex As Integer
                Private observedKeyIndex As Integer
                Private observedKey As Integer

                Private Sub Buttons_Click(Index As Integer)
                    observedIndex = Index
                End Sub

                Private Sub Buttons_KeyPress(Index As Integer, KeyAscii As Integer)
                    observedKeyIndex = Index
                    observedKey = KeyAscii
                    KeyAscii = Asc("z")
                End Sub

                Public Property Get LastIndex() As Integer
                    LastIndex = observedIndex
                End Property

                Public Property Get LastKeyIndex() As Integer
                    LastKeyIndex = observedKeyIndex
                End Property

                Public Property Get LastKey() As Integer
                    LastKey = observedKey
                End Property
                """);

            var result = VBProjectCompilation.Create(projectPath)
                .EmitManagedApplication(Path.Combine(directory, "ControlArrayEvents.dll"));
            Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Lowering.Analysis.Diagnostics));
            Assert.IsNotNull(result.AssemblyPath);

            using var host = new WinFormsHost();
            VBInteraction.Host = host;
            var assembly = Assembly.Load(File.ReadAllBytes(result.AssemblyPath!));
            var formType = assembly.GetType("VB6.Generated.__vb6_class_Main", throwOnError: true)!;
            var form = Activator.CreateInstance(formType)!;
            host.Load(form);
            Assert.IsTrue(host.TryInvokeMember(form, "Show", Array.Empty<object?>(), out _));
            Assert.IsTrue(host.TryGetMember(form, "Buttons(0)", Array.Empty<object?>(), out var first));
            Assert.IsTrue(host.TryGetMember(form, "Buttons(2)", Array.Empty<object?>(), out var second));
            Assert.IsFalse(host.TryGetMember(form, "Buttons(1)", Array.Empty<object?>(), out _));
            Assert.IsInstanceOfType<Button>(first);
            Assert.IsInstanceOfType<Button>(second);

            var lastIndexGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("LastIndex", StringComparison.OrdinalIgnoreCase));
            ((Button)first!).PerformClick();
            Application.DoEvents();
            Assert.AreEqual((short)0, lastIndexGetter.Invoke(form, null));

            ((Button)second!).PerformClick();
            Application.DoEvents();
            Assert.AreEqual((short)2, lastIndexGetter.Invoke(form, null));

            var lastKeyIndexGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("LastKeyIndex", StringComparison.OrdinalIgnoreCase));
            var lastKeyGetter = formType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name.Contains("LastKey", StringComparison.OrdinalIgnoreCase) &&
                    !method.Name.Contains("LastKeyIndex", StringComparison.OrdinalIgnoreCase));
            var secondControl = (Control)second!;
            secondControl.CreateControl();
            secondControl.Focus();
            _ = SendMessage(secondControl.Handle, WindowMessageChar, (IntPtr)'x', IntPtr.Zero);
            Application.DoEvents();
            Assert.AreEqual((short)2, lastKeyIndexGetter.Invoke(form, null));
            Assert.AreEqual((short)120, lastKeyGetter.Invoke(form, null));
        }
        finally
        {
            VBInteraction.Host = previousHost;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [STATestMethod]
    public void HostBridgesNativeRichTextMouseDownWithParameterizedComEventInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native RichTextBox OCX validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var richText = host.CreateControl(owner, "Editor", "RichTextLib.RichTextBox")!;
        Assert.IsInstanceOfType<AxHost>(richText);
        var control = (Control)richText;
        control.CreateControl();

        var sink = new NativeRichTextMouseDownEventSink();
        VBEvents.SubscribeMethod(richText, "MouseDown", sink, "OnMouseDown");
        try
        {
            var point = (IntPtr)((20 << 16) | 10);
            _ = SendMessage(control.Handle, WindowMessageLeftButtonDown, (IntPtr)1, point);
            _ = SendMessage(control.Handle, WindowMessageLeftButtonUp, IntPtr.Zero, point);
            Application.DoEvents();

            Assert.AreEqual(1, sink.MouseDownCount);
            Assert.AreEqual((short)1, sink.Button);
            Assert.AreEqual((short)0, sink.Shift);
            Assert.AreEqual(10f, sink.X, 0.1f);
            Assert.AreEqual(20f, sink.Y, 0.1f);
        }
        finally
        {
            VBEvents.UnsubscribeMethod(richText, "MouseDown", sink, "OnMouseDown");
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void NativeTreeViewNodesCanBeReadThroughComDispatchInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.TreeCtrl.2", throwOnError: false) is not { } comType)
        {
            return;
        }

        var nativeType = typeof(WinFormsHost).GetNestedType(
            "NativeActiveXControl",
            BindingFlags.NonPublic)!;
        using var control = (Control)Activator.CreateInstance(nativeType, comType.GUID)!;
        using var form = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000)
        };
        form.Controls.Add(control);
        form.Show();
        control.CreateControl();

        var comObject = ((IVBComObjectProvider)control).ComObject;
        Assert.IsNotNull(comObject);
        Assert.IsTrue(VBDynamicDispatch.TryGetComMember(
            comObject,
            "Nodes",
            Array.Empty<object?>(),
            out var nodes));
        Assert.IsNotNull(nodes);
        Assert.IsTrue(VBDynamicDispatch.TryGetComMember(
            nodes,
            "Count",
            Array.Empty<object?>(),
            out var count));
        Assert.AreEqual((short)0, count);
    }

    [STATestMethod]
    public void NativeTreeViewRaisesDesignerEventsAcrossBothSubscriptionPathsInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.TreeCtrl.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native TreeView validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new TreeViewEventSink();
        host.Load(owner);

        var tree = host.CreateControl(owner, "tvProject", "MSComctlLib.TreeView")!;
        var sink = (Control)host.CreateControl(owner, "sink", "TextBox")!;
        Assert.IsInstanceOfType<AxHost>(tree);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var treeControl = (Control)tree;
        treeControl.Size = new Size(300, 200);
        treeControl.CreateControl();
        sink.CreateControl();

        Assert.IsTrue(host.TryGetMember(tree, "Nodes", Array.Empty<object?>(), out var nodes));
        Assert.IsTrue(VBDynamicDispatch.TryInvokeComMember(
            nodes,
            "Add",
            new object?[] { Type.Missing, Type.Missing, "root", "Root" },
            out _));

        // NodeClick belongs to the control's own event interface, so it must arrive through the
        // COM connection point. The node sits at the top left of the tree.
        treeControl.Focus();
        Application.DoEvents();

        // The root label sits in the first row, to the right of the indent. Sweep a few offsets so
        // the hit does not depend on the OCX's exact indent metrics.
        foreach (var x in new[] { 25, 35, 45, 60 })
        {
            var point = (IntPtr)((8 << 16) | x);
            _ = SendMessage(treeControl.Handle, WindowMessageLeftButtonDown, (IntPtr)1, point);
            _ = SendMessage(treeControl.Handle, WindowMessageLeftButtonUp, IntPtr.Zero, point);
            Application.DoEvents();
            if (owner.NodeClickCount >= 1)
            {
                break;
            }
        }

        // Focus events are extender events: absent from the OCX, supplied by the wrapper.
        sink.Focus();
        Application.DoEvents();

        Assert.IsTrue(owner.GotFocusCount >= 1, "tvProject_GotFocus did not fire on the native TreeView.");
        Assert.IsTrue(owner.LostFocusCount >= 1, "tvProject_LostFocus did not fire on the native TreeView.");
        // Click comes from the same connection point and proves the messages arrived, so a missing
        // NodeClick means the subscription is gone rather than the click.
        Assert.IsTrue(owner.ClickCount >= 1, "The synthetic click never reached the native TreeView.");
        Assert.IsTrue(
            owner.NodeClickCount >= 1,
            "tvProject_NodeClick did not fire on the native TreeView, although the click arrived.");
        Assert.IsNotNull(owner.ClickedNode, "NodeClick must hand over the clicked node.");

        host.Unload(owner);
    }

    private sealed class TreeViewEventSink
    {
        public int GotFocusCount { get; private set; }
        public int LostFocusCount { get; private set; }
        public int NodeClickCount { get; private set; }
        public int ClickCount { get; private set; }
        public object? ClickedNode { get; private set; }

        private void tvProject_Click() => ClickCount++;

        private void tvProject_GotFocus() => GotFocusCount++;

        private void tvProject_LostFocus() => LostFocusCount++;

        private void tvProject_NodeClick(object node)
        {
            NodeClickCount++;
            ClickedNode = node;
        }
    }

    [STATestMethod]
    public void HostHostsNativeTreeViewNodesThroughRawComDispatchInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.TreeCtrl.2", throwOnError: false) is null)
        {
            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        var tree = host.CreateControl(owner, "Tree1", "MSComctlLib.TreeView")!;

        Assert.IsInstanceOfType<AxHost>(tree);
        Assert.IsInstanceOfType<IVBComObjectProvider>(tree);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        ((Control)tree).CreateControl();
        Assert.IsNotNull(((IVBComObjectProvider)tree).ComObject);

        Assert.IsTrue(host.TryGetMember(tree, "Nodes", Array.Empty<object?>(), out var nodes));
        Assert.IsNotNull(nodes);
        Assert.IsTrue(host.TryGetMember(nodes!, "Count", Array.Empty<object?>(), out var count));
        Assert.AreEqual((short)0, count);

        Assert.IsTrue(VBDynamicDispatch.TryInvokeComMember(
            nodes,
            "Add",
            new object?[] { Type.Missing, Type.Missing, "root", "Root" },
            out var node));
        Assert.IsNotNull(node);
        Assert.IsTrue(VBDynamicDispatch.TryGetComMember(
            node,
            "Text",
            Array.Empty<object?>(),
            out var text));
        Assert.AreEqual("Root", text);
        Assert.IsTrue(VBDynamicDispatch.TrySetComMember(
            node,
            "Text",
            Array.Empty<object?>(),
            "Changed"));
        Assert.IsTrue(VBDynamicDispatch.TryGetComMember(
            node,
            "Text",
            Array.Empty<object?>(),
            out text));
        Assert.AreEqual("Changed", text);
        var enumeratedNodes = VBInteraction.EnumerateControls(nodes);
        Assert.AreEqual(1, enumeratedNodes.Length);
        Assert.IsNotNull(enumeratedNodes[0]);
        Assert.IsTrue(host.TryGetMember(
            nodes!,
            "Item",
            new object?[] { (short)1 },
            out var indexedNode));
        Assert.IsNotNull(indexedNode);
        Assert.IsTrue(VBDynamicDispatch.TryGetComMember(
            indexedNode,
            "Text",
            Array.Empty<object?>(),
            out text));
        Assert.AreEqual("Changed", text);
        Assert.IsTrue(host.TryGetMember(nodes!, "Count", Array.Empty<object?>(), out count));
        Assert.AreEqual((short)1, count);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostActivatesRegisteredStandardOcxComponentsInX86()
    {
        if (Environment.Is64BitProcess)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native OCX validation must run in a 32-bit test process.");
            }

            return;
        }

        var visualOcx = new[]
        {
            ("ImageList", "MSComctlLib.ImageListCtrl.2"),
            ("ImageCombo", "MSComctlLib.ImageComboCtl.2"),
            ("ListView", "MSComctlLib.ListViewCtrl.2"),
            ("ProgressBar", "MSComctlLib.ProgCtrl.2"),
            ("Slider", "MSComctlLib.Slider.2"),
            ("StatusBar", "MSComctlLib.SBarCtrl.2"),
            ("TabStrip", "MSComctlLib.TabStrip.2"),
            ("Toolbar", "MSComctlLib.Toolbar.2"),
            ("RichTextBox", "RICHTEXT.RichtextCtrl.1")
        };
        if (visualOcx.Any(ocx => Type.GetTypeFromProgID(ocx.Item2, throwOnError: false) is null) ||
            Type.GetTypeFromProgID("MSComDlg.CommonDialog.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("One or more required standard OCX ProgIDs are not registered.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        foreach (var (typeName, _) in visualOcx)
        {
            var vbTypeName = typeName == "RichTextBox"
                ? "RichTextLib.RichTextBox"
                : $"MSComctlLib.{typeName}";
            var control = host.CreateControl(owner, typeName, vbTypeName)!;
            Assert.IsInstanceOfType<AxHost>(control, typeName);
            ((Control)control).CreateControl();
            Assert.IsNotNull(((IVBComObjectProvider)control).ComObject, typeName);
        }

        var directProgIdControl = host.CreateControl(
            owner,
            "DirectTree",
            "MSComctlLib.TreeCtrl.2")!;
        Assert.IsInstanceOfType<AxHost>(directProgIdControl);
        ((Control)directProgIdControl).CreateControl();
        Assert.IsNotNull(((IVBComObjectProvider)directProgIdControl).ComObject);

        var imageList = host.CreateControl(owner, "Images", "MSComctlLib.ImageList")!;
        Assert.IsTrue(host.TrySetMember(imageList, "ImageWidth", Array.Empty<object?>(), (short)16));
        Assert.IsTrue(host.TryGetMember(imageList, "ImageWidth", Array.Empty<object?>(), out var imageWidth));
        Assert.AreEqual((short)16, imageWidth);
        Assert.IsTrue(host.TryGetMember(imageList, "ListImages", Array.Empty<object?>(), out var listImages));
        using var imageBitmap = new Bitmap(1, 1);
        imageBitmap.SetPixel(0, 0, Color.Red);
        var picture = typeof(AxHost).GetMethod(
            "GetIPictureDispFromPicture",
            BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object?[] { imageBitmap });
        Assert.IsTrue(VBDynamicDispatch.TryInvokeComMember(
            listImages,
            "Add",
            new object?[] { (short)1, "root", picture },
            out _));

        var imageCombo = host.CreateControl(owner, "Combo", "MSComctlLib.ImageCombo")!;
        Assert.IsTrue(host.TrySetMember(imageCombo, "Locked", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TryGetMember(imageCombo, "Locked", Array.Empty<object?>(), out var locked));
        Assert.AreEqual(true, locked);
        Assert.IsTrue(host.TrySetMember(imageCombo, "ImageList", Array.Empty<object?>(), imageList));
        Assert.IsTrue(host.TryGetMember(imageCombo, "ImageList", Array.Empty<object?>(), out var assignedImageList));
        Assert.IsNotNull(assignedImageList);

        var dialog = host.CreateControl(owner, "Dialog", "MSComDlg.CommonDialog")!;
        Assert.IsInstanceOfType<IVBComObjectProvider>(dialog);
        Assert.IsFalse(dialog is CommonDialogProxy);
        Assert.IsTrue(host.TrySetMember(dialog, "FileName", Array.Empty<object?>(), "legacy.txt"));
        Assert.IsTrue(host.TryGetMember(dialog, "FileName", Array.Empty<object?>(), out var fileName));
        Assert.AreEqual("legacy.txt", fileName);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostGivesAUserControlItsAmbientAndExtenderObjects()
    {
        using var host = new WinFormsHost();
        var owner = new UserControlOwner();
        host.Load(owner);

        var generated = host.CreateControl(owner, "Widget1", typeof(GeneratedUserControlStub).FullName!)!;

        // Ambient trägt, was der Container vorschlägt. UserMode ist wahr -- es gibt keinen
        // Entwurfsmodus, in dem dieser Code liefe.
        Assert.IsTrue(host.TryGetMember(generated, "Ambient", Array.Empty<object?>(), out var ambient));
        Assert.IsNotNull(ambient);
        Assert.AreEqual(true, VBDynamicDispatch.GetMember(ambient, "UserMode"));
        Assert.IsNotNull(VBDynamicDispatch.GetMember(ambient, "Font"));

        // Extender trägt, was der Container besitzt -- ein UserControl benennt sich nicht selbst.
        Assert.IsTrue(host.TryGetMember(generated, "Extender", Array.Empty<object?>(), out var extender));
        Assert.IsNotNull(extender);
        Assert.AreEqual("Widget1", VBDynamicDispatch.GetMember(extender, "Name"));
    }

    [STATestMethod]
    public void HostReportsUserControlVisibilityChanges()
    {
        using var host = new WinFormsHost();
        var owner = new UserControlOwner();
        host.Load(owner);

        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        var generated = host.CreateControl(owner, "Widget1", typeof(GeneratedUserControlStub).FullName!)!;
        var stub = (GeneratedUserControlStub)generated;

        // Ein Control, das nie versteckt wurde, wird nicht für versteckt erklärt.
        Assert.AreEqual(0, stub.HideCount);

        Assert.IsTrue(host.TrySetMember(generated, "Visible", Array.Empty<object?>(), false));
        Assert.AreEqual(1, stub.HideCount);

        // Zweimal derselbe Wert ist keine Änderung und damit kein Ereignis.
        Assert.IsTrue(host.TrySetMember(generated, "Visible", Array.Empty<object?>(), false));
        Assert.AreEqual(1, stub.HideCount);

        var shown = stub.ShowCount;
        Assert.IsTrue(host.TrySetMember(generated, "Visible", Array.Empty<object?>(), true));
        Assert.AreEqual(shown + 1, stub.ShowCount);
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

        // Ein frisch angelegtes UserControl bekommt InitProperties, nicht ReadProperties: Es hat
        // nichts Gespeichertes, das wiederhergestellt werden könnte. Dieser Test hat das vorher
        // andersherum behauptet -- eine Herleitung, keine gemessene VB6-Eigenschaft.
        Assert.AreEqual(1, generatedStub.InitPropertiesCount);
        Assert.AreEqual(0, generatedStub.ReadPropertiesCount);
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
    public void HostDrawsACircleAndStretchesItByTheAspectRatio()
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
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

            // Mittelpunkt bei Pixel 96/96, Radius 30 Pixel -- 15 Twips pro Pixel bei 96 DPI.
            VBInteraction.GraphicsCircle(
                1440,
                1440,
                450,
                ColorTranslator.ToOle(Color.Red),
                null,
                null,
                null,
                false);

            Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var image));
            using var snapshot = new Bitmap((Bitmap)image!);

            // Auf dem Rand rechts vom Mittelpunkt liegt Farbe, im Mittelpunkt nicht -- Circle
            // zeichnet den Umriss, nicht die Flaeche.
            var rim = snapshot.GetPixel(126, 96);
            Assert.IsTrue(rim.R > 180 && rim.G < 120 && rim.B < 120);
            var centre = snapshot.GetPixel(96, 96);
            Assert.IsFalse(centre.R > 180 && centre.G < 120 && centre.B < 120);

            // Das Seitenverhaeltnis streckt die y-Achse: mit 2.0 liegt der obere Rand doppelt so
            // weit vom Mittelpunkt entfernt wie der rechte.
            VBInteraction.GraphicsCircle(
                1440,
                1440,
                450,
                ColorTranslator.ToOle(Color.Blue),
                null,
                null,
                2.0,
                false);

            using var stretched = new Bitmap((Bitmap)image!);
            var stretchedRim = stretched.GetPixel(96, 36);
            Assert.IsTrue(stretchedRim.B > 150 && stretchedRim.R < 120);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostSetsAPixelAndTracksTheDrawingPosition()
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
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

            // Twips: 1440 pro Zoll, 15 pro Pixel bei 96 DPI -- 1440 Twips sind Pixel 96.
            VBInteraction.GraphicsPSet(1440, 1440, ColorTranslator.ToOle(Color.Red), false);

            Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var image));
            using var snapshot = new Bitmap((Bitmap)image!);
            var pixel = snapshot.GetPixel(96, 96);
            Assert.IsTrue(pixel.R > 180 && pixel.G < 120 && pixel.B < 120);

            // Nur dieser eine Punkt ist gesetzt.
            var neighbour = snapshot.GetPixel(98, 98);
            Assert.IsFalse(neighbour.R > 180 && neighbour.G < 120 && neighbour.B < 120);

            // PSet laesst die Zeichenposition auf dem gesetzten Punkt stehen.
            Assert.IsTrue(host.TryGetMember(owner, "CurrentX", Array.Empty<object?>(), out var currentX));
            Assert.IsTrue(host.TryGetMember(owner, "CurrentY", Array.Empty<object?>(), out var currentY));
            Assert.AreEqual(1440f, currentX);
            Assert.AreEqual(1440f, currentY);

            // Step misst von dort aus weiter.
            VBInteraction.GraphicsPSet(1440, 0, ColorTranslator.ToOle(Color.Blue), true);
            using var stepped = new Bitmap((Bitmap)image!);
            var steppedPixel = stepped.GetPixel(192, 96);
            Assert.IsTrue(steppedPixel.B > 150 && steppedPixel.R < 120);
            Assert.IsTrue(host.TryGetMember(owner, "CurrentX", Array.Empty<object?>(), out var afterStep));
            Assert.AreEqual(2880f, afterStep);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostArrangesMdiChildrenAndReportsTheActiveForm()
    {
        using var host = new WinFormsHost();
        var parent = new object();
        var first = new object();
        var second = new object();

        host.Load(parent);
        host.Load(first);
        host.Load(second);
        Assert.IsTrue(host.TrySetMember(parent, "MDIForm", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(first, "MDIChild", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(second, "MDIChild", Array.Empty<object?>(), true));

        Assert.IsTrue(host.TryInvokeMember(parent, "Show", Array.Empty<object?>(), out _));
        Assert.IsTrue(host.TryInvokeMember(first, "Show", Array.Empty<object?>(), out _));
        Assert.IsTrue(host.TryInvokeMember(second, "Show", Array.Empty<object?>(), out _));

        // ActiveForm antwortet mit dem VB6-Objekt, nicht mit dem Fenster dahinter.
        Assert.IsTrue(host.TryGetMember(parent, "ActiveForm", Array.Empty<object?>(), out var active));
        Assert.IsTrue(ReferenceEquals(active, first) || ReferenceEquals(active, second), "ActiveForm");

        // Der Fensterzustand eines Kindes bleibt erhalten und traegt die VB6-Konstanten:
        // 0 Normal, 1 Minimized, 2 Maximized. Das ist die "persistente Fensterzustand"-Zusage
        // der MDI-Karte.
        Assert.IsTrue(host.TrySetMember(first, "WindowState", Array.Empty<object?>(), 2));
        Assert.IsTrue(host.TryGetMember(first, "WindowState", Array.Empty<object?>(), out var maximized));
        Assert.AreEqual(2, maximized);
        Assert.IsTrue(host.TrySetMember(first, "WindowState", Array.Empty<object?>(), 1));
        Assert.IsTrue(host.TryGetMember(first, "WindowState", Array.Empty<object?>(), out var minimized));
        Assert.AreEqual(1, minimized);
        Assert.IsTrue(host.TrySetMember(first, "WindowState", Array.Empty<object?>(), 0));

        // Die vier VB6-Anordnungen; alles andere meldet 380 statt sich eine auszusuchen.
        foreach (var arrangement in new[] { 0, 1, 2, 3 })
        {
            Assert.IsTrue(host.TryInvokeMember(parent, "Arrange", new object?[] { arrangement }, out _));
        }

        var raised = Assert.ThrowsExactly<VB6RaisedError>(() =>
            host.TryInvokeMember(parent, "Arrange", new object?[] { 9 }, out _));
        Assert.AreEqual(380, raised.Number);
        VBErrors.Clear();
    }

    [STATestMethod]
    public void HostMarksTheWindowListMenu()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TrySetMember(owner, "MDIForm", Array.Empty<object?>(), true));

        var menu = host.CreateControl(owner, "mnuFenster", "Menu")!;
        Assert.IsTrue(host.TrySetMember(menu, "Caption", Array.Empty<object?>(), "Fenster"));
        Assert.IsTrue(host.TrySetMember(menu, "WindowList", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TryGetMember(menu, "WindowList", Array.Empty<object?>(), out var windowList));
        Assert.AreEqual(true, windowList);

        Assert.IsTrue(host.TrySetMember(menu, "WindowList", Array.Empty<object?>(), false));
        Assert.IsTrue(host.TryGetMember(menu, "WindowList", Array.Empty<object?>(), out var cleared));
        Assert.AreEqual(false, cleared);
    }

    [STATestMethod]
    public void HostLoadsAndUnloadsMenuArrayElements()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var template = host.CreateControl(owner, "mnuDatei", "Menu")!;
        Assert.IsTrue(host.TrySetMember(template, "Caption", Array.Empty<object?>(), "Datei"));

        // Ein Menü-Array ist ein Control-Array, dessen Elemente keine Controls sind. Vorher gab
        // dieser Weg still null zurück, und Load auf ein Menü tat schlicht nichts.
        var element = host.LoadControlArrayElement(owner, "mnuDatei", 1, template);
        Assert.IsNotNull(element);

        // Das geladene Element erbt die Eigenschaften der Vorlage -- ausser der Sichtbarkeit.
        Assert.IsTrue(host.TryGetMember(element!, "Caption", Array.Empty<object?>(), out var caption));
        Assert.AreEqual("Datei", caption);
        Assert.IsTrue(host.TryGetMember(element!, "Visible", Array.Empty<object?>(), out var visible));
        Assert.AreEqual(false, visible);

        Assert.IsTrue(host.TrySetMember(element!, "Caption", Array.Empty<object?>(), "Zuletzt"));
        Assert.IsTrue(host.TryGetMember(element!, "Caption", Array.Empty<object?>(), out var renamed));
        Assert.AreEqual("Zuletzt", renamed);

        // Derselbe Index liefert dasselbe Element; erst nach Unload entsteht ein neues.
        Assert.AreSame(element, host.LoadControlArrayElement(owner, "mnuDatei", 1, template));
        host.UnloadControlArrayElement(owner, "mnuDatei", 1, element);
        Assert.AreNotSame(element, host.LoadControlArrayElement(owner, "mnuDatei", 1, template));
    }

    [STATestMethod]
    public void HostCreatesScrollBarsAndMapsTheirVb6Range()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        foreach (var typeName in new[] { "VScrollBar", "HScrollBar" })
        {
            var bar = host.CreateControl(owner, typeName + "1", typeName);
            Assert.IsInstanceOfType<ScrollBar>(bar, typeName);

            Assert.IsTrue(host.TrySetMember(bar!, "Min", Array.Empty<object?>(), 10));
            Assert.IsTrue(host.TrySetMember(bar!, "Max", Array.Empty<object?>(), 20));
            Assert.IsTrue(host.TrySetMember(bar!, "LargeChange", Array.Empty<object?>(), 5));
            Assert.IsTrue(host.TrySetMember(bar!, "SmallChange", Array.Empty<object?>(), 2));

            // VB6 erreicht sein Max; eine WinForms-Scrollbar erreicht ihr eigenes Maximum nicht,
            // weil der Schieber LargeChange Einheiten der Bahn belegt.
            Assert.IsTrue(host.TrySetMember(bar!, "Value", Array.Empty<object?>(), 20));
            Assert.IsTrue(host.TryGetMember(bar!, "Value", Array.Empty<object?>(), out var atMaximum));
            Assert.AreEqual(20, atMaximum, typeName);

            Assert.IsTrue(host.TryGetMember(bar!, "Min", Array.Empty<object?>(), out var minimum));
            Assert.IsTrue(host.TryGetMember(bar!, "Max", Array.Empty<object?>(), out var maximum));
            Assert.IsTrue(host.TryGetMember(bar!, "LargeChange", Array.Empty<object?>(), out var largeChange));
            Assert.IsTrue(host.TryGetMember(bar!, "SmallChange", Array.Empty<object?>(), out var smallChange));
            Assert.AreEqual(10, minimum);
            Assert.AreEqual(20, maximum);
            Assert.AreEqual(5, largeChange);
            Assert.AreEqual(2, smallChange);

            // Ausserhalb von Min..Max meldet VB6 380 statt stillschweigend zu begrenzen.
            var raised = Assert.ThrowsExactly<VB6RaisedError>(() =>
                host.TrySetMember(bar!, "Value", Array.Empty<object?>(), 21));
            Assert.AreEqual(380, raised.Number);
            VBErrors.Clear();
        }
    }

    [STATestMethod]
    public void HostRaisesScrollBarChangeOnAssignment()
    {
        using var host = new WinFormsHost();
        var owner = new EventRecorder();
        host.Load(owner);

        var bar = host.CreateControl(owner, "VScroll1", "VScrollBar");
        Assert.IsTrue(host.TrySubscribeEvent(bar!, "Change", owner, nameof(EventRecorder.OnChange)));

        Assert.IsTrue(host.TrySetMember(bar!, "Max", Array.Empty<object?>(), 100));
        Assert.IsTrue(host.TrySetMember(bar!, "Value", Array.Empty<object?>(), 40));

        // Change ist hier nicht TextChanged: der Wrapper trägt den VB6-Namen selbst, und der
        // schlägt die Übersetzungstabelle.
        Assert.AreEqual(1, owner.Changes);

        Assert.IsTrue(host.TrySetMember(bar!, "Value", Array.Empty<object?>(), 40));
        Assert.AreEqual(1, owner.Changes, "Ein unveränderter Wert löst kein Change aus.");
    }

    [STATestMethod]
    public void HostListsDirectoriesAndFilesForTheFileSystemControls()
    {
        var root = Path.Combine(Path.GetTempPath(), "VB6HostFileSystemControls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "alpha"));
        Directory.CreateDirectory(Path.Combine(root, "beta"));
        File.WriteAllText(Path.Combine(root, "one.txt"), "1");
        File.WriteAllText(Path.Combine(root, "two.txt"), "2");
        File.WriteAllText(Path.Combine(root, "three.dat"), "3");

        try
        {
            using var host = new WinFormsHost();
            var owner = new EventRecorder();
            host.Load(owner);

            var directories = host.CreateControl(owner, "Dir1", "DirListBox")!;
            Assert.IsTrue(host.TrySubscribeEvent(directories, "Change", owner, nameof(EventRecorder.OnChange)));
            Assert.IsTrue(host.TrySetMember(directories, "Path", Array.Empty<object?>(), root));
            Assert.AreEqual(1, owner.Changes);

            Assert.IsTrue(host.TryGetMember(directories, "ListCount", Array.Empty<object?>(), out var directoryCount));
            Assert.AreEqual(2, directoryCount);
            Assert.IsTrue(host.TryGetMember(directories, "List", new object?[] { 0 }, out var firstDirectory));
            Assert.AreEqual("alpha", firstDirectory);

            // Der negative Index läuft die Vorfahren hoch -- List(-1) ist das Elternverzeichnis.
            Assert.IsTrue(host.TryGetMember(directories, "List", new object?[] { -1 }, out var parent));
            Assert.AreEqual(Path.GetDirectoryName(root), parent);

            var files = host.CreateControl(owner, "File1", "FileListBox")!;
            Assert.IsTrue(host.TrySetMember(files, "Path", Array.Empty<object?>(), root));
            Assert.IsTrue(host.TryGetMember(files, "ListCount", Array.Empty<object?>(), out var allFiles));
            Assert.AreEqual(3, allFiles);

            Assert.IsTrue(host.TrySetMember(files, "Pattern", Array.Empty<object?>(), "*.txt"));
            Assert.IsTrue(host.TryGetMember(files, "ListCount", Array.Empty<object?>(), out var textFiles));
            Assert.AreEqual(2, textFiles);

            // Ein qualifizierter FileName setzt den Pfad mit und wählt die Datei aus.
            Assert.IsTrue(host.TrySetMember(files, "FileName", Array.Empty<object?>(), Path.Combine(root, "two.txt")));
            Assert.IsTrue(host.TryGetMember(files, "FileName", Array.Empty<object?>(), out var selected));
            Assert.AreEqual("two.txt", selected);

            // Ein Pfad, den es nicht gibt, meldet 76 statt eine leere Liste zu zeigen.
            var raised = Assert.ThrowsExactly<VB6RaisedError>(() =>
                host.TrySetMember(files, "Path", Array.Empty<object?>(), Path.Combine(root, "missing")));
            Assert.AreEqual(76, raised.Number);
            VBErrors.Clear();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    public void HostListsTheAvailableDrives()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var drives = host.CreateControl(owner, "Drive1", "DriveListBox")!;
        Assert.IsTrue(host.TryGetMember(drives, "ListCount", Array.Empty<object?>(), out var count));
        Assert.IsTrue((int)count! > 0);

        // VB6 listet Laufwerke klein geschrieben als "c:", die Bezeichnung folgt in Klammern.
        Assert.IsTrue(host.TryGetMember(drives, "Drive", Array.Empty<object?>(), out var current));
        var expected = (Path.GetPathRoot(Environment.CurrentDirectory) ?? string.Empty)[..1].ToLowerInvariant() + ":";
        StringAssert.StartsWith((string)current!, expected);

        var raised = Assert.ThrowsExactly<VB6RaisedError>(() =>
            host.TrySetMember(drives, "Drive", Array.Empty<object?>(), "§:"));
        Assert.AreEqual(68, raised.Number);
        VBErrors.Clear();
    }

    [STATestMethod]
    public void HostCarriesTheDeferredDataAndOleSurfaces()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var ole = host.CreateControl(owner, "OLE1", "OLE")!;
        Assert.IsTrue(host.TrySetMember(ole, "Class", Array.Empty<object?>(), "Excel.Sheet"));
        Assert.IsTrue(host.TryGetMember(ole, "Class", Array.Empty<object?>(), out var oleClass));
        Assert.AreEqual("Excel.Sheet", oleClass);

        // Ein Container ohne Objekt meldet vbOLENone.
        Assert.IsTrue(host.TryGetMember(ole, "OLEType", Array.Empty<object?>(), out var oleType));
        Assert.AreEqual(3, oleType);

        // Die Verben hängen an der generischen ActiveX-Schicht. Sie melden, statt still nichts
        // zu tun.
        var verb = Assert.ThrowsExactly<VB6RaisedError>(() =>
            host.TryInvokeMember(ole, "DoVerb", Array.Empty<object?>(), out _));
        Assert.AreEqual(445, verb.Number);
        VBErrors.Clear();

        var data = host.CreateControl(owner, "Data1", "Data")!;
        Assert.IsTrue(host.TrySetMember(data, "RecordSource", Array.Empty<object?>(), "Kunden"));
        Assert.IsTrue(host.TryGetMember(data, "RecordSource", Array.Empty<object?>(), out var recordSource));
        Assert.AreEqual("Kunden", recordSource);

        var refresh = Assert.ThrowsExactly<VB6RaisedError>(() =>
            host.TryInvokeMember(data, "Refresh", Array.Empty<object?>(), out _));
        Assert.AreEqual(445, refresh.Number);
        VBErrors.Clear();
    }

    [STATestMethod]
    public void HostReportsAnUnknownIntrinsicControlInsteadOfShowingAPanel()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        // Die intrinsische Menge ist vollständig; ein Name ausserhalb davon ist ein Control, das
        // auch VB6 nicht hätte erzeugen können.
        var raised = Assert.ThrowsExactly<VB6RaisedError>(() =>
            host.CreateControl(owner, "Ghost1", "SuperGrid"));
        Assert.AreEqual(429, raised.Number);
        VBErrors.Clear();

        // Ein qualifizierter Name gehört einer Typbibliothek -- der Platzhalter bleibt, bis die
        // generische ActiveX-Schicht steht.
        var userControl = host.CreateControl(owner, "Tool1", "Visia.McToolBar");
        Assert.IsInstanceOfType<Control>(userControl);
    }

    private sealed class EventRecorder
    {
        public int Changes { get; private set; }

        public void OnChange() => Changes++;
    }

    [STATestMethod]
    public void HostReadsBackTheColourOfASinglePoint()
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
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));
            Assert.IsTrue(host.TrySetMember(owner, "BackColor", Array.Empty<object?>(), ColorTranslator.ToOle(Color.White)));

            VBInteraction.GraphicsPSet(1440, 1440, ColorTranslator.ToOle(Color.Red), false);

            var drawn = ColorTranslator.FromOle(VBInteraction.GraphicsPoint(1440, 1440));
            Assert.IsTrue(drawn.R > 180 && drawn.G < 120 && drawn.B < 120);

            // Ein Pixel, auf das nichts gezeichnet wurde, meldet die Hintergrundfarbe -- nicht das
            // durchsichtige Schwarz der Zeichenflaeche.
            var untouched = ColorTranslator.FromOle(VBInteraction.GraphicsPoint(0, 0));
            Assert.AreEqual(Color.White.ToArgb(), untouched.ToArgb());

            // Ausserhalb der Flaeche antwortet VB6 mit -1.
            Assert.AreEqual(-1, VBInteraction.GraphicsPoint(100000, 100000));
            Assert.AreEqual(-1, VBInteraction.GraphicsPoint(-15, 0));
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostReadsAPointFromAQualifiedTarget()
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
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

            VBInteraction.GraphicsPSet(owner, 1440, 720, ColorTranslator.ToOle(Color.Lime), false);

            Assert.IsTrue(host.TryInvokeMember(owner, "Point", new object?[] { 1440, 720 }, out var read));
            var colour = ColorTranslator.FromOle(VBConversions.CLng(read));
            Assert.IsTrue(colour.G > 180 && colour.R < 120 && colour.B < 120);

            Assert.IsTrue(host.TryInvokeMember(owner, "Point", new object?[] { 100000, 0 }, out var outside));
            Assert.AreEqual(-1, VBConversions.CLng(outside));
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
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

            // VB6 only keeps drawing output in a persistent surface while AutoRedraw is on.
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

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

            // VB6 only keeps drawing output in a persistent surface while AutoRedraw is on.
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

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

        // VB6 only keeps drawing output in a persistent surface while AutoRedraw is on.
        Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));

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

        // VB6 only keeps drawing output in a persistent surface while AutoRedraw is on.
        Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));

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
        var keyDown = new KeyEventArgs(Keys.A | Keys.Shift | Keys.Control);
        typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, new object[] { keyDown });
        var keyPress = new KeyPressEventArgs('x');
        typeof(Control).GetMethod("OnKeyPress", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(textBox, new object[] { keyPress });

        Assert.AreEqual(3, owner.MouseButton);
        Assert.AreEqual((short)0, owner.MouseShift);
        Assert.AreEqual(10f * 1440f / textBox.DeviceDpi, owner.MouseX);
        Assert.AreEqual(20f * 1440f / textBox.DeviceDpi, owner.MouseY);
        Assert.AreEqual(65, owner.KeyCode);
        Assert.AreEqual((short)3, owner.KeyShift);
        Assert.AreEqual((short)'x', owner.KeyAscii);
        Assert.IsTrue(keyDown.SuppressKeyPress);
        Assert.AreEqual('y', keyPress.KeyChar);

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
    public void HostRaisesFormPaintOnlyWhileAutoRedrawIsOff()
    {
        using var host = new WinFormsHost();
        var owner = new PaintEventSink();
        using var form = new Form();
        using var surface = new Bitmap(120, 120);
        using var graphics = Graphics.FromImage(surface);

        host.Register(owner, form);
        host.Load(owner);

        RaisePaint(form, graphics);
        Assert.AreEqual(1, owner.PaintCount, "VB6 raises Paint while AutoRedraw is off.");

        // With AutoRedraw on, the persistent surface carries the output and VB6 stays silent.
        Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));
        RaisePaint(form, graphics);
        Assert.AreEqual(1, owner.PaintCount, "AutoRedraw suppresses the Paint event.");

        Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), false));
        RaisePaint(form, graphics);
        Assert.AreEqual(2, owner.PaintCount, "Turning AutoRedraw off restores the Paint event.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRoutesDrawingInsidePaintToThePaintContext()
    {
        using var host = new WinFormsHost();
        var previousHost = VBInteraction.Host;
        using var form = new Form();
        using var surface = new Bitmap(120, 120);
        using var graphics = Graphics.FromImage(surface);

        // A VB6 Paint handler redraws by issuing the same drawing statements again; they have to
        // land on the paint context, not on a stored bitmap nobody is going to show.
        var owner = new PaintEventSink
        {
            PaintCallback = () => VBInteraction.GraphicsLine(
                0,
                0,
                1440,
                1440,
                ColorTranslator.ToOle(Color.Red),
                false,
                false,
                false)
        };

        try
        {
            VBInteraction.Host = host;
            host.Register(owner, form);
            host.Load(owner);

            RaisePaint(form, graphics);

            Assert.AreEqual(1, owner.PaintCount);
            var painted = surface.GetPixel(48, 48);
            Assert.IsTrue(
                painted.R > 180 && painted.G < 120 && painted.B < 120,
                $"The Paint handler's line did not reach the paint context: {painted}.");
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostDiscardsThePersistentSurfaceWhenAutoRedrawIsTurnedOff()
    {
        using var host = new WinFormsHost();
        var previousHost = VBInteraction.Host;
        var owner = new object();

        try
        {
            VBInteraction.Host = host;
            host.Load(owner);
            Assert.IsTrue(host.TrySetMember(owner, "Width", Array.Empty<object?>(), 4320));
            Assert.IsTrue(host.TrySetMember(owner, "Height", Array.Empty<object?>(), 2880));
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), true));

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

            // VB6 throws the AutoRedraw bitmap away when the property is turned off.
            Assert.IsTrue(host.TrySetMember(owner, "AutoRedraw", Array.Empty<object?>(), false));
            Assert.IsTrue(host.TryGetMember(owner, "Picture", Array.Empty<object?>(), out var discarded));
            Assert.IsNull(discarded);
        }
        finally
        {
            VBInteraction.Host = previousHost;
            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostAppliesEveryVb6ScaleModeToDrawingCoordinates()
    {
        // VB6 defines each ScaleMode as a fixed number of units per inch, so one inch of drawing
        // must land on the same pixel extent no matter which unit expresses it. Character mode is
        // the asymmetric one: 120 twips wide but 240 twips high, so 12 units across equal 6 down.
        foreach (var (scaleMode, unitsX, unitsY, name) in new[]
        {
            (ScaleModeTwip, 1440f, 1440f, "twip"),
            (ScaleModePoint, 72f, 72f, "point"),
            (ScaleModeCharacter, 12f, 6f, "character"),
            (ScaleModeInch, 1f, 1f, "inch"),
            (ScaleModeMillimetre, 25.4f, 25.4f, "millimetre"),
            (ScaleModeCentimetre, 2.54f, 2.54f, "centimetre")
        })
        {
            AssertScaledBoxExtent(scaleMode, unitsX, unitsY, expected: null, name);
        }

        // Pixel mode is the identity: the numbers are already device pixels.
        AssertScaledBoxExtent(ScaleModePixel, 50f, 50f, expected: 50f, "pixel");
    }

    [STATestMethod]
    public void HostConnectsTheActiveXSpecificEventSignatures()
    {
        using var host = new WinFormsHost();
        var owner = new OcxEventSink();
        host.Load(owner);

        var treeView = (TreeView)host.CreateControl(owner, "tvProject", "MSComctlLib.TreeView")!;
        var richText = (RichTextBox)host.CreateControl(owner, "RTB", "RichTextLib.RichTextBox")!;
        var combo = (Control)host.CreateControl(owner, "cmbObject", "MSComctlLib.ImageCombo")!;

        var node = treeView.Nodes.Add("root", "Root");
        Raise(
            treeView,
            "OnNodeMouseClick",
            new TreeNodeMouseClickEventArgs(node, MouseButtons.Left, 1, 0, 0));
        Raise(richText, "OnSelectionChanged", EventArgs.Empty);
        Raise(combo, "OnDropDown", EventArgs.Empty);

        // VB6 hands NodeClick the clicked Node, not the WinForms mouse arguments.
        Assert.AreEqual(1, owner.NodeClickCount);
        Assert.AreEqual("Root", owner.ClickedNodeText);
        Assert.AreEqual("root", owner.ClickedNodeKey);

        // SelChange and Dropdown take no arguments in VB6.
        Assert.AreEqual(1, owner.SelChangeCount);
        Assert.AreEqual(1, owner.DropdownCount);

        host.Unload(owner);
    }

    private static void Raise(Control control, string methodName, EventArgs arguments) =>
        control.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { arguments });

    private sealed class OcxEventSink
    {
        public int NodeClickCount { get; private set; }
        public string? ClickedNodeText { get; private set; }
        public string? ClickedNodeKey { get; private set; }
        public int SelChangeCount { get; private set; }
        public int DropdownCount { get; private set; }

        private void tvProject_NodeClick(object node)
        {
            NodeClickCount++;
            ClickedNodeText = (node as TreeNodeProxy)?.Text;
            ClickedNodeKey = (node as TreeNodeProxy)?.Key;
        }

        private void RTB_SelChange() => SelChangeCount++;

        private void cmbObject_Dropdown() => DropdownCount++;
    }

    [STATestMethod]
    public void HostClonesTheDesignerElementWhenLoadingAControlArrayElement()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var template = (Control)host.CreateControl(owner, "ctlButton(0)", "CommandButton")!;
        template.SetBounds(10, 20, 120, 40);
        template.Text = "Design";
        template.Visible = true;

        var loaded = host.LoadControlArrayElement(owner, "ctlButton", 1, template) as Control;

        Assert.IsNotNull(loaded, "Load must create the element.");
        Assert.AreNotSame(template, loaded);
        Assert.AreEqual(template.GetType(), loaded!.GetType(), "The clone keeps the template's type.");
        Assert.AreEqual(template.Bounds, loaded.Bounds, "VB6 clones position and size.");
        Assert.AreEqual("Design", loaded.Text);
        Assert.AreSame(template.Parent, loaded.Parent, "The clone joins the template's container.");

        // A freshly loaded element is hidden in VB6 — it sits exactly on top of its template.
        Assert.IsFalse(loaded.Visible);

        // Loading the same index again returns the element rather than creating a second one.
        Assert.AreSame(loaded, host.LoadControlArrayElement(owner, "ctlButton", 1, template));

        host.UnloadControlArrayElement(owner, "ctlButton", 1, loaded);
        Assert.IsNull(loaded.Parent, "Unload removes the element from its container.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRejectsAnInvalidScaleModeLikeVb6()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;

        var error = Assert.ThrowsException<VB6RaisedError>(() =>
            host.TrySetMember(pictureBox, "ScaleMode", Array.Empty<object?>(), 8));
        Assert.AreEqual(380, error.Number, "VB6 reports Invalid property value for ScaleMode 8.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostAppliesAllSixteenDrawModesToPersistentSurface()
    {
        const int sourceRgb = 0x00FF_0000; // red pen
        const int destinationRgb = 0x0000_00FF; // blue destination

        for (var drawMode = 1; drawMode <= 16; drawMode++)
        {
            using var host = new WinFormsHost();
            var owner = new object();
            host.Load(owner);
            var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;
            pictureBox.Size = new Size(32, 32);
            Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));

            host.GraphicsLine(
                pictureBox,
                new VBGraphicsLine(
                    0,
                    0,
                    480,
                    480,
                    ColorTranslator.ToOle(Color.Blue),
                    false,
                    DrawBox: true,
                    Fill: true));
            Assert.IsTrue(host.TrySetMember(pictureBox, "DrawMode", Array.Empty<object?>(), drawMode));
            host.GraphicsLine(
                pictureBox,
                new VBGraphicsLine(
                    0,
                    0,
                    480,
                    480,
                    ColorTranslator.ToOle(Color.Red),
                    false,
                    DrawBox: true,
                    Fill: true));

            Assert.IsNotNull(pictureBox.Image, $"DrawMode {drawMode}: no persistent surface.");
            using var snapshot = new Bitmap(pictureBox.Image!);
            var actual = snapshot.GetPixel(16, 16);
            var expected = Rop2(drawMode, sourceRgb, destinationRgb);
            Assert.AreEqual((expected >> 16) & 0xFF, actual.R, $"DrawMode {drawMode} red channel.");
            Assert.AreEqual((expected >> 8) & 0xFF, actual.G, $"DrawMode {drawMode} green channel.");
            Assert.AreEqual(expected & 0xFF, actual.B, $"DrawMode {drawMode} blue channel.");

            host.Unload(owner);
        }
    }

    [STATestMethod]
    public void HostRejectsInvalidDrawModeLikeVb6()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;

        var error = Assert.ThrowsException<VB6RaisedError>(() =>
            host.TrySetMember(pictureBox, "DrawMode", Array.Empty<object?>(), 17));
        Assert.AreEqual(380, error.Number, "VB6 reports Invalid property value for DrawMode 17.");

        host.Unload(owner);
    }

    private const int ScaleModeTwip = 1;
    private const int ScaleModePoint = 2;
    private const int ScaleModePixel = 3;
    private const int ScaleModeCharacter = 4;
    private const int ScaleModeInch = 5;
    private const int ScaleModeMillimetre = 6;
    private const int ScaleModeCentimetre = 7;

    private static int Rop2(int drawMode, int pen, int destination) => drawMode switch
    {
        1 => 0,
        2 => ~(pen | destination) & 0x00FF_FFFF,
        3 => destination & ~pen & 0x00FF_FFFF,
        4 => ~pen & 0x00FF_FFFF,
        5 => pen & ~destination & 0x00FF_FFFF,
        6 => ~destination & 0x00FF_FFFF,
        7 => pen ^ destination,
        8 => ~(pen & destination) & 0x00FF_FFFF,
        9 => pen & destination,
        10 => ~(pen ^ destination) & 0x00FF_FFFF,
        11 => destination,
        12 => (~pen | destination) & 0x00FF_FFFF,
        13 => pen,
        14 => (pen | ~destination) & 0x00FF_FFFF,
        15 => pen | destination,
        16 => 0x00FF_FFFF,
        _ => throw new ArgumentOutOfRangeException(nameof(drawMode))
    };

    /// <summary>
    /// Draws a filled box of the given size in the given scale mode and checks the pixel extent.
    /// A null <paramref name="expected"/> means "one inch", resolved against the control's DPI.
    /// </summary>
    private static void AssertScaledBoxExtent(
        int scaleMode,
        float unitsX,
        float unitsY,
        float? expected,
        string name)
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "Picture1", "PictureBox")!;
        pictureBox.Size = new Size(400, 400);
        Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(pictureBox, "ScaleMode", Array.Empty<object?>(), scaleMode));

        var extent = expected ?? pictureBox.DeviceDpi;
        host.GraphicsLine(
            pictureBox,
            new VBGraphicsLine(
                0,
                0,
                unitsX,
                unitsY,
                ColorTranslator.ToOle(Color.Red),
                false,
                DrawBox: true,
                Fill: true));

        Assert.IsNotNull(pictureBox.Image, $"{name}: nothing was drawn.");
        using var snapshot = new Bitmap(pictureBox.Image!);
        var inside = snapshot.GetPixel((int)extent - 3, (int)extent - 3);
        var outside = snapshot.GetPixel((int)extent + 4, (int)extent + 4);
        Assert.IsTrue(
            inside.R > 180 && inside.G < 120,
            $"{name}: expected the box to cover {extent} pixels, but ({(int)extent - 3}) was {inside}.");
        Assert.IsFalse(
            outside.R > 180 && outside.G < 120,
            $"{name}: the box reached past {extent} pixels at ({(int)extent + 4}).");

        host.Unload(owner);
    }

    /// <summary>
    /// Asserts that a designer-convention handler was reached on the native control.
    ///
    /// The assertion is "at least once" on purpose. What is under test is that the VB6 event name
    /// resolves at all — through the OCX connection point, or through the hosting wrapper for the
    /// extender events the control does not implement. How often a focus event repeats is an
    /// AxHost artifact of moving focus between wrapper and inner window, not a VB6 contract.
    /// </summary>
    private static void AssertDesignerHandlerRan(
        Type formType,
        object form,
        string propertyName,
        string handlerName)
    {
        var getter = formType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(method => method.Name.Contains(propertyName, StringComparison.OrdinalIgnoreCase));
        var count = Convert.ToInt32(getter.Invoke(form, null), System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(
            count >= 1,
            $"{handlerName} did not fire through the native connection point.");
    }

    private static void RaisePaint(Control control, Graphics graphics) =>
        typeof(Control).GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(control, new object[] { new PaintEventArgs(graphics, control.ClientRectangle) });

    [STATestMethod]
    public void HostMapsConventionalFormLifecycleEvents()
    {
        using var host = new WinFormsHost();
        var owner = new FormLifecycleEventSink();
        using var form = new Form();

        host.Register(owner, form);
        host.Load(owner);
        Assert.AreEqual(1, owner.InitializeCount);

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
        Assert.AreEqual(1, owner.TerminateCount);

        var disposedOwner = new FormLifecycleEventSink();
        using var disposedForm = new Form();
        host.Register(disposedOwner, disposedForm);
        host.Load(disposedOwner);
        host.Dispose();
        Assert.AreEqual(1, disposedOwner.TerminateCount);
    }

    [STATestMethod]
    public void HostAttachesMdiChildFormsToMdiContainers()
    {
        using var host = new WinFormsHost();
        var parentOwner = new object();
        var childOwner = new object();
        using var parent = new Form();
        using var child = new Form();

        host.Register(parentOwner, parent);
        host.Register(childOwner, child);

        Assert.IsTrue(host.TrySetMember(parentOwner, "MDIForm", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(childOwner, "MDIChild", Array.Empty<object?>(), true));
        Assert.AreSame(parent, child.MdiParent);
        Assert.IsTrue(parent.IsMdiContainer);
        Assert.IsTrue(host.TryGetMember(childOwner, "MDIChild", Array.Empty<object?>(), out var value));
        Assert.AreEqual(true, value);

        host.Unload(childOwner);
        host.Unload(parentOwner);
    }

    private sealed class EventSink
    {
        public int ChangeCount { get; private set; }

        private void Text1_Change() => ChangeCount++;
    }

    private sealed class NativeRichTextEventSink
    {
        public int ChangeCount { get; private set; }

        private void OnChange() => ChangeCount++;
    }

    private sealed class MutableComObjectProvider : IVBComObjectProvider
    {
        public MutableComObjectProvider(object? comObject)
        {
            ComObject = comObject;
        }

        public object? ComObject { get; set; }
    }

    private sealed class NativeRichTextKeyPressEventSink
    {
        public int KeyPressCount { get; private set; }
        public short OriginalKeyAscii { get; private set; }

        private void OnKeyPress(ref short keyAscii)
        {
            KeyPressCount++;
            OriginalKeyAscii = keyAscii;
            keyAscii = (short)'y';
        }
    }

    private sealed class NativeRichTextMouseDownEventSink
    {
        public int MouseDownCount { get; private set; }
        public short Button { get; private set; }
        public short Shift { get; private set; }
        public float X { get; private set; }
        public float Y { get; private set; }

        private void OnMouseDown(short button, short shift, float x, float y)
        {
            MouseDownCount++;
            Button = button;
            Shift = shift;
            X = x;
            Y = y;
        }
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

        private void OnKeyDown(ref short keyCode, short shift)
        {
            KeyCode = keyCode;
            KeyShift = shift;
            keyCode = 0;
        }

        private void OnKeyPress(ref short keyAscii)
        {
            KeyAscii = keyAscii;
            keyAscii = (short)'y';
        }

        private void Form_Resize() => FormResizeCount++;
    }

    private sealed class PaintEventSink
    {
        public int PaintCount { get; private set; }

        public Action? PaintCallback { get; init; }

        private void Form_Paint()
        {
            PaintCount++;
            PaintCallback?.Invoke();
        }
    }

    private sealed class FormLifecycleEventSink
    {
        public int InitializeCount { get; private set; }
        public int TerminateCount { get; private set; }
        public int ActivateCount { get; private set; }
        public int DeactivateCount { get; private set; }
        public int QueryUnloadCount { get; private set; }
        public int UnloadCount { get; private set; }
        public short UnloadMode { get; private set; }

        private void Form_Initialize() => InitializeCount++;

        private void Form_Terminate() => TerminateCount++;

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

    [STATestMethod]
    public void HostGivesAUserControlThePropertiesTheContainerPersisted()
    {
        using var host = new WinFormsHost();
        var owner = new UserControlOwner();
        host.Load(owner);

        // Innerhalb der Designer-Huelle: genau so erreicht ein UserControl die Werte, die der
        // Container in seiner .frm fuer diese Instanz abgelegt hat.
        host.BeginDesignerInitialization(owner);
        var generated = (GeneratedUserControlStub)host.CreateControl(
            owner,
            "Widget1",
            typeof(GeneratedUserControlStub).FullName!)!;

        // Vor dem Schliessen faellt die Entscheidung noch nicht: welche der beiden Prozeduren VB6
        // ruft, haengt daran, ob etwas gespeichert war -- und das steht erst am Ende fest.
        Assert.AreEqual(0, generated.InitPropertiesCount);
        Assert.AreEqual(0, generated.ReadPropertiesCount);

        host.TrySetMember(generated, "Beschriftung", Array.Empty<object?>(), "aus dem Designer");
        host.CompleteDesignerInitialization(owner);

        // Ein Control mit gespeichertem Zustand wird wiederhergestellt, nicht neu angelegt. Vorher
        // war die Tuete bei jeder Erzeugung leer, und UserControl_ReadProperties lief nie.
        Assert.AreEqual(1, generated.ReadPropertiesCount);
        Assert.AreEqual(0, generated.InitPropertiesCount);
        Assert.AreEqual("aus dem Designer", generated.DesignerValue);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostAppliesTabOrderAndZOrderFromTheDesigner()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        var first = (Control)host.CreateControl(owner, "txtEins", "TextBox")!;
        var second = (Control)host.CreateControl(owner, "txtZwei", "TextBox")!;

        // Der Korpus schreibt TabIndex und TabStop fuer fast jedes Control. Beide gab es im Host
        // gar nicht, also stand die Tabulatorfolge in der Reihenfolge der Erzeugung.
        Assert.IsTrue(host.TrySetMember(first, "TabIndex", Array.Empty<object?>(), 1));
        Assert.IsTrue(host.TrySetMember(second, "TabIndex", Array.Empty<object?>(), 0));
        Assert.IsTrue(host.TrySetMember(second, "TabStop", Array.Empty<object?>(), false));

        Assert.IsTrue(host.TryGetMember(first, "TabIndex", Array.Empty<object?>(), out var firstIndex));
        Assert.AreEqual(1, firstIndex);
        Assert.IsTrue(host.TryGetMember(second, "TabIndex", Array.Empty<object?>(), out var secondIndex));
        Assert.AreEqual(0, secondIndex);
        Assert.IsTrue(host.TryGetMember(second, "TabStop", Array.Empty<object?>(), out var tabStop));
        Assert.AreEqual(false, tabStop);

        // ZOrder 0 holt nach vorn, ZOrder 1 schickt nach hinten -- das ist die VB6-Bedeutung.
        Assert.IsTrue(host.TryInvokeMember(second, "ZOrder", [0], out _));
        Assert.AreEqual(0, second.Parent!.Controls.GetChildIndex(second));
        Assert.IsTrue(host.TryInvokeMember(second, "ZOrder", [1], out _));
        Assert.AreEqual(
            second.Parent!.Controls.Count - 1,
            second.Parent!.Controls.GetChildIndex(second));

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostShowsAFormModallyWhenTheStyleSaysSo()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);

        // Show vbModal blockiert in VB6 bis zum Entladen; das Argument wurde hier verworfen, und
        // ein Programm lief an dem Dialog vorbei, auf den es wartete. Der Test schliesst das
        // Fenster aus dem Shown-Ereignis heraus, damit der modale Aufruf zurueckkehren kann.
        using var form = new Form { Text = "Modal" };
        host.Register(owner, form);

        var beobachtet = false;
        form.Shown += (sender, _) =>
        {
            beobachtet = ((Form)sender!).Modal;
            ((Form)sender!).Close();
        };

        Assert.IsTrue(host.TryInvokeMember(owner, "Show", [1], out _));
        Assert.IsTrue(beobachtet, "Show vbModal muss die Form modal anzeigen.");

        host.Unload(owner);
    }

    /// <summary>Schreibt mit, in welcher Reihenfolge die Form-Lebenszyklusereignisse eintreffen.</summary>
    private sealed class FormLifecycleSink
    {
        public List<string> Trace { get; } = new();

        private void Form_Initialize() => Trace.Add("Initialize");

        private void Form_Load() => Trace.Add("Load");

        private void Form_Activate() => Trace.Add("Activate");

        private void Form_Deactivate() => Trace.Add("Deactivate");

        private void Form_Unload(ref short cancel) => Trace.Add("Unload");

        private void Form_Terminate() => Trace.Add("Terminate");
    }

    [STATestMethod]
    public void HostRaisesTheFormLifecycleInTheVb6Order()
    {
        using var host = new WinFormsHost();
        var owner = new FormLifecycleSink();

        // VB6 ordnet fest: Initialize kommt vor Load, Load vor Activate, und Terminate zuletzt --
        // nach Unload. Ein Handler, der sich auf diese Reihenfolge verlaesst, ist der Normalfall.
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));
        host.Unload(owner);

        var trace = owner.Trace;
        Assert.IsTrue(trace.Count > 0, "Kein Lebenszyklusereignis erreicht die Form.");
        CollectionAssert.Contains(trace, "Initialize", string.Join(" | ", trace));
        CollectionAssert.Contains(trace, "Load");
        CollectionAssert.Contains(trace, "Unload");
        CollectionAssert.Contains(trace, "Terminate");

        Assert.IsTrue(
            trace.IndexOf("Initialize") < trace.IndexOf("Load"),
            string.Join(" ", trace));
        Assert.IsTrue(
            trace.IndexOf("Load") < trace.IndexOf("Unload"),
            string.Join(" ", trace));
        Assert.IsTrue(
            trace.IndexOf("Unload") < trace.IndexOf("Terminate"),
            string.Join(" ", trace));

        if (trace.Contains("Activate"))
        {
            Assert.IsTrue(
                trace.IndexOf("Load") < trace.IndexOf("Activate"),
                string.Join(" ", trace));
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

        public int InitPropertiesCount { get; private set; }
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }

        private void UserControl_Initialize() => InitializeCount++;

        private void UserControl_InitProperties()
        {
            InitPropertiesCount++;

            // InitProperties ist die Stelle, an der ein neues Control seine Vorgaben setzt;
            // WriteProperties schreibt sie beim Beenden weg.
            Defaults["Caption"] = "persisted";
        }

        public Dictionary<string, object?> Defaults { get; } = new(StringComparer.OrdinalIgnoreCase);

        private void UserControl_Show() => ShowCount++;

        private void UserControl_Hide() => HideCount++;

        /// <summary>Was der Container fuer diese Instanz abgelegt hatte, sofern etwas da war.</summary>
        public object? DesignerValue { get; private set; }

        private void UserControl_ReadProperties(object propertyBag)
        {
            ReadPropertiesCount++;
            var bag = (VBPropertyBag)propertyBag;
            DesignerValue = bag.ReadProperty("Beschriftung");
            bag.WriteProperty("Caption", "persisted");
            ReadPropertyValue = bag.ReadProperty("Caption");
        }

        private void UserControl_WriteProperties(object propertyBag)
        {
            WritePropertiesCount++;
            var bag = (VBPropertyBag)propertyBag;
            foreach (var entry in Defaults)
            {
                bag.WriteProperty(entry.Key, entry.Value);
            }

            WritePropertyValue = bag.ReadProperty("Caption");
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

    /// <summary>
    /// A VB6 form stores its size as ClientWidth/ClientHeight in twips and never writes
    /// Width/Height at form level, so a host that only knew the latter left every emitted form at
    /// the WinForms default size no matter what the designer said.
    /// </summary>
    [STATestMethod]
    public void HostAppliesTheDesignerClientSizeToAForm()
    {
        using var host = new WinFormsHost();
        var owner = new object();

        host.Load(owner);

        Assert.IsTrue(host.TrySetMember(owner, "ClientWidth", Array.Empty<object?>(), 8160));
        Assert.IsTrue(host.TrySetMember(owner, "ClientHeight", Array.Empty<object?>(), 5280));

        Assert.IsTrue(host.TryGetMember(owner, "ClientWidth", Array.Empty<object?>(), out var width));
        Assert.IsTrue(host.TryGetMember(owner, "ClientHeight", Array.Empty<object?>(), out var height));
        Assert.AreEqual(8160, Convert.ToInt32(width));
        Assert.AreEqual(5280, Convert.ToInt32(height));

        // ScaleWidth/ScaleHeight read the same client area, so both names have to agree.
        Assert.IsTrue(host.TryGetMember(owner, "ScaleWidth", Array.Empty<object?>(), out var scaleWidth));
        Assert.IsTrue(host.TryGetMember(owner, "ScaleHeight", Array.Empty<object?>(), out var scaleHeight));
        Assert.AreEqual(8160, Convert.ToInt32(scaleWidth));
        Assert.AreEqual(5280, Convert.ToInt32(scaleHeight));
    }

    [STATestMethod]
    public void HostLoadsPersistedDesignerStateIntoANativeActiveXControlInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.Slider.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native property bag validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var slider = host.CreateControl(owner, "sldWert", "MSComctlLib.Slider")!;
        Assert.IsInstanceOfType<AxHost>(slider);

        // Genau die Werte, die der Designer in die .frm schreibt. _ExtentX und _ExtentY stehen dort
        // fuer jedes OCX -- und der Einzelzugriff kann sie nicht setzen: das Control weist sie ab.
        // Vor dieser Karte waren sie damit verloren, ohne dass irgendwo etwas gemeldet wurde.
        Assert.IsFalse(host.TrySetMember(slider, "_ExtentX", Array.Empty<object?>(), 4657));
        Assert.IsFalse(host.TrySetMember(slider, "_ExtentY", Array.Empty<object?>(), 873));
        Assert.IsTrue(host.TrySetMember(slider, "Min", Array.Empty<object?>(), 0));
        Assert.IsTrue(host.TrySetMember(slider, "Max", Array.Empty<object?>(), 50));
        Assert.IsTrue(host.TrySetMember(slider, "Value", Array.Empty<object?>(), 17));
        Assert.IsTrue(host.TrySetMember(slider, "TickFrequency", Array.Empty<object?>(), 5));

        host.CompleteDesignerInitialization(owner);

        // Das Control nach seinem eigenen Zustand fragen. Was es zurueckschreibt, hat es
        // uebernommen -- eine Zusage, die keine Herleitung ersetzt.
        var persisted = (IVBPersistPropertyBag)((IVBComObjectProvider)slider).ComObject!;
        var saved = new RecordingPropertyBag();
        persisted.Save(saved, clearDirty: true, saveAllProperties: true);

        Assert.AreEqual(4657, Convert.ToInt32(saved.Written["_ExtentX"]));
        Assert.AreEqual(873, Convert.ToInt32(saved.Written["_ExtentY"]));

        // Und die Uebergabe darf nicht kosten, was der Einzelzugriff schon gesetzt hat.
        Assert.AreEqual(50, Convert.ToInt32(saved.Written["Max"]));
        Assert.AreEqual(17, Convert.ToInt32(saved.Written["Value"]));
        Assert.AreEqual(5, Convert.ToInt32(saved.Written["TickFrequency"]));

        host.Unload(owner);
    }

    [STATestMethod]
    public void NativeImageListTakesItsNestedDesignerImagesInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.ImageListCtrl.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native property bag validation requires a registered 32-bit control.");
            }

            return;
        }

        // So schreibt der Designer eine ImageList in die .frm: BeginProperty Images, darin je ein
        // BeginProperty ListImageN. Als Einzelzuweisung erreicht das ein natives Control nicht --
        // es fragt nach der Sammlung als Objekt, nicht nach ihren Namen.
        var control = Activator.CreateInstance(
            Type.GetTypeFromProgID("MSComctlLib.ImageListCtrl.2", throwOnError: true)!)!;
        try
        {
            var applied = VBComPersistence.TryApplyDesignerState(
                control,
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["_ExtentX"] = 1005,
                    ["_ExtentY"] = 1005,
                    ["ImageWidth"] = 16,
                    ["ImageHeight"] = 16,
                    ["Images.NumListImages"] = 2,
                    ["Images.ListImage1.Key"] = "Ordner",
                    ["Images.ListImage2.Key"] = "Datei"
                });

            Assert.IsTrue(applied);

            var listImages = control.GetType().InvokeMember(
                "ListImages",
                BindingFlags.GetProperty,
                binder: null,
                control,
                args: null)!;
            var count = listImages.GetType().InvokeMember(
                "Count",
                BindingFlags.GetProperty,
                binder: null,
                listImages,
                args: null);

            Assert.AreEqual(2, Convert.ToInt32(count));
        }
        finally
        {
            if (Marshal.IsComObject(control))
            {
                Marshal.FinalReleaseComObject(control);
            }
        }
    }

    [STATestMethod]
    public void NativeImageListTakesAPictureFromTheDesignerEnvelopeInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("MSComctlLib.ImageListCtrl.2", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native property bag validation requires a registered 32-bit control.");
            }

            return;
        }

        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Red);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
        var payload = "__VB6_FRX_BASE64__" + Convert.ToBase64String(stream.ToArray());

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var imageList = host.CreateControl(owner, "ilTest", "MSComctlLib.ImageList")!;
        Assert.IsInstanceOfType<AxHost>(imageList);

        // So schreibt der Designer eine ImageList: die Bilder liegen in der .frx und erreichen den
        // Host als kodierte Nutzlast. Die als Zeichenkette weiterzureichen hat das Control die
        // Zeichenkette als Schnittstellenzeiger lesen lassen -- der Prozess starb mit einer
        // Zugriffsverletzung, nicht mit einem fehlenden Bild.
        foreach (var (name, value) in new (string, object)[]
        {
            ("ImageWidth", 16),
            ("ImageHeight", 16),
            ("Images.NumListImages", 1),
            ("Images.ListImage1.Key", "rot"),
            ("Images.ListImage1.Picture", payload)
        })
        {
            host.TrySetMember(imageList, name, Array.Empty<object?>(), value);
        }

        host.CompleteDesignerInitialization(owner);

        var comObject = ((IVBComObjectProvider)imageList).ComObject!;
        var listImages = comObject.GetType().InvokeMember(
            "ListImages", BindingFlags.GetProperty, binder: null, comObject, args: null)!;
        Assert.AreEqual(
            1,
            Convert.ToInt32(listImages.GetType().InvokeMember(
                "Count", BindingFlags.GetProperty, binder: null, listImages, args: null)));

        var entry = listImages.GetType().InvokeMember(
            "Item",
            BindingFlags.GetProperty | BindingFlags.InvokeMethod,
            binder: null,
            listImages,
            new object[] { 1 })!;
        Assert.AreEqual(
            "rot",
            entry.GetType().InvokeMember("Key", BindingFlags.GetProperty, binder: null, entry, args: null));
        Assert.IsNotNull(
            entry.GetType().InvokeMember("Picture", BindingFlags.GetProperty, binder: null, entry, args: null),
            "Das Bild aus der .frx muss als Bildobjekt ankommen, nicht als Nutzlast.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void NativeRichTextBoxKeepsItsDesignerTextAcrossTheEnvelopeInX86()
    {
        if (Environment.Is64BitProcess ||
            Type.GetTypeFromProgID("RICHTEXT.RichtextCtrl.1", throwOnError: false) is null)
        {
            if (RequireNativeOcx)
            {
                Assert.Fail("Native property bag validation requires a registered 32-bit control.");
            }

            return;
        }

        using var host = new WinFormsHost(preferNativeActiveX: true);
        var owner = new object();
        host.Load(owner);
        Assert.IsTrue(host.TryInvokeMember(owner, "Show", Array.Empty<object?>(), out _));

        var richText = host.CreateControl(owner, "rtfTest", "RichTextLib.RichTextBox")!;
        Assert.IsInstanceOfType<AxHost>(richText);

        // So steht der Text im Korpus: TextRTF = $"frmInfo.frx":2CFA. Das Control fragt TextRTF
        // ueber die Eigenschaftstuete ab -- die .frx-Seite braucht dafuer kein IPersistStreamInit.
        Assert.IsTrue(host.TrySetMember(
            richText,
            "TextRTF",
            Array.Empty<object?>(),
            @"{tf1ansi Hallo aus der frxpar}"));

        // Und die Uebergabe des ganzen Zustands darf ihn nicht wieder kosten.
        host.CompleteDesignerInitialization(owner);

        Assert.IsTrue(host.TryGetMember(richText, "Text", Array.Empty<object?>(), out var text));
        StringAssert.Contains(Convert.ToString(text), "Hallo aus der frx", Convert.ToString(text));

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostStartsATimerFromItsDesignerIntervalAlone()
    {
        using var host = new WinFormsHost();
        var owner = new TimerEventSink();
        host.Load(owner);

        // Eine .frm schreibt fuer einen Timer haeufig nur das Interval; Enabled ist in VB6 von
        // Haus aus True. Blieb der WinForms-Timer deshalb aus, feuerte er nie -- im Korpus stand
        // das Splashfenster dadurch fuer immer still, statt nach zwei Sekunden zu uebergeben.
        var timer = host.CreateControl(owner, "Timer1", "Timer")!;
        Assert.IsTrue(host.TryGetMember(timer, "Enabled", Array.Empty<object?>(), out var enabled));
        Assert.AreEqual(true, enabled);

        // Interval 0 heisst in VB6 "feuert nicht", und ein WinForms-Timer nimmt keine 0 an. Die
        // beiden Zustaende werden deshalb getrennt gehalten.
        Assert.IsTrue(host.TryGetMember(timer, "Interval", Array.Empty<object?>(), out var interval));
        Assert.AreEqual(0, interval);

        Assert.IsTrue(host.TrySetMember(timer, "Interval", Array.Empty<object?>(), 250));
        Assert.IsTrue(host.TryGetMember(timer, "Interval", Array.Empty<object?>(), out interval));
        Assert.AreEqual(250, interval);

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostRaisesNoEventWhileTheDesignerEnvelopeIsOpen()
    {
        using var host = new WinFormsHost();
        var owner = new TimerEventSink();
        host.Load(owner);

        // VB6 legt eine Form zuerst aus und laesst das Programm danach laufen. WinForms meldet
        // Resize, sobald eine Groesse zugewiesen wird -- im Korpus rief das conInTab_Resize auf,
        // waehrend das Line-Control zwei Zeilen weiter unten noch nicht existierte.
        host.BeginDesignerInitialization(owner);
        var timer = host.CreateControl(owner, "Timer1", "Timer")!;
        var raiseTick = timer.GetType().GetMethod("RaiseTick", BindingFlags.Instance | BindingFlags.NonPublic)!;

        raiseTick.Invoke(timer, null);
        Assert.AreEqual(0, owner.TickCount, "Waehrend der Huelle laeuft kein Ereignis.");

        host.CompleteDesignerInitialization(owner);

        raiseTick.Invoke(timer, null);
        Assert.AreEqual(1, owner.TickCount, "Danach schon.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostDrawsWithTheConfiguredDrawWidth()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "picCanvas", "PictureBox")!;
        pictureBox.Size = new Size(40, 40);
        Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(pictureBox, "ScaleMode", Array.Empty<object?>(), 3));

        // VB6 gibt DrawWidth mit 1 vor, und der Stift war hier fest auf einem Pixel -- ein
        // Programm, das DrawWidth setzte, zeichnete trotzdem haarfein, und das Setzen selbst
        // wurde nicht einmal beantwortet.
        Assert.IsTrue(host.TryGetMember(pictureBox, "DrawWidth", Array.Empty<object?>(), out var width));
        Assert.AreEqual(1, width);

        Assert.IsTrue(host.TrySetMember(pictureBox, "DrawWidth", Array.Empty<object?>(), 5));
        Assert.IsTrue(host.TryGetMember(pictureBox, "DrawWidth", Array.Empty<object?>(), out width));
        Assert.AreEqual(5, width);

        host.GraphicsPSet(pictureBox, new VBGraphicsPoint(20f, 20f, Color: 255, IsStep: false));

        var surface = (Bitmap)pictureBox.Image!;

        // Ein PSet mit DrawWidth 5 setzt ein Quadrat von fuenf Pixeln um den Punkt, nicht einen.
        Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), surface.GetPixel(20, 20).ToArgb());
        Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), surface.GetPixel(21, 21).ToArgb());
        Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), surface.GetPixel(19, 19).ToArgb());

        var error = Assert.ThrowsException<VB6RaisedError>(() =>
            host.TrySetMember(pictureBox, "DrawWidth", Array.Empty<object?>(), 0));
        Assert.AreEqual(380, error.Number, "VB6 meldet Invalid property value fuer DrawWidth 0.");

        host.Unload(owner);
    }

    [STATestMethod]
    public void HostClipsDrawingToTheTargetSurface()
    {
        using var host = new WinFormsHost();
        var owner = new object();
        host.Load(owner);
        var pictureBox = (PictureBox)host.CreateControl(owner, "picCanvas", "PictureBox")!;
        pictureBox.Size = new Size(20, 20);
        Assert.IsTrue(host.TrySetMember(pictureBox, "AutoRedraw", Array.Empty<object?>(), true));
        Assert.IsTrue(host.TrySetMember(pictureBox, "ScaleMode", Array.Empty<object?>(), 3));

        // Eine Linie, die weit ueber die Flaeche hinauslaeuft, wird beschnitten -- sie darf weder
        // scheitern noch ausserhalb wirken. Das ist die Zusage "clipping" der Karte.
        host.GraphicsLine(
            pictureBox,
            new VBGraphicsLine(-500f, 10f, 500f, 10f, IsStep: false, Color: 255, DrawBox: false, Fill: false));

        var surface = (Bitmap)pictureBox.Image!;
        Assert.AreEqual(20, surface.Width);
        Assert.AreEqual(20, surface.Height);
        Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), surface.GetPixel(0, 10).ToArgb());
        Assert.AreEqual(Color.FromArgb(255, 255, 0, 0).ToArgb(), surface.GetPixel(19, 10).ToArgb());

        // Ein Punkt weit ausserhalb hinterlaesst nichts und reisst nichts ab.
        host.GraphicsPSet(pictureBox, new VBGraphicsPoint(400f, 400f, Color: 65280, IsStep: false));
        Assert.AreEqual(20, surface.Width);

        host.Unload(owner);
    }

    /// <summary>Traegt den Namen, den der Emitter einer generierten Form gibt.</summary>
    private sealed class __vb6_class_frmProbe
    {
    }

    [STATestMethod]
    public void HostNamesAFormWithoutACaptionAfterItsVB6Name()
    {
        using var host = new WinFormsHost();
        var owner = new __vb6_class_frmProbe();
        host.Load(owner);

        // Eine .frm ohne Caption -- ein randloses Splashfenster etwa -- bekam den emittierten
        // Typnamen in den Titelbalken. Gemessen am Korpus stand dort "__vb6_class_frmSplash".
        // Das Namensschema des Emitters darf nicht zu beobachtbarem Programmverhalten werden.
        Assert.IsTrue(host.TryGetMember(owner, "Caption", Array.Empty<object?>(), out var caption));
        Assert.AreEqual("frmProbe", Convert.ToString(caption));

        Assert.IsTrue(host.TryGetMember(owner, "Name", Array.Empty<object?>(), out var name));
        Assert.AreEqual("frmProbe", Convert.ToString(name));

        host.Unload(owner);
    }

    /// <summary>Nimmt entgegen, was ein Control ueber sich selbst zu sagen hat.</summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class RecordingPropertyBag : IVBPropertyBag
    {
        public Dictionary<string, object?> Written { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int Read(string propertyName, ref object? value, IntPtr errorLog) =>
            unchecked((int)0x80070490);

        public int Write(string propertyName, ref object? value)
        {
            Written[propertyName] = value;
            return 0;
        }
    }
}
