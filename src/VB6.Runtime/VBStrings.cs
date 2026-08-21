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
    /// The two-argument VB6 Mid, which returns everything from <paramref name="start"/> onwards.
    /// </summary>
    public static string Mid(string value, int start)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 Mid requires a one-based start position.");
        }

        return start > value.Length ? string.Empty : value[(start - 1)..];
    }

    /// <summary>
    /// VB6 Left. A length beyond the end of the string returns the whole string rather than
    /// failing, which is why this cannot be a plain Substring.
    /// </summary>
    public static string Left(string value, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "VB6 Left requires a non-negative length.");
        }

        return length >= value.Length ? value : value[..length];
    }

    /// <summary>VB6 Right, clipped the same way Left is.</summary>
    public static string Right(string value, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "VB6 Right requires a non-negative length.");
        }

        return length >= value.Length ? value : value[^length..];
    }

    /// <summary>
    /// VB6 UCase. Casing is invariant here for the same reason conversions are: a compiled program
    /// has to behave identically on every machine.
    /// </summary>
    public static string UCase(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToUpperInvariant();
    }

    public static string LCase(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToLowerInvariant();
    }

    /// <summary>
    /// VB6 Trim removes spaces, not every kind of whitespace, so a trailing tab survives it. Using
    /// the .NET Trim here would quietly drop characters VB6 keeps.
    /// </summary>
    public static string Trim(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim(' ');
    }

    public static string LTrim(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.TrimStart(' ');
    }

    public static string RTrim(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.TrimEnd(' ');
    }

    /// <summary>
    /// VB6 Asc. Restricted to ASCII for the same reason Chr is: anything above 127 depends on the
    /// active code page, which the compiler does not model yet.
    /// </summary>
    public static int Asc(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            throw new ArgumentException("VB6 Asc requires a non-empty string.", nameof(value));
        }

        var character = value[0];
        if (character > 127)
        {
            throw new NotSupportedException(
                "The current VB6 Asc subset supports ASCII character codes 0 through 127 only.");
        }

        return character;
    }

    /// <summary>
    /// VB6 IsNumeric. It answers whether the value could be read as a number, which is true for
    /// numeric strings and for every numeric subtype, and false for Empty and for text.
    /// </summary>
    public static bool IsNumeric(object? value) => value switch
    {
        null => false,
        bool => true,
        byte or short or int or long or float or double or decimal or VBCurrency => true,
        string text => double.TryParse(
            text.Trim(),
            System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
            System.Globalization.CultureInfo.InvariantCulture,
            out _),
        _ => false
    };

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
