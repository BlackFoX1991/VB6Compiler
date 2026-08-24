using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

/// <summary>
/// WinForms implementation of the portable VB6 host contract. Forms and generated VB objects
/// are registered explicitly; unknown OCX types receive a host-neutral Panel until a dedicated
/// ActiveX adapter is installed.
/// </summary>
public sealed class WinFormsHost : IVB6Host, IDisposable
{
    private const string FrxResourcePrefix = "__VB6_FRX_BASE64__";

    private readonly Dictionary<object, FormBinding> _bindings =
        new(ReferenceEqualityComparer.Instance);
    private readonly List<EventBinding> _events = new();
    private readonly List<ContextMenuStrip> _popups = new();
    private readonly Dictionary<TreeView, TreeViewState> _treeViewStates =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<RichTextBox, RichTextBoxState> _richTextBoxStates =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Control, DesignerControlState> _designerControlStates =
        new(ReferenceEqualityComparer.Instance);
    private readonly ToolTip _toolTip = new();
    private bool _disposed;

    public void Register(object vbObject, Form form)
    {
        ArgumentNullException.ThrowIfNull(vbObject);
        ArgumentNullException.ThrowIfNull(form);
        ThrowIfDisposed();

        if (_bindings.TryGetValue(vbObject, out var existing) &&
            !ReferenceEquals(existing.Form, form))
        {
            existing.Form.Dispose();
        }

        _bindings[vbObject] = new FormBinding(form);
    }

    public void RegisterControl(object vbObject, string name, Control control)
    {
        ArgumentNullException.ThrowIfNull(vbObject);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(control);
        ThrowIfDisposed();

        var binding = GetOrCreateBinding(vbObject);
        binding.Controls[name] = control;
        if (control.Parent is null && control is not Form)
        {
            binding.Form.Controls.Add(control);
        }

        AttachGeneratedControlEvents(vbObject, control, name);
    }

    public void DoEvents() => Application.DoEvents();

    public void PopupMenu(object? menu, int flags, float x, float y)
    {
        ThrowIfDisposed();
        _ = flags;

        if (menu is not MenuProxy menuProxy)
        {
            return;
        }

        var binding = _bindings.Values.FirstOrDefault(candidate =>
            candidate.Components.Values.Any(component => ReferenceEquals(component, menuProxy)));
        if (binding is null)
        {
            return;
        }

        var popup = new ContextMenuStrip();
        var sourceItems = menuProxy.DropDownItems.Count > 0
            ? menuProxy.DropDownItems.Cast<ToolStripItem>()
            : new[] { (ToolStripItem)menuProxy };
        foreach (var item in sourceItems)
        {
            popup.Items.Add(ClonePopupItem(item));
        }

        _popups.Add(popup);
        popup.Closed += (_, _) =>
        {
            _popups.Remove(popup);
            popup.Dispose();
        };

        var form = binding.Form;
        var dpi = form.DeviceDpi > 0 ? form.DeviceDpi : 96;
        var location = x == 0 && y == 0
            ? form.PointToClient(Cursor.Position)
            : new Point(
                FromTwips(x, 1440f / dpi),
                FromTwips(y, 1440f / dpi));
        popup.Show(form, location);
    }

    public void GraphicsLine(VBGraphicsLine line)
    {
        ThrowIfDisposed();
        var target = _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(form => !form.IsDisposed);
        if (target is not null)
        {
            RenderGraphicsLine(target, line);
        }
    }

    public void GraphicsLine(object? target, VBGraphicsLine line)
    {
        ThrowIfDisposed();
        if (ResolveDrawingTarget(target) is { } control)
        {
            RenderGraphicsLine(control, line);
        }
    }

    private void RenderGraphicsLine(Control target, VBGraphicsLine line)
    {
        var surface = GetDrawingSurface(target);
        var state = GetDesignerControlState(target);
        var scale = state.ScaleMode switch
        {
            3 => 1f,
            2 => target.DeviceDpi / 72f,
            _ => target.DeviceDpi / 1440f
        };
        var startX = line.StartX * scale;
        var startY = line.StartY * scale;
        var endX = (line.IsStep ? line.StartX + line.EndX : line.EndX) * scale;
        var endY = (line.IsStep ? line.StartY + line.EndY : line.EndY) * scale;
        var color = Color.Black;
        if (line.Color is int oleColor)
        {
            try
            {
                color = ColorTranslator.FromOle(oleColor);
            }
            catch (ArgumentException)
            {
                color = Color.Black;
            }
        }

        using var graphics = Graphics.FromImage(surface);
        using var pen = new Pen(color, 1f);
        if (line.DrawBox)
        {
            var rectangle = RectangleF.FromLTRB(
                Math.Min(startX, endX),
                Math.Min(startY, endY),
                Math.Max(startX, endX),
                Math.Max(startY, endY));
            if (line.Fill)
            {
                using var brush = new SolidBrush(color);
                graphics.FillRectangle(brush, rectangle);
            }
            else
            {
                graphics.DrawRectangle(pen, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            }
        }
        else
        {
            graphics.DrawLine(pen, startX, startY, endX, endY);
        }

        target.Invalidate();
    }

    public void PaintPicture(VBPaintPicture picture)
    {
        ThrowIfDisposed();
        var target = _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(form => !form.IsDisposed);
        if (target is not null)
        {
            _ = TryRenderPaintPicture(target, picture);
        }
    }

    private bool TryRenderPaintPicture(Control target, VBPaintPicture picture)
    {
        if (!TryGetPaintPictureImage(picture.Picture, out var source, out var ownsSource))
        {
            return false;
        }

        try
        {
            var surface = GetDrawingSurface(target);
            var state = GetDesignerControlState(target);
            var scale = state.ScaleMode switch
            {
                3 => 1f,
                2 => target.DeviceDpi / 72f,
                _ => target.DeviceDpi / 1440f
            };
            var x = picture.X * scale;
            var y = picture.Y * scale;
            var width = picture.Width == 0 ? source!.Width : picture.Width * scale;
            var height = picture.Height == 0 ? source!.Height : picture.Height * scale;

            using var graphics = Graphics.FromImage(surface);
            graphics.DrawImage(source!, new RectangleF(x, y, width, height));
            target.Invalidate();
        }
        finally
        {
            if (ownsSource)
            {
                source!.Dispose();
            }
        }

        return true;
    }

    public int RunMessageLoop()
    {
        ThrowIfDisposed();
        var form = _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(candidate => !candidate.IsDisposed);
        if (form is null)
        {
            return 0;
        }

        // VB6 startup code may omit an explicit Show call when the form is the project startup
        // object. Application.Run does not make every host-created form visible consistently, so
        // the WinForms boundary enforces the startup-form contract here.
        if (!form.Visible)
        {
            form.Show();
        }

        Application.Run(form);
        return 0;
    }

    public void Load(object target)
    {
        ThrowIfDisposed();
        _ = GetOrCreateBinding(target);
        TrySubscribeEvent(target, "Load", target, "Form_Load");
        AttachGeneratedFormEvents(target);
    }

    public void Unload(object target)
    {
        ThrowIfDisposed();
        if (!_bindings.Remove(target, out var binding))
        {
            return;
        }

        foreach (var treeView in binding.Controls.Values.OfType<TreeView>())
        {
            _treeViewStates.Remove(treeView);
        }

        foreach (var richTextBox in binding.Controls.Values.OfType<RichTextBox>())
        {
            _richTextBoxStates.Remove(richTextBox);
        }

        foreach (var control in binding.Controls.Values)
        {
            if (_designerControlStates.Remove(control, out var state))
            {
                state.Dispose();
            }
        }

        if (_designerControlStates.Remove(binding.Form, out var formState))
        {
            formState.Dispose();
        }

        foreach (var eventBinding in _events
                     .Where(eventBinding =>
                         ReferenceEquals(eventBinding.Source, target) ||
                         ReferenceEquals(eventBinding.Target, target) ||
                         ReferenceEquals(eventBinding.EventSource, binding.Form) ||
                         binding.Controls.Values.Any(control =>
                             ReferenceEquals(control, eventBinding.EventSource)))
                     .ToArray())
        {
            RemoveEventBinding(eventBinding);
        }

        binding.Form.Hide();
        binding.Form.Dispose();
    }

    public object? CreateControl(object owner, string name, string typeName)
    {
        ThrowIfDisposed();
        var binding = GetOrCreateBinding(owner);
        if (binding.Controls.TryGetValue(name, out var existing))
        {
            return existing;
        }
        if (binding.Components.TryGetValue(name, out var existingComponent))
        {
            return existingComponent;
        }

        var separator = name.LastIndexOf('.');
        var parentName = separator < 0 ? null : name[..separator];
        var logicalName = separator < 0 ? name : name[(separator + 1)..];
        var parent = parentName is not null && binding.Controls.TryGetValue(parentName, out var parentControl)
            ? parentControl
            : null;
        var hostObject = CreateControlInstance(typeName);
        if (hostObject is not Control control)
        {
            if (hostObject is MenuProxy menu)
            {
                menu.Name = logicalName;
                binding.Components.Add(name, menu);
                if (!binding.Components.ContainsKey(logicalName))
                {
                    binding.Components.Add(logicalName, menu);
                }

                var menuStrip = GetOrCreateMenuStrip(binding);
                if (parentName is not null &&
                    binding.Components.TryGetValue(parentName, out var parentComponent) &&
                    parentComponent is MenuProxy parentMenu)
                {
                    parentMenu.DropDownItems.Add(menu);
                }
                else
                {
                    menuStrip.Items.Add(menu);
                }

                AttachGeneratedMenuEvents(owner, menu, logicalName);
                return menu;
            }

            if (hostObject is CommonDialogProxy dialog)
            {
                dialog.Name = logicalName;
            }
            else if (hostObject is ImageListProxy imageList)
            {
                imageList.Name = logicalName;
            }

            binding.Components.Add(name, hostObject);
            if (!binding.Components.ContainsKey(logicalName))
            {
                binding.Components.Add(logicalName, hostObject);
            }

            return hostObject;
        }

        control.Name = logicalName.Replace("(", "_", StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(",", "_", StringComparison.Ordinal);
        binding.Controls.Add(name, control);
        if (!binding.Controls.ContainsKey(logicalName))
        {
            binding.Controls.Add(logicalName, control);
        }

        (parent?.Controls ?? binding.Form.Controls).Add(control);
        AttachGeneratedControlEvents(owner, control, logicalName);
        return control;
    }

    public void EnsureForm(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ThrowIfDisposed();
        if (target is CommonDialogProxy or
            ImageListProxy or
            TreeNodesProxy or
            TreeNodeProxy or
            ComboItemsProxy or
            ComboItemProxy)
        {
            return;
        }

        _ = GetOrCreateBinding(target);
    }

    public bool TryGetMember(
        object target,
        string memberName,
        object?[] arguments,
        out object? value)
    {
        ThrowIfDisposed();
        if (target is not Control &&
            _bindings.TryGetValue(target, out var ownerBinding) &&
            ownerBinding.Controls.TryGetValue(memberName, out var namedControl))
        {
            value = namedControl;
            return true;
        }

        if (target is not Control &&
            _bindings.TryGetValue(target, out ownerBinding) &&
            ownerBinding.Components.TryGetValue(memberName, out var namedComponent))
        {
            value = namedComponent;
            return true;
        }

        if (target is ImageListProxy imageList && arguments.Length == 0)
        {
            if (string.Equals(memberName, "ImageWidth", StringComparison.OrdinalIgnoreCase))
            {
                value = imageList.ImageWidth;
                return true;
            }

            if (string.Equals(memberName, "ImageHeight", StringComparison.OrdinalIgnoreCase))
            {
                value = imageList.ImageHeight;
                return true;
            }
        }

        if (target is MenuProxy menu && arguments.Length == 0 &&
            TryReadMenuProperty(menu, memberName, out value))
        {
            return true;
        }

        if (target is CommonDialogProxy commonDialog && arguments.Length == 0)
        {
            if (string.Equals(memberName, "FileName", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.FileName;
                return true;
            }

            if (string.Equals(memberName, "Filter", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.Filter;
                return true;
            }

            if (string.Equals(memberName, "DialogTitle", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.DialogTitle;
                return true;
            }

            if (string.Equals(memberName, "FilterIndex", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.FilterIndex;
                return true;
            }

            if (string.Equals(memberName, "CancelError", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.CancelError;
                return true;
            }

            if (string.Equals(memberName, "DefaultExt", StringComparison.OrdinalIgnoreCase))
            {
                value = commonDialog.DefaultExt;
                return true;
            }
        }

        if (target is TreeView treeView)
        {
            if (string.Equals(memberName, "Nodes", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = new TreeNodesProxy(treeView);
                return true;
            }

            if (string.Equals(memberName, "SelectedItem", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = treeView.SelectedNode is { } selected
                    ? new TreeNodeProxy(treeView, selected)
                    : null;
                return true;
            }
        }

        if (target is ImageComboControl imageCombo)
        {
            if (string.Equals(memberName, "ComboItems", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = new ComboItemsProxy(imageCombo);
                return true;
            }

            if (string.Equals(memberName, "SelectedItem", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = imageCombo.SelectedIndex >= 0 &&
                        imageCombo.SelectedIndex < imageCombo.Entries.Count
                    ? new ComboItemProxy(imageCombo, imageCombo.Entries[imageCombo.SelectedIndex])
                    : null;
                return true;
            }
        }

        if (TryResolveControl(target, memberName, arguments, out var resolved))
        {
            if (string.Equals(memberName, "Controls", StringComparison.OrdinalIgnoreCase))
            {
                value = resolved!.Controls;
                return true;
            }

            if (string.Equals(memberName, "Item", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length > 0)
            {
                value = GetIndexedControl(resolved!, arguments[0]);
                return true;
            }

            if (TryReadListProperty(resolved!, memberName, arguments, out value))
            {
                return true;
            }

            if (TryReadControlProperty(resolved!, memberName, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    public bool TrySetMember(
        object target,
        string memberName,
        object?[] arguments,
        object? value)
    {
        ThrowIfDisposed();
        if (target is ImageListProxy designerImageList && arguments.Length == 0 &&
            TrySetImageListDesignerProperty(designerImageList, memberName, value))
        {
            return true;
        }

        if (target is MenuProxy menu && arguments.Length == 0 &&
            TryWriteMenuProperty(menu, memberName, value))
        {
            return true;
        }

        if (target is ImageComboControl imageCombo &&
            string.Equals(memberName, "ImageList", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            imageCombo.ImageList = value;
            return true;
        }

        if (target is ImageListProxy imageList && arguments.Length == 0)
        {
            if (string.Equals(memberName, "ImageWidth", StringComparison.OrdinalIgnoreCase))
            {
                imageList.ImageWidth = VBConversions.CLng(value);
                return true;
            }

            if (string.Equals(memberName, "ImageHeight", StringComparison.OrdinalIgnoreCase))
            {
                imageList.ImageHeight = VBConversions.CLng(value);
                return true;
            }
        }

        if (target is CommonDialogProxy commonDialog && arguments.Length == 0)
        {
            if (string.Equals(memberName, "FileName", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.FileName = VBConversions.CStr(value);
                return true;
            }

            if (string.Equals(memberName, "Filter", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.Filter = VBConversions.CStr(value);
                return true;
            }

            if (string.Equals(memberName, "DialogTitle", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.DialogTitle = VBConversions.CStr(value);
                return true;
            }

            if (string.Equals(memberName, "FilterIndex", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.FilterIndex = VBConversions.CLng(value);
                return true;
            }

            if (string.Equals(memberName, "CancelError", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.CancelError = VBConversions.CBool(value);
                return true;
            }

            if (string.Equals(memberName, "DefaultExt", StringComparison.OrdinalIgnoreCase))
            {
                commonDialog.DefaultExt = VBConversions.CStr(value);
                return true;
            }
        }

        if (target is TreeView treeView && arguments.Length == 0)
        {
            if (string.Equals(memberName, "Style", StringComparison.OrdinalIgnoreCase))
            {
                GetTreeViewState(treeView).Style = VBConversions.CLng(value);
                return true;
            }

            if (string.Equals(memberName, "LineStyle", StringComparison.OrdinalIgnoreCase))
            {
                GetTreeViewState(treeView).LineStyle = VBConversions.CLng(value);
                return true;
            }
        }

        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
        }

        return TryWriteControlProperty(resolved, memberName, value) ||
               TryWriteListProperty(resolved, memberName, arguments, value);
    }

    public bool TryInvokeMember(
        object target,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        ThrowIfDisposed();
        result = null;
        if (target is MenuProxy menu && TryInvokeMenuMember(menu, memberName, arguments))
        {
            return true;
        }

        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
        }

        if (TryInvokeListMember(resolved, memberName, arguments))
        {
            return true;
        }

        if (resolved is RichTextBox richTextBox &&
            TryInvokeRichTextBoxMember(richTextBox, memberName, arguments, out result))
        {
            return true;
        }

        if (string.Equals(memberName, "PaintPicture", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 5 &&
            TryRenderPaintPicture(
                resolved,
                new VBPaintPicture(
                    arguments[0],
                    VBConversions.CSng(arguments[1]),
                    VBConversions.CSng(arguments[2]),
                    VBConversions.CSng(arguments[3]),
                    VBConversions.CSng(arguments[4]))))
        {
            return true;
        }

        if (string.Equals(memberName, "Show", StringComparison.OrdinalIgnoreCase))
        {
            if (resolved is Form form)
            {
                form.Show();
            }
            else
            {
                resolved.Show();
            }

            return true;
        }

        if (string.Equals(memberName, "Hide", StringComparison.OrdinalIgnoreCase))
        {
            resolved.Hide();
            return true;
        }

        if (string.Equals(memberName, "SetFocus", StringComparison.OrdinalIgnoreCase))
        {
            resolved.Focus();
            return true;
        }

        if (string.Equals(memberName, "Refresh", StringComparison.OrdinalIgnoreCase))
        {
            resolved.Refresh();
            return true;
        }

        return false;
    }

    public bool TrySubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        var eventSource = ResolveEventSource(source);
        var eventInfo = eventSource is null
            ? null
            : FindEvent(eventSource.GetType(), eventName);
        var method = FindHandler(target.GetType(), methodName);
        if (eventSource is null || eventInfo is null || method is null ||
            eventInfo.EventHandlerType is null)
        {
            return false;
        }

        var handler = CreateEventDelegate(
            eventInfo.EventHandlerType,
            target,
            method,
            eventName,
            eventSource);
        if (handler is null)
        {
            return false;
        }

        eventInfo.AddEventHandler(eventSource, handler);
        _events.Add(new EventBinding(
            source,
            eventName,
            target,
            methodName,
            eventSource,
            eventInfo,
            handler));
        return true;
    }

    public void UnsubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName)
    {
        foreach (var binding in _events
                     .Where(binding =>
                         ReferenceEquals(binding.Source, source) &&
                         ReferenceEquals(binding.Target, target) &&
                         string.Equals(binding.EventName, eventName, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(binding.MethodName, methodName, StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            RemoveEventBinding(binding);
        }
    }

    public IEnumerable<object?>? EnumerateControls(object? target)
    {
        if (target is Control control)
        {
            return control.Controls.Cast<object?>().ToArray();
        }

        if (target is not null && _bindings.TryGetValue(target, out var binding))
        {
            return binding.Form.Controls.Cast<object?>().ToArray();
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var binding in _events.ToArray())
        {
            RemoveEventBinding(binding);
        }

        _events.Clear();
        foreach (var popup in _popups.ToArray())
        {
            popup.Dispose();
        }

        _popups.Clear();
        foreach (var binding in _bindings.Values)
        {
            binding.Form.Dispose();
        }

        _bindings.Clear();
        _treeViewStates.Clear();
        _richTextBoxStates.Clear();
        foreach (var state in _designerControlStates.Values)
        {
            state.Dispose();
        }

        _designerControlStates.Clear();
        _toolTip.Dispose();
    }

    private FormBinding GetOrCreateBinding(object target)
    {
        if (_bindings.TryGetValue(target, out var binding))
        {
            return binding;
        }

        var form = new Form
        {
            Name = target.GetType().Name,
            Text = target.GetType().Name,
            StartPosition = FormStartPosition.Manual
        };
        binding = new FormBinding(form);
        _bindings.Add(target, binding);
        return binding;
    }

    private static MenuStrip GetOrCreateMenuStrip(FormBinding binding)
    {
        if (binding.MenuStrip is not null)
        {
            return binding.MenuStrip;
        }

        var menuStrip = new MenuStrip
        {
            Name = "__VB6MenuStrip",
            TabStop = false
        };
        binding.MenuStrip = menuStrip;
        binding.Form.MainMenuStrip = menuStrip;
        binding.Form.Controls.Add(menuStrip);
        return menuStrip;
    }

    private static ToolStripItem ClonePopupItem(ToolStripItem source)
    {
        if (source is ToolStripSeparator || string.Equals(source.Text, "-", StringComparison.Ordinal))
        {
            return new ToolStripSeparator();
        }

        if (source is not ToolStripMenuItem sourceMenu)
        {
            return new ToolStripMenuItem(source.Text)
            {
                Enabled = source.Enabled,
                Visible = source.Visible,
                Tag = source.Tag
            };
        }

        var clone = new ToolStripMenuItem(sourceMenu.Text)
        {
            Name = sourceMenu.Name,
            Enabled = sourceMenu.Enabled,
            Visible = sourceMenu.Visible,
            Checked = sourceMenu.Checked,
            CheckOnClick = sourceMenu.CheckOnClick,
            Tag = sourceMenu.Tag
        };
        clone.Click += (_, _) => sourceMenu.PerformClick();
        foreach (ToolStripItem child in sourceMenu.DropDownItems)
        {
            clone.DropDownItems.Add(ClonePopupItem(child));
        }

        return clone;
    }

    private Control? ResolveDrawingTarget(object? target)
    {
        if (target is Control control && !control.IsDisposed)
        {
            return control;
        }

        if (target is not null &&
            _bindings.TryGetValue(target, out var binding) &&
            !binding.Form.IsDisposed)
        {
            return binding.Form;
        }

        return null;
    }

    private Bitmap GetDrawingSurface(Control target)
    {
        var state = GetDesignerControlState(target);
        var width = Math.Max(1, target.ClientSize.Width);
        var height = Math.Max(1, target.ClientSize.Height);
        if (state.DrawingSurface is { } existing &&
            existing.Width == width &&
            existing.Height == height)
        {
            return existing;
        }

        var surface = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(surface))
        {
            graphics.Clear(Color.Transparent);
            var source = target is PictureBox pictureBox
                ? pictureBox.Image
                : target.BackgroundImage;
            if (source is not null)
            {
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }

            if (state.DrawingSurface is { } previous)
            {
                graphics.DrawImage(previous, 0, 0, previous.Width, previous.Height);
                previous.Dispose();
            }
        }

        state.DrawingSurface = surface;
        if (target is PictureBox picture)
        {
            picture.Image = surface;
            picture.SizeMode = PictureBoxSizeMode.Normal;
        }
        else
        {
            target.BackgroundImage = surface;
            target.BackgroundImageLayout = ImageLayout.None;
        }

        return surface;
    }

    private bool TryResolveControl(
        object target,
        string memberName,
        object?[] arguments,
        out Control? control)
    {
        if (target is Control direct)
        {
            control = direct;
            return true;
        }

        if (_bindings.TryGetValue(target, out var binding))
        {
            if (binding.Controls.TryGetValue(memberName, out var named))
            {
                control = named;
                return true;
            }

            control = binding.Form;
            return true;
        }

        control = null;
        return false;
    }

    private void AttachGeneratedControlEvents(object owner, Control control, string name)
    {
        var baseName = name.Split('(')[0];
        if (control is TimerControl)
        {
            TrySubscribeEvent(control, "Tick", owner, baseName + "_Timer");
            return;
        }

        TrySubscribeEvent(control, "Click", owner, baseName + "_Click");
        TrySubscribeEvent(control, "TextChanged", owner, baseName + "_Change");
        TrySubscribeEvent(control, "Enter", owner, baseName + "_GotFocus");
        TrySubscribeEvent(control, "Leave", owner, baseName + "_LostFocus");
        TrySubscribeEvent(control, "DoubleClick", owner, baseName + "_DblClick");
        TrySubscribeEvent(control, "MouseDown", owner, baseName + "_MouseDown");
        TrySubscribeEvent(control, "MouseUp", owner, baseName + "_MouseUp");
        TrySubscribeEvent(control, "MouseMove", owner, baseName + "_MouseMove");
        TrySubscribeEvent(control, "KeyDown", owner, baseName + "_KeyDown");
        TrySubscribeEvent(control, "KeyPress", owner, baseName + "_KeyPress");
        TrySubscribeEvent(control, "KeyUp", owner, baseName + "_KeyUp");
        TrySubscribeEvent(control, "Resize", owner, baseName + "_Resize");
    }

    private void AttachGeneratedMenuEvents(object owner, MenuProxy menu, string name)
    {
        var baseName = name.Split('(')[0];
        TrySubscribeEvent(menu, "Click", owner, baseName + "_Click");
    }

    private void AttachGeneratedFormEvents(object target)
    {
        TrySubscribeEvent(target, "Click", target, "Form_Click");
        TrySubscribeEvent(target, "Resize", target, "Form_Resize");
        TrySubscribeEvent(target, "MouseDown", target, "Form_MouseDown");
        TrySubscribeEvent(target, "MouseUp", target, "Form_MouseUp");
        TrySubscribeEvent(target, "MouseMove", target, "Form_MouseMove");
        TrySubscribeEvent(target, "KeyDown", target, "Form_KeyDown");
        TrySubscribeEvent(target, "KeyPress", target, "Form_KeyPress");
        TrySubscribeEvent(target, "KeyUp", target, "Form_KeyUp");
    }

    private void RemoveEventBinding(EventBinding binding)
    {
        binding.Event.RemoveEventHandler(binding.EventSource, binding.Handler);
        _events.Remove(binding);
    }

    private object? ResolveEventSource(object source)
    {
        if (source is Control or ToolStripItem)
        {
            return source;
        }

        return _bindings.TryGetValue(source, out var binding) ? binding.Form : null;
    }

    private static EventInfo? FindEvent(Type type, string name)
    {
        var normalized = name switch
        {
            "Change" => "TextChanged",
            "DblClick" => "DoubleClick",
            "GotFocus" => "Enter",
            "LostFocus" => "Leave",
            _ => name
        };
        return type.GetEvents(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(@event =>
                string.Equals(@event.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static MethodInfo? FindHandler(Type type, string methodName) =>
        type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
        type.GetMethod(
            "__vb6_" + Mangle(methodName),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Delegate? CreateEventDelegate(
        Type delegateType,
        object target,
        MethodInfo method,
        string eventName,
        object eventSource)
    {
        var invoke = delegateType.GetMethod("Invoke");
        if (invoke is null || invoke.ReturnType != typeof(void))
        {
            return null;
        }

        var parameters = invoke.GetParameters();
        if (parameters.Any(parameter => parameter.ParameterType.IsByRef))
        {
            return null;
        }

        var expressions = parameters
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        var callback = typeof(WinFormsHost).GetMethod(
            nameof(InvokeEventHandler),
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(InvokeEventHandler));
        var arguments = Expression.NewArrayInit(
            typeof(object),
            expressions.Select(expression => Expression.Convert(expression, typeof(object))));
        var body = Expression.Call(
            callback,
            Expression.Constant(target),
            Expression.Constant(method),
            Expression.Constant(eventName),
            Expression.Constant(eventSource),
            arguments);
        return Expression.Lambda(delegateType, body, expressions).Compile();
    }

    private static void InvokeEventHandler(
        object target,
        MethodInfo method,
        string eventName,
        object eventSource,
        object?[] eventArguments)
    {
        eventArguments = AdaptEventArguments(eventName, eventSource, eventArguments);
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var offset = eventArguments.Length == parameters.Length
            ? 0
            : Math.Max(0, eventArguments.Length - parameters.Length);
        for (var index = 0; index < parameters.Length; index++)
        {
            var sourceIndex = Math.Min(eventArguments.Length - 1, offset + index);
            var value = sourceIndex < 0 ? null : eventArguments[sourceIndex];
            arguments[index] = ConvertEventArgument(
                value,
                parameters[index].ParameterType.IsByRef
                    ? parameters[index].ParameterType.GetElementType()!
                    : parameters[index].ParameterType);
        }

        try
        {
            method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object?[] AdaptEventArguments(
        string eventName,
        object eventSource,
        object?[] eventArguments)
    {
        var normalized = eventName.ToUpperInvariant();
        if (eventArguments.Length == 2 && eventArguments[1] is MouseEventArgs mouse)
        {
            if (normalized is "MOUSEDOWN" or "MOUSEUP" or "MOUSEMOVE")
            {
                return new object?[]
                {
                    ToVbMouseButton(mouse.Button),
                    ToVbShift(Control.ModifierKeys),
                    ToTwips(eventSource, mouse.X),
                    ToTwips(eventSource, mouse.Y)
                };
            }
        }

        if (eventArguments.Length == 2 && eventArguments[1] is KeyEventArgs key)
        {
            if (normalized is "KEYDOWN" or "KEYUP")
            {
                return new object?[]
                {
                    key.KeyValue,
                    ToVbShift(key.Modifiers)
                };
            }
        }

        if (eventArguments.Length == 2 && eventArguments[1] is KeyPressEventArgs keyPress &&
            normalized == "KEYPRESS")
        {
            return new object?[] { (short)keyPress.KeyChar };
        }

        return eventArguments;
    }

    private static int ToVbMouseButton(MouseButtons button)
    {
        var result = 0;
        if ((button & MouseButtons.Left) != 0) result |= 1;
        if ((button & MouseButtons.Right) != 0) result |= 2;
        if ((button & MouseButtons.Middle) != 0) result |= 4;
        if ((button & MouseButtons.XButton1) != 0) result |= 8;
        if ((button & MouseButtons.XButton2) != 0) result |= 16;
        return result;
    }

    private static short ToVbShift(Keys modifiers)
    {
        var result = 0;
        if ((modifiers & Keys.Shift) != 0) result |= 1;
        if ((modifiers & Keys.Control) != 0) result |= 2;
        if ((modifiers & Keys.Alt) != 0) result |= 4;
        return (short)result;
    }

    private static float ToTwips(object source, int pixels)
    {
        var dpi = source is Control control && control.DeviceDpi > 0
            ? control.DeviceDpi
            : 96;
        return pixels * 1440f / dpi;
    }

    private static object? ConvertEventArgument(object? value, Type targetType)
    {
        if (value is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(string)) return VBConversions.CStr(value);
        if (targetType == typeof(byte)) return VBConversions.CByte(value);
        if (targetType == typeof(short)) return VBConversions.CInt(value);
        if (targetType == typeof(int)) return VBConversions.CLng(value);
        if (targetType == typeof(long)) return VBConversions.CLngLng(value);
        if (targetType == typeof(float)) return VBConversions.CSng(value);
        if (targetType == typeof(double)) return VBConversions.CDbl(value);
        if (targetType == typeof(bool)) return VBConversions.CBool(value);
        if (targetType == typeof(char)) return Convert.ToChar(value, System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }

    private static string Mangle(string name) =>
        new(name.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());

    private static object? GetIndexedControl(Control owner, object? index)
    {
        if (index is string name)
        {
            return owner.Controls.Cast<Control>().FirstOrDefault(control =>
                string.Equals(control.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        var numericIndex = index is null ? 0 : VBConversions.CLng(index);
        return numericIndex >= 0 && numericIndex < owner.Controls.Count
            ? owner.Controls[numericIndex]
            : null;
    }

    private static bool TryInvokeListMember(
        Control control,
        string memberName,
        object?[] arguments)
    {
        var items = GetListItems(control);
        if (items is null)
        {
            return false;
        }

        if (string.Equals(memberName, "AddItem", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new TargetParameterCountException("AddItem expects an item and an optional index.");
            }

            var text = VBConversions.CStr(arguments[0]);
            var index = arguments.Length == 2 ? VBConversions.CLng(arguments[1]) : items.Count;
            if (index < 0 || index > items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(arguments), "The VB6 list index is outside the collection.");
            }

            items.Insert(index, text);
            return true;
        }

        if (string.Equals(memberName, "RemoveItem", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length != 1)
            {
                throw new TargetParameterCountException("RemoveItem expects an index.");
            }

            items.RemoveAt(VBConversions.CLng(arguments[0]));
            return true;
        }

        if (string.Equals(memberName, "Clear", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            items.Clear();
            return true;
        }

        return false;
    }

    private bool TryReadListProperty(
        Control control,
        string memberName,
        object?[] arguments,
        out object? value)
    {
        var items = GetListItems(control);
        if (items is not null)
        {
            if (string.Equals(memberName, "ListCount", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = items.Count;
                return true;
            }

            if (string.Equals(memberName, "ListIndex", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                value = control switch
                {
                    ListBox list => list.SelectedIndex,
                    ComboBox combo => combo.SelectedIndex,
                    _ => -1
                };
                return true;
            }

            if (string.Equals(memberName, "List", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 1)
            {
                value = items[VBConversions.CLng(arguments[0])]?.ToString() ?? string.Empty;
                return true;
            }
        }

        if (control is TextBoxBase textBox)
        {
            if (string.Equals(memberName, "SelStart", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                value = textBox.SelectionStart;
                return true;
            }

            if (string.Equals(memberName, "SelLength", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                value = textBox.SelectionLength;
                return true;
            }

            if (string.Equals(memberName, "SelText", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                value = textBox is RichTextBox
                    ? NormalizeVbLineEndings(textBox.SelectedText)
                    : textBox.SelectedText;
                return true;
            }
        }

        if (control is RichTextBox richTextBox &&
            TryReadRichTextBoxProperty(richTextBox, memberName, arguments, out value))
        {
            return true;
        }

        if (control is CheckBox checkBox &&
            string.Equals(memberName, "Value", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            value = checkBox.Checked;
            return true;
        }

        if (control is RadioButton radioButton &&
            string.Equals(memberName, "Value", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            value = radioButton.Checked;
            return true;
        }

        value = null;
        return false;
    }

    private bool TryWriteListProperty(
        Control control,
        string memberName,
        object?[] arguments,
        object? value)
    {
        var items = GetListItems(control);
        if (items is not null)
        {
            if (string.Equals(memberName, "ListIndex", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 0)
            {
                var index = VBConversions.CLng(value);
                switch (control)
                {
                    case ListBox list:
                        list.SelectedIndex = index;
                        break;
                    case ComboBox combo:
                        combo.SelectedIndex = index;
                        break;
                }

                return true;
            }

            if (string.Equals(memberName, "List", StringComparison.OrdinalIgnoreCase) &&
                arguments.Length == 1)
            {
                items[VBConversions.CLng(arguments[0])] = VBConversions.CStr(value);
                return true;
            }
        }

        if (control is TextBoxBase textBox)
        {
            if (string.Equals(memberName, "SelStart", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                textBox.SelectionStart = VBConversions.CLng(value);
                return true;
            }

            if (string.Equals(memberName, "SelLength", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                textBox.SelectionLength = VBConversions.CLng(value);
                return true;
            }

            if (string.Equals(memberName, "SelText", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0)
            {
                textBox.SelectedText = VBConversions.CStr(value);
                return true;
            }
        }

        if (control is RichTextBox richTextBox &&
            TryWriteRichTextBoxProperty(richTextBox, memberName, arguments, value))
        {
            return true;
        }

        if (control is CheckBox checkBox &&
            string.Equals(memberName, "Value", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            checkBox.Checked = VBConversions.CBool(value);
            return true;
        }

        if (control is RadioButton radioButton &&
            string.Equals(memberName, "Value", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 0)
        {
            radioButton.Checked = VBConversions.CBool(value);
            return true;
        }

        return false;
    }

    private bool TryReadRichTextBoxProperty(
        RichTextBox richTextBox,
        string memberName,
        object?[] arguments,
        out object? value)
    {
        if (arguments.Length != 0)
        {
            value = null;
            return false;
        }

        if (string.Equals(memberName, "TextRTF", StringComparison.OrdinalIgnoreCase))
        {
            value = richTextBox.Rtf;
            return true;
        }

        if (string.Equals(memberName, "SelColor", StringComparison.OrdinalIgnoreCase))
        {
            value = ColorTranslator.ToOle(richTextBox.SelectionColor);
            return true;
        }

        if (string.Equals(memberName, "SelBold", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(memberName, "SelItalic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(memberName, "SelUnderline", StringComparison.OrdinalIgnoreCase))
        {
            var style = richTextBox.SelectionFont?.Style ?? richTextBox.Font.Style;
            value = string.Equals(memberName, "SelBold", StringComparison.OrdinalIgnoreCase)
                ? style.HasFlag(FontStyle.Bold)
                : string.Equals(memberName, "SelItalic", StringComparison.OrdinalIgnoreCase)
                    ? style.HasFlag(FontStyle.Italic)
                    : style.HasFlag(FontStyle.Underline);
            return true;
        }

        if (string.Equals(memberName, "FileName", StringComparison.OrdinalIgnoreCase))
        {
            value = GetRichTextBoxState(richTextBox).FileName;
            return true;
        }

        if (string.Equals(memberName, "Modified", StringComparison.OrdinalIgnoreCase))
        {
            value = richTextBox.Modified;
            return true;
        }

        if (string.Equals(memberName, "RightMargin", StringComparison.OrdinalIgnoreCase))
        {
            value = richTextBox.RightMargin;
            return true;
        }

        if (string.Equals(memberName, "HideSelection", StringComparison.OrdinalIgnoreCase))
        {
            value = richTextBox.HideSelection;
            return true;
        }

        value = null;
        return false;
    }

    private bool TryWriteRichTextBoxProperty(
        RichTextBox richTextBox,
        string memberName,
        object?[] arguments,
        object? value)
    {
        if (arguments.Length != 0)
        {
            return false;
        }

        if (string.Equals(memberName, "TextRTF", StringComparison.OrdinalIgnoreCase))
        {
            var rtf = VBConversions.CStr(value);
            if (rtf.Length == 0)
            {
                richTextBox.Clear();
            }
            else
            {
                richTextBox.Rtf = rtf;
            }

            return true;
        }

        if (string.Equals(memberName, "SelColor", StringComparison.OrdinalIgnoreCase))
        {
            richTextBox.SelectionColor = ColorTranslator.FromOle(VBConversions.CLng(value));
            return true;
        }

        if (string.Equals(memberName, "SelBold", StringComparison.OrdinalIgnoreCase))
        {
            SetSelectionStyle(richTextBox, FontStyle.Bold, VBConversions.CBool(value));
            return true;
        }

        if (string.Equals(memberName, "SelItalic", StringComparison.OrdinalIgnoreCase))
        {
            SetSelectionStyle(richTextBox, FontStyle.Italic, VBConversions.CBool(value));
            return true;
        }

        if (string.Equals(memberName, "SelUnderline", StringComparison.OrdinalIgnoreCase))
        {
            SetSelectionStyle(richTextBox, FontStyle.Underline, VBConversions.CBool(value));
            return true;
        }

        if (string.Equals(memberName, "FileName", StringComparison.OrdinalIgnoreCase))
        {
            GetRichTextBoxState(richTextBox).FileName = VBConversions.CStr(value);
            return true;
        }

        if (string.Equals(memberName, "Modified", StringComparison.OrdinalIgnoreCase))
        {
            richTextBox.Modified = VBConversions.CBool(value);
            return true;
        }

        if (string.Equals(memberName, "RightMargin", StringComparison.OrdinalIgnoreCase))
        {
            richTextBox.RightMargin = VBConversions.CLng(value);
            return true;
        }

        if (string.Equals(memberName, "HideSelection", StringComparison.OrdinalIgnoreCase))
        {
            richTextBox.HideSelection = VBConversions.CBool(value);
            return true;
        }

        return false;
    }

    private static void SetSelectionStyle(RichTextBox richTextBox, FontStyle style, bool enabled)
    {
        var current = richTextBox.SelectionFont ?? richTextBox.Font;
        var nextStyle = enabled ? current.Style | style : current.Style & ~style;
        using var nextFont = new Font(current.FontFamily, current.Size, nextStyle);
        richTextBox.SelectionFont = nextFont;
    }

    private bool TryInvokeRichTextBoxMember(
        RichTextBox richTextBox,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        result = null;
        if (string.Equals(memberName, "LoadFile", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new TargetParameterCountException("LoadFile expects a file name and an optional file type.");
            }

            var path = VBConversions.CStr(arguments[0]);
            var streamType = GetRichTextBoxStreamType(arguments, defaultType: 0);
            richTextBox.LoadFile(path, streamType);
            if (streamType == RichTextBoxStreamType.PlainText)
            {
                richTextBox.Text = NormalizeVbLineEndings(richTextBox.Text);
            }

            GetRichTextBoxState(richTextBox).FileName = path;
            richTextBox.Modified = false;
            return true;
        }

        if (string.Equals(memberName, "SaveFile", StringComparison.OrdinalIgnoreCase))
        {
            if (arguments.Length is < 1 or > 2)
            {
                throw new TargetParameterCountException("SaveFile expects a file name and an optional file type.");
            }

            var path = VBConversions.CStr(arguments[0]);
            richTextBox.SaveFile(path, GetRichTextBoxStreamType(arguments, defaultType: 0));
            GetRichTextBoxState(richTextBox).FileName = path;
            richTextBox.Modified = false;
            return true;
        }

        if (string.Equals(memberName, "GetLineFromChar", StringComparison.OrdinalIgnoreCase) &&
            arguments.Length == 1)
        {
            result = richTextBox.GetLineFromCharIndex(VBConversions.CLng(arguments[0]));
            return true;
        }

        return false;
    }

    private static RichTextBoxStreamType GetRichTextBoxStreamType(
        object?[] arguments,
        int defaultType)
    {
        var fileType = arguments.Length == 2 ? VBConversions.CLng(arguments[1]) : defaultType;
        return fileType == 1
            ? RichTextBoxStreamType.PlainText
            : RichTextBoxStreamType.RichText;
    }

    private static string NormalizeVbLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "\r\n", StringComparison.Ordinal);

    private static IList? GetListItems(Control control) => control switch
    {
        ListBox list => list.Items,
        ComboBox combo => combo.Items,
        _ => null
    };

    private bool TryReadControlProperty(
        Control control,
        string memberName,
        out object? value)
    {
        if (control is Form form && TryReadFormProperty(form, memberName, out value))
        {
            return true;
        }

        if (TryReadDesignerControlProperty(control, memberName, out value))
        {
            return true;
        }

        var twipsPerPixelX = 1440f / control.DeviceDpi;
        var twipsPerPixelY = 1440f / control.DeviceDpi;
        if (string.Equals(memberName, "Left", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Left, twipsPerPixelX);
        else if (string.Equals(memberName, "Top", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Top, twipsPerPixelY);
        else if (string.Equals(memberName, "Width", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Width, twipsPerPixelX);
        else if (string.Equals(memberName, "Height", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Height, twipsPerPixelY);
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase)) value = control.Visible;
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase)) value = control is TimerControl timer ? timer.TimerEnabled : control.Enabled;
        else if (control is TimerControl timer && string.Equals(memberName, "Interval", StringComparison.OrdinalIgnoreCase)) value = timer.Interval;
        else if (string.Equals(memberName, "Name", StringComparison.OrdinalIgnoreCase)) value = control.Name;
        else if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase))
        {
            value = control is RichTextBox
                ? NormalizeVbLineEndings(control.Text)
                : control.Text;
        }
        else if (string.Equals(memberName, "BackColor", StringComparison.OrdinalIgnoreCase)) value = ColorTranslator.ToOle(control.BackColor);
        else if (string.Equals(memberName, "ForeColor", StringComparison.OrdinalIgnoreCase)) value = ColorTranslator.ToOle(control.ForeColor);
        else if (string.Equals(memberName, "ScaleWidth", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Width, twipsPerPixelX);
        else if (string.Equals(memberName, "ScaleHeight", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Height, twipsPerPixelY);
        else if (string.Equals(memberName, "hWnd", StringComparison.OrdinalIgnoreCase)) value = control.Handle.ToInt64();
        else if (string.Equals(memberName, "hDC", StringComparison.OrdinalIgnoreCase)) value = 0L;
        else if (string.Equals(memberName, "hInstance", StringComparison.OrdinalIgnoreCase)) value = 0L;
        else if (control is ImageComboControl imageCombo && string.Equals(memberName, "ImageList", StringComparison.OrdinalIgnoreCase)) value = imageCombo.ImageList;
        else if (control is TreeView treeStyle && string.Equals(memberName, "Style", StringComparison.OrdinalIgnoreCase)) value = GetTreeViewState(treeStyle).Style;
        else if (control is TreeView treeLineStyle && string.Equals(memberName, "LineStyle", StringComparison.OrdinalIgnoreCase)) value = GetTreeViewState(treeLineStyle).LineStyle;
        else if (string.Equals(memberName, "Font", StringComparison.OrdinalIgnoreCase)) value = ToVBFont(control.Font);
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private bool TryWriteControlProperty(Control control, string memberName, object? value)
    {
        if (control is Form form && TryWriteFormProperty(form, memberName, value))
        {
            return true;
        }

        if (TryWriteDesignerControlProperty(control, memberName, value))
        {
            return true;
        }

        var twipsPerPixelX = 1440f / control.DeviceDpi;
        var twipsPerPixelY = 1440f / control.DeviceDpi;
        if (string.Equals(memberName, "Left", StringComparison.OrdinalIgnoreCase)) control.Left = FromTwips(value, twipsPerPixelX);
        else if (string.Equals(memberName, "Top", StringComparison.OrdinalIgnoreCase)) control.Top = FromTwips(value, twipsPerPixelY);
        else if (string.Equals(memberName, "Width", StringComparison.OrdinalIgnoreCase)) control.Width = FromTwips(value, twipsPerPixelX);
        else if (string.Equals(memberName, "Height", StringComparison.OrdinalIgnoreCase)) control.Height = FromTwips(value, twipsPerPixelY);
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase)) control.Visible = VBConversions.CBool(value);
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            if (control is TimerControl timer)
            {
                timer.TimerEnabled = VBConversions.CBool(value);
            }
            else
            {
                control.Enabled = VBConversions.CBool(value);
            }
        }
        else if (control is TimerControl timer && string.Equals(memberName, "Interval", StringComparison.OrdinalIgnoreCase)) timer.Interval = VBConversions.CLng(value);
        else if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase)) control.Text = VBConversions.CStr(value);
        else if (string.Equals(memberName, "BackColor", StringComparison.OrdinalIgnoreCase)) control.BackColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        else if (string.Equals(memberName, "ForeColor", StringComparison.OrdinalIgnoreCase)) control.ForeColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        else if (control is ImageComboControl imageCombo && string.Equals(memberName, "ImageList", StringComparison.OrdinalIgnoreCase)) imageCombo.ImageList = value;
        else if (control is TreeView treeStyle && string.Equals(memberName, "Style", StringComparison.OrdinalIgnoreCase)) GetTreeViewState(treeStyle).Style = VBConversions.CLng(value);
        else if (control is TreeView treeLineStyle && string.Equals(memberName, "LineStyle", StringComparison.OrdinalIgnoreCase)) GetTreeViewState(treeLineStyle).LineStyle = VBConversions.CLng(value);
        else if (string.Equals(memberName, "Font", StringComparison.OrdinalIgnoreCase) && value is VBFont font) control.Font = FromVBFont(font, control.Font);
        else return false;

        return true;
    }

    private static bool TryReadMenuProperty(
        MenuProxy menu,
        string memberName,
        out object? value)
    {
        if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Text;
        }
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Visible;
        }
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Enabled;
        }
        else if (string.Equals(memberName, "Checked", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Checked;
        }
        else if (string.Equals(memberName, "Index", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.VbIndex;
        }
        else if (string.Equals(memberName, "Name", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Name;
        }
        else if (string.Equals(memberName, "Tag", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Tag;
        }
        else if (string.Equals(memberName, "Shortcut", StringComparison.OrdinalIgnoreCase))
        {
            value = menu.Shortcut;
        }
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private static bool TryWriteMenuProperty(
        MenuProxy menu,
        string memberName,
        object? value)
    {
        if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase))
        {
            menu.Text = VBConversions.CStr(value);
        }
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase))
        {
            menu.Visible = VBConversions.CBool(value);
        }
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            menu.Enabled = VBConversions.CBool(value);
        }
        else if (string.Equals(memberName, "Checked", StringComparison.OrdinalIgnoreCase))
        {
            menu.Checked = VBConversions.CBool(value);
        }
        else if (string.Equals(memberName, "Index", StringComparison.OrdinalIgnoreCase))
        {
            menu.VbIndex = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "Tag", StringComparison.OrdinalIgnoreCase))
        {
            menu.Tag = value;
        }
        else if (string.Equals(memberName, "Shortcut", StringComparison.OrdinalIgnoreCase))
        {
            menu.Shortcut = VBConversions.CLng(value);
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool TryInvokeMenuMember(
        MenuProxy menu,
        string memberName,
        object?[] arguments)
    {
        if (arguments.Length != 0)
        {
            return false;
        }

        if (string.Equals(memberName, "Show", StringComparison.OrdinalIgnoreCase))
        {
            menu.Visible = true;
            return true;
        }

        if (string.Equals(memberName, "Hide", StringComparison.OrdinalIgnoreCase))
        {
            menu.Visible = false;
            return true;
        }

        if (string.Equals(memberName, "PerformClick", StringComparison.OrdinalIgnoreCase))
        {
            menu.PerformClick();
            return true;
        }

        return false;
    }

    private bool TryReadFormProperty(Form form, string memberName, out object? value)
    {
        if (string.Equals(memberName, "Icon", StringComparison.OrdinalIgnoreCase))
        {
            value = form.Icon;
            return true;
        }

        if (string.Equals(memberName, "Picture", StringComparison.OrdinalIgnoreCase))
        {
            value = form.BackgroundImage;
            return true;
        }

        if (string.Equals(memberName, "BorderStyle", StringComparison.OrdinalIgnoreCase))
        {
            value = ToVbFormBorderStyle(form.FormBorderStyle);
            return true;
        }

        if (string.Equals(memberName, "ControlBox", StringComparison.OrdinalIgnoreCase))
        {
            value = form.ControlBox;
            return true;
        }

        if (string.Equals(memberName, "MaxButton", StringComparison.OrdinalIgnoreCase))
        {
            value = form.MaximizeBox;
            return true;
        }

        if (string.Equals(memberName, "MinButton", StringComparison.OrdinalIgnoreCase))
        {
            value = form.MinimizeBox;
            return true;
        }

        if (string.Equals(memberName, "ShowInTaskbar", StringComparison.OrdinalIgnoreCase))
        {
            value = form.ShowInTaskbar;
            return true;
        }

        if (string.Equals(memberName, "StartUpPosition", StringComparison.OrdinalIgnoreCase))
        {
            value = ToVbStartUpPosition(form.StartPosition);
            return true;
        }

        if (string.Equals(memberName, "WindowState", StringComparison.OrdinalIgnoreCase))
        {
            value = form.WindowState switch
            {
                FormWindowState.Minimized => 1,
                FormWindowState.Maximized => 2,
                _ => 0
            };
            return true;
        }

        value = null;
        return false;
    }

    private bool TryWriteFormProperty(Form form, string memberName, object? value)
    {
        if (string.Equals(memberName, "Icon", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCreateIcon(value, out var icon))
            {
                form.Icon = icon;
            }

            return true;
        }

        if (string.Equals(memberName, "Picture", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCreateImage(value, out var image))
            {
                form.BackgroundImage = image;
            }

            return true;
        }

        if (string.Equals(memberName, "BorderStyle", StringComparison.OrdinalIgnoreCase))
        {
            form.FormBorderStyle = FromVbFormBorderStyle(VBConversions.CLng(value));
            return true;
        }

        if (string.Equals(memberName, "ControlBox", StringComparison.OrdinalIgnoreCase))
        {
            form.ControlBox = VBConversions.CBool(value);
            return true;
        }

        if (string.Equals(memberName, "MaxButton", StringComparison.OrdinalIgnoreCase))
        {
            form.MaximizeBox = VBConversions.CBool(value);
            return true;
        }

        if (string.Equals(memberName, "MinButton", StringComparison.OrdinalIgnoreCase))
        {
            form.MinimizeBox = VBConversions.CBool(value);
            return true;
        }

        if (string.Equals(memberName, "ShowInTaskbar", StringComparison.OrdinalIgnoreCase))
        {
            form.ShowInTaskbar = VBConversions.CBool(value);
            return true;
        }

        if (string.Equals(memberName, "StartUpPosition", StringComparison.OrdinalIgnoreCase))
        {
            form.StartPosition = FromVbStartUpPosition(VBConversions.CLng(value));
            return true;
        }

        if (string.Equals(memberName, "WindowState", StringComparison.OrdinalIgnoreCase))
        {
            form.WindowState = VBConversions.CLng(value) switch
            {
                1 => FormWindowState.Minimized,
                2 => FormWindowState.Maximized,
                _ => FormWindowState.Normal
            };
            return true;
        }

        return false;
    }

    private bool TryReadDesignerControlProperty(
        Control control,
        string memberName,
        out object? value)
    {
        if (control is ShapeControl shape && TryReadShapeProperty(shape, memberName, out value))
        {
            return true;
        }

        if (control is LineControl line && TryReadLineProperty(line, memberName, out value))
        {
            return true;
        }

        if (string.Equals(memberName, "BorderStyle", StringComparison.OrdinalIgnoreCase))
        {
            value = control switch
            {
                TextBoxBase textBox => (int)textBox.BorderStyle,
                PictureBox pictureBox => (int)pictureBox.BorderStyle,
                Panel panel => (int)panel.BorderStyle,
                _ => GetDesignerControlState(control).BorderStyle
            };
            return true;
        }

        if (string.Equals(memberName, "Appearance", StringComparison.OrdinalIgnoreCase))
        {
            value = control is ButtonBase button
                ? button.FlatStyle == FlatStyle.Flat ? 0 : 1
                : GetDesignerControlState(control).Appearance;
            return true;
        }

        if (string.Equals(memberName, "Tag", StringComparison.OrdinalIgnoreCase))
        {
            value = control.Tag;
            return true;
        }

        if (string.Equals(memberName, "ToolTipText", StringComparison.OrdinalIgnoreCase))
        {
            value = _toolTip.GetToolTip(control);
            return true;
        }

        if (string.Equals(memberName, "Picture", StringComparison.OrdinalIgnoreCase))
        {
            value = control is PictureBox pictureBox
                ? pictureBox.Image
                : control.BackgroundImage;
            return true;
        }

        var state = GetDesignerControlState(control);
        if (string.Equals(memberName, "AutoRedraw", StringComparison.OrdinalIgnoreCase)) value = state.AutoRedraw;
        else if (string.Equals(memberName, "FillStyle", StringComparison.OrdinalIgnoreCase)) value = state.FillStyle;
        else if (string.Equals(memberName, "MousePointer", StringComparison.OrdinalIgnoreCase)) value = state.MousePointer;
        else if (string.Equals(memberName, "ScaleMode", StringComparison.OrdinalIgnoreCase)) value = state.ScaleMode;
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private bool TryWriteDesignerControlProperty(
        Control control,
        string memberName,
        object? value)
    {
        if (control is ShapeControl shape && TryWriteShapeProperty(shape, memberName, value))
        {
            return true;
        }

        if (control is LineControl line && TryWriteLineProperty(line, memberName, value))
        {
            return true;
        }

        if (string.Equals(memberName, "BorderStyle", StringComparison.OrdinalIgnoreCase))
        {
            var borderStyle = (BorderStyle)Math.Clamp(VBConversions.CLng(value), 0, 2);
            switch (control)
            {
                case TextBoxBase textBox:
                    textBox.BorderStyle = borderStyle;
                    break;
                case PictureBox pictureBox:
                    pictureBox.BorderStyle = borderStyle;
                    break;
                case Panel panel:
                    panel.BorderStyle = borderStyle;
                    break;
                default:
                    GetDesignerControlState(control).BorderStyle = (int)borderStyle;
                    break;
            }

            return true;
        }

        if (string.Equals(memberName, "Appearance", StringComparison.OrdinalIgnoreCase))
        {
            var appearance = VBConversions.CLng(value);
            if (control is ButtonBase button)
            {
                button.FlatStyle = appearance == 0 ? FlatStyle.Flat : FlatStyle.Standard;
            }
            else
            {
                GetDesignerControlState(control).Appearance = appearance;
            }

            return true;
        }

        if (string.Equals(memberName, "Tag", StringComparison.OrdinalIgnoreCase))
        {
            control.Tag = value;
            return true;
        }

        if (string.Equals(memberName, "ToolTipText", StringComparison.OrdinalIgnoreCase))
        {
            _toolTip.SetToolTip(control, VBConversions.CStr(value));
            return true;
        }

        if (string.Equals(memberName, "Picture", StringComparison.OrdinalIgnoreCase))
        {
            if (TryCreateImage(value, out var image))
            {
                if (control is PictureBox pictureBox)
                {
                    pictureBox.Image = image;
                }
                else
                {
                    control.BackgroundImage = image;
                }
            }

            return true;
        }

        var state = GetDesignerControlState(control);
        if (string.Equals(memberName, "AutoRedraw", StringComparison.OrdinalIgnoreCase)) state.AutoRedraw = VBConversions.CBool(value);
        else if (string.Equals(memberName, "FillStyle", StringComparison.OrdinalIgnoreCase)) state.FillStyle = VBConversions.CLng(value);
        else if (string.Equals(memberName, "MousePointer", StringComparison.OrdinalIgnoreCase)) state.MousePointer = VBConversions.CLng(value);
        else if (string.Equals(memberName, "ScaleMode", StringComparison.OrdinalIgnoreCase)) state.ScaleMode = VBConversions.CLng(value);
        else return false;

        return true;
    }

    private static bool TryReadShapeProperty(
        ShapeControl shape,
        string memberName,
        out object? value)
    {
        if (string.Equals(memberName, "BorderColor", StringComparison.OrdinalIgnoreCase))
        {
            value = ColorTranslator.ToOle(shape.BorderColor);
        }
        else if (string.Equals(memberName, "BorderWidth", StringComparison.OrdinalIgnoreCase))
        {
            value = shape.BorderWidth;
        }
        else if (string.Equals(memberName, "BackStyle", StringComparison.OrdinalIgnoreCase))
        {
            value = shape.BackStyle;
        }
        else if (string.Equals(memberName, "FillColor", StringComparison.OrdinalIgnoreCase))
        {
            value = ColorTranslator.ToOle(shape.FillColor.IsEmpty ? shape.BackColor : shape.FillColor);
        }
        else if (string.Equals(memberName, "FillStyle", StringComparison.OrdinalIgnoreCase))
        {
            value = shape.FillStyle;
        }
        else if (string.Equals(memberName, "Shape", StringComparison.OrdinalIgnoreCase))
        {
            value = shape.Shape;
        }
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private static bool TryWriteShapeProperty(
        ShapeControl shape,
        string memberName,
        object? value)
    {
        if (string.Equals(memberName, "BorderColor", StringComparison.OrdinalIgnoreCase))
        {
            shape.BorderColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        }
        else if (string.Equals(memberName, "BorderWidth", StringComparison.OrdinalIgnoreCase))
        {
            shape.BorderWidth = Math.Max(0, VBConversions.CLng(value));
        }
        else if (string.Equals(memberName, "BackStyle", StringComparison.OrdinalIgnoreCase))
        {
            shape.BackStyle = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "FillColor", StringComparison.OrdinalIgnoreCase))
        {
            shape.FillColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        }
        else if (string.Equals(memberName, "FillStyle", StringComparison.OrdinalIgnoreCase))
        {
            shape.FillStyle = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "Shape", StringComparison.OrdinalIgnoreCase))
        {
            shape.Shape = VBConversions.CLng(value);
        }
        else
        {
            return false;
        }

        shape.Invalidate();
        return true;
    }

    private static bool TryReadLineProperty(
        LineControl line,
        string memberName,
        out object? value)
    {
        if (string.Equals(memberName, "BorderColor", StringComparison.OrdinalIgnoreCase))
        {
            value = ColorTranslator.ToOle(line.BorderColor);
        }
        else if (string.Equals(memberName, "BorderWidth", StringComparison.OrdinalIgnoreCase))
        {
            value = line.BorderWidth;
        }
        else if (string.Equals(memberName, "X1", StringComparison.OrdinalIgnoreCase))
        {
            value = line.X1;
        }
        else if (string.Equals(memberName, "Y1", StringComparison.OrdinalIgnoreCase))
        {
            value = line.Y1;
        }
        else if (string.Equals(memberName, "X2", StringComparison.OrdinalIgnoreCase))
        {
            value = line.X2;
        }
        else if (string.Equals(memberName, "Y2", StringComparison.OrdinalIgnoreCase))
        {
            value = line.Y2;
        }
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private static bool TryWriteLineProperty(
        LineControl line,
        string memberName,
        object? value)
    {
        if (string.Equals(memberName, "BorderColor", StringComparison.OrdinalIgnoreCase))
        {
            line.BorderColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        }
        else if (string.Equals(memberName, "BorderWidth", StringComparison.OrdinalIgnoreCase))
        {
            line.BorderWidth = Math.Max(0, VBConversions.CLng(value));
        }
        else if (string.Equals(memberName, "X1", StringComparison.OrdinalIgnoreCase))
        {
            line.X1 = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "Y1", StringComparison.OrdinalIgnoreCase))
        {
            line.Y1 = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "X2", StringComparison.OrdinalIgnoreCase))
        {
            line.X2 = VBConversions.CLng(value);
        }
        else if (string.Equals(memberName, "Y2", StringComparison.OrdinalIgnoreCase))
        {
            line.Y2 = VBConversions.CLng(value);
        }
        else
        {
            return false;
        }

        line.Invalidate();
        return true;
    }

    private DesignerControlState GetDesignerControlState(Control control)
    {
        if (_designerControlStates.TryGetValue(control, out var state))
        {
            return state;
        }

        state = new DesignerControlState();
        _designerControlStates.Add(control, state);
        return state;
    }

    private static bool TryCreateImage(object? value, out Image? image)
    {
        image = null;
        if (!TryDecodeFrxResource(value, out var resource))
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(resource, writable: false);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            image = new Bitmap(source);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (ExternalException)
        {
            return false;
        }
    }

    private static bool TryGetPaintPictureImage(
        object? value,
        out Image? image,
        out bool ownsImage)
    {
        ownsImage = false;
        if (value is Image existing)
        {
            image = existing;
            return true;
        }

        if (TryCreateImage(value, out image))
        {
            ownsImage = true;
            return true;
        }

        if (value is VBPicture picture && !string.IsNullOrWhiteSpace(picture.FileName))
        {
            try
            {
                using var loaded = Image.FromFile(picture.FileName);
                image = new Bitmap(loaded);
                ownsImage = true;
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (ExternalException)
            {
            }
            catch (IOException)
            {
            }
            catch (OutOfMemoryException)
            {
            }
        }

        image = null;
        return false;
    }

    private bool TrySetImageListDesignerProperty(
        ImageListProxy imageList,
        string memberName,
        object? value)
    {
        const string prefix = "ListImage";
        var separator = memberName.IndexOf('.');
        if (separator <= prefix.Length ||
            !memberName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(memberName.AsSpan(prefix.Length, separator - prefix.Length), out var index) ||
            index <= 0)
        {
            return false;
        }

        var propertyName = memberName[(separator + 1)..];
        if (!propertyName.Equals("Picture", StringComparison.OrdinalIgnoreCase) &&
            !propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        while (imageList.ListImages.Count < index)
        {
            imageList.ListImages.Add();
        }

        var entry = imageList.ListImages.Item(index)
            ?? throw new InvalidOperationException($"ImageList entry {index} could not be created.");
        if (propertyName.Equals("Key", StringComparison.OrdinalIgnoreCase))
        {
            entry.Key = VBConversions.CStr(value);
        }
        else if (TryCreateImage(value, out var image))
        {
            entry.Picture = image;
        }
        else
        {
            entry.Picture = value;
        }

        return true;
    }

    private static bool TryCreateIcon(object? value, out Icon? icon)
    {
        icon = null;
        if (!TryDecodeFrxResource(value, out var resource))
        {
            return false;
        }

        try
        {
            using var stream = new MemoryStream(resource, writable: false);
            using var source = new Icon(stream);
            icon = (Icon)source.Clone();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryDecodeFrxResource(object? value, out byte[] resource)
    {
        resource = Array.Empty<byte>();
        if (value is not string encoded ||
            !encoded.StartsWith(FrxResourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            resource = UnwrapFrxPicture(Convert.FromBase64String(encoded[FrxResourcePrefix.Length..]));
            return resource.Length != 0;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static byte[] UnwrapFrxPicture(byte[] resource)
    {
        var header = new byte[] { 0x6C, 0x74, 0x00, 0x00 };
        var headerOffset = resource.AsSpan().IndexOf(header);
        if (headerOffset < 0)
        {
            return resource;
        }

        var lengthOffset = headerOffset + header.Length;
        if (lengthOffset + sizeof(int) > resource.Length)
        {
            throw new InvalidDataException("The .frx picture header is truncated.");
        }

        var length = BitConverter.ToInt32(resource, lengthOffset);
        var payloadOffset = lengthOffset + sizeof(int);
        if (length < 0 || length > resource.Length - payloadOffset)
        {
            throw new InvalidDataException("The .frx picture payload exceeds its resource.");
        }

        return resource.AsSpan(payloadOffset, length).ToArray();
    }

    private static int ToVbFormBorderStyle(FormBorderStyle style) => style switch
    {
        FormBorderStyle.None => 0,
        FormBorderStyle.FixedSingle => 1,
        FormBorderStyle.Fixed3D => 2,
        FormBorderStyle.FixedDialog => 3,
        FormBorderStyle.FixedToolWindow => 4,
        FormBorderStyle.SizableToolWindow => 5,
        _ => 2
    };

    private static FormBorderStyle FromVbFormBorderStyle(int style) => style switch
    {
        0 => FormBorderStyle.None,
        1 => FormBorderStyle.FixedSingle,
        3 => FormBorderStyle.FixedDialog,
        4 => FormBorderStyle.FixedToolWindow,
        5 => FormBorderStyle.SizableToolWindow,
        _ => FormBorderStyle.Sizable
    };

    private static int ToVbStartUpPosition(FormStartPosition position) => position switch
    {
        FormStartPosition.CenterParent => 1,
        FormStartPosition.CenterScreen => 2,
        FormStartPosition.WindowsDefaultLocation => 3,
        _ => 0
    };

    private static FormStartPosition FromVbStartUpPosition(int position) => position switch
    {
        1 => FormStartPosition.CenterParent,
        2 => FormStartPosition.CenterScreen,
        3 => FormStartPosition.WindowsDefaultLocation,
        _ => FormStartPosition.Manual
    };

    private static VBFont ToVBFont(Font font) => new()
    {
        Name = font.Name,
        Size = font.Size,
        Bold = font.Bold,
        Italic = font.Italic,
        Underline = font.Underline,
        Strikethrough = font.Strikeout,
        Weight = font.Bold ? 700 : 400
    };

    private static Font FromVBFont(VBFont value, Font fallback)
    {
        var style = FontStyle.Regular;
        if (value.Bold) style |= FontStyle.Bold;
        if (value.Italic) style |= FontStyle.Italic;
        if (value.Underline) style |= FontStyle.Underline;
        if (value.Strikethrough) style |= FontStyle.Strikeout;
        var name = string.IsNullOrWhiteSpace(value.Name) ? fallback.Name : value.Name;
        var size = value.Size <= 0 ? fallback.Size : value.Size;
        return new Font(name, size, style);
    }

    private static int ToTwips(int pixels, float twipsPerPixel) =>
        Convert.ToInt32(Math.Round(pixels * twipsPerPixel, MidpointRounding.AwayFromZero));

    private static int FromTwips(object? value, float twipsPerPixel) =>
        Convert.ToInt32(Math.Round(VBConversions.CDbl(value) / twipsPerPixel, MidpointRounding.AwayFromZero));

    private TreeViewState GetTreeViewState(TreeView treeView)
    {
        if (_treeViewStates.TryGetValue(treeView, out var state))
        {
            return state;
        }

        state = new TreeViewState();
        _treeViewStates.Add(treeView, state);
        return state;
    }

    private RichTextBoxState GetRichTextBoxState(RichTextBox richTextBox)
    {
        if (_richTextBoxStates.TryGetValue(richTextBox, out var state))
        {
            return state;
        }

        state = new RichTextBoxState();
        _richTextBoxStates.Add(richTextBox, state);
        return state;
    }

    private static object CreateControlInstance(string typeName) =>
        typeName.ToUpperInvariant() switch
        {
            "COMMANDBUTTON" => new Button(),
            "TEXTBOX" => new TextBox(),
            "FRAME" => new GroupBox(),
            "PICTUREBOX" => new PictureBox(),
            "IMAGE" => new PictureBox(),
            "LINE" => new LineControl(),
            "MENU" => new MenuProxy(),
            "LABEL" => new Label(),
            "CHECKBOX" => new CheckBox(),
            "OPTIONBUTTON" => new RadioButton(),
            "COMBOBOX" => new ComboBox(),
            "LISTBOX" => new ListBox(),
            "TIMER" => new TimerControl(),
            "TREEVIEW" or "MSCOMCTLLIB.TREEVIEW" => new TreeView(),
            "RICHTEXTBOX" or "RICHTEXTLIB.RICHTEXTBOX" => new RichTextBox(),
            "COMMONDIALOG" or "MSCOMDLG.COMMONDIALOG" => new CommonDialogProxy(),
            "IMAGELIST" or "MSCOMCTLLIB.IMAGELIST" => new ImageListProxy(),
            "IMAGECOMBO" or "MSCOMCTLLIB.IMAGECOMBO" => new ImageComboControl(),
            "SHAPE" => new ShapeControl(),
            _ => new Panel()
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class TimerControl : Panel
    {
        private readonly System.Windows.Forms.Timer _timer = new();

        public TimerControl()
        {
            Visible = false;
            _timer.Interval = 100;
            _timer.Tick += (_, arguments) => Tick?.Invoke(this, arguments);
        }

        public event EventHandler? Tick;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal bool TimerEnabled
        {
            get => _timer.Enabled;
            set => _timer.Enabled = value;
        }

        private void RaiseTick() => Tick?.Invoke(this, EventArgs.Empty);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class MenuProxy : ToolStripMenuItem
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int VbIndex { get; set; } = -1;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Shortcut { get; set; }
    }

    private sealed class LineControl : Control
    {
        public LineControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
            BorderColor = Color.Black;
            BorderWidth = 1;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int X1 { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Y1 { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int X2 { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Y2 { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BorderWidth { get; set; }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            if (BorderWidth <= 0)
            {
                return;
            }

            var scale = DeviceDpi / 1440f;
            using var pen = new Pen(BorderColor, Math.Max(1f, BorderWidth * scale));
            eventArgs.Graphics.DrawLine(
                pen,
                X1 * scale,
                Y1 * scale,
                X2 * scale,
                Y2 * scale);
        }
    }

    private sealed class ShapeControl : Control
    {
        public ShapeControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.UserPaint,
                true);
            BackColor = Color.Transparent;
            BorderColor = Color.Black;
            BorderWidth = 1;
            BackStyle = 1;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BorderWidth { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BackStyle { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int FillStyle { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Shape { get; set; }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            base.OnPaint(eventArgs);
            var rectangle = ClientRectangle;
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            var borderWidth = Math.Max(0, BorderWidth);
            var inset = Math.Max(0, (int)Math.Ceiling(borderWidth / 2f));
            rectangle.Inflate(-inset, -inset);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return;
            }

            using var path = CreateShapePath(rectangle, Shape);
            if (BackStyle != 0 && FillStyle == 0)
            {
                using var brush = new SolidBrush(FillColor.IsEmpty ? BackColor : FillColor);
                eventArgs.Graphics.FillPath(brush, path);
            }

            if (borderWidth > 0)
            {
                using var pen = new Pen(BorderColor, borderWidth);
                eventArgs.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateShapePath(Rectangle rectangle, int shape)
        {
            var path = new GraphicsPath();
            switch (shape)
            {
                case 1:
                case 3:
                    var size = Math.Min(rectangle.Width, rectangle.Height);
                    rectangle = new Rectangle(
                        rectangle.X + (rectangle.Width - size) / 2,
                        rectangle.Y + (rectangle.Height - size) / 2,
                        size,
                        size);
                    break;
            }

            switch (shape)
            {
                case 2:
                case 3:
                    path.AddEllipse(rectangle);
                    break;
                case 4:
                case 5:
                    var radius = Math.Min(rectangle.Width, rectangle.Height) / 4;
                    AddRoundedRectangle(path, rectangle, radius);
                    break;
                default:
                    path.AddRectangle(rectangle);
                    break;
            }

            return path;
        }

        private static void AddRoundedRectangle(GraphicsPath path, Rectangle rectangle, int radius)
        {
            var diameter = Math.Max(1, radius * 2);
            path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }
    }

    private sealed class FormBinding
    {
        public FormBinding(Form form)
        {
            Form = form;
        }

        public Form Form { get; }

        public Dictionary<string, Control> Controls { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, object> Components { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public MenuStrip? MenuStrip { get; set; }
    }

    private sealed class TreeViewState
    {
        public int Style { get; set; }

        public int LineStyle { get; set; }
    }

    private sealed class DesignerControlState
    {
        public int BorderStyle { get; set; }

        public int Appearance { get; set; }

        public bool AutoRedraw { get; set; }

        public int FillStyle { get; set; }

        public int MousePointer { get; set; }

        public int ScaleMode { get; set; } = 1;

        public Bitmap? DrawingSurface { get; set; }

        public void Dispose() => DrawingSurface?.Dispose();
    }

    private sealed class RichTextBoxState
    {
        public string FileName { get; set; } = string.Empty;
    }

    private sealed class EventBinding
    {
        public EventBinding(
            object source,
            string eventName,
            object target,
            string methodName,
            object eventSource,
            EventInfo @event,
            Delegate handler)
        {
            Source = source;
            EventName = eventName;
            Target = target;
            MethodName = methodName;
            EventSource = eventSource;
            Event = @event;
            Handler = handler;
        }

        public object Source { get; }
        public string EventName { get; }
        public object Target { get; }
        public string MethodName { get; }
        public object EventSource { get; }
        public EventInfo Event { get; }
        public Delegate Handler { get; }
    }
}
