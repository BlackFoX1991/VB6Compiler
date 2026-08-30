namespace VB6.Runtime;

/// <summary>Preserves the VB6 Date Variant subtype while storing its OLE Automation value.</summary>
public sealed record VBDateValue(double OADate);

/// <summary>Preserves a VB6 Error Variant value without throwing it as a runtime exception.</summary>
public sealed record VBErrorValue(int Code);

/// <summary>
/// Runtime representation of VB6 Variant state values. Empty is represented by a null object
/// reference because that is also the default value of a Variant slot. The other state values need
/// identity-bearing sentinels so predicates can distinguish them from Empty.
/// </summary>
public static class VBVariants
{
    public const int MissingErrorNumber = 448;

    private sealed class NullValueMarker { }
    private sealed class NothingValueMarker { }
    private sealed class MissingValueMarker { }

    private static readonly object NullMarker = new NullValueMarker();
    private static readonly object NothingMarker = new NothingValueMarker();
    private static readonly object MissingMarker = new MissingValueMarker();

    public static object? EmptyValue() => null;

    public static object NullValue() => NullMarker;

    public static object NothingValue() => NothingMarker;

    public static object MissingValue() => MissingMarker;

    public static void ThrowIfMissing(object? value)
    {
        if (IsMissing(value))
        {
            throw new VB6MissingArgumentException();
        }
    }

    public static void ThrowIfMissing(object? left, object? right)
    {
        if (IsMissing(left) || IsMissing(right))
        {
            throw new VB6MissingArgumentException();
        }
    }

    public static void ThrowIfArray(object? value)
    {
        if (IsArray(value))
        {
            throw new VB6TypeMismatchException("Array Variant values cannot be used with this operator.");
        }
    }

    public static void ThrowIfArray(object? left, object? right)
    {
        if (IsArray(left) || IsArray(right))
        {
            throw new VB6TypeMismatchException("Array Variant values cannot be used with this operator.");
        }
    }

    public static bool IsEmpty(object? value) => value is null;

    public static bool IsNull(object? value) => ReferenceEquals(value, NullMarker);

    public static bool IsNothing(object? value) => ReferenceEquals(value, NothingMarker);

    public static bool IsMissing(object? value) => ReferenceEquals(value, MissingMarker);

    public static bool IsError(object? value) => value is VBErrorValue;

    public static bool IsArray(object? value) => value is IVBArray or Array;

    public static bool IsDate(object? value) =>
        IsDate(value, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware date predicate using the selected text parsing culture.</summary>
    public static bool IsDate(object? value, VBCompatibilityProfile compatibilityProfile)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        if (value is VBDateValue or DateTime)
        {
            return true;
        }

        return value is string text && DateTime.TryParse(
            text,
            compatibilityProfile == VBCompatibilityProfile.VB6Sp6
                ? System.Globalization.CultureInfo.CurrentCulture
                : System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces |
                System.Globalization.DateTimeStyles.AssumeLocal,
            out _);
    }

    public static bool IsObject(object? value) => value switch
    {
        NothingValueMarker => true,
        null or NullValueMarker or MissingValueMarker or VBErrorValue or VBDateValue or
            VBCurrency or string or bool or byte or ushort or uint or ulong or
            short or int or long or float or double or decimal or IntPtr or DateTime or IVBArray or Array => false,
        _ => true
    };

    public static bool ToBoolean(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        ThrowIfMissing(value);
        ThrowIfArray(value);
        return IsNull(value) ? false : VBConversions.CBool(value);
    }

    public static string ArrayTypeName(object? value)
    {
        if (value is IVBArray describedArray && describedArray.ElementTypeName is { } describedType)
        {
            return describedType + "()";
        }

        var elementType = value switch
        {
            IVBArray array => GetArrayElementType(array.GetType()),
            Array array => array.GetType().GetElementType() ?? typeof(object),
            _ => throw new VB6TypeMismatchException("The Variant does not contain an array.")
        };
        return GetTypeName(elementType) + "()";
    }

    private static Type GetArrayElementType(Type arrayType)
    {
        if (arrayType.IsGenericType &&
            arrayType.GetGenericTypeDefinition() == typeof(VBArray<>))
        {
            return arrayType.GetGenericArguments()[0];
        }

        return arrayType.GetElementType() ?? typeof(object);
    }

    public static short VarType(object? value)
    {
        if (value is IVBArray or Array)
        {
            return ArrayVarType(value);
        }

        return VarType(value, depth: 0);
    }

    private static short VarType(object? value, int depth)
    {
        if (value is IVBArray or Array)
        {
            return ArrayVarType(value);
        }

        if (depth < 8 &&
            VBVariantObject.TryGetDefaultValue(value, out var defaultValue) &&
            !ReferenceEquals(value, defaultValue))
        {
            return VarType(defaultValue, depth + 1);
        }

        return value switch
        {
            null => 0,
            NullValueMarker => 1,
            short => 2,
            int => 3,
            float => 4,
            double => 5,
            VBCurrency => 6,
            string => 8,
            NothingValueMarker => 9,
            MissingValueMarker => 10,
            VBErrorValue => 10,
            bool => 11,
            byte => 17,
            ushort => 18,
            uint => 20,
            long => 20,
            ulong => 21,
            IntPtr => IntPtr.Size == 8 ? (short)20 : (short)3,
            decimal => 14,
            VBDateValue => 7,
            DateTime => 7,
            _ => 9
        };
    }

    private static short ArrayVarType(object array)
    {
        if (array is IVBArray describedArray && describedArray.ElementVarType != 0)
        {
            return checked((short)(8192 + describedArray.ElementVarType));
        }

        var arrayType = array.GetType();
        var elementType = GetArrayElementType(arrayType);
        var elementVarType = elementType == typeof(object) ? 12
            : elementType == typeof(string) ? 8
            : elementType == typeof(bool) ? 11
            : elementType == typeof(byte) ? 17
            : elementType == typeof(ushort) ? 18
            : elementType == typeof(short) ? 2
            : elementType == typeof(int) ? 3
            : elementType == typeof(uint) ? 20
            : elementType == typeof(long) ? 20
            : elementType == typeof(ulong) ? 21
            : elementType == typeof(IntPtr) ? (IntPtr.Size == 8 ? 20 : 3)
            : elementType == typeof(float) ? 4
            : elementType == typeof(double) ? 5
            : elementType == typeof(VBCurrency) ? 6
            : elementType == typeof(decimal) ? 14
            : elementType == typeof(VBDateValue) || elementType == typeof(DateTime) ? 7
            : 36;
        return checked((short)(8192 + elementVarType));
    }

    private static string GetTypeName(Type type) => type == typeof(object) ? "Variant"
        : type == typeof(string) ? "String"
        : type == typeof(bool) ? "Boolean"
        : type == typeof(byte) ? "Byte"
        : type == typeof(ushort) ? "UShort"
        : type == typeof(short) ? "Integer"
        : type == typeof(uint) ? "UInteger"
        : type == typeof(int) ? "Long"
        : type == typeof(ulong) ? "ULong"
        : type == typeof(long) ? "LongLong"
        : type == typeof(IntPtr) ? "LongPtr"
        : type == typeof(float) ? "Single"
        : type == typeof(double) ? "Double"
        : type == typeof(VBCurrency) ? "Currency"
        : type == typeof(decimal) ? "Decimal"
        : type == typeof(VBDateValue) || type == typeof(DateTime) ? "Date"
        : type.Name;
}
