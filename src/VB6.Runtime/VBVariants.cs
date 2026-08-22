namespace VB6.Runtime;

/// <summary>Preserves the VB6 Date Variant subtype while storing its OLE Automation value.</summary>
public sealed record VBDateValue(double OADate);

/// <summary>
/// Runtime representation of VB6 Variant state values. Empty is represented by a null object
/// reference because that is also the default value of a Variant slot. The other state values need
/// identity-bearing sentinels so predicates can distinguish them from Empty.
/// </summary>
public static class VBVariants
{
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

    public static bool IsEmpty(object? value) => value is null;

    public static bool IsNull(object? value) => ReferenceEquals(value, NullMarker);

    public static bool IsMissing(object? value) => ReferenceEquals(value, MissingMarker);

    public static bool ToBoolean(object? value) => IsNull(value) ? false : VBConversions.CBool(value);

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
            bool => 11,
            byte => 17,
            long => 20,
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
            : elementType == typeof(short) ? 2
            : elementType == typeof(int) ? 3
            : elementType == typeof(long) ? 20
            : elementType == typeof(float) ? 4
            : elementType == typeof(double) ? 5
            : elementType == typeof(VBCurrency) ? 6
            : elementType == typeof(decimal) ? 14
            : 36;
        return checked((short)(8192 + elementVarType));
    }
}
