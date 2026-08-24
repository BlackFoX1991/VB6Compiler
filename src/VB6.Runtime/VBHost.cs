using System.Collections;

namespace VB6.Runtime;

/// <summary>
/// Host boundary for VB6 forms, controls and UI-sensitive interaction. The compiler/runtime core
/// stays portable; a WinForms or another UI host can provide the concrete implementation.
/// </summary>
public interface IVB6Host
{
    void DoEvents();

    void Load(object target);

    void Unload(object target);

    object? CreateControl(object owner, string name, string typeName);

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

    IEnumerable<object?>? EnumerateControls(object? target);
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
