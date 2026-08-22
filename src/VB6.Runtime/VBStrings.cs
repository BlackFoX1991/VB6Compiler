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
    /// Implements VB6 Like for the language wildcard subset: <c>?</c> matches one character,
    /// <c>*</c> matches zero or more, <c>#</c> matches an ASCII digit, and bracket character
    /// lists support negation and inclusive ranges. Matching is ordinal by default and
    /// case-insensitive for <c>Option Compare Text</c>.
    /// </summary>
    public static bool Like(object? value, object? pattern, bool textCompare)
    {
        if (VBVariants.IsNull(value) || VBVariants.IsNull(pattern))
        {
            return false;
        }

        var input = VBConversions.CStr(value);
        var expression = VBConversions.CStr(pattern);
        return LikeCore(input, expression, textCompare);
    }

    private static bool LikeCore(string input, string pattern, bool textCompare)
    {
        var inputIndex = 0;
        var patternIndex = 0;
        var starPatternIndex = -1;
        var starInputIndex = -1;

        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starPatternIndex = patternIndex++;
                starInputIndex = inputIndex;
                continue;
            }

            if (patternIndex < pattern.Length &&
                TryMatchToken(input, inputIndex, pattern, ref patternIndex, textCompare))
            {
                inputIndex++;
                continue;
            }

            if (starPatternIndex >= 0)
            {
                patternIndex = starPatternIndex + 1;
                inputIndex = ++starInputIndex;
                continue;
            }

            return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static bool TryMatchToken(
        string input,
        int inputIndex,
        string pattern,
        ref int patternIndex,
        bool textCompare)
    {
        var token = pattern[patternIndex++];
        return token switch
        {
            '?' => true,
            '#' => input[inputIndex] is >= '0' and <= '9',
            '[' => MatchCharacterList(input[inputIndex], pattern, ref patternIndex, textCompare),
            _ => CharactersEqual(input[inputIndex], token, textCompare)
        };
    }

    private static bool MatchCharacterList(char value, string pattern, ref int patternIndex, bool textCompare)
    {
        var negated = patternIndex < pattern.Length && pattern[patternIndex] == '!';
        if (negated)
        {
            patternIndex++;
        }

        var matched = false;
        var closed = false;
        while (patternIndex < pattern.Length)
        {
            if (pattern[patternIndex] == ']')
            {
                patternIndex++;
                closed = true;
                break;
            }

            var first = pattern[patternIndex++];
            if (patternIndex + 1 < pattern.Length && pattern[patternIndex] == '-' &&
                pattern[patternIndex + 1] != ']')
            {
                patternIndex++;
                var last = pattern[patternIndex++];
                matched |= InCharacterRange(value, first, last, textCompare);
            }
            else
            {
                matched |= CharactersEqual(value, first, textCompare);
            }
        }

        if (!closed)
        {
            return false;
        }

        return negated ? !matched : matched;
    }

    private static bool InCharacterRange(char value, char first, char last, bool textCompare)
    {
        if (textCompare)
        {
            value = char.ToUpperInvariant(value);
            first = char.ToUpperInvariant(first);
            last = char.ToUpperInvariant(last);
        }

        return first <= value && value <= last;
    }

    private static bool CharactersEqual(char left, char right, bool textCompare) =>
        textCompare
            ? char.ToUpperInvariant(left) == char.ToUpperInvariant(right)
            : left == right;

    /// <summary>Implements the two- to four-argument VB6 InStr form.</summary>
    public static int InStr(int start, string string1, string string2, int compare)
    {
        ArgumentNullException.ThrowIfNull(string1);
        ArgumentNullException.ThrowIfNull(string2);
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 InStr uses one-based start positions.");
        }

        if (start > string1.Length)
        {
            return 0;
        }

        if (string2.Length == 0)
        {
            return start;
        }

        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var index = string1.IndexOf(string2, start - 1, comparison);
        return index < 0 ? 0 : index + 1;
    }

    /// <summary>Implements the two- to four-argument VB6 InStrRev form.</summary>
    public static int InStrRev(string stringCheck, string stringMatch, int start, int compare)
    {
        ArgumentNullException.ThrowIfNull(stringCheck);
        ArgumentNullException.ThrowIfNull(stringMatch);
        if (start == 0 || start < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 InStrRev uses -1 or a positive start position.");
        }

        var searchEnd = start < 0 ? stringCheck.Length : Math.Min(start, stringCheck.Length);
        if (stringMatch.Length == 0)
        {
            return searchEnd;
        }

        if (searchEnd < stringMatch.Length)
        {
            return 0;
        }

        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var index = searchEnd - stringMatch.Length; index >= 0; index--)
        {
            if (stringCheck.AsSpan(index, stringMatch.Length).Equals(stringMatch.AsSpan(), comparison))
            {
                return index + 1;
            }
        }

        return 0;
    }

    /// <summary>Implements VB6 Replace with one-based start and optional replacement count.</summary>
    public static string Replace(string expression, string find, string replacement, int start, int count, int compare)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(find);
        ArgumentNullException.ThrowIfNull(replacement);
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 Replace uses one-based start positions.");
        }

        if (count == 0 || find.Length == 0 || start > expression.Length)
        {
            return expression;
        }

        var prefixLength = start - 1;
        var prefix = expression[..prefixLength];
        var suffix = expression[prefixLength..];
        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var builder = new System.Text.StringBuilder(expression.Length);
        builder.Append(prefix);
        var cursor = 0;
        var replacements = 0;
        while (cursor < suffix.Length)
        {
            var match = suffix.IndexOf(find, cursor, comparison);
            if (match < 0 || count >= 0 && replacements >= count)
            {
                builder.Append(suffix[cursor..]);
                break;
            }

            builder.Append(suffix[cursor..match]);
            builder.Append(replacement);
            cursor = match + find.Length;
            replacements++;
        }

        if (cursor == suffix.Length)
        {
            // The loop consumed the entire suffix through its final match.
            return builder.ToString();
        }

        return builder.ToString();
    }

    /// <summary>Creates a string containing exactly <paramref name="number"/> spaces.</summary>
    public static string Space(int number)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "VB6 Space requires a non-negative count.");
        }

        return new string(' ', number);
    }

    /// <summary>Implements VB6 Split while preserving the zero-based result array.</summary>
    public static VBArray<string> Split(string expression, string delimiter, int limit, int compare)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(delimiter);
        if (limit == 0)
        {
            return new VBArray<string>(new VBArrayBound(0, -1));
        }

        if (delimiter.Length == 0)
        {
            return CreateStringArray(new[] { expression });
        }

        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var values = new List<string>();
        var cursor = 0;
        while (cursor <= expression.Length)
        {
            if (limit > 0 && values.Count == limit - 1)
            {
                values.Add(expression[cursor..]);
                break;
            }

            var index = expression.IndexOf(delimiter, cursor, comparison);
            if (index < 0)
            {
                values.Add(expression[cursor..]);
                break;
            }

            values.Add(expression[cursor..index]);
            cursor = index + delimiter.Length;
        }

        return CreateStringArray(values);
    }

    /// <summary>
    /// Implements the portable StrConv subset used by VB6 source. LCID is accepted for signature
    /// compatibility; the compiler runtime deliberately uses invariant casing.
    /// </summary>
    public static string StrConv(string value, int conversion, int lcid)
    {
        ArgumentNullException.ThrowIfNull(value);
        return conversion switch
        {
            1 => value.ToUpperInvariant(),
            2 => value.ToLowerInvariant(),
            3 => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant()),
            64 or 128 => value,
            _ => throw new NotSupportedException($"VB6 StrConv conversion '{conversion}' is not supported by the portable runtime.")
        };
    }

    private static VBArray<string> CreateStringArray(IReadOnlyList<string> values)
    {
        var array = new VBArray<string>(new VBArrayBound(0, values.Count - 1));
        for (var index = 0; index < values.Count; index++)
        {
            array[index] = values[index];
        }

        return array;
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
