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

    // VB6 ScaleMode constants. User (0) has no custom scale in this host yet; the remaining
    // values are exact unit definitions.
    private const int ScaleModeUser = 0;
    private const int ScaleModeTwip = 1;
    private const int ScaleModePoint = 2;
    private const int ScaleModePixel = 3;
    private const int ScaleModeCharacter = 4;
    private const int ScaleModeInch = 5;
    private const int ScaleModeMillimeter = 6;
    private const int ScaleModeCentimeter = 7;

    // VB6 DrawMode values are the GDI ROP2 values plus one-based names. Keeping the numeric
    // contract here makes the managed raster fallback independent of a native HDC.
    private const int DrawModeBlackness = 1;
    private const int DrawModeNotMergePen = 2;
    private const int DrawModeMaskNotPen = 3;
    private const int DrawModeNotCopyPen = 4;
    private const int DrawModeMaskPenNot = 5;
    private const int DrawModeNot = 6;
    private const int DrawModeXorPen = 7;
    private const int DrawModeNotMaskPen = 8;
    private const int DrawModeMaskPen = 9;
    private const int DrawModeNotXorPen = 10;
    private const int DrawModeNop = 11;
    private const int DrawModeMergeNotPen = 12;
    private const int DrawModeCopyPen = 13;
    private const int DrawModeMergePenNot = 14;
    private const int DrawModeMergePen = 15;
    private const int DrawModeWhiteness = 16;

    private readonly bool _preferNativeActiveX;

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
    private int _screenMousePointer;
    private VBPrinterState _printer = VBPrinterState.Headless;
    private bool _disposed;

    public WinFormsHost(
        bool preferNativeActiveX = false,
        VBCompatibilityProfile compatibilityProfile = VBCompatibilityProfile.Deterministic)
    {
        _preferNativeActiveX = preferNativeActiveX;
        CompatibilityProfile = compatibilityProfile;
    }

    public VBCompatibilityProfile CompatibilityProfile { get; }

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

    public void SendKeys(string keys, bool wait)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(keys);

        if (wait)
        {
            System.Windows.Forms.SendKeys.SendWait(keys);
        }
        else
        {
            System.Windows.Forms.SendKeys.Send(keys);
        }
    }

    public bool TryGetScreenState(out VBScreenState? screen)
    {
        ThrowIfDisposed();

        var activeForm = Form.ActiveForm;
        if (activeForm is null || activeForm.IsDisposed)
        {
            activeForm = _bindings.Values
                .Select(binding => binding.Form)
                .FirstOrDefault(form => !form.IsDisposed && form.ContainsFocus);
        }

        var activeControl = FindActiveControl(activeForm);
        var dpi = activeForm is { IsDisposed: false, DeviceDpi: > 0 }
            ? activeForm.DeviceDpi
            : 96;
        screen = new VBScreenState(
            FindGeneratedForm(activeForm),
            activeControl,
            1440f / dpi,
            1440f / dpi,
            _screenMousePointer);
        return true;
    }

    public bool TrySetScreenMousePointer(int mousePointer)
    {
        ThrowIfDisposed();
        _screenMousePointer = mousePointer;
        System.Windows.Forms.Cursor.Current = ToScreenCursor(mousePointer);
        return true;
    }

    public bool TryGetPrinterState(out VBPrinterState? printer)
    {
        ThrowIfDisposed();
        printer = _printer;
        return true;
    }

    public bool TrySetPrinterState(VBPrinterState printer)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(printer);
        _printer = printer;
        return true;
    }

    private object? FindGeneratedForm(Form? form)
    {
        if (form is null || form.IsDisposed)
        {
            return null;
        }

        foreach (var (vbObject, binding) in _bindings)
        {
            if (ReferenceEquals(binding.Form, form))
            {
                return vbObject;
            }
        }

        return form;
    }

    private static Control? FindActiveControl(Control? control)
    {
        while (control is ContainerControl container && container.ActiveControl is { } child)
        {
            control = child;
        }

        return control is Form ? null : control;
    }

    private static Cursor ToScreenCursor(int mousePointer) => mousePointer switch
    {
        1 => Cursors.Arrow,
        2 => Cursors.Cross,
        3 => Cursors.IBeam,
        5 or 15 => Cursors.SizeAll,
        6 => Cursors.SizeNESW,
        7 => Cursors.SizeNS,
        8 => Cursors.SizeNWSE,
        9 => Cursors.SizeWE,
        10 => Cursors.UpArrow,
        11 => Cursors.WaitCursor,
        12 => Cursors.No,
        13 => Cursors.AppStarting,
        14 => Cursors.Help,
        _ => Cursors.Default
    };

    public bool TryGetClipboardText(out string? text) => TryGetClipboardText(1, out text);

    public bool TryGetClipboardText(int format, out string? text)
    {
        ThrowIfDisposed();

        try
        {
            if (TryGetTextDataFormat(format, out var textFormat) &&
                System.Windows.Forms.Clipboard.ContainsText(textFormat))
            {
                text = System.Windows.Forms.Clipboard.GetText(textFormat);
                return true;
            }
        }
        catch (ExternalException)
        {
        }
        catch (ThreadStateException)
        {
        }

        text = null;
        return false;
    }

    public bool TrySetClipboardText(string text, int format)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(text);
        if (!TryGetTextDataFormat(format, out var textFormat))
        {
            return false;
        }

        try
        {
            System.Windows.Forms.Clipboard.SetText(text, textFormat);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (ThreadStateException)
        {
            return false;
        }
    }

    public bool TryGetClipboardData(int format, out object? data)
    {
        ThrowIfDisposed();
        if (format == 0 || !TryGetClipboardDataFormat(format, out var dataFormat))
        {
            data = null;
            return false;
        }

        try
        {
            var clipboard = System.Windows.Forms.Clipboard.GetDataObject();
            if (clipboard?.GetDataPresent(dataFormat, autoConvert: false) == true)
            {
                data = clipboard.GetData(dataFormat, autoConvert: false);
                return true;
            }
        }
        catch (ExternalException)
        {
        }
        catch (ThreadStateException)
        {
        }

        data = null;
        return false;
    }

    public bool TrySetClipboardData(object? data, int format)
    {
        ThrowIfDisposed();
        if (data is null || format == 0 || !TryGetClipboardDataFormat(format, out var dataFormat))
        {
            return false;
        }

        try
        {
            System.Windows.Forms.Clipboard.SetData(dataFormat, data);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (ThreadStateException)
        {
            return false;
        }
    }

    public bool TryGetClipboardFormat(int format, out bool available)
    {
        ThrowIfDisposed();
        try
        {
            if (TryGetTextDataFormat(format, out var textFormat))
            {
                available = System.Windows.Forms.Clipboard.ContainsText(textFormat);
                return true;
            }

            if (TryGetClipboardDataFormat(format, out var dataFormat))
            {
                available = System.Windows.Forms.Clipboard.ContainsData(dataFormat);
                return true;
            }
        }
        catch (ExternalException)
        {
        }
        catch (ThreadStateException)
        {
        }

        available = false;
        return false;
    }

    public bool TryClearClipboard()
    {
        ThrowIfDisposed();
        try
        {
            System.Windows.Forms.Clipboard.Clear();
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (ThreadStateException)
        {
            return false;
        }
    }

    private static bool TryGetTextDataFormat(int format, out TextDataFormat textFormat)
    {
        textFormat = format switch
        {
            1 => TextDataFormat.Text,
            13 => TextDataFormat.UnicodeText,
            -16639 => TextDataFormat.Rtf,
            _ => default
        };
        return format is 1 or 13 or -16639;
    }

    private static bool TryGetClipboardDataFormat(int format, out string dataFormat)
    {
        dataFormat = format switch
        {
            1 => DataFormats.Text,
            2 => DataFormats.Bitmap,
            3 => DataFormats.MetafilePict,
            8 => DataFormats.Dib,
            9 => DataFormats.Palette,
            13 => DataFormats.UnicodeText,
            14 => DataFormats.EnhancedMetafile,
            15 => DataFormats.FileDrop,
            -16639 => DataFormats.Rtf,
            _ => string.Empty
        };
        return dataFormat.Length != 0;
    }

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

    public void GraphicsCircle(VBGraphicsCircle circle)
    {
        ThrowIfDisposed();
        var target = _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(form => !form.IsDisposed);
        if (target is not null)
        {
            RenderGraphicsCircle(target, circle);
        }
    }

    public void GraphicsCircle(object? target, VBGraphicsCircle circle)
    {
        ThrowIfDisposed();
        if (ResolveDrawingTarget(target) is { } control)
        {
            RenderGraphicsCircle(control, circle);
        }
    }

    private void RenderGraphicsCircle(Control target, VBGraphicsCircle circle)
    {
        var state = GetDesignerControlState(target);
        var scale = GetScaleFactors(target, state);

        var centerX = (circle.IsStep ? state.CurrentX + circle.X : circle.X) * scale.X;
        var centerY = (circle.IsStep ? state.CurrentY + circle.Y : circle.Y) * scale.Y;

        // VB6 measures the radius along x and stretches the y axis by the aspect ratio.
        var aspect = circle.Aspect is { } declared && declared > 0f ? declared : 1f;
        var radiusX = circle.Radius * scale.X;
        var radiusY = circle.Radius * scale.Y * aspect;

        var color = Color.Black;
        if (circle.Color is int oleColor)
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

        if (state.DrawMode != DrawModeCopyPen &&
            state.AutoRedraw &&
            state.ActivePaintGraphics is null)
        {
            var surface = GetDrawingSurface(target);
            using var source = new Bitmap(surface.Width, surface.Height);
            using (var sourceGraphics = Graphics.FromImage(source))
            {
                ConfigureRasterGraphics(sourceGraphics);
                DrawGraphicsCircle(sourceGraphics, centerX, centerY, radiusX, radiusY, color, circle);
            }

            ApplyRasterOperation(surface, source, state.DrawMode);
            target.Invalidate();
        }
        else
        {
            using var drawing = BeginDrawing(target);
            DrawGraphicsCircle(drawing.Graphics, centerX, centerY, radiusX, radiusY, color, circle);
        }

        state.CurrentX = circle.IsStep ? state.CurrentX + circle.X : circle.X;
        state.CurrentY = circle.IsStep ? state.CurrentY + circle.Y : circle.Y;
    }

    /// <summary>
    /// Draws the full circle, or the arc between the two angles. VB6 measures angles in radians
    /// counter-clockwise from three o'clock, while GDI+ measures degrees clockwise from there, so
    /// the sweep is negated. A negative angle asks for the radius line as well, which turns the arc
    /// into a pie segment.
    /// </summary>
    private static void DrawGraphicsCircle(
        Graphics graphics,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY,
        Color color,
        VBGraphicsCircle circle)
    {
        var bounds = new RectangleF(
            centerX - radiusX,
            centerY - radiusY,
            radiusX * 2f,
            radiusY * 2f);
        using var pen = new Pen(color, 1f);

        if (circle.Start is not { } start || circle.End is not { } end)
        {
            graphics.DrawEllipse(pen, bounds);
            return;
        }

        const float FullTurn = (float)(Math.PI * 2);
        var isSegment = start < 0f || end < 0f;
        var startAngle = -Math.Abs(start) * 360f / FullTurn;
        var endAngle = -Math.Abs(end) * 360f / FullTurn;
        var sweep = endAngle - startAngle;
        if (sweep > 0f)
        {
            sweep -= 360f;
        }

        if (isSegment)
        {
            graphics.DrawPie(pen, bounds, startAngle, sweep);
        }
        else
        {
            graphics.DrawArc(pen, bounds, startAngle, sweep);
        }
    }

    public void GraphicsPSet(VBGraphicsPoint point)
    {
        ThrowIfDisposed();
        var target = _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(form => !form.IsDisposed);
        if (target is not null)
        {
            RenderGraphicsPSet(target, point);
        }
    }

    public void GraphicsPSet(object? target, VBGraphicsPoint point)
    {
        ThrowIfDisposed();
        if (ResolveDrawingTarget(target) is { } control)
        {
            RenderGraphicsPSet(control, point);
        }
    }

    /// <summary>
    /// Sets one pixel. The surface selection is the same three-way choice Line makes: a raster
    /// operation other than CopyPen on a persistent AutoRedraw surface is merged through
    /// <see cref="ApplyRasterOperation"/>, everything else draws directly.
    /// </summary>
    private void RenderGraphicsPSet(Control target, VBGraphicsPoint point)
    {
        var state = GetDesignerControlState(target);
        var scale = GetScaleFactors(target, state);

        // Step makes the coordinates relative to the current drawing position, exactly as for Line.
        var x = (point.IsStep ? state.CurrentX + point.X : point.X) * scale.X;
        var y = (point.IsStep ? state.CurrentY + point.Y : point.Y) * scale.Y;

        var color = Color.Black;
        if (point.Color is int oleColor)
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

        if (state.DrawMode != DrawModeCopyPen &&
            state.AutoRedraw &&
            state.ActivePaintGraphics is null)
        {
            var surface = GetDrawingSurface(target);
            using var source = new Bitmap(surface.Width, surface.Height);
            using (var sourceGraphics = Graphics.FromImage(source))
            {
                ConfigureRasterGraphics(sourceGraphics);
                DrawGraphicsPoint(sourceGraphics, x, y, color);
            }

            ApplyRasterOperation(surface, source, state.DrawMode);
            target.Invalidate();
        }
        else
        {
            using var drawing = BeginDrawing(target);
            DrawGraphicsPoint(drawing.Graphics, x, y, color);
        }

        // VB6 leaves the current drawing position on the pixel that was just set.
        state.CurrentX = point.IsStep ? state.CurrentX + point.X : point.X;
        state.CurrentY = point.IsStep ? state.CurrentY + point.Y : point.Y;
    }

    private static void DrawGraphicsPoint(Graphics graphics, float x, float y, Color color)
    {
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, x, y, 1f, 1f);
    }

    private void RenderGraphicsLine(Control target, VBGraphicsLine line)
    {
        var state = GetDesignerControlState(target);
        var scale = GetScaleFactors(target, state);
        var startX = line.StartX * scale.X;
        var startY = line.StartY * scale.Y;
        var endX = (line.IsStep ? line.StartX + line.EndX : line.EndX) * scale.X;
        var endY = (line.IsStep ? line.StartY + line.EndY : line.EndY) * scale.Y;
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

        if (state.DrawMode != DrawModeCopyPen &&
            state.AutoRedraw &&
            state.ActivePaintGraphics is null)
        {
            var surface = GetDrawingSurface(target);
            using var source = new Bitmap(surface.Width, surface.Height);
            using (var sourceGraphics = Graphics.FromImage(source))
            {
                ConfigureRasterGraphics(sourceGraphics);
                DrawGraphicsLine(sourceGraphics, startX, startY, endX, endY, color, line.DrawBox, line.Fill);
            }

            ApplyRasterOperation(surface, source, state.DrawMode);
            target.Invalidate();
            return;
        }

        using var drawing = BeginDrawing(target);
        DrawGraphicsLine(drawing.Graphics, startX, startY, endX, endY, color, line.DrawBox, line.Fill);
    }

    private static void DrawGraphicsLine(
        Graphics graphics,
        float startX,
        float startY,
        float endX,
        float endY,
        Color color,
        bool drawBox,
        bool fill)
    {
        using var pen = new Pen(color, 1f);
        if (drawBox)
        {
            var rectangle = RectangleF.FromLTRB(
                Math.Min(startX, endX),
                Math.Min(startY, endY),
                Math.Max(startX, endX),
                Math.Max(startY, endY));
            if (fill)
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

    public void GraphicsClear(object? target)
    {
        ThrowIfDisposed();
        var control = ResolveDrawingTarget(target) ?? _bindings.Values
            .Select(binding => binding.Form)
            .FirstOrDefault(form => !form.IsDisposed);
        if (control is null)
        {
            return;
        }

        var state = GetDesignerControlState(control);
        var color = control.BackColor;
        if (state.ActivePaintGraphics is { } activePaint)
        {
            activePaint.Clear(color);
            return;
        }

        if (state.AutoRedraw)
        {
            var surface = GetDrawingSurface(control);
            using var graphics = Graphics.FromImage(surface);
            graphics.Clear(color);
            control.Invalidate();
            return;
        }

        using var drawing = control.CreateGraphics();
        drawing.Clear(color);
    }

    private bool TryRenderPaintPicture(Control target, VBPaintPicture picture)
    {
        if (!TryGetPaintPictureImage(picture.Picture, out var source, out var ownsSource))
        {
            return false;
        }

        try
        {
            var state = GetDesignerControlState(target);
            var scale = GetScaleFactors(target, state);
            var x = picture.X * scale.X;
            var y = picture.Y * scale.Y;
            var width = picture.Width == 0 ? source!.Width : picture.Width * scale.X;
            var height = picture.Height == 0 ? source!.Height : picture.Height * scale.Y;

            if (state.DrawMode != DrawModeCopyPen &&
                state.AutoRedraw &&
                state.ActivePaintGraphics is null)
            {
                var surface = GetDrawingSurface(target);
                using var sourceLayer = new Bitmap(surface.Width, surface.Height);
                using (var sourceGraphics = Graphics.FromImage(sourceLayer))
                {
                    ConfigureRasterGraphics(sourceGraphics);
                    sourceGraphics.DrawImage(source!, new RectangleF(x, y, width, height));
                }

                ApplyRasterOperation(surface, sourceLayer, state.DrawMode);
                target.Invalidate();
                return true;
            }

            using var drawing = BeginDrawing(target);
            drawing.Graphics.DrawImage(source!, new RectangleF(x, y, width, height));
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
        if (target is Control control && target is not Form)
        {
            if (!control.IsDisposed)
            {
                if (control.Parent is not null && !control.IsHandleCreated)
                {
                    control.CreateControl();
                }

                control.Visible = true;
            }

            return;
        }

        var binding = GetOrCreateBinding(target);
        if (binding.FormInitialized)
        {
            return;
        }

        binding.FormInitialized = true;
        InvokeGeneratedUserControlLifecycle(target, "Form_Initialize");
        TrySubscribeEvent(target, "Load", target, "Form_Load");
        AttachGeneratedFormEvents(target);
    }

    public void Unload(object target)
    {
        ThrowIfDisposed();
        if (target is Control targetControl && target is not Form)
        {
            VBEvents.UnsubscribeObject(targetControl);
            if (!targetControl.IsDisposed)
            {
                targetControl.Visible = false;
            }

            return;
        }

        if (!_bindings.TryGetValue(target, out var binding))
        {
            return;
        }

        InvokeBindingTermination(target, binding);

        _bindings.Remove(target);

        foreach (var hostedObject in binding.HostedObjects.ToArray())
        {
            if (_bindings.ContainsKey(hostedObject))
            {
                Unload(hostedObject);
            }
        }

        foreach (var treeView in binding.Controls.Values.OfType<TreeView>())
        {
            _treeViewStates.Remove(treeView);
        }

        foreach (var richTextBox in binding.Controls.Values.OfType<RichTextBox>())
        {
            _richTextBoxStates.Remove(richTextBox);
        }

        VBEvents.UnsubscribeObject(target);
        foreach (var source in binding.Controls.Values.Cast<object>().Concat(binding.Components.Values))
        {
            VBEvents.UnsubscribeObject(source);
        }

        DisposeComponents(binding);

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
        if (TryCreateGeneratedUserControl(
                owner,
                binding,
                logicalName,
                name,
                typeName,
                parent,
                out var generatedUserControl))
        {
            return generatedUserControl;
        }

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
            else if (hostObject is NativeComComponent nativeComponent)
            {
                nativeComponent.Name = logicalName;
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

    /// <summary>
    /// Creates a control array element at runtime for <c>Load ctlButton(3)</c>. VB6 clones the
    /// element the designer created, down to position and size, and starts the copy hidden so the
    /// program decides when it appears — a freshly loaded element that showed itself immediately
    /// would land on top of its template.
    /// </summary>
    public object? LoadControlArrayElement(object owner, string name, int index, object? template)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfDisposed();

        if (template is not Control source)
        {
            return null;
        }

        var binding = GetOrCreateBinding(owner);
        var elementName = FormatControlArrayElementName(name, index);
        if (binding.Controls.TryGetValue(elementName, out var existing))
        {
            return existing;
        }

        if (Activator.CreateInstance(source.GetType()) is not Control clone)
        {
            return null;
        }

        CopyControlArrayTemplate(source, clone);
        clone.Name = elementName
            .Replace("(", "_", StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal);
        binding.Controls.Add(elementName, clone);
        (source.Parent ?? binding.Form.Controls.Owner as Control)?.Controls.Add(clone);
        if (clone.Parent is null)
        {
            binding.Form.Controls.Add(clone);
        }

        AttachGeneratedControlEvents(owner, clone, elementName);
        return clone;
    }

    public void UnloadControlArrayElement(object owner, string name, int index, object? element)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ThrowIfDisposed();

        var elementName = FormatControlArrayElementName(name, index);
        if (_bindings.TryGetValue(owner, out var binding))
        {
            binding.Controls.Remove(elementName);
        }

        if (element is Control control)
        {
            control.Parent?.Controls.Remove(control);
            control.Dispose();
        }
    }

    private static string FormatControlArrayElementName(string name, int index) =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{name}({index})");

    /// <summary>
    /// Copies the VB6-visible state a loaded element inherits from its template. The clone starts
    /// hidden regardless of the template's visibility, which is what VB6 does.
    /// </summary>
    private static void CopyControlArrayTemplate(Control source, Control clone)
    {
        clone.SetBounds(source.Left, source.Top, source.Width, source.Height);
        clone.Font = source.Font;
        clone.ForeColor = source.ForeColor;
        clone.BackColor = source.BackColor;
        clone.Text = source.Text;
        clone.Enabled = source.Enabled;
        clone.RightToLeft = source.RightToLeft;
        clone.Visible = false;
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

        if (VBDynamicDispatch.TryGetComMember(target, memberName, arguments, out value))
        {
            return true;
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

            if (resolved is IVBComObjectProvider &&
                VBDynamicDispatch.TryGetComMember(resolved, memberName, arguments, out value))
            {
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

            if (VBDynamicDispatch.TryGetComMember(resolved, memberName, arguments, out value))
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

        if (VBDynamicDispatch.TrySetComMember(target, memberName, arguments, value))
        {
            return true;
        }

        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
        }

        return (resolved is IVBComObjectProvider &&
                VBDynamicDispatch.TrySetComMember(resolved, memberName, arguments, value)) ||
               TryWriteControlProperty(resolved, memberName, value) ||
               TryWriteListProperty(resolved, memberName, arguments, value) ||
               VBDynamicDispatch.TrySetComMember(resolved, memberName, arguments, value);
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

        if (VBDynamicDispatch.TryInvokeComMember(target, memberName, arguments, out result))
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
                AttachGeneratedNativeControlEvents(target);
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

        return VBDynamicDispatch.TryInvokeComMember(resolved, memberName, arguments, out result);
    }

    public bool TrySubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName)
        => TrySubscribeEvent(source, eventName, target, methodName, controlArrayIndex: null);

    private bool TrySubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName,
        int? controlArrayIndex)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        if (source is IVBComObjectProvider)
        {
            if (VBEvents.TrySubscribeComMethod(source, eventName, target, methodName))
            {
                return true;
            }

            // Not every VB6 event on an ActiveX control comes from the control. Focus events are
            // supplied by the container's extender, so they are absent from the OCX event
            // interface and have to come from the hosting wrapper instead.
            if (source is not Control comControl)
            {
                return false;
            }

            return TrySubscribeWrapperEvent(comControl, eventName, target, methodName, controlArrayIndex);
        }

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
            eventSource,
            controlArrayIndex);
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

    /// <summary>
    /// Subscribes a VB6 event on the managed wrapper of a native ActiveX control. Used only when
    /// the control's own event interface does not carry the event — the extender events.
    /// </summary>
    private bool TrySubscribeWrapperEvent(
        Control control,
        string eventName,
        object target,
        string methodName,
        int? controlArrayIndex)
    {
        var eventInfo = FindEvent(control.GetType(), eventName);
        var method = FindHandler(target.GetType(), methodName);
        if (eventInfo?.EventHandlerType is null || method is null)
        {
            return false;
        }

        var handler = CreateEventDelegate(
            eventInfo.EventHandlerType,
            target,
            method,
            eventName,
            control,
            controlArrayIndex);
        if (handler is null)
        {
            return false;
        }

        try
        {
            eventInfo.AddEventHandler(control, handler);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is NotSupportedException)
        {
            // AxHost rejects the inherited events an ActiveX control does not implement. That is
            // an answer, not a failure: the event simply does not exist on this control.
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }

        _events.Add(new EventBinding(
            control,
            eventName,
            target,
            methodName,
            control,
            eventInfo,
            handler));
        return true;
    }

    /// <summary>
    /// Subscribes a VB6 Paint handler. Paint does not go through the generic reflection path
    /// because it needs host state: VB6 raises it only while AutoRedraw is off, and the drawing
    /// statements inside the handler must target the paint context rather than a stored bitmap.
    /// The subscription is still registered like any other so that UnsubscribeEvent keeps working.
    /// </summary>
    private bool TrySubscribeVb6Paint(
        object source,
        object target,
        string methodName,
        int? controlArrayIndex)
    {
        ThrowIfDisposed();
        if (ResolveEventSource(source) is not Control control)
        {
            return false;
        }

        var eventInfo = FindEvent(control.GetType(), "Paint");
        var method = FindHandler(target.GetType(), methodName);
        if (eventInfo is null || method is null || method.GetParameters().Length > 1)
        {
            return false;
        }

        PaintEventHandler handler = (_, arguments) =>
            DispatchVb6Paint(control, target, method, arguments, controlArrayIndex);
        eventInfo.AddEventHandler(control, handler);
        _events.Add(new EventBinding(
            source,
            "Paint",
            target,
            methodName,
            control,
            eventInfo,
            handler));
        return true;
    }

    private void DispatchVb6Paint(
        Control control,
        object target,
        MethodInfo method,
        PaintEventArgs paintArguments,
        int? controlArrayIndex)
    {
        var state = GetDesignerControlState(control);

        // VB6 raises Paint only when AutoRedraw is off. With AutoRedraw on, the persistent surface
        // already carries the output and the handler must not run at all.
        if (state.AutoRedraw)
        {
            return;
        }

        var parameters = method.GetParameters();
        var arguments = parameters.Length == 1
            ? new[] { ConvertEventArgument(controlArrayIndex ?? 0, parameters[0].ParameterType) }
            : Array.Empty<object?>();

        var previous = state.ActivePaintGraphics;
        state.ActivePaintGraphics = paintArguments.Graphics;
        try
        {
            method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
        finally
        {
            state.ActivePaintGraphics = previous;
        }
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
        foreach (var entry in _bindings.ToArray())
        {
            VBEvents.UnsubscribeObject(entry.Key);
            foreach (var source in entry.Value.Controls.Values.Cast<object>().Concat(entry.Value.Components.Values))
            {
                VBEvents.UnsubscribeObject(source);
            }

            InvokeBindingTermination(entry.Key, entry.Value);
            DisposeComponents(entry.Value);
            entry.Value.Form.Dispose();
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

    private bool TryGetFormBinding(Form form, out FormBinding binding)
    {
        binding = _bindings.Values.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Form, form))!;
        return binding is not null;
    }

    private void ApplyMdiParent(FormBinding binding)
    {
        if (!binding.IsMdiChild || binding.Form.IsDisposed)
        {
            binding.Form.MdiParent = null;
            return;
        }

        var parent = _bindings.Values.FirstOrDefault(candidate =>
            !ReferenceEquals(candidate, binding) &&
            candidate.Form.IsMdiContainer &&
            !candidate.Form.IsDisposed);
        if (parent is not null)
        {
            binding.Form.MdiParent = parent.Form;
        }
    }

    private void ApplyMdiChildren(Form parent)
    {
        foreach (var binding in _bindings.Values
                     .Where(candidate => candidate.IsMdiChild)
                     .ToArray())
        {
            ApplyMdiParent(binding);
        }
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

    /// <summary>
    /// Converts VB6 scale units into device pixels. VB6 defines every ScaleMode except User as a
    /// fixed number of units per inch, so the factor is exact rather than approximated. Character
    /// mode is the reason this returns one factor per axis: a character is 120 twips wide but 240
    /// twips high, so a single scalar cannot express it.
    /// </summary>
    /// <summary>
    /// VB6 rejects a ScaleMode outside 0..7 with error 380 rather than picking something close.
    /// Silently falling back to twips would move every drawing coordinate without saying so.
    /// </summary>
    private static int ValidateScaleMode(object? value)
    {
        var scaleMode = VBConversions.CLng(value);
        if (scaleMode is < ScaleModeUser or > ScaleModeCentimeter)
        {
            VBErrors.Raise(
                380,
                "ScaleMode",
                "Invalid property value",
                string.Empty,
                0);
        }

        return scaleMode;
    }

    /// <summary>Validates the sixteen GDI ROP2 modes exposed by VB6 DrawMode.</summary>
    private static int ValidateDrawMode(object? value)
    {
        var drawMode = VBConversions.CLng(value);
        if (drawMode is < DrawModeBlackness or > DrawModeWhiteness)
        {
            VBErrors.Raise(
                380,
                "DrawMode",
                "Invalid property value",
                string.Empty,
                0);
        }

        return drawMode;
    }

    private static (float X, float Y) GetScaleFactors(Control target, DesignerControlState state)
    {
        var dpi = target.DeviceDpi;
        return state.ScaleMode switch
        {
            ScaleModePoint => (dpi / 72f, dpi / 72f),
            ScaleModePixel => (1f, 1f),
            ScaleModeCharacter => (dpi / 12f, dpi / 6f),
            ScaleModeInch => (dpi, dpi),
            ScaleModeMillimeter => (dpi / 25.4f, dpi / 25.4f),
            ScaleModeCentimeter => (dpi / 2.54f, dpi / 2.54f),

            // Twip is the VB6 default. User mode carries no custom scale yet — VB6 reports its
            // coordinates in twips until ScaleWidth/ScaleHeight define one, so twips is the
            // faithful answer here rather than a stand-in.
            _ => (dpi / 1440f, dpi / 1440f)
        };
    }

    /// <summary>
    /// Resolves where a VB6 drawing statement goes. VB6 ties this to AutoRedraw: with AutoRedraw
    /// on, output accumulates in a persistent bitmap that the control redraws by itself; with
    /// AutoRedraw off, output goes straight to the visible surface and is lost on the next
    /// repaint — which is the reason the Paint event exists at all. Inside a Paint handler the
    /// statements target that paint context, so a redraw lands where the repaint expects it.
    /// </summary>
    private DrawingScope BeginDrawing(Control target)
    {
        var state = GetDesignerControlState(target);
        if (state.ActivePaintGraphics is { } activePaint)
        {
            return new DrawingScope(activePaint, ownsGraphics: false, invalidate: null);
        }

        if (state.AutoRedraw)
        {
            var surface = GetDrawingSurface(target);
            return new DrawingScope(Graphics.FromImage(surface), ownsGraphics: true, invalidate: target);
        }

        return new DrawingScope(target.CreateGraphics(), ownsGraphics: true, invalidate: null);
    }

    private static void ConfigureRasterGraphics(Graphics graphics)
    {
        graphics.Clear(Color.Transparent);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
    }

    /// <summary>
    /// Applies the VB6/GDI ROP2 operation to the opaque pixels of a source layer. GDI+ does not
    /// expose ROP2 directly, so persistent AutoRedraw surfaces use a small source/destination
    /// raster merge. This keeps all sixteen DrawMode values deterministic and also works for
    /// PaintPicture without depending on a native screen DC.
    /// </summary>
    private static void ApplyRasterOperation(Bitmap destination, Bitmap source, int drawMode)
    {
        if (drawMode == DrawModeNop)
        {
            return;
        }

        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                var sourcePixel = source.GetPixel(x, y);
                if (sourcePixel.A == 0)
                {
                    continue;
                }

                var destinationPixel = destination.GetPixel(x, y);
                var sourceRgb = (uint)(sourcePixel.ToArgb() & 0x00FF_FFFF);
                var destinationRgb = (uint)(destinationPixel.ToArgb() & 0x00FF_FFFF);
                var result = ApplyRop2(drawMode, sourceRgb, destinationRgb);
                destination.SetPixel(x, y, Color.FromArgb(unchecked((int)(0xFF00_0000 | result))));
            }
        }
    }

    private static uint ApplyRop2(int drawMode, uint pen, uint destination)
    {
        const uint mask = 0x00FF_FFFF;
        return drawMode switch
        {
            DrawModeBlackness => 0,
            DrawModeNotMergePen => ~(pen | destination) & mask,
            DrawModeMaskNotPen => destination & ~pen & mask,
            DrawModeNotCopyPen => ~pen & mask,
            DrawModeMaskPenNot => pen & ~destination & mask,
            DrawModeNot => ~destination & mask,
            DrawModeXorPen => pen ^ destination,
            DrawModeNotMaskPen => ~(pen & destination) & mask,
            DrawModeMaskPen => pen & destination,
            DrawModeNotXorPen => ~(pen ^ destination) & mask,
            DrawModeNop => destination,
            DrawModeMergeNotPen => (~pen | destination) & mask,
            DrawModeCopyPen => pen,
            DrawModeMergePenNot => (pen | ~destination) & mask,
            DrawModeMergePen => pen | destination,
            DrawModeWhiteness => mask,
            _ => throw new ArgumentOutOfRangeException(nameof(drawMode))
        };
    }

    /// <summary>
    /// Drops the persistent drawing surface. VB6 discards the AutoRedraw bitmap when AutoRedraw
    /// is turned off, so a later repaint shows the Paint handler's output rather than stale pixels.
    /// </summary>
    private void DiscardDrawingSurface(Control target)
    {
        var state = GetDesignerControlState(target);
        if (state.DrawingSurface is null)
        {
            return;
        }

        if (target is PictureBox pictureBox && ReferenceEquals(pictureBox.Image, state.DrawingSurface))
        {
            pictureBox.Image = null;
        }
        else if (ReferenceEquals(target.BackgroundImage, state.DrawingSurface))
        {
            target.BackgroundImage = null;
        }

        state.DrawingSurface.Dispose();
        state.DrawingSurface = null;
        target.Invalidate();
    }

    private readonly struct DrawingScope : IDisposable
    {
        private readonly bool _ownsGraphics;
        private readonly Control? _invalidate;

        public DrawingScope(Graphics graphics, bool ownsGraphics, Control? invalidate)
        {
            Graphics = graphics;
            _ownsGraphics = ownsGraphics;
            _invalidate = invalidate;
        }

        public Graphics Graphics { get; }

        public void Dispose()
        {
            if (_ownsGraphics)
            {
                Graphics.Dispose();
            }

            _invalidate?.Invalidate();
        }
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
        var baseName = GetControlEventBaseName(name, out var controlArrayIndex);
        if (control is TimerControl)
        {
            TrySubscribeEvent(control, "Tick", owner, baseName + "_Timer", controlArrayIndex);
            return;
        }

        // Subscribe under the VB6 event names. FindEvent maps them onto the WinForms events of the
        // managed adapters, while a native OCX needs its own name on the COM connection point —
        // "TextChanged" or "Enter" mean nothing to an ActiveX control.
        TrySubscribeEvent(control, "Click", owner, baseName + "_Click", controlArrayIndex);
        TrySubscribeEvent(control, "Change", owner, baseName + "_Change", controlArrayIndex);
        TrySubscribeEvent(control, "GotFocus", owner, baseName + "_GotFocus", controlArrayIndex);
        TrySubscribeEvent(control, "LostFocus", owner, baseName + "_LostFocus", controlArrayIndex);
        TrySubscribeEvent(control, "DblClick", owner, baseName + "_DblClick", controlArrayIndex);
        TrySubscribeEvent(control, "MouseDown", owner, baseName + "_MouseDown", controlArrayIndex);
        TrySubscribeEvent(control, "MouseUp", owner, baseName + "_MouseUp", controlArrayIndex);
        TrySubscribeEvent(control, "MouseMove", owner, baseName + "_MouseMove", controlArrayIndex);
        TrySubscribeEvent(control, "KeyDown", owner, baseName + "_KeyDown", controlArrayIndex);
        TrySubscribeEvent(control, "KeyPress", owner, baseName + "_KeyPress", controlArrayIndex);
        TrySubscribeEvent(control, "KeyUp", owner, baseName + "_KeyUp", controlArrayIndex);
        TrySubscribeEvent(control, "Resize", owner, baseName + "_Resize", controlArrayIndex);
        TrySubscribeVb6Paint(control, owner, baseName + "_Paint", controlArrayIndex);
        AttachOcxControlEvents(owner, control, baseName, controlArrayIndex);
    }

    /// <summary>
    /// The events the ActiveX controls add on top of the intrinsic set. The managed adapters must
    /// deliver the same VB6 signature as the native OCX path, so a program cannot tell which one
    /// it is running on: <c>NodeClick</c> hands over a Node, <c>SelChange</c> and <c>Dropdown</c>
    /// take no arguments.
    /// </summary>
    private void AttachOcxControlEvents(
        object owner,
        Control control,
        string baseName,
        int? controlArrayIndex)
    {
        switch (control)
        {
            case TreeView:
                TrySubscribeEvent(control, "NodeClick", owner, baseName + "_NodeClick", controlArrayIndex);
                break;
            case RichTextBox:
                TrySubscribeEvent(control, "SelChange", owner, baseName + "_SelChange", controlArrayIndex);
                break;
            case ComboBox:
                TrySubscribeEvent(control, "Dropdown", owner, baseName + "_Dropdown", controlArrayIndex);
                break;
            case IVBComObjectProvider:
                // A native OCX is none of the managed adapter types, so the control itself has to
                // say which of these it implements: the connection point simply refuses the rest.
                TrySubscribeEvent(control, "NodeClick", owner, baseName + "_NodeClick", controlArrayIndex);
                TrySubscribeEvent(control, "SelChange", owner, baseName + "_SelChange", controlArrayIndex);
                TrySubscribeEvent(control, "Dropdown", owner, baseName + "_Dropdown", controlArrayIndex);
                break;
        }
    }

    private static string GetControlEventBaseName(string name, out int? controlArrayIndex)
    {
        var separator = name.LastIndexOf('.');
        var logicalName = separator >= 0 ? name[(separator + 1)..] : name;
        var open = logicalName.LastIndexOf('(');
        if (open >= 0 &&
            open + 1 < logicalName.Length - 1 &&
            logicalName.EndsWith(')') &&
            int.TryParse(
                logicalName.AsSpan(open + 1, logicalName.Length - open - 2),
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedIndex))
        {
            controlArrayIndex = parsedIndex;
            return logicalName[..open];
        }

        controlArrayIndex = null;
        return logicalName;
    }

    private void AttachGeneratedNativeControlEvents(object owner)
    {
        if (!_bindings.TryGetValue(owner, out var binding))
        {
            return;
        }

        foreach (var entry in binding.Controls)
        {
            if (entry.Key.Contains('.', StringComparison.Ordinal) ||
                entry.Value is not IVBComObjectProvider ||
                entry.Value.IsDisposed)
            {
                continue;
            }

            if (!entry.Value.IsHandleCreated)
            {
                entry.Value.CreateControl();
            }

            VBEvents.RetryComSubscriptions(entry.Value);
            AttachGeneratedControlEvents(owner, entry.Value, entry.Key);
        }
    }

    private void AttachGeneratedMenuEvents(object owner, MenuProxy menu, string name)
    {
        var baseName = name.Split('(')[0];
        TrySubscribeEvent(menu, "Click", owner, baseName + "_Click");
    }

    private void AttachGeneratedFormEvents(object target)
    {
        TrySubscribeEvent(target, "Click", target, "Form_Click");
        TrySubscribeEvent(target, "Activate", target, "Form_Activate");
        TrySubscribeEvent(target, "Deactivate", target, "Form_Deactivate");
        TrySubscribeEvent(target, "QueryUnload", target, "Form_QueryUnload");
        TrySubscribeEvent(target, "Unload", target, "Form_Unload");
        TrySubscribeEvent(target, "Resize", target, "Form_Resize");
        TrySubscribeEvent(target, "MouseDown", target, "Form_MouseDown");
        TrySubscribeEvent(target, "MouseUp", target, "Form_MouseUp");
        TrySubscribeEvent(target, "MouseMove", target, "Form_MouseMove");
        TrySubscribeEvent(target, "KeyDown", target, "Form_KeyDown");
        TrySubscribeEvent(target, "KeyPress", target, "Form_KeyPress");
        TrySubscribeEvent(target, "KeyUp", target, "Form_KeyUp");
        TrySubscribeVb6Paint(target, target, "Form_Paint", null);
    }

    private void AttachGeneratedUserControlEvents(object target)
    {
        TrySubscribeEvent(target, "Click", target, "UserControl_Click");
        TrySubscribeEvent(target, "DoubleClick", target, "UserControl_DblClick");
        TrySubscribeEvent(target, "Resize", target, "UserControl_Resize");
        TrySubscribeEvent(target, "MouseDown", target, "UserControl_MouseDown");
        TrySubscribeEvent(target, "MouseUp", target, "UserControl_MouseUp");
        TrySubscribeEvent(target, "MouseMove", target, "UserControl_MouseMove");
        TrySubscribeEvent(target, "KeyDown", target, "UserControl_KeyDown");
        TrySubscribeEvent(target, "KeyPress", target, "UserControl_KeyPress");
        TrySubscribeEvent(target, "KeyUp", target, "UserControl_KeyUp");
        TrySubscribeVb6Paint(target, target, "UserControl_Paint", null);
    }

    private static void InvokeGeneratedUserControlLifecycle(object target, string methodName)
    {
        var method = FindHandler(target.GetType(), methodName);
        if (method is null || method.GetParameters().Length != 0)
        {
            return;
        }

        try
        {
            method.Invoke(target, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static void InvokeBindingTermination(object target, FormBinding binding)
    {
        if (binding.IsGeneratedUserControl)
        {
            if (binding.UserControlPropertyBag is { } propertyBag)
            {
                InvokeGeneratedUserControlPropertyBagLifecycle(
                    target,
                    "UserControl_WriteProperties",
                    propertyBag);
            }

            InvokeGeneratedUserControlLifecycle(target, "UserControl_Terminate");
        }
        else if (binding.FormInitialized)
        {
            InvokeGeneratedUserControlLifecycle(target, "Form_Terminate");
        }
    }

    private static void InvokeGeneratedUserControlPropertyBagLifecycle(
        object target,
        string methodName,
        VBPropertyBag propertyBag)
    {
        var method = FindHandler(target.GetType(), methodName);
        if (method is null || method.GetParameters() is not [{ } parameter])
        {
            return;
        }

        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()!
            : parameter.ParameterType;
        if (!parameterType.IsAssignableFrom(propertyBag.GetType()))
        {
            return;
        }

        try
        {
            method.Invoke(target, new object?[] { propertyBag });
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private void RemoveEventBinding(EventBinding binding)
    {
        binding.Event.RemoveEventHandler(binding.EventSource, binding.Handler);
        _events.Remove(binding);
    }

    private object? ResolveEventSource(object source)
    {
        // Native ActiveX wrappers inherit WinForms events such as TextChanged from AxHost.
        // Prefer the underlying COM connection point so VB6 event names keep their OCX
        // identity and signature instead of binding to the wrapper's managed event.
        if (source is IVBComObjectProvider)
        {
            return null;
        }

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
            "Activate" => "Activated",
            "QueryUnload" => "FormClosing",
            "Unload" => "FormClosed",

            // ActiveX events the managed adapters stand in for.
            "NodeClick" => "NodeMouseClick",
            "SelChange" => "SelectionChanged",
            "Dropdown" => "DropDown",
            _ => name
        };
        return type.GetEvents(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(@event =>
                string.Equals(@event.Name, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static MethodInfo? FindHandler(Type type, string methodName)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetMethods(flags)
                   .FirstOrDefault(candidate =>
                       string.Equals(candidate.Name, methodName, StringComparison.OrdinalIgnoreCase)) ??
               type.GetMethods(flags)
                   .FirstOrDefault(candidate =>
                       string.Equals(
                           candidate.Name,
                           "__vb6_" + Mangle(methodName),
                           StringComparison.OrdinalIgnoreCase));
    }

    private static Delegate? CreateEventDelegate(
        Type delegateType,
        object target,
        MethodInfo method,
        string eventName,
        object eventSource,
        int? controlArrayIndex)
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
            arguments,
            Expression.Constant(controlArrayIndex, typeof(int?)));
        return Expression.Lambda(delegateType, body, expressions).Compile();
    }

    private static void InvokeEventHandler(
        object target,
        MethodInfo method,
        string eventName,
        object eventSource,
        object?[] eventArguments,
        int? controlArrayIndex)
    {
        var sourceArguments = eventArguments;
        eventArguments = AdaptEventArguments(eventName, eventSource, sourceArguments, controlArrayIndex);
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
            ApplyEventArgumentChanges(eventName, sourceArguments, arguments, parameters);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    private static object?[] AdaptEventArguments(
        string eventName,
        object eventSource,
        object?[] eventArguments,
        int? controlArrayIndex)
    {
        var normalized = eventName.ToUpperInvariant();
        if (eventArguments.Length == 2 && eventArguments[1] is MouseEventArgs mouse)
        {
            if (normalized is "MOUSEDOWN" or "MOUSEUP" or "MOUSEMOVE")
            {
                return AddControlArrayIndex(new object?[]
                {
                    ToVbMouseButton(mouse.Button),
                    ToVbShift(Control.ModifierKeys),
                    ToTwips(eventSource, mouse.X),
                    ToTwips(eventSource, mouse.Y)
                }, controlArrayIndex);
            }
        }

        // VB6 hands NodeClick the clicked Node, not the WinForms mouse arguments.
        if (normalized == "NODECLICK" &&
            eventArguments.Length == 2 &&
            eventArguments[1] is TreeNodeMouseClickEventArgs { Node: { } clickedNode } &&
            eventSource is TreeView clickedTree)
        {
            return AddControlArrayIndex(
                new object?[] { new TreeNodeProxy(clickedTree, clickedNode) },
                controlArrayIndex);
        }

        // SelChange and Dropdown carry no VB6 arguments; drop the WinForms sender/EventArgs pair.
        if (normalized is "SELCHANGE" or "DROPDOWN" && eventArguments.Length == 2)
        {
            return AddControlArrayIndex(Array.Empty<object?>(), controlArrayIndex);
        }

        if (eventArguments.Length == 2 && eventArguments[1] is KeyEventArgs key)
        {
            if (normalized is "KEYDOWN" or "KEYUP")
            {
                return AddControlArrayIndex(new object?[]
                {
                    key.KeyValue,
                    ToVbShift(key.Modifiers)
                }, controlArrayIndex);
            }
        }

        if (eventArguments.Length == 2 && eventArguments[1] is KeyPressEventArgs keyPress &&
            normalized == "KEYPRESS")
        {
            return AddControlArrayIndex(new object?[] { (short)keyPress.KeyChar }, controlArrayIndex);
        }

        if (eventArguments.Length == 2 &&
            eventArguments[1] is FormClosingEventArgs closing &&
            normalized == "QUERYUNLOAD")
        {
            return AddControlArrayIndex(new object?[]
            {
                closing.Cancel ? 1 : 0,
                ToVbUnloadMode(closing.CloseReason)
            }, controlArrayIndex);
        }

        if (eventArguments.Length == 2 &&
            eventArguments[1] is FormClosedEventArgs &&
            normalized == "UNLOAD")
        {
            return AddControlArrayIndex(new object?[] { 0 }, controlArrayIndex);
        }

        if (controlArrayIndex is int index)
        {
            return new object?[] { index };
        }

        return eventArguments;
    }

    private static object?[] AddControlArrayIndex(object?[] eventArguments, int? controlArrayIndex)
    {
        if (controlArrayIndex is not int index)
        {
            return eventArguments;
        }

        return new[] { (object?)index }.Concat(eventArguments).ToArray();
    }

    private static void ApplyEventArgumentChanges(
        string eventName,
        object?[] sourceArguments,
        object?[] handlerArguments,
        ParameterInfo[] handlerParameters)
    {
        if (handlerArguments.Length == 0 ||
            handlerParameters.Length == 0 ||
            sourceArguments.Length != 2)
        {
            return;
        }

        var byRefIndex = Array.FindIndex(
            handlerParameters,
            parameter => parameter.ParameterType.IsByRef);
        if (byRefIndex < 0)
        {
            return;
        }

        if (sourceArguments[1] is FormClosingEventArgs closing &&
            string.Equals(eventName, "QueryUnload", StringComparison.OrdinalIgnoreCase))
        {
            closing.Cancel = VBConversions.CBool(handlerArguments[byRefIndex]);
            return;
        }

        if (sourceArguments[1] is KeyPressEventArgs keyPress &&
            string.Equals(eventName, "KeyPress", StringComparison.OrdinalIgnoreCase))
        {
            keyPress.KeyChar = Convert.ToChar(
                VBConversions.CInt(handlerArguments[byRefIndex]),
                System.Globalization.CultureInfo.InvariantCulture);
            return;
        }

        if (sourceArguments[1] is KeyEventArgs keyEvent &&
            (string.Equals(eventName, "KeyDown", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(eventName, "KeyUp", StringComparison.OrdinalIgnoreCase)))
        {
            var keyCode = VBConversions.CLng(handlerArguments[byRefIndex]);
            if (keyCode != keyEvent.KeyValue)
            {
                // WinForms exposes KeyCode as read-only. A VB6 handler that changes the ByRef
                // code therefore suppresses the current key operation instead of leaking the
                // original event through with a different managed code.
                keyEvent.Handled = true;
                keyEvent.SuppressKeyPress = true;
            }
        }
    }

    private static int ToVbUnloadMode(CloseReason reason) => reason switch
    {
        CloseReason.ApplicationExitCall => 2,
        CloseReason.TaskManagerClosing => 3,
        CloseReason.MdiFormClosing => 4,
        CloseReason.FormOwnerClosing => 5,
        _ => 0
    };

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

        if (control is Form mdiForm &&
            string.Equals(memberName, "MDIChild", StringComparison.OrdinalIgnoreCase) &&
            TryGetFormBinding(mdiForm, out var mdiBinding))
        {
            value = mdiBinding.IsMdiChild;
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
        else if (string.Equals(memberName, "ScaleWidth", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "ClientWidth", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Width, twipsPerPixelX);
        else if (string.Equals(memberName, "ScaleHeight", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "ClientHeight", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Height, twipsPerPixelY);
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

        if (control is Form mdiForm &&
            string.Equals(memberName, "MDIForm", StringComparison.OrdinalIgnoreCase))
        {
            mdiForm.IsMdiContainer = VBConversions.CBool(value);
            if (mdiForm.IsMdiContainer)
            {
                ApplyMdiChildren(mdiForm);
            }

            return true;
        }

        if (control is Form mdiChild &&
            string.Equals(memberName, "MDIChild", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetFormBinding(mdiChild, out var mdiBinding))
            {
                mdiBinding.IsMdiChild = VBConversions.CBool(value);
                ApplyMdiParent(mdiBinding);
            }
            else if (!VBConversions.CBool(value))
            {
                mdiChild.MdiParent = null;
            }

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
        // ClientWidth/ClientHeight are the form's client area, which is what the designer stores
        // and what ScaleWidth/ScaleHeight read back.
        else if (string.Equals(memberName, "ClientWidth", StringComparison.OrdinalIgnoreCase))
        {
            control.ClientSize = new Size(FromTwips(value, twipsPerPixelX), control.ClientSize.Height);
        }
        else if (string.Equals(memberName, "ClientHeight", StringComparison.OrdinalIgnoreCase))
        {
            control.ClientSize = new Size(control.ClientSize.Width, FromTwips(value, twipsPerPixelY));
        }
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
        else if (string.Equals(memberName, "CurrentX", StringComparison.OrdinalIgnoreCase)) value = state.CurrentX;
        else if (string.Equals(memberName, "CurrentY", StringComparison.OrdinalIgnoreCase)) value = state.CurrentY;
        else if (string.Equals(memberName, "DrawMode", StringComparison.OrdinalIgnoreCase)) value = state.DrawMode;
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
        if (string.Equals(memberName, "AutoRedraw", StringComparison.OrdinalIgnoreCase))
        {
            var autoRedraw = VBConversions.CBool(value);
            if (state.AutoRedraw && !autoRedraw)
            {
                // VB6 throws away the AutoRedraw bitmap when the property is turned off.
                DiscardDrawingSurface(control);
            }

            state.AutoRedraw = autoRedraw;
        }
        else if (string.Equals(memberName, "FillStyle", StringComparison.OrdinalIgnoreCase)) state.FillStyle = VBConversions.CLng(value);
        else if (string.Equals(memberName, "MousePointer", StringComparison.OrdinalIgnoreCase)) state.MousePointer = VBConversions.CLng(value);
        else if (string.Equals(memberName, "ScaleMode", StringComparison.OrdinalIgnoreCase)) state.ScaleMode = ValidateScaleMode(value);
        else if (string.Equals(memberName, "CurrentX", StringComparison.OrdinalIgnoreCase)) state.CurrentX = VBConversions.CSng(value);
        else if (string.Equals(memberName, "CurrentY", StringComparison.OrdinalIgnoreCase)) state.CurrentY = VBConversions.CSng(value);
        else if (string.Equals(memberName, "DrawMode", StringComparison.OrdinalIgnoreCase)) state.DrawMode = ValidateDrawMode(value);
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
        if (TryDecodeFrxResource(value, out var resource))
        {
            try
            {
                using var stream = new MemoryStream(resource, writable: false);
                using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
                image = new Bitmap(source);
                return true;
            }
            catch (ArgumentException)
            {
            }
            catch (ExternalException)
            {
            }
        }

        return value is VBPicture picture &&
               TryLoadImageFile(picture.FileName, out image);
    }

    private static bool TryLoadImageFile(string fileName, out Image? image)
    {
        image = null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        try
        {
            using var source = Image.FromFile(fileName);
            image = new Bitmap(source);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (OutOfMemoryException)
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

        if (value is VBPicture picture &&
            TryLoadImageFile(picture.FileName, out image))
        {
            ownsImage = true;
            return true;
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

    private object CreateControlInstance(string typeName)
    {
        if (_preferNativeActiveX && TryCreateNativeActiveX(typeName, out var nativeControl))
        {
            return nativeControl!;
        }

        if (_preferNativeActiveX && TryCreateNativeComComponent(typeName, out var nativeComponent))
        {
            return nativeComponent!;
        }

        return typeName.ToUpperInvariant() switch
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
    }

    private static bool TryCreateNativeActiveX(string typeName, out Control? control)
    {
        control = null;
        var normalizedTypeName = typeName.Trim().ToUpperInvariant();
        var progId = normalizedTypeName switch
        {
            "MSCOMCTLLIB.TREEVIEW" or "MSCOMCTLLIB.TREECTRL" => "MSComctlLib.TreeCtrl.2",
            "MSCOMCTLLIB.LISTVIEW" or "MSCOMCTLLIB.LISTVIEWCTRL" => "MSComctlLib.ListViewCtrl.2",
            "MSCOMCTLLIB.PROGRESSBAR" or "MSCOMCTLLIB.PROGCTRL" => "MSComctlLib.ProgCtrl.2",
            "MSCOMCTLLIB.SLIDER" => "MSComctlLib.Slider.2",
            "MSCOMCTLLIB.STATUSBAR" or "MSCOMCTLLIB.SBARCTRL" => "MSComctlLib.SBarCtrl.2",
            "MSCOMCTLLIB.TABSTRIP" => "MSComctlLib.TabStrip.2",
            "MSCOMCTLLIB.TOOLBAR" => "MSComctlLib.Toolbar.2",
            "MSCOMCTLLIB.IMAGELIST" => "MSComctlLib.ImageListCtrl.2",
            "MSCOMCTLLIB.IMAGECOMBO" => "MSComctlLib.ImageComboCtl.2",
            "RICHTEXTBOX" or "RICHTEXTLIB.RICHTEXTBOX" => "RICHTEXT.RichtextCtrl.1",
            _ when normalizedTypeName.StartsWith("MSCOMDLG.COMMONDIALOG", StringComparison.Ordinal) => null,
            _ when typeName.Contains('.', StringComparison.Ordinal) => typeName.Trim(),
            _ => null
        };
        if (progId is null)
        {
            return false;
        }

        try
        {
            var comType = Type.GetTypeFromProgID(progId, throwOnError: false);
            if (comType is null || comType.GUID == Guid.Empty)
            {
                return false;
            }

            // Activation first distinguishes a registered control from one registered in the
            // other process architecture. Managed adapters remain the deterministic fallback.
            var instance = Activator.CreateInstance(comType);
            if (instance is null)
            {
                return false;
            }

            var isVisualControl = SupportsOleObject(instance);
            if (Marshal.IsComObject(instance))
            {
                Marshal.FinalReleaseComObject(instance);
            }

            if (!isVisualControl)
            {
                return false;
            }

            control = new NativeActiveXControl(comType.GUID);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool SupportsOleObject(object instance)
    {
        if (!Marshal.IsComObject(instance))
        {
            return false;
        }

        var unknown = Marshal.GetIUnknownForObject(instance);
        try
        {
            var oleObjectIid = new Guid("00000112-0000-0000-C000-000000000046");
            var result = Marshal.QueryInterface(unknown, in oleObjectIid, out var oleObject);
            if (result < 0)
            {
                return false;
            }

            Marshal.Release(oleObject);
            return true;
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    private static bool TryCreateNativeComComponent(
        string typeName,
        out NativeComComponent? component)
    {
        component = null;
        var progId = typeName.ToUpperInvariant() switch
        {
            "COMMONDIALOG" or "MSCOMDLG.COMMONDIALOG" => "MSComDlg.CommonDialog.1",
            _ => null
        };
        if (progId is null)
        {
            return false;
        }

        try
        {
            var comType = Type.GetTypeFromProgID(progId, throwOnError: false);
            var instance = comType is null ? null : Activator.CreateInstance(comType);
            if (instance is null || !Marshal.IsComObject(instance))
            {
                return false;
            }

            component = new NativeComComponent(instance);
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static void DisposeComponents(FormBinding binding)
    {
        foreach (var component in binding.Components.Values.Distinct().OfType<IDisposable>())
        {
            component.Dispose();
        }
    }

    private bool TryCreateGeneratedUserControl(
        object owner,
        FormBinding ownerBinding,
        string logicalName,
        string qualifiedName,
        string typeName,
        Control? parent,
        out object? generatedUserControl)
    {
        generatedUserControl = null;
        var generatedType = owner.GetType().Assembly.GetType(
            typeName,
            throwOnError: false,
            ignoreCase: true);
        if (generatedType is null ||
            generatedType.IsAbstract ||
            generatedType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null) is null)
        {
            return false;
        }

        generatedUserControl = Activator.CreateInstance(generatedType, nonPublic: true);
        if (generatedUserControl is null)
        {
            return false;
        }

        var generatedBinding = GetOrCreateBinding(generatedUserControl);
        generatedBinding.IsGeneratedUserControl = true;
        var hostedForm = generatedBinding.Form;
        hostedForm.TopLevel = false;
        hostedForm.FormBorderStyle = FormBorderStyle.None;
        hostedForm.ShowInTaskbar = false;
        hostedForm.Dock = DockStyle.Fill;
        (parent?.Controls ?? ownerBinding.Form.Controls).Add(hostedForm);

        ownerBinding.Components[qualifiedName] = generatedUserControl;
        ownerBinding.Components.TryAdd(logicalName, generatedUserControl);
        ownerBinding.HostedObjects.Add(generatedUserControl);
        generatedBinding.UserControlPropertyBag = new VBPropertyBag();
        AttachGeneratedUserControlEvents(generatedUserControl);
        InvokeGeneratedUserControlLifecycle(generatedUserControl, "UserControl_Initialize");
        InvokeGeneratedUserControlPropertyBagLifecycle(
            generatedUserControl,
            "UserControl_ReadProperties",
            generatedBinding.UserControlPropertyBag);
        hostedForm.Show();
        return true;
    }

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

    private sealed class NativeActiveXControl : AxHost, IVBComTypeInfoProvider
    {
        private object? _comObject;
        private readonly Guid _classId;

        public NativeActiveXControl(Guid clsid)
            : base(clsid.ToString("B"))
        {
            _classId = clsid;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        public Guid ComClassId => _classId;

        public object? ComObject
        {
            get
            {
                if (_comObject is not null)
                {
                    return _comObject;
                }

                try
                {
                    if (!IsHandleCreated)
                    {
                        CreateControl();
                    }

                    _comObject = GetOcx();
                    return _comObject;
                }
                catch (COMException)
                {
                    return null;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
            }
        }
    }

    private sealed class NativeComComponent : IVBComObjectProvider, IDisposable
    {
        private object? _comObject;

        public NativeComComponent(object comObject)
        {
            _comObject = comObject;
        }

        public string Name { get; set; } = string.Empty;

        public object? ComObject => _comObject;

        public void Dispose()
        {
            var comObject = Interlocked.Exchange(ref _comObject, null);
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
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

        public List<object> HostedObjects { get; } = new();

        public bool IsGeneratedUserControl { get; set; }

        public VBPropertyBag? UserControlPropertyBag { get; set; }

        public bool IsMdiChild { get; set; }

        public bool FormInitialized { get; set; }

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

        /// <summary>
        /// Non-null only while a VB6 Paint handler runs for this control. Drawing statements
        /// issued from the handler target this context instead of the persistent surface.
        /// </summary>
        public Graphics? ActivePaintGraphics { get; set; }

        public int FillStyle { get; set; }

        public int MousePointer { get; set; }

        public int ScaleMode { get; set; } = 1;

        /// <summary>
        /// The VB6 drawing position in scale units. PSet leaves it on the pixel it set, and a Step
        /// coordinate is measured from it.
        /// </summary>
        public float CurrentX { get; set; }

        public float CurrentY { get; set; }

        public int DrawMode { get; set; } = DrawModeCopyPen;

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
