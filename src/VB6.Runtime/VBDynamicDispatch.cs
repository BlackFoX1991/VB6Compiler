using System.Reflection;

namespace VB6.Runtime;

/// <summary>
/// Runtime dispatch for VB6 Object and Variant member access. Generated class procedures are
/// emitted as methods named <c>__vb6_...</c>; ordinary CLR members remain valid fallbacks for host
/// objects and future COM adapters.
/// </summary>
public static class VBDynamicDispatch
{
    public static object? GetMember(object? target, string memberName) =>
        InvokeMember(target, memberName, Array.Empty<object?>());

    public static object? GetIndexedMember(
        object? target,
        string memberName,
        VBArray<object> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = arguments.EnumerateValues().ToArray();
        return InvokeMember(target, memberName, values);
    }

    public static void SetMember(object? target, string memberName, object? value) =>
        SetMemberCore(target, memberName, Array.Empty<object?>(), value);

    public static void SetIndexedMember(
        object? target,
        string memberName,
        VBArray<object> arguments,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        SetMemberCore(target, memberName, arguments.EnumerateValues().ToArray(), value);
    }

    public static object? InvokeMember(
        object? target,
        string memberName,
        VBArray<object> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var values = arguments.EnumerateValues().ToArray();
        var result = InvokeMember(target, memberName, values);
        for (var index = 0; index < values.Length; index++)
        {
            arguments[index] = values[index];
        }

        return result;
    }

    private static object? InvokeMember(object? target, string memberName, object?[] arguments)
    {
        var method = FindMethod(target, memberName, arguments.Length);
        if (method is not null)
        {
            return InvokeMethod(target!, method, arguments);
        }

        var property = FindProperty(target, memberName);
        if (property is not null)
        {
            var converted = ConvertPropertyArguments(property, arguments);
            return property.GetValue(target, converted);
        }

        throw MissingMember(target, memberName);
    }

    private static void SetMemberCore(
        object? target,
        string memberName,
        object?[] arguments,
        object? value)
    {
        var setterArguments = new object?[arguments.Length + 1];
        Array.Copy(arguments, setterArguments, arguments.Length);
        setterArguments[^1] = value;

        var method = FindMethod(target, memberName, setterArguments.Length);
        if (method is not null)
        {
            InvokeMethod(target!, method, setterArguments);
            return;
        }

        var property = FindProperty(target, memberName);
        if (property is not null && property.CanWrite)
        {
            property.SetValue(target, value, ConvertPropertyArguments(property, arguments));
            return;
        }

        throw MissingMember(target, memberName);
    }

    private static object? InvokeMethod(object target, MethodInfo method, object?[] arguments)
    {
        var parameters = method.GetParameters();
        var converted = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            converted[index] = ConvertArgument(arguments[index], parameters[index].ParameterType);
        }

        var result = method.Invoke(target, converted);
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType.IsByRef && index < arguments.Length)
            {
                arguments[index] = converted[index];
            }
        }

        return result;
    }

    private static MethodInfo? FindMethod(object? target, string memberName, int argumentCount)
    {
        var type = RequireTarget(target).GetType();
        var generatedName = "__vb6_" + Mangle(memberName);
        return type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method =>
                string.Equals(method.Name, memberName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method.Name, generatedName, StringComparison.OrdinalIgnoreCase))
            .Where(method => method.GetParameters().Length == argumentCount)
            .OrderBy(method => string.Equals(method.Name, generatedName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
    }

    private static PropertyInfo? FindProperty(object? target, string memberName)
    {
        var type = RequireTarget(target).GetType();
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(property =>
                string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase));
    }

    private static object?[] ConvertPropertyArguments(PropertyInfo property, object?[] arguments)
    {
        var indexParameters = property.GetIndexParameters();
        if (indexParameters.Length != arguments.Length)
        {
            throw new TargetParameterCountException(
                $"Member '{property.Name}' expects {indexParameters.Length} index argument(s), but {arguments.Length} were supplied.");
        }

        var converted = new object?[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            converted[index] = ConvertArgument(arguments[index], indexParameters[index].ParameterType);
        }

        return converted;
    }

    private static object RequireTarget(object? target) =>
        target ?? throw new NullReferenceException("Object member access requires a non-empty object reference.");

    private static Exception MissingMember(object? target, string memberName) =>
        new MissingMemberException(
            RequireTarget(target).GetType().FullName,
            memberName);

    private static object? ConvertArgument(object? value, Type parameterType)
    {
        var targetType = parameterType.IsByRef
            ? parameterType.GetElementType()!
            : parameterType;
        if (value is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(object) || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(byte)) return VBConversions.CByte(value);
        if (targetType == typeof(short)) return VBConversions.CInt(value);
        if (targetType == typeof(int)) return VBConversions.CLng(value);
        if (targetType == typeof(long)) return VBConversions.CLngLng(value);
        if (targetType == typeof(float)) return VBConversions.CSng(value);
        if (targetType == typeof(double)) return VBConversions.CDbl(value);
        if (targetType == typeof(bool)) return VBConversions.CBool(value);
        if (targetType == typeof(string)) return VBConversions.CStr(value);
        if (targetType == typeof(VBCurrency)) return VBConversions.CCur(value);

        throw new InvalidCastException(
            $"Cannot pass a Variant value of type '{value.GetType().Name}' to '{targetType.Name}'.");
    }

    private static string Mangle(string name)
    {
        var characters = name
            .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();
        return characters.Length == 0 ? "unnamed" : new string(characters);
    }
}
