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

    /// <summary>
    /// Implements the three-argument VB6 Mid/Mid$ intrinsic. VB6 positions are one-based.
    /// A start beyond the end of the string returns an empty string and length is clipped to the
    /// remaining characters.
    /// </summary>
    public static string Mid(string value, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (start < 1 || length < 0)
        {
            throw new ArgumentOutOfRangeException(
                start < 1 ? nameof(start) : nameof(length),
                "VB6 Mid requires a one-based start position and a non-negative length.");
        }

        if (start > value.Length)
        {
            return string.Empty;
        }

        var zeroBasedStart = start - 1;
        var available = value.Length - zeroBasedStart;
        return value.Substring(zeroBasedStart, Math.Min(length, available));
    }

    /// <summary>
    /// Implements the ASCII subset of VB6 Chr that is reachable in the current corpus.
    /// Extended ANSI values depend on the active VB6 code page and remain an explicit runtime
    /// boundary until code-page handling is modeled by the compiler/runtime.
    /// </summary>
    public static string Chr(int charCode)
    {
        if (charCode is < 0 or > 127)
        {
            throw new NotSupportedException(
                "The current VB6 Chr subset supports ASCII character codes 0 through 127 only.");
        }

        return ((char)charCode).ToString();
    }
}
