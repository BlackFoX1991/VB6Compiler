namespace VB6.Runtime;

/// <summary>Reference identity used by the VB6 <c>Is</c> operator.</summary>
public static class VBObjectIdentity
{
    public static bool IsSame(object? left, object? right) => ReferenceEquals(left, right);

    public static bool IsType(object? value, Type targetType) =>
        value is not null && targetType.IsInstanceOfType(value);
}
