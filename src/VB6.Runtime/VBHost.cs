using System.Collections;

namespace VB6.Runtime;

/// <summary>
/// Host boundary for VB6 forms, controls and UI-sensitive interaction. The compiler/runtime core
/// stays portable; a WinForms or another UI host can provide the concrete implementation.
/// </summary>
public interface IVB6Host
{
    /// <summary>Compatibility contract selected for this host instance.</summary>
    VBCompatibilityProfile CompatibilityProfile => VBCompatibilityProfile.Deterministic;

    void DoEvents();

    /// <summary>Shows a VB6 menu through the configured UI host.</summary>
    void PopupMenu(object? menu, int flags, float x, float y)
    {
    }

    /// <summary>Draws a VB6 graphics Line operation on the active host surface.</summary>
    void GraphicsLine(VBGraphicsLine line)
    {
    }

    /// <summary>Draws a graphics Line operation on a specific Form or control target.</summary>
    void GraphicsLine(object? target, VBGraphicsLine line)
    {
        GraphicsLine(line);
    }

    /// <summary>
    /// Reads the colour of one pixel. Returning <see langword="false"/> keeps the documented VB6
    /// answer for a point outside the surface, which is -1.
    /// </summary>
    bool TryGetGraphicsPoint(float x, float y, out int color)
    {
        color = -1;
        return false;
    }

    /// <summary>Draws a VB6 Circle, arc or segment on the active host surface.</summary>
    void GraphicsCircle(VBGraphicsCircle circle)
    {
    }

    /// <summary>Draws a Circle on a specific Form or control target.</summary>
    void GraphicsCircle(object? target, VBGraphicsCircle circle)
    {
        GraphicsCircle(circle);
    }

    /// <summary>Sets a single VB6 PSet pixel on the active host surface.</summary>
    void GraphicsPSet(VBGraphicsPoint point)
    {
    }

    /// <summary>Sets a single PSet pixel on a specific Form or control target.</summary>
    void GraphicsPSet(object? target, VBGraphicsPoint point)
    {
        GraphicsPSet(point);
    }

    /// <summary>Draws a supported VB6 PaintPicture operation on the active host surface.</summary>
    void PaintPicture(VBPaintPicture picture)
    {
    }

    /// <summary>Clears the active or specified VB6 drawing surface.</summary>
    void GraphicsClear(object? target)
    {
    }

    /// <summary>Sends VB6 keyboard input through the active UI host.</summary>
    void SendKeys(string keys, bool wait)
    {
    }

    /// <summary>Tries to read text from the host clipboard.</summary>
    bool TryGetClipboardText(out string? text)
    {
        text = null;
        return false;
    }

    /// <summary>Tries to read text in a concrete VB6 clipboard format.</summary>
    bool TryGetClipboardText(int format, out string? text)
    {
        if (format == 1)
        {
            return TryGetClipboardText(out text);
        }

        text = null;
        return false;
    }

    /// <summary>Tries to write text in a concrete VB6 clipboard format.</summary>
    bool TrySetClipboardText(string text, int format) => false;

    /// <summary>Tries to read opaque clipboard data in a concrete VB6 clipboard format.</summary>
    bool TryGetClipboardData(int format, out object? data)
    {
        data = null;
        return false;
    }

    /// <summary>Tries to write opaque clipboard data in a concrete VB6 clipboard format.</summary>
    bool TrySetClipboardData(object? data, int format) => false;

    /// <summary>Tries to query whether a clipboard format is currently available.</summary>
    bool TryGetClipboardFormat(int format, out bool available)
    {
        available = false;
        return false;
    }

    /// <summary>Tries to clear every format from the host clipboard.</summary>
    bool TryClearClipboard() => false;

    /// <summary>
    /// Tries to expose the current VB6 <c>Screen</c> state. Returning <see langword="false"/>
    /// keeps the runtime on its deterministic, desktop-independent fallback.
    /// </summary>
    bool TryGetScreenState(out VBScreenState? screen)
    {
        screen = null;
        return false;
    }

    /// <summary>Tries to set the process-wide VB6 <c>Screen.MousePointer</c> value.</summary>
    bool TrySetScreenMousePointer(int mousePointer) => false;

    /// <summary>
    /// Tries to expose the selected VB6 <c>Printer</c> and its current document state. Hosts that
    /// return a state own subsequent updates through <see cref="TrySetPrinterState"/> as well.
    /// </summary>
    bool TryGetPrinterState(out VBPrinterState? printer)
    {
        printer = null;
        return false;
    }

    /// <summary>Tries to apply an updated selected-printer or print-document state.</summary>
    bool TrySetPrinterState(VBPrinterState printer) => false;

    /// <summary>Tries to append one text operation to the active print document.</summary>
    bool TryWritePrinterText(string text) => false;

    /// <summary>Tries to emit the current page and start a new one.</summary>
    bool TryAdvancePrinterPage() => false;

    /// <summary>Tries to complete or abort the current print document.</summary>
    bool TryCompletePrinterDocument(bool abort) => false;

    /// <summary>Tries to measure printer text in its currently selected coordinate system.</summary>
    bool TryMeasurePrinterText(string text, out float width, out float height)
    {
        width = 0f;
        height = 0f;
        return false;
    }

    /// <summary>Tries to draw a supported PaintPicture operation into the active printer document.</summary>
    bool TryPaintPrinter(VBPaintPicture picture) => false;

    /// <summary>Lets a host display a message box instead of using the deterministic headless result.</summary>
    bool TryShowMessageBox(string prompt, int buttons, string title, out short result)
    {
        result = 0;
        return false;
    }

    /// <summary>Lets a host collect InputBox input instead of returning the deterministic default.</summary>
    bool TryShowInputBox(
        string prompt,
        string title,
        string defaultResponse,
        float xpos,
        float ypos,
        string helpFile,
        int context,
        out string? response)
    {
        response = null;
        return false;
    }

    /// <summary>Lets a host provide a persistent registry/settings value.</summary>
    bool TryGetSetting(string appName, string section, string key, out string? value)
    {
        value = null;
        return false;
    }

    /// <summary>Lets a host persist a registry/settings value.</summary>
    bool TrySaveSetting(string appName, string section, string key, string value) => false;

    /// <summary>
    /// Lets a host delete a key, a section, or the complete application settings entry. The
    /// presence flags preserve VB6's omitted-argument distinction from an explicitly empty name.
    /// </summary>
    bool TryDeleteSetting(
        string appName,
        bool hasSection,
        string? section,
        bool hasKey,
        string? key) => false;

    /// <summary>
    /// Lets a host return the two-dimensional Variant array used by <c>GetAllSettings</c>.
    /// Returning <see langword="false"/> uses the deterministic process-local fallback.
    /// </summary>
    bool TryGetAllSettings(string appName, string section, out VBArray<object>? settings)
    {
        settings = null;
        return false;
    }

    void Load(object target);

    void Unload(object target);

    object? CreateControl(object owner, string name, string typeName);

    /// <summary>
    /// Creates a control array element at runtime, as <c>Load ctlButton(3)</c> does. VB6 clones
    /// the element the designer created — position, size and every other property — and starts the
    /// copy hidden, so the program decides when it appears. Returns the new control, or null when
    /// the host cannot clone the template.
    /// </summary>
    object? LoadControlArrayElement(object owner, string name, int index, object? template) => null;

    /// <summary>
    /// Destroys a control array element created by <see cref="LoadControlArrayElement"/>.
    /// </summary>
    void UnloadControlArrayElement(object owner, string name, int index, object? element)
    {
    }

    /// <summary>Ensures that a generated Form/UserControl has a host binding before initialization.</summary>
    void EnsureForm(object target)
    {
    }

    /// <summary>
    /// Marks the end of the designer envelope of a Form/UserControl: every control exists and has
    /// received the properties the designer wrote. VB6 hands an ActiveX control its persisted state
    /// as a whole at this point rather than property by property, and a control that keeps its
    /// state in its own blob has no other way to get it back. A host without ActiveX controls has
    /// nothing to do here.
    /// </summary>
    void CompleteDesignerInitialization(object target)
    {
    }

    bool TryGetMember(
        object target,
        string memberName,
        object?[] arguments,
        out object? value);

    bool TrySetMember(
        object target,
        string memberName,
        object?[] arguments,
        object? value);

    bool TryInvokeMember(
        object target,
        string memberName,
        object?[] arguments,
        out object? result);

    /// <summary>Tries to connect a generated VB6 handler to a host event.</summary>
    bool TrySubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName) => false;

    /// <summary>Removes a previously connected generated VB6 handler.</summary>
    void UnsubscribeEvent(
        object source,
        string eventName,
        object target,
        string methodName)
    {
    }

    IEnumerable<object?>? EnumerateControls(object? target);
}

/// <summary>
/// Snapshot of the small process-wide surface exposed by VB6's <c>Screen</c> object. Form and
/// control values remain host-owned objects so a concrete UI adapter can preserve their identity.
/// </summary>
public sealed record VBScreenState(
    object? ActiveForm,
    object? ActiveControl,
    float TwipsPerPixelX,
    float TwipsPerPixelY,
    int MousePointer)
{
    /// <summary>Portable 96-DPI fallback used when no interactive desktop host is installed.</summary>
    public static VBScreenState Headless { get; } = new(
        ActiveForm: null,
        ActiveControl: null,
        TwipsPerPixelX: 15f,
        TwipsPerPixelY: 15f,
        MousePointer: 0);
}

/// <summary>
/// Portable state of VB6's selected <c>Printer</c> object and its active print document. It is a
/// value snapshot so hosts can validate page settings before accepting an update.
/// </summary>
public sealed record VBPrinterState(
    string DeviceName,
    string DriverName,
    string Port,
    string DocumentName,
    string OutputFile,
    int ColorMode,
    int Copies,
    int DrawMode,
    int DrawStyle,
    int DrawWidth,
    int Duplex,
    int FillColor,
    int FillStyle,
    int ForeColor,
    int Hdc,
    int Height,
    int Orientation,
    int PaperBin,
    int PaperSize,
    int PrintQuality,
    int ScaleMode,
    int Page,
    int Width,
    int Zoom,
    float CurrentX,
    float CurrentY,
    float ScaleHeight,
    float ScaleLeft,
    float ScaleTop,
    float ScaleWidth,
    float TwipsPerPixelX,
    float TwipsPerPixelY,
    bool TrackDefault,
    bool IsDefaultPrinter,
    object? Font)
{
    /// <summary>Safe letter-sized, 96-DPI printer state for deterministic headless execution.</summary>
    public static VBPrinterState Headless { get; } = new(
        DeviceName: string.Empty,
        DriverName: string.Empty,
        Port: string.Empty,
        DocumentName: "VB6Compiler",
        OutputFile: string.Empty,
        ColorMode: 2,
        Copies: 1,
        DrawMode: 13,
        DrawStyle: 0,
        DrawWidth: 1,
        Duplex: 1,
        FillColor: 0,
        FillStyle: 1,
        ForeColor: 0,
        Hdc: 0,
        Height: 15840,
        Orientation: 1,
        PaperBin: 0,
        PaperSize: 1,
        PrintQuality: -4,
        ScaleMode: 1,
        Page: 0,
        Width: 12240,
        Zoom: 100,
        CurrentX: 0f,
        CurrentY: 0f,
        ScaleHeight: 15840f,
        ScaleLeft: 0f,
        ScaleTop: 0f,
        ScaleWidth: 12240f,
        TwipsPerPixelX: 15f,
        TwipsPerPixelY: 15f,
        TrackDefault: true,
        IsDefaultPrinter: false,
        Font: new VBFont());
}

/// <summary>
/// Exposes the underlying RCW when a host wraps a native COM object in a managed control shell.
/// This keeps WinForms geometry and COM automation/event dispatch on the same VB6 value.
/// </summary>
public interface IVBComObjectProvider
{
    object? ComObject { get; }
}

/// <summary>
/// Supplies the registered coclass identity when a native host wrapper has to resolve COM
/// event metadata from the control's registered type library.
/// </summary>
public interface IVBComTypeInfoProvider : IVBComObjectProvider
{
    Guid ComClassId { get; }
}

/// <summary>
/// Headless control object used when no UI host is installed. It keeps the standard VB6 control
/// properties available to compiled code and can be replaced by a concrete host control later.
/// </summary>
public sealed class VBControlProxy : IEnumerable<object?>
{
    private readonly List<object?> _controls = new();

    public VBControlProxy(string name, string typeName, object? owner)
    {
        Name = name;
        TypeName = typeName;
        Owner = owner;
    }

    public object? Owner { get; }

    public string TypeName { get; }

    public string Name { get; set; }

    public int Index { get; set; } = -1;

    public int Left { get; set; }

    public int Top { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Visible { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public string Caption { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public int BackColor { get; set; }

    public int ForeColor { get; set; }

    public int BorderStyle { get; set; }

    public int Appearance { get; set; }

    public int MousePointer { get; set; }

    public int ScaleHeight { get; set; }

    public int ScaleWidth { get; set; }

    public float CurrentX { get; set; }

    public float CurrentY { get; set; }

    public int FillStyle { get; set; }

    public object? Picture { get; set; }

    public object? Image { get; set; }

    public object? Font { get; set; }

    public int hWnd { get; set; }

    public int hInstance { get; set; }

    public int hDC { get; set; }

    public bool MDIChild { get; set; }

    public IReadOnlyList<object?> Controls => _controls;

    public void AddControl(object? control) => _controls.Add(control);

    public void SetFocus()
    {
    }

    public void Show() => Visible = true;

    public void Hide() => Visible = false;

    public IEnumerator<object?> GetEnumerator() => _controls.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
