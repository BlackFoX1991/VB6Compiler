namespace VB6.Runtime;

/// <summary>
/// VB6 string/storage-size intrinsics that are shared by generated code.
/// </summary>
public static class VBStrings
{
    /// <summary>
    /// Implements the VB6 Len intrinsic for the scalar values currently representable by the runtime.
    /// Strings return their character count; non-string scalar Variants return their VB6 storage size.
    /// The current Variant Empty representation (<see langword="null"/>) has length zero.
    /// </summary>
    public static int Len(object? value) => value switch
    {
        null => 0,
        string text => text.Length,
        byte => 1,
        short => 2,
        int => 4,
        long => 8,
        float => 4,
        double => 8,
        bool => 2,
        VBCurrency => 8,
        _ => throw new InvalidCastException(
            $"CLR value of type '{value.GetType().FullName}' is not supported by the VB6 Len intrinsic.")
    };
}
