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
