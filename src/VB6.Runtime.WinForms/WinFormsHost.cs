using System.Collections;
using System.ComponentModel;
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
        AttachGeneratedFormEvents(target);
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
        if (!TryResolveControl(target, memberName, arguments, out var resolved) ||
            resolved is null)
        {
            return false;
        }

        if (TryInvokeListMember(resolved, memberName, arguments))
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

    private static bool TryReadListProperty(
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
                value = textBox.SelectedText;
                return true;
            }
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

    private static bool TryWriteListProperty(
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

    private static IList? GetListItems(Control control) => control switch
    {
        ListBox list => list.Items,
        ComboBox combo => combo.Items,
        _ => null
    };

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
        else if (string.Equals(memberName, "Enabled", StringComparison.OrdinalIgnoreCase)) value = control is TimerControl timer ? timer.TimerEnabled : control.Enabled;
        else if (control is TimerControl timer && string.Equals(memberName, "Interval", StringComparison.OrdinalIgnoreCase)) value = timer.Interval;
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
            "TIMER" => new TimerControl(),
            "TREEVIEW" or "MSCOMCTLLIB.TREEVIEW" => new TreeView(),
            "RICHTEXTBOX" or "RICHTEXTLIB.RICHTEXTBOX" => new RichTextBox(),
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
