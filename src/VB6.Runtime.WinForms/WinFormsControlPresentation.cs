using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using VB6.Runtime;

namespace VB6.Runtime.WinForms;

/// <summary>
/// Installs the WinForms presentation for generated controls.
///
/// It is found by name from <see cref="VBControlPresentationHost"/> -- the runtime must not
/// reference this assembly, or every headless program would drag a UI framework along -- and the
/// method below is the entry point that lookup calls. Renaming either the type or the method
/// breaks the seam without breaking the build, so both names are also stated in
/// <c>VBControlPresentationHost</c>.
/// </summary>
[SupportedOSPlatform("windows")]
public static class WinFormsControlPresentationFactory
{
    public static void Install() =>
        VBControlPresentationHost.Register(control => new WinFormsControlPresentation(control));
}

/// <summary>
/// A generated control's window and drawing, in a container that is not ours.
///
/// The host does the building: a generated control's designer envelope runs in its constructor and
/// creates its children through the ambient <see cref="VBInteraction.Host"/>, so the only thing
/// that has to happen before that is for a host to exist. Everything here is about the window
/// afterwards -- where it hangs, how big it is, and how it gets into a device context the container
/// owns.
///
/// The window stays a WinForms <see cref="Form"/> re-parented into the container rather than a
/// <c>UserControl</c>, because that is what the host already builds for a generated control and
/// what its designer initialisation places children into. Making it something else here would mean
/// two different shapes for the same VB6 construct depending on who hosts it.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WinFormsControlPresentation : IVBControlPresentation
{
    private const int GwlStyle = -16;
    private const int WsChild = 0x40000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int SrcCopy = 0x00CC0020;

    /// <summary>
    /// HIMETRIC is 1/100 mm and a screen is measured in pixels, so the conversion needs the device
    /// resolution. 96 is the value OLE itself assumes when it has no better one, and using it here
    /// keeps <c>SetExtent</c> and <c>GetExtent</c> mutually consistent -- a container that sets a
    /// size and reads it back gets what it set.
    /// </summary>
    private const int HiMetricPerInch = 2540;

    private readonly object _control;
    private Form? _window;
    private bool _uiActive;

    public WinFormsControlPresentation(object control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));

        // The host has to be in place before the generated constructor's designer envelope runs,
        // and this runs from the base constructor -- one step earlier. An existing host is left
        // alone: a program that hosts forms of its own already has one, and replacing it would
        // orphan every window it owns.
        VBInteraction.Host ??= new WinFormsHost(preferNativeActiveX: true);
    }

    public IntPtr WindowHandle => TryGetWindow() is { IsDisposed: false } window && window.IsHandleCreated
        ? window.Handle
        : IntPtr.Zero;

    /// <summary>
    /// Puts the control's window inside the container's.
    ///
    /// <c>SetParent</c> alone is not enough: a window created as a top-level form keeps
    /// <c>WS_POPUP</c>, and a popup child of a foreign window is clipped and painted by nobody.
    /// The style has to become <c>WS_CHILD</c> in the same breath, which is the part that is easy
    /// to leave out because the window does appear -- just in the wrong place and without
    /// clipping.
    /// </summary>
    public bool Activate(IntPtr parentWindow, IntPtr positionRectangle, bool uiActive)
    {
        if (TryGetWindow() is not { IsDisposed: false } window)
        {
            return false;
        }

        window.FormBorderStyle = FormBorderStyle.None;
        window.ShowInTaskbar = false;
        window.StartPosition = FormStartPosition.Manual;
        window.MinimumSize = Size.Empty;

        if (!window.IsHandleCreated)
        {
            window.CreateControl();
            _ = window.Handle;
        }

        if (parentWindow != IntPtr.Zero)
        {
            var style = GetWindowLong(window.Handle, GwlStyle);
            style = (style & ~WsPopup) | WsChild | WsVisible;
            _ = SetWindowLong(window.Handle, GwlStyle, style);
            _ = SetParent(window.Handle, parentWindow);
        }

        if (TryReadRectangle(positionRectangle, out var bounds))
        {
            window.Bounds = bounds;
        }

        window.Visible = true;
        _uiActive = uiActive;
        if (uiActive)
        {
            window.Focus();
        }

        return true;
    }

    public void Hide()
    {
        if (TryGetWindow() is { IsDisposed: false } window)
        {
            window.Visible = false;
        }
    }

    /// <summary>
    /// Gives the window back. The container's window is going away or the control is being
    /// deactivated entirely, and a child left parented to a dying window is destroyed with it --
    /// WinForms then finds a handle it still believes in.
    /// </summary>
    public void Deactivate()
    {
        _uiActive = false;
        if (TryGetWindow() is not { IsDisposed: false } window || !window.IsHandleCreated)
        {
            return;
        }

        window.Visible = false;
        _ = SetParent(window.Handle, IntPtr.Zero);
        var style = GetWindowLong(window.Handle, GwlStyle);
        _ = SetWindowLong(window.Handle, GwlStyle, (style & ~WsChild) | WsPopup);
    }

    public void DeactivateUi() => _uiActive = false;

    public void SetObjectRects(IntPtr positionRectangle, IntPtr clipRectangle)
    {
        if (TryGetWindow() is { IsDisposed: false } window &&
            TryReadRectangle(positionRectangle, out var bounds))
        {
            window.Bounds = bounds;
        }
    }

    /// <summary>
    /// Takes the extent OLE means: the size of the control's *content*.
    ///
    /// So it is the client size, not the window size. Setting <c>Size</c> instead was measured to
    /// give 136 pixels where 96 were asked for -- a form still carrying a caption and a border
    /// counts both into its size, and the control would sit inside a frame the container never
    /// asked for.
    /// </summary>
    public void Resize(VBOleSize extent)
    {
        if (TryGetWindow() is { IsDisposed: false } window)
        {
            window.ClientSize = new Size(ToPixels(extent.Width), ToPixels(extent.Height));
        }
    }

    /// <summary>
    /// Draws the control into a device context the container owns.
    ///
    /// This is what lets a control be *placed* rather than only run: a design surface, a print
    /// preview and a thumbnail all draw a control they never activate. The window is rendered into
    /// a bitmap and blitted, because the container's context is not this window's -- painting
    /// straight into it would ignore its mapping and its clipping.
    /// </summary>
    public bool Draw(IntPtr deviceContext, IntPtr bounds)
    {
        if (deviceContext == IntPtr.Zero || TryGetWindow() is not { IsDisposed: false } window)
        {
            return false;
        }

        if (!window.IsHandleCreated)
        {
            window.CreateControl();
            _ = window.Handle;
        }

        var target = TryReadRectangle(bounds, out var requested) && requested is { Width: > 0, Height: > 0 }
            ? requested
            : new Rectangle(Point.Empty, window.Size);
        if (target is { Width: <= 0 } or { Height: <= 0 })
        {
            return false;
        }

        using var bitmap = new Bitmap(target.Width, target.Height);
        window.DrawToBitmap(bitmap, new Rectangle(0, 0, target.Width, target.Height));

        // Ueber einen eigenen Speicher-DC statt Graphics.FromHdc: Der Kontext des Containers kann
        // ein Metafile oder ein Druckerkontext sein, und ein BitBlt darauf ist die eine Form, die
        // GDI in allen drei Faellen gleich behandelt.
        var handle = bitmap.GetHbitmap();
        var memory = CreateCompatibleDC(deviceContext);
        try
        {
            var previous = SelectObject(memory, handle);
            try
            {
                return BitBlt(
                    deviceContext, target.X, target.Y, target.Width, target.Height,
                    memory, 0, 0, SrcCopy);
            }
            finally
            {
                _ = SelectObject(memory, previous);
            }
        }
        finally
        {
            _ = DeleteDC(memory);
            _ = DeleteObject(handle);
        }
    }

    /// <summary>True while the container has given this control the keyboard.</summary>
    internal bool IsUiActive => _uiActive;

    private Form? TryGetWindow()
    {
        if (_window is { IsDisposed: false })
        {
            return _window;
        }

        _window = VBInteraction.Host is WinFormsHost host ? host.TryGetWindow(_control) : null;
        if (_window is { IsDisposed: false } window)
        {
            // A control has no frame of its own -- the container draws around it. Stripping the
            // border here rather than at activation keeps window size and client size the same
            // number everywhere else in this class, which is what OLE's extent means.
            window.FormBorderStyle = FormBorderStyle.None;
            window.ShowInTaskbar = false;
            window.StartPosition = FormStartPosition.Manual;
        }

        return _window;
    }

    private static int ToPixels(int hiMetric) =>
        (int)Math.Round((double)hiMetric * 96 / HiMetricPerInch, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Reads an OLE <c>RECT</c>: four <c>LONG</c>s, and the last two are edges rather than a size.
    /// Reading them as width and height gives a window that grows with its own position.
    /// </summary>
    private static bool TryReadRectangle(IntPtr pointer, out Rectangle rectangle)
    {
        rectangle = Rectangle.Empty;
        if (pointer == IntPtr.Zero)
        {
            return false;
        }

        var left = Marshal.ReadInt32(pointer, 0);
        var top = Marshal.ReadInt32(pointer, 4);
        var right = Marshal.ReadInt32(pointer, 8);
        var bottom = Marshal.ReadInt32(pointer, 12);
        rectangle = Rectangle.FromLTRB(left, top, right, bottom);
        return true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtrW")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr handle);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(
        IntPtr destination,
        int x,
        int y,
        int width,
        int height,
        IntPtr source,
        int sourceX,
        int sourceY,
        int operation);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}
