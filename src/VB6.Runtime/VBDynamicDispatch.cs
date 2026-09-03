using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

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

    public static object? GetDefaultMember(object? target, int[] arguments) =>
        GetDefaultMember(target, arguments.Cast<object?>().ToArray());

    public static object? GetDefaultMember(object? target, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (TryInvokeComDefaultMember(target, arguments, setProperty: false, out var comResult))
        {
            return comResult;
        }

        return InvokeMember(
            target,
            ResolveDefaultMemberName(target),
            arguments);
    }

    public static void SetDefaultMember(object? target, int[] arguments, object? value) =>
        SetDefaultMember(target, arguments.Cast<object?>().ToArray(), value);

    public static void SetDefaultMember(object? target, object?[] arguments, object? value)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var setterArguments = new object?[arguments.Length + 1];
        Array.Copy(arguments, setterArguments, arguments.Length);
        setterArguments[^1] = value;
        if (TryInvokeComDefaultMember(target, setterArguments, setProperty: true, out _))
        {
            return;
        }

        SetMemberCore(
            target,
            ResolveDefaultMemberName(target),
            arguments,
            value);
    }

    /// <summary>
    /// Tries to read a member directly from a COM object or an object that exposes its RCW
    /// through <see cref="IVBComObjectProvider"/>. Hosts use this boundary when a native
    /// ActiveX wrapper is also a WinForms <see cref="System.Windows.Forms.Control"/>.
    /// </summary>
    public static bool TryGetComMember(
        object? target,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        ArgumentNullException.ThrowIfNull(arguments);
        return TryInvokeComBoundary(target, memberName, arguments, setProperty: false, out result);
    }

    /// <summary>Tries to write a COM property, including indexed properties.</summary>
    public static bool TrySetComMember(
        object? target,
        string memberName,
        object?[] arguments,
        object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        ArgumentNullException.ThrowIfNull(arguments);
        var setterArguments = new object?[arguments.Length + 1];
        Array.Copy(arguments, setterArguments, arguments.Length);
        setterArguments[^1] = value;
        return TryInvokeComBoundary(target, memberName, setterArguments, setProperty: true, out _);
    }

    /// <summary>Tries to invoke a method or property on a COM object.</summary>
    public static bool TryInvokeComMember(
        object? target,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        ArgumentNullException.ThrowIfNull(arguments);
        return TryInvokeComBoundary(target, memberName, arguments, setProperty: false, out result);
    }

    private static bool TryInvokeComBoundary(
        object? target,
        string memberName,
        object?[] arguments,
        bool setProperty,
        out object? result)
    {
        var comObject = GetComObject(target);
        if (comObject is null ||
            !OperatingSystem.IsWindows() ||
            !Marshal.IsComObject(comObject))
        {
            result = null;
            return false;
        }

        return TryInvokeComMember(comObject, memberName, arguments, setProperty, out result);
    }

    private static bool TryInvokeComDefaultMember(
        object? target,
        object?[] arguments,
        bool setProperty,
        out object? result)
    {
        var dispatchTarget = GetComObject(target);
        if (dispatchTarget is null ||
            !OperatingSystem.IsWindows() ||
            !Marshal.IsComObject(dispatchTarget))
        {
            result = null;
            return false;
        }

        return TryInvokeComMember(dispatchTarget, string.Empty, arguments, setProperty, out result);
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
        if (VBInteraction.TryGetHostMember(target, memberName, arguments, out var hostResult) ||
            VBInteraction.TryInvokeHostMember(target, memberName, arguments, out hostResult))
        {
            return hostResult;
        }

        var method = FindMethod(target, memberName, arguments.Length);
        if (method is not null)
        {
            return InvokeMethod(target!, method, arguments);
        }

        var property = FindProperty(target, memberName);
        if (property is not null)
        {
            var converted = ConvertPropertyArguments(property, arguments);
            return Unwrapped(() => property.GetValue(target, converted));
        }

        if (FindPublicField(target, memberName) is { } field)
        {
            var stored = field.GetValue(target);
            if (arguments.Length == 0)
            {
                return stored;
            }

            if (stored is IVBArray array)
            {
                return array.GetObjectValue(ToArrayIndices(memberName, array, arguments));
            }
        }

        if (TryInvokeComMember(target!, memberName, arguments, setProperty: false, out var comResult))
        {
            return comResult;
        }

        throw MissingMember(target, memberName);
    }

    private static void SetMemberCore(
        object? target,
        string memberName,
        object?[] arguments,
        object? value)
    {
        if (VBInteraction.TrySetHostMember(target, memberName, arguments, value))
        {
            return;
        }

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
            property.SetValue(
                target,
                ConvertArgument(value, property.PropertyType),
                ConvertPropertyArguments(property, arguments));
            return;
        }

        if (FindPublicField(target, memberName) is { } field)
        {
            if (arguments.Length == 0)
            {
                field.SetValue(target, ConvertArgument(value, field.FieldType));
                return;
            }

            // Ein Arrayfeld ist eine Referenz: das Element wird im vorhandenen Speicher
            // gesetzt, das Feld selbst bleibt unberuehrt.
            if (field.GetValue(target) is IVBArray array)
            {
                array.SetObjectValue(ToArrayIndices(memberName, array, arguments), value);
                return;
            }
        }

        var comArguments = new object?[arguments.Length + 1];
        Array.Copy(arguments, comArguments, arguments.Length);
        comArguments[^1] = value;
        if (TryInvokeComMember(target!, memberName, comArguments, setProperty: true, out _))
        {
            return;
        }

        throw MissingMember(target, memberName);
    }

    private static bool TryInvokeComMember(
        object target,
        string memberName,
        object?[] arguments,
        bool setProperty,
        out object? result)
    {
        target = GetComObject(target) ?? target;
        if (VBComDispatch.TryInvoke(target, memberName, arguments, setProperty, out result))
        {
            return true;
        }

        var flags = BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.IgnoreCase |
            BindingFlags.OptionalParamBinding |
            (setProperty
                ? BindingFlags.SetProperty
                : BindingFlags.InvokeMethod | BindingFlags.GetProperty);

        try
        {
            result = target.GetType().InvokeMember(
                memberName,
                flags,
                binder: null,
                target,
                arguments,
                CultureInfo.InvariantCulture);
            return true;
        }
        catch (MissingMethodException)
        {
            result = null;
            return false;
        }
        catch (MissingFieldException)
        {
            result = null;
            return false;
        }
        catch (InvalidCastException)
        {
            result = null;
            return false;
        }
        catch (ArgumentException)
        {
            result = null;
            return false;
        }
        catch (COMException exception) when (IsMissingComMember(exception))
        {
            result = null;
            return false;
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is InvalidCastException or ArgumentException ||
                  exception.InnerException is COMException comException && IsMissingComMember(comException))
        {
            result = null;
            return false;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            // The member ran and raised a VB6 error of its own. Reflection wraps it, and a
            // TargetInvocationException carries no VB6 number, so it would land in the catch-all 5
            // and hide, for example, the 9 that Collection.Item reports for a position outside the
            // collection. Rethrowing the original keeps the number the early-bound path produces.
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static bool IsMissingComMember(COMException exception) =>
        exception.ErrorCode is unchecked((int)0x80020003) or unchecked((int)0x80020006);

    private static object? GetComObject(object? target) =>
        target is IVBComObjectProvider provider ? provider.ComObject : target;

    /// <summary>
    /// Puts named arguments of a late-bound call into their parameter positions. A COM target
    /// resolves the names itself through GetIDsOfNames; a managed one has the parameter names
    /// right there in its metadata, and this is where they get matched. A name the member does
    /// not have is VB6 error 448, the same answer the COM path gives.
    /// </summary>
    private static object?[] ResolveNamedArguments(ParameterInfo[] parameters, object?[] arguments)
    {
        var hasNamed = false;
        foreach (var argument in arguments)
        {
            if (argument is VBNamedArgument)
            {
                hasNamed = true;
                break;
            }
        }

        if (!hasNamed)
        {
            return arguments;
        }

        var slots = new object?[Math.Max(parameters.Length, arguments.Length)];
        var filled = new bool[slots.Length];
        var nextPositional = 0;
        foreach (var argument in arguments)
        {
            if (argument is VBNamedArgument named)
            {
                var position = -1;
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (string.Equals(parameters[index].Name, named.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        position = index;
                        break;
                    }
                }

                if (position < 0)
                {
                    VBErrors.Raise(448, named.Name, "Named argument not found", string.Empty, 0);
                }

                slots[position] = named.Value;
                filled[position] = true;
                continue;
            }

            while (nextPositional < filled.Length && filled[nextPositional])
            {
                nextPositional++;
            }

            slots[nextPositional] = argument;
            filled[nextPositional] = true;
            nextPositional++;
        }

        var last = Array.FindLastIndex(filled, entry => entry);
        var resolved = new object?[last + 1];
        for (var index = 0; index <= last; index++)
        {
            resolved[index] = filled[index] ? slots[index] : VBVariants.MissingValue();
        }

        return resolved;
    }

    private static object? InvokeMethod(object target, MethodInfo method, object?[] arguments)
    {
        var parameters = method.GetParameters();
        arguments = ResolveNamedArguments(parameters, arguments);
        var converted = new object?[parameters.Length];
        var sourceIndexes = Enumerable.Repeat(-1, parameters.Length).ToArray();
        var argumentIndex = 0;
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameter = parameters[index];
            if (parameter.GetCustomAttribute<ParamArrayAttribute>() is not null)
            {
                var elementType = parameter.ParameterType.GetElementType()
                    ?? throw new InvalidOperationException("A ParamArray parameter must be an array.");
                var remaining = arguments.Length - argumentIndex;
                var values = Array.CreateInstance(elementType, remaining);
                for (var valueIndex = 0; valueIndex < remaining; valueIndex++)
                {
                    values.SetValue(
                        ConvertArgument(arguments[argumentIndex++], elementType),
                        valueIndex);
                }

                converted[index] = values;
                continue;
            }

            if (argumentIndex < arguments.Length)
            {
                sourceIndexes[index] = argumentIndex;
                converted[index] = ConvertArgument(arguments[argumentIndex++], parameter.ParameterType);
                continue;
            }

            converted[index] = OptionalValue(parameter);
        }

        if (argumentIndex != arguments.Length)
        {
            throw new TargetParameterCountException(
                $"Member '{method.Name}' received {arguments.Length} argument(s), but only {argumentIndex} could be bound.");
        }

        var result = Unwrapped(() => method.Invoke(target, converted));
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType.IsByRef && sourceIndexes[index] >= 0)
            {
                arguments[sourceIndexes[index]] = converted[index];
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
            .Where(method => CanAcceptArgumentCount(method, argumentCount))
            .OrderBy(method => string.Equals(method.Name, generatedName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(method => method.GetParameters().Length == argumentCount ? 0 : 1)
            .FirstOrDefault();
    }

    private static bool CanAcceptArgumentCount(MethodInfo method, int argumentCount)
    {
        var parameters = method.GetParameters();
        var hasParamArray = parameters.LastOrDefault()?.GetCustomAttribute<ParamArrayAttribute>() is not null;
        var fixedParameterCount = hasParamArray ? parameters.Length - 1 : parameters.Length;
        var requiredParameterCount = parameters
            .Take(fixedParameterCount)
            .Count(parameter => !parameter.IsOptional && !parameter.HasDefaultValue);

        if (argumentCount < requiredParameterCount)
        {
            return false;
        }

        return hasParamArray
            ? argumentCount >= requiredParameterCount
            : argumentCount <= parameters.Length;
    }

    private static object? OptionalValue(ParameterInfo parameter)
    {
        if (parameter.HasDefaultValue &&
            parameter.DefaultValue is not DBNull &&
            !ReferenceEquals(parameter.DefaultValue, Missing.Value))
        {
            return ConvertArgument(parameter.DefaultValue, parameter.ParameterType);
        }

        var targetType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()!
            : parameter.ParameterType;

        // Ein ausgelassenes optionales Variant-Argument ist in VB6 Missing, nicht Empty.
        // Nur so beantwortet IsMissing im Ziel die Frage "wurde das Argument uebergeben".
        if (targetType == typeof(object))
        {
            return VBVariants.MissingValue();
        }

        return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
    }

    private static PropertyInfo? FindProperty(object? target, string memberName)
    {
        var type = RequireTarget(target).GetType();
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(property =>
                string.Equals(property.Name, memberName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds a VB6 <c>Public</c> field of a generated class. Such a field is real storage and is
    /// emitted as a CLR field, so a member lookup that only walks methods and properties misses it
    /// entirely - a late-bound <c>o.N</c> then reports 438 although the field is right there.
    /// A <c>Private</c> field stays invisible: the emitter gives it CLR private visibility while a
    /// Public one becomes assembly-visible, so the CLR attribute carries the VB6 contract.
    /// </summary>
    private static FieldInfo? FindPublicField(object? target, string memberName)
    {
        var type = RequireTarget(target).GetType();
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(field =>
                !field.IsPrivate &&
                string.Equals(field.Name, memberName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Converts the index arguments of a late-bound array field access. A wrong index count is a
    /// subscript error, the same one an early-bound access to the wrong rank produces.
    /// </summary>
    private static int[] ToArrayIndices(string memberName, IVBArray array, object?[] arguments)
    {
        if (arguments.Length != array.Rank)
        {
            throw new VB6RuntimeErrorException(
                9,
                $"Member '{memberName}' has rank {array.Rank}, but {arguments.Length} index(es) were supplied.");
        }

        var indices = new int[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            indices[index] = VBConversions.CLng(arguments[index]);
        }

        return indices;
    }

    private static object?[] ConvertPropertyArguments(PropertyInfo property, object?[] arguments)
    {
        arguments = ResolveNamedArguments(property.GetIndexParameters(), arguments);
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

    /// <summary>
    /// Rejects a target that carries no object. A concrete object variable holds the CLR null
    /// reference for <c>Nothing</c>, but a Variant holds the identity-bearing marker instead - and
    /// that marker is an ordinary object as far as reflection is concerned, so a member lookup on
    /// it would find nothing and report 438 where VB6 reports 91. A Variant carrying a scalar is a
    /// different failure: nothing is missing, the value simply is not an object, which is 424.
    /// </summary>
    private static object RequireTarget(object? target)
    {
        if (target is null || VBVariants.IsNothing(target))
        {
            throw new NullReferenceException("Object member access requires a non-empty object reference.");
        }

        return VBVariants.IsObject(target)
            ? target
            : throw new VB6RuntimeErrorException(424, "Member access requires an object reference.");
    }

    /// <summary>
    /// Runs a reflection call so that a VB6 error raised inside the member keeps its number.
    /// Reflection wraps it in a <see cref="TargetInvocationException"/>, which carries no number of
    /// its own and would end up in the catch-all 5 - hiding, for example, the 9 that
    /// <c>Collection.Item</c> reports for a position outside the collection.
    /// </summary>
    private static object? Unwrapped(Func<object?> call)
    {
        try
        {
            return call();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static string ResolveDefaultMemberName(object? target)
    {
        var type = RequireTarget(target).GetType();
        return type.GetCustomAttribute<DefaultMemberAttribute>()?.MemberName ?? "Item";
    }

    private static Exception MissingMember(object? target, string memberName) =>
        new MissingMemberException(
            RequireTarget(target).GetType().FullName,
            memberName);

    private static object? ConvertArgument(object? value, Type parameterType)
    {
        var targetType = parameterType.IsByRef
            ? parameterType.GetElementType()!
            : parameterType;
        var nullableType = Nullable.GetUnderlyingType(targetType);
        if (nullableType is not null)
        {
            return value is null ? null : ConvertArgument(value, nullableType);
        }

        if (value is null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType == typeof(object) || targetType.IsInstanceOfType(value))
        {
            return value;
        }

        if (targetType == typeof(byte)) return VBConversions.ConvertCByte(value);
        if (targetType == typeof(short)) return VBConversions.ConvertCInt(value);
        if (targetType == typeof(int)) return VBConversions.ConvertCLng(value);
        if (targetType == typeof(long)) return VBConversions.ConvertCLngLng(value);
        if (targetType == typeof(ushort)) return VBConversions.ConvertCUShort(value);
        if (targetType == typeof(uint)) return VBConversions.ConvertCUInt(value);
        if (targetType == typeof(ulong)) return VBConversions.ConvertCULng(value);
        if (targetType == typeof(IntPtr)) return VBConversions.ConvertCLngPtr(value);
        if (targetType == typeof(float)) return VBConversions.ConvertCSng(value);
        if (targetType == typeof(double)) return VBConversions.ConvertCDbl(value);
        if (targetType == typeof(decimal))
        {
            return Convert.ToDecimal(VBConversions.CDec(value), CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool)) return VBConversions.ConvertCBool(value);
        if (targetType == typeof(string)) return VBConversions.ConvertCStr(value);
        if (targetType == typeof(VBCurrency)) return VBConversions.ConvertCCur(value);
        if (targetType == typeof(VBDateValue)) return new VBDateValue(VBConversions.CDate(value));
        if (targetType.IsEnum)
        {
            var underlying = ConvertArgument(value, Enum.GetUnderlyingType(targetType));
            return Enum.ToObject(targetType, underlying!);
        }

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
