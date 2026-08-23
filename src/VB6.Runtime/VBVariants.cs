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

    public static bool IsEmpty(object? value) => value is null;

    public static bool IsNull(object? value) => ReferenceEquals(value, NullMarker);

    public static bool IsNothing(object? value) => ReferenceEquals(value, NothingMarker);

    public static bool IsMissing(object? value) => ReferenceEquals(value, MissingMarker);

    public static bool IsError(object? value) => value is VBErrorValue;

    public static bool IsArray(object? value) => value is IVBArray;

    public static bool IsDate(object? value)
    {
        if (value is VBDateValue or DateTime)
        {
            return true;
        }

        return value is string text && DateTime.TryParse(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces |
                System.Globalization.DateTimeStyles.AssumeLocal,
            out _);
    }

    public static bool IsObject(object? value) => value switch
    {
        NothingValueMarker => true,
        null or NullValueMarker or MissingValueMarker or VBErrorValue or VBDateValue or
            VBCurrency or string or bool or byte or ushort or uint or ulong or
            short or int or long or float or double or decimal or IntPtr or IVBArray => false,
        _ => true
    };

    public static bool ToBoolean(object? value)
    {
        ThrowIfMissing(value);
        return IsNull(value) ? false : VBConversions.CBool(value);
    }

    public static short VarType(object? value)
    {
        if (value is IVBArray)
        {
            return ArrayVarType(value.GetType());
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
            _ => 9
        };
    }

    private static short ArrayVarType(Type arrayType)
    {
        var elementType = arrayType.IsGenericType &&
                          arrayType.GetGenericTypeDefinition() == typeof(VBArray<>)
            ? arrayType.GetGenericArguments()[0]
            : typeof(object);
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
            : 36;
        return checked((short)(8192 + elementVarType));
    }
}
