using System.Drawing;
using System.Linq.Expressions;
using System.Reflection;
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
    private readonly List<EventBinding> _events = new();
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

    public void Load(object target)
    {
        ThrowIfDisposed();
        _ = GetOrCreateBinding(target);
        TrySubscribeEvent(target, "Load", target, "Form_Load");
    }

    public void Unload(object target)
    {
        ThrowIfDisposed();
        if (!_bindings.Remove(target, out var binding))
        {
            return;
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

        var separator = name.LastIndexOf('.');
        var parentName = separator < 0 ? null : name[..separator];
        var logicalName = separator < 0 ? name : name[(separator + 1)..];
        var parent = parentName is not null && binding.Controls.TryGetValue(parentName, out var parentControl)
            ? parentControl
            : null;
        var control = CreateControlInstance(typeName);
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

        var handler = CreateEventDelegate(eventInfo.EventHandlerType, target, method);
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

    private void AttachGeneratedControlEvents(object owner, Control control, string name)
    {
        var baseName = name.Split('(')[0];
        TrySubscribeEvent(control, "Click", owner, baseName + "_Click");
        TrySubscribeEvent(control, "TextChanged", owner, baseName + "_Change");
        TrySubscribeEvent(control, "Enter", owner, baseName + "_GotFocus");
        TrySubscribeEvent(control, "Leave", owner, baseName + "_LostFocus");
        TrySubscribeEvent(control, "DoubleClick", owner, baseName + "_DblClick");
    }

    private void RemoveEventBinding(EventBinding binding)
    {
        binding.Event.RemoveEventHandler(binding.EventSource, binding.Handler);
        _events.Remove(binding);
    }

    private object? ResolveEventSource(object source)
    {
        if (source is Control)
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
        MethodInfo method)
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
            arguments);
        return Expression.Lambda(delegateType, body, expressions).Compile();
    }

    private static void InvokeEventHandler(
        object target,
        MethodInfo method,
        object?[] eventArguments)
    {
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
