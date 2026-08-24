using System.Runtime.InteropServices;
using System.Text;

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
        ushort => 2,
        uint => 4,
        ulong => 8,
        IntPtr => IntPtr.Size,
        float => 4,
        double => 8,
        bool => 2,
        VBDateValue => 8,
        VBCurrency => 8,
        _ when IsGeneratedUserDefinedType(value) => Marshal.SizeOf(value),
        _ => throw new InvalidCastException(
            $"CLR value of type '{value.GetType().FullName}' is not supported by the VB6 Len intrinsic.")
    };

    private static bool IsGeneratedUserDefinedType(object value)
    {
        var type = value.GetType();
        return type.IsValueType &&
               string.Equals(type.Namespace, "VB6.Generated", StringComparison.Ordinal);
    }

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

    /// <summary>Parses the numeric prefix accepted by the VB6 Val intrinsic.</summary>
    public static double Val(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var text = value.TrimStart();
        if (text.Length == 0)
        {
            return 0d;
        }

        var sign = 1;
        var offset = 0;
        if (text[0] is '+' or '-')
        {
            sign = text[0] == '-' ? -1 : 1;
            offset = 1;
        }

        if (text.AsSpan(offset).StartsWith("&H", StringComparison.OrdinalIgnoreCase) ||
            text.AsSpan(offset).StartsWith("&O", StringComparison.OrdinalIgnoreCase))
        {
            var isHex = text[offset + 1] is 'H' or 'h';
            var digits = text.AsSpan(offset + 2);
            var valueDigits = 0;
            var digitCount = 0;
            foreach (var digit in digits)
            {
                var numeric = isHex
                    ? digit switch
                    {
                        >= '0' and <= '9' => digit - '0',
                        >= 'A' and <= 'F' => digit - 'A' + 10,
                        >= 'a' and <= 'f' => digit - 'a' + 10,
                        _ => -1
                    }
                    : digit is >= '0' and <= '7' ? digit - '0' : -1;
                if (numeric < 0)
                {
                    break;
                }

                valueDigits = checked(valueDigits * (isHex ? 16 : 8) + numeric);
                digitCount++;
            }

            return digitCount == 0 ? 0d : sign * valueDigits;
        }

        var end = offset;
        var hasDigits = false;
        var hasDecimalPoint = false;
        while (end < text.Length)
        {
            var character = text[end];
            if (character is >= '0' and <= '9')
            {
                hasDigits = true;
                end++;
                continue;
            }

            if (character == '.' && !hasDecimalPoint)
            {
                hasDecimalPoint = true;
                end++;
                continue;
            }

            break;
        }

        if (!hasDigits)
        {
            return 0d;
        }

        if (end < text.Length && text[end] is 'e' or 'E')
        {
            var exponentEnd = end + 1;
            if (exponentEnd < text.Length && text[exponentEnd] is '+' or '-')
            {
                exponentEnd++;
            }

            var exponentStart = exponentEnd;
            while (exponentEnd < text.Length && text[exponentEnd] is >= '0' and <= '9')
            {
                exponentEnd++;
            }

            if (exponentEnd > exponentStart)
            {
                end = exponentEnd;
            }
        }

        return sign * double.Parse(text[offset..end], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a VB6 numeric value as an uppercase hexadecimal Long.</summary>
    public static string Hex(object? value)
    {
        var number = VBConversions.CLng(value ?? 0);
        return number < 0
            ? unchecked((uint)number).ToString("X8", System.Globalization.CultureInfo.InvariantCulture)
            : number.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Formats a VB6 numeric value as an uppercase octal Variant string.</summary>
    public static object? Oct(object? value)
    {
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        var number = VBConversions.CLng(value ?? 0);
        return number < 0
            ? Convert.ToString(unchecked((long)(uint)number), 8)
            : Convert.ToString(number, 8);
    }

    /// <summary>Formats a numeric value using VB6's leading sign space and invariant decimal point.</summary>
    public static string Str(object? value)
    {
        if (value is not null and not byte and not short and not int and not long and
            not float and not double and not decimal and not VBCurrency and not IntPtr)
        {
            throw new InvalidCastException("VB6 Str requires a numeric value.");
        }

        var text = VBConversions.CStr(value);
        if (text.Length == 0)
        {
            text = "0";
        }

        return text[0] == '-' ? text : " " + text;
    }

    /// <summary>Creates a repeated-character string for the VB6 String intrinsic.</summary>
    public static string String(int number, object? character)
    {
        if (number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "VB6 String requires a non-negative count.");
        }

        var code = character switch
        {
            string text when text.Length > 0 => text[0],
            string => 0,
            _ => VBConversions.CLng(character ?? 0)
        };
        if (code is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(character), "VB6 String character codes must be between 0 and 255.");
        }

        return new string((char)code, number);
    }

    /// <summary>
    /// Implements the deterministic numeric and string subset of VB6 Format/Format$.
    /// Numeric masks use the invariant culture and the .NET custom numeric grammar for the
    /// compatible VB6 placeholders <c>0</c>, <c>#</c>, grouping, decimals, percent and sections.
    /// Date/time masks outside the explicitly supported token subset and locale-dependent named
    /// formats remain intentionally unsupported.
    /// </summary>
    public static string FormatValue(
        object? expression,
        string format,
        int firstDayOfWeek,
        int firstWeekOfYear)
    {
        ArgumentNullException.ThrowIfNull(format);
        _ = firstDayOfWeek;
        _ = firstWeekOfYear;

        if (expression is string text)
        {
            return FormatString(text, format);
        }

        if (VBVariants.IsNull(expression))
        {
            return FormatString(string.Empty, format, isNull: true);
        }

        if (expression is VBDateValue date)
        {
            return FormatDate(date, format);
        }

        if (!TryGetFormatNumber(expression, out var number))
        {
            throw new InvalidCastException(
                $"CLR value of type '{expression?.GetType().FullName ?? "null"}' is not supported by the VB6 Format intrinsic.");
        }

        if (format.Length == 0)
        {
            return number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        var numericFormat = format switch
        {
            "General Number" => "G29",
            "Currency" => "$#,##0.00;($#,##0.00)",
            "Fixed" => "0.00",
            "Standard" => "#,##0.00",
            "Percent" => "0.00%",
            "Scientific" => "0.00E+00",
            _ => format
        };

        try
        {
            return number.ToString(numericFormat, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (FormatException exception)
        {
            throw new NotSupportedException(
                $"Format mask '{format}' is outside the current numeric Format subset.",
                exception);
        }
    }

    private static string FormatDate(VBDateValue value, string format)
    {
        DateTime date;
        try
        {
            date = DateTime.FromOADate(value.OADate);
        }
        catch (ArgumentException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value.OADate,
                "The VB6 Date value is outside the OLE Automation date range.");
        }

        if (format.Length == 0)
        {
            return date.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }

        var namedFormat = format.ToUpperInvariant() switch
        {
            "GENERAL DATE" => "yyyy-mm-dd hh:nn:ss",
            "SHORT DATE" => "yyyy-mm-dd",
            "LONG DATE" => "dddd, dd mmmm yyyy",
            "SHORT TIME" => "hh:nn",
            "LONG TIME" => "hh:nn:ss",
            _ => null
        };
        return FormatDateTokens(date, namedFormat ?? format);
    }

    private static string FormatDateTokens(DateTime value, string format)
    {
        var result = new System.Text.StringBuilder();
        var hasAmPm = format.Contains("AM/PM", StringComparison.OrdinalIgnoreCase) ||
                      format.Contains("A/P", StringComparison.OrdinalIgnoreCase);
        var inTime = false;

        for (var index = 0; index < format.Length;)
        {
            var character = format[index];
            if (character is '\'' or '"')
            {
                var quote = character;
                var end = format.IndexOf(quote, index + 1);
                if (end < 0)
                {
                    throw new NotSupportedException($"Date Format mask '{format}' has an unterminated literal.");
                }

                result.Append(format, index + 1, end - index - 1);
                index = end + 1;
                continue;
            }

            if (format.AsSpan(index).StartsWith("AM/PM", StringComparison.OrdinalIgnoreCase))
            {
                var meridiem = value.Hour < 12 ? "AM" : "PM";
                result.Append(char.IsLower(format[index]) ? meridiem.ToLowerInvariant() : meridiem);
                index += "AM/PM".Length;
                continue;
            }

            if (format.AsSpan(index).StartsWith("A/P", StringComparison.OrdinalIgnoreCase))
            {
                var meridiem = value.Hour < 12 ? "A" : "P";
                result.Append(char.IsLower(format[index]) ? meridiem.ToLowerInvariant() : meridiem);
                index += "A/P".Length;
                continue;
            }

            var token = char.ToLowerInvariant(character);
            if (token is not ('y' or 'm' or 'd' or 'h' or 'n' or 's'))
            {
                if (char.IsLetter(character))
                {
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' is outside the current date/time subset.");
                }

                result.Append(character);
                index++;
                continue;
            }

            var count = CountToken(format, index, character);
            switch (token)
            {
                case 'y':
                    result.Append(count >= 4
                        ? value.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture)
                        : count == 2
                            ? value.Year.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)
                            : value.DayOfYear.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'm' when inTime && count <= 2:
                    result.Append(value.Minute.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'm' when count >= 4:
                    result.Append(value.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'm' when count == 3:
                    result.Append(value.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'm':
                    result.Append(value.Month.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'd' when count >= 4:
                    result.Append(value.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'd' when count == 3:
                    result.Append(value.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'd':
                    result.Append(value.Day.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'h':
                    inTime = true;
                    var hour = hasAmPm ? value.Hour % 12 : value.Hour;
                    if (hasAmPm && hour == 0)
                    {
                        hour = 12;
                    }

                    result.Append(hour.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 'n':
                    inTime = true;
                    result.Append(value.Minute.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case 's':
                    inTime = true;
                    result.Append(value.Second.ToString(count == 2 ? "D2" : "D", System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }

            index += count;
        }

        return result.ToString();
    }

    private static int CountToken(string format, int start, char token)
    {
        var count = 1;
        while (start + count < format.Length && format[start + count] == token)
        {
            count++;
        }

        return count;
    }

    private static string FormatString(string value, string format, bool isNull = false)
    {
        if (format.Length == 0)
        {
            return isNull ? "Null" : value;
        }

        var sections = format.Split(';', 2, StringSplitOptions.None);
        var selectedFormat = value.Length == 0 || isNull
            ? sections.Length == 2 ? sections[1] : sections[0]
            : sections[0];
        var forceLower = selectedFormat.Contains('<');
        var forceUpper = selectedFormat.Contains('>');
        var leftToRight = selectedFormat.Contains('!');
        var pattern = selectedFormat
            .Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Replace("!", string.Empty, StringComparison.Ordinal);
        var placeholderCount = pattern.Count(character => character is '@' or '&');

        if (placeholderCount == 0)
        {
            if (pattern.Length == 0 && (forceLower || forceUpper))
            {
                return forceLower ? value.ToLowerInvariant() : value.ToUpperInvariant();
            }

            if (sections.Length == 2 && (value.Length == 0 || isNull))
            {
                return ApplyStringCase(pattern, forceLower, forceUpper);
            }

            throw new NotSupportedException(
                $"String Format mask '{format}' is outside the current string placeholder subset.");
        }

        var characters = value.ToCharArray();
        var nextCharacter = leftToRight ? 0 : characters.Length - 1;
        var step = leftToRight ? 1 : -1;
        var placeholderPositions = pattern
            .Select((character, index) => (character, index))
            .Where(entry => entry.character is '@' or '&')
            .Select(entry => entry.index)
            .ToArray();
        var replacements = new Dictionary<int, char?>();
        foreach (var position in leftToRight
                     ? placeholderPositions
                     : placeholderPositions.Reverse())
        {
            var hasCharacter = leftToRight
                ? nextCharacter < characters.Length
                : nextCharacter >= 0;
            replacements[position] = hasCharacter
                ? characters[nextCharacter]
                : pattern[position] == '@' ? ' ' : null;
            if (hasCharacter)
            {
                nextCharacter += step;
            }
        }

        var result = new StringBuilder(pattern.Length);
        for (var position = 0; position < pattern.Length; position++)
        {
            var character = pattern[position];
            if (character is not ('@' or '&'))
            {
                result.Append(character);
                continue;
            }

            if (replacements[position] is char replacement)
            {
                result.Append(replacement);
            }
        }

        return ApplyStringCase(result.ToString(), forceLower, forceUpper);
    }

    private static string ApplyStringCase(string value, bool forceLower, bool forceUpper) =>
        forceLower
            ? value.ToLowerInvariant()
            : forceUpper
                ? value.ToUpperInvariant()
                : value;

    private static bool TryGetFormatNumber(object? value, out IFormattable number)
    {
        switch (value)
        {
            case byte numberValue:
                number = numberValue;
                return true;
            case short numberValue:
                number = numberValue;
                return true;
            case int numberValue:
                number = numberValue;
                return true;
            case long numberValue:
                number = numberValue;
                return true;
            case float numberValue:
                number = numberValue;
                return true;
            case double numberValue:
                number = numberValue;
                return true;
            case decimal numberValue:
                number = numberValue;
                return true;
            case VBCurrency currency:
                number = currency.ToDecimal();
                return true;
            default:
                number = 0m;
                return false;
        }
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

    /// <summary>Joins a zero-based VB6 string array with the requested delimiter.</summary>
    public static string Join(VBArray<string> sourceArray, string delimiter)
    {
        ArgumentNullException.ThrowIfNull(sourceArray);
        ArgumentNullException.ThrowIfNull(delimiter);

        if (sourceArray.Length == 0)
        {
            return string.Empty;
        }

        var values = new string[sourceArray.Length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = sourceArray[sourceArray.LBound() + index];
        }

        return string.Join(delimiter, values);
    }

    /// <summary>Filters a string array by substring match while preserving source order.</summary>
    public static VBArray<string> Filter(VBArray<string> sourceArray, string match, bool include, int compare)
    {
        ArgumentNullException.ThrowIfNull(sourceArray);
        ArgumentNullException.ThrowIfNull(match);

        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var values = new List<string>();
        for (var index = 0; index < sourceArray.Length; index++)
        {
            var value = sourceArray[sourceArray.LBound() + index];
            var matches = value.Contains(match, comparison);
            if (matches == include)
            {
                values.Add(value);
            }
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

    /// <summary>Returns the UTF-16 character represented by a VB6 ChrW code.</summary>
    public static string ChrW(int code)
    {
        if (code < short.MinValue || code > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(code), "VB6 ChrW accepts values from -32768 through 65535.");
        }

        return ((char)(ushort)code).ToString();
    }

    /// <summary>Returns the signed UTF-16 code unit of the first character in a string.</summary>
    public static short AscW(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            throw new ArgumentException("VB6 AscW requires a non-empty string.", nameof(value));
        }

        return unchecked((short)value[0]);
    }
}
