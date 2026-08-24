using System.Drawing;
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
    private readonly Dictionary<object, FormBinding> _bindings =
        new(ReferenceEqualityComparer.Instance);
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
    }

    public void DoEvents() => Application.DoEvents();

    public void Load(object target)
    {
        ThrowIfDisposed();
        _ = GetOrCreateBinding(target);
    }

    public void Unload(object target)
    {
        ThrowIfDisposed();
        if (!_bindings.Remove(target, out var binding))
        {
            return;
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

        var control = CreateControlInstance(typeName);
        control.Name = name.Replace("(", "_", StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(",", "_", StringComparison.Ordinal);
        binding.Controls.Add(name, control);
        binding.Form.Controls.Add(control);
        return control;
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
        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
        }

        return TryWriteControlProperty(resolved, memberName, value);
    }

    public bool TryInvokeMember(
        object target,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        ThrowIfDisposed();
        result = null;
        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
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
        foreach (var binding in _bindings.Values)
        {
            binding.Form.Dispose();
        }

        _bindings.Clear();
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

    private static bool TryReadControlProperty(
        Control control,
        string memberName,
        out object? value)
    {
        var twipsPerPixelX = 1440f / control.DeviceDpi;
        var twipsPerPixelY = 1440f / control.DeviceDpi;
        if (string.Equals(memberName, "Left", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Left, twipsPerPixelX);
        else if (string.Equals(memberName, "Top", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Top, twipsPerPixelY);
        else if (string.Equals(memberName, "Width", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Width, twipsPerPixelX);
        else if (string.Equals(memberName, "Height", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.Height, twipsPerPixelY);
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase)) value = control.Visible;
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase)) value = control.Enabled;
        else if (string.Equals(memberName, "Name", StringComparison.OrdinalIgnoreCase)) value = control.Name;
        else if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase)) value = control.Text;
        else if (string.Equals(memberName, "BackColor", StringComparison.OrdinalIgnoreCase)) value = ColorTranslator.ToOle(control.BackColor);
        else if (string.Equals(memberName, "ForeColor", StringComparison.OrdinalIgnoreCase)) value = ColorTranslator.ToOle(control.ForeColor);
        else if (string.Equals(memberName, "ScaleWidth", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Width, twipsPerPixelX);
        else if (string.Equals(memberName, "ScaleHeight", StringComparison.OrdinalIgnoreCase)) value = ToTwips(control.ClientSize.Height, twipsPerPixelY);
        else if (string.Equals(memberName, "hWnd", StringComparison.OrdinalIgnoreCase)) value = control.Handle.ToInt64();
        else if (string.Equals(memberName, "hDC", StringComparison.OrdinalIgnoreCase)) value = 0L;
        else if (string.Equals(memberName, "hInstance", StringComparison.OrdinalIgnoreCase)) value = 0L;
        else if (string.Equals(memberName, "Font", StringComparison.OrdinalIgnoreCase)) value = ToVBFont(control.Font);
        else
        {
            value = null;
            return false;
        }

        return true;
    }

    private static bool TryWriteControlProperty(Control control, string memberName, object? value)
    {
        var twipsPerPixelX = 1440f / control.DeviceDpi;
        var twipsPerPixelY = 1440f / control.DeviceDpi;
        if (string.Equals(memberName, "Left", StringComparison.OrdinalIgnoreCase)) control.Left = FromTwips(value, twipsPerPixelX);
        else if (string.Equals(memberName, "Top", StringComparison.OrdinalIgnoreCase)) control.Top = FromTwips(value, twipsPerPixelY);
        else if (string.Equals(memberName, "Width", StringComparison.OrdinalIgnoreCase)) control.Width = FromTwips(value, twipsPerPixelX);
        else if (string.Equals(memberName, "Height", StringComparison.OrdinalIgnoreCase)) control.Height = FromTwips(value, twipsPerPixelY);
        else if (string.Equals(memberName, "Visible", StringComparison.OrdinalIgnoreCase)) control.Visible = VBConversions.CBool(value);
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase)) control.Enabled = VBConversions.CBool(value);
        else if (string.Equals(memberName, "Caption", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(memberName, "Text", StringComparison.OrdinalIgnoreCase)) control.Text = VBConversions.CStr(value);
        else if (string.Equals(memberName, "BackColor", StringComparison.OrdinalIgnoreCase)) control.BackColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        else if (string.Equals(memberName, "ForeColor", StringComparison.OrdinalIgnoreCase)) control.ForeColor = ColorTranslator.FromOle(VBConversions.CLng(value));
        else if (string.Equals(memberName, "Font", StringComparison.OrdinalIgnoreCase) && value is VBFont font) control.Font = FromVBFont(font, control.Font);
        else return false;

        return true;
    }

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

    private static Control CreateControlInstance(string typeName) =>
        typeName.ToUpperInvariant() switch
        {
            "COMMANDBUTTON" => new Button(),
            "TEXTBOX" => new TextBox(),
            "FRAME" => new GroupBox(),
            "PICTUREBOX" => new PictureBox(),
            "IMAGE" => new PictureBox(),
            "LABEL" => new Label(),
            "CHECKBOX" => new CheckBox(),
            "OPTIONBUTTON" => new RadioButton(),
            "COMBOBOX" => new ComboBox(),
            "LISTBOX" => new ListBox(),
            "TREEVIEW" or "MSCOMCTLLIB.TREEVIEW" => new TreeView(),
            "RICHTEXTBOX" or "RICHTEXTLIB.RICHTEXTBOX" => new RichTextBox(),
            _ => new Panel()
        };

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
    }
}
