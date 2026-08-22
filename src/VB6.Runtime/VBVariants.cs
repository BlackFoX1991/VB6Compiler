namespace VB6.Runtime;

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

    public static short VarType(object? value) => value switch
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
        _ => 9
    };
}
