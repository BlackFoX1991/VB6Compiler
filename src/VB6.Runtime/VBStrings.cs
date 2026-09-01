using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;

namespace VB6.Runtime;

/// <summary>
/// VB6 string/storage-size intrinsics that are shared by generated code.
/// </summary>
public static class VBStrings
{
    private const int WindowsAnsiCodePage = 1252;
    private static readonly Encoding WindowsAnsiEncoding = CreateWindowsAnsiEncoding();

    private static Encoding CreateWindowsAnsiEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            WindowsAnsiCodePage,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static Encoding GetAnsiEncoding(VBCompatibilityProfile profile)
    {
        if (profile != VBCompatibilityProfile.VB6Sp6)
        {
            return WindowsAnsiEncoding;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            0,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }

    private static bool IsUndefinedWindowsAnsiByte(byte value) =>
        value is 0x81 or 0x8D or 0x8F or 0x90 or 0x9D;

    /// <summary>
    /// Implements the VB6 Len intrinsic for the scalar values currently representable by the runtime.
    /// Strings return their character count; non-string scalar Variants return their VB6 storage size.
    /// The current Variant Empty representation (<see langword="null"/>) has length zero.
    /// </summary>
    public static object Len(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return value switch
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
            DateTime => 8,
            VBCurrency => 8,
            _ when IsGeneratedUserDefinedType(value) => Marshal.SizeOf(value),
            _ => throw new InvalidCastException(
                $"CLR value of type '{value.GetType().FullName}' is not supported by the VB6 Len intrinsic.")
        };
    }

    private static bool IsGeneratedUserDefinedType(object value)
    {
        var type = value.GetType();
        return type.IsValueType &&
               string.Equals(type.Namespace, "VB6.Generated", StringComparison.Ordinal);
    }

    /// <summary>
    /// Implements the VB6 LenB intrinsic. Managed VB6 strings are UTF-16, so each UTF-16 code
    /// unit contributes two bytes. A Null Variant remains Null; generated UDTs use their native
    /// managed layout, including padding between fields.
    /// </summary>
    public static object LenB(object? value)
        => LenB(value, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware LenB using the active Windows ANSI code page for VB6Sp6.</summary>
    public static object LenB(object? value, VBCompatibilityProfile profile)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return value switch
        {
            null => 0,
            string text => profile == VBCompatibilityProfile.Deterministic
                ? checked(text.Length * sizeof(char))
                : GetAnsiEncoding(profile).GetByteCount(text),
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
            DateTime => 8,
            VBCurrency => 8,
            _ when IsGeneratedUserDefinedType(value) => Marshal.SizeOf(value),
            _ => throw new InvalidCastException(
                $"CLR value of type '{value.GetType().FullName}' is not supported by the VB6 LenB intrinsic.")
        };
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
    /// Implements the byte-oriented MidB intrinsic. The requested positions and length are
    /// measured in the active VB6 string encoding rather than UTF-16 characters.
    /// </summary>
    public static string MidB(string value, int start)
        => MidB(value, start, -1, VBCompatibilityProfile.Deterministic);

    public static string MidB(string value, int start, VBCompatibilityProfile profile)
        => MidB(value, start, -1, profile);

    public static string MidB(string value, int start, int length)
        => MidB(value, start, length, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware MidB using the process ANSI code page for VB6Sp6.</summary>
    public static string MidB(
        string value,
        int start,
        int length,
        VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (start < 1 || length < -1)
        {
            throw new ArgumentOutOfRangeException(
                start < 1 ? nameof(start) : nameof(length),
                "VB6 MidB requires a one-based start and a non-negative length.");
        }

        var bytes = EncodeByteString(value, profile);
        if (start > bytes.Length || length == 0)
        {
            return string.Empty;
        }

        var offset = start - 1;
        var count = length < 0 ? bytes.Length - offset : Math.Min(length, bytes.Length - offset);
        return DecodeByteSlice(bytes, offset, Math.Max(0, count), profile);
    }

    /// <summary>
    /// Implements the VB6 Mid statement. The target keeps its original length: replacement
    /// characters are copied from the replacement string, never beyond the target or an explicit
    /// length. A negative length is the internal marker for the omitted length form.
    /// </summary>
    public static string MidAssign(string target, int start, string replacement, int length)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(replacement);
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 Mid assignment uses a one-based start position.");
        }

        if (length < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "VB6 Mid assignment requires a non-negative length.");
        }

        if (start > target.Length || replacement.Length == 0)
        {
            return target;
        }

        var available = target.Length - (start - 1);
        var requested = length < 0 ? replacement.Length : Math.Min(length, replacement.Length);
        var count = Math.Min(available, requested);
        if (count == 0)
        {
            return target;
        }

        var result = target.ToCharArray();
        replacement.AsSpan(0, count).CopyTo(result.AsSpan(start - 1, count));
        return new string(result);
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

    /// <summary>Returns a byte-counted suffix, as documented for RightB.</summary>
    public static string RightB(string value, int length)
        => RightB(value, length, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware RightB using the active VB6 string encoding.</summary>
    public static string RightB(string value, int length, VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "VB6 RightB requires a non-negative length.");
        }

        var bytes = EncodeByteString(value, profile);
        var count = Math.Min(length, bytes.Length);
        return DecodeByteSlice(bytes, bytes.Length - count, count, profile);
    }

    /// <summary>Returns a byte-counted prefix, as documented for LeftB.</summary>
    public static string LeftB(string value, int length)
        => LeftB(value, length, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware LeftB using the active VB6 string encoding.</summary>
    public static string LeftB(string value, int length, VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "VB6 LeftB requires a non-negative length.");
        }

        var bytes = EncodeByteString(value, profile);
        return DecodeByteSlice(bytes, 0, Math.Min(length, bytes.Length), profile);
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

    /// <summary>Returns the first character's Windows-1252 byte value for the VB6 Asc intrinsic.</summary>
    public static int Asc(string value)
        => Asc(value, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware Asc using the active Windows ANSI code page for VB6Sp6.</summary>
    public static int Asc(string value, VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            throw new ArgumentException("VB6 Asc requires a non-empty string.", nameof(value));
        }

        var encoding = GetAnsiEncoding(profile);
        var character = value[0];
        if (character <= 127)
        {
            return character;
        }

        try
        {
            var bytes = encoding.GetBytes(new[] { character });
            if (bytes.Length == 1 && !IsUndefinedWindowsAnsiByte(bytes[0]))
            {
                return bytes[0];
            }

            throw new NotSupportedException(
                $"VB6 Asc cannot represent U+{(int)character:X4} in the active ANSI code page.");
        }
        catch (EncoderFallbackException exception)
        {
            throw new NotSupportedException(
                $"VB6 Asc cannot represent U+{(int)character:X4} in the active ANSI code page.",
                exception);
        }
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
        value = VBVariantObject.ResolveDefaultValue(value);
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
        value = VBVariantObject.ResolveDefaultValue(value);
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

        character = VBVariantObject.ResolveDefaultValue(character);
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
    /// Implements the numeric and string subset of VB6 Format/Format$. The compatibility-free
    /// overload uses invariant culture; the profile-aware overload applies the active culture
    /// for VB6Sp6 numeric separators and localized date names.
    /// </summary>
    public static string FormatValue(
        object? expression,
        string format,
        int firstDayOfWeek,
        int firstWeekOfYear) =>
        FormatValue(
            expression,
            format,
            firstDayOfWeek,
            firstWeekOfYear,
            VBCompatibilityProfile.Deterministic);

    /// <summary>
    /// Profile-aware implementation of VB6 Format/Format$. The SP6 profile uses the active
    /// process culture for numeric separators and localized date names; deterministic callers
    /// retain the invariant behavior of the legacy four-argument overload.
    /// </summary>
    public static string FormatValue(
        object? expression,
        string format,
        int firstDayOfWeek,
        int firstWeekOfYear,
        VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (format.Length > 257)
        {
            format = format[..257];
        }

        _ = firstDayOfWeek;
        _ = firstWeekOfYear;
        expression = VBVariantObject.ResolveDefaultValue(expression);

        if (expression is string text)
        {
            return FormatString(text, format);
        }

        if (VBVariants.IsNull(expression))
        {
            var sections = SplitFormatSections(format, 4);
            return sections.Count == 4
                ? FormatString(string.Empty, sections[3], isNull: true)
                : FormatString(string.Empty, format, isNull: true);
        }

        if (expression is null)
        {
            if (format.Length == 0)
            {
                return string.Empty;
            }

            if (IsNumericFormat(format))
            {
                return FormatNumber((short)0, (short)0, format, profile);
            }

            if (IsDateFormat(format))
            {
                return FormatDate(new VBDateValue(0d), format, firstDayOfWeek, firstWeekOfYear, profile);
            }

            return FormatString(string.Empty, format);
        }

        if (expression is VBDateValue date)
        {
            if (IsNumericFormat(format))
            {
                return FormatNumber(date.OADate, date.OADate, format, profile);
            }

            return FormatDate(date, format, firstDayOfWeek, firstWeekOfYear, profile);
        }

        if (expression is DateTime dateTime)
        {
            var oaDate = dateTime.ToOADate();
            if (IsNumericFormat(format))
            {
                return FormatNumber(oaDate, oaDate, format, profile);
            }

            return FormatDate(
                new VBDateValue(oaDate),
                format,
                firstDayOfWeek,
                firstWeekOfYear,
                profile);
        }

        if (!TryGetFormatNumber(expression, out var number))
        {
            throw new InvalidCastException(
                $"CLR value of type '{expression?.GetType().FullName ?? "null"}' is not supported by the VB6 Format intrinsic.");
        }

        return FormatNumber(number, expression, format, profile);
    }

    /// <summary>Returns the UTF-16 code-unit reversal used by the VB6 StrReverse intrinsic.</summary>
    public static string StrReverse(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var result = expression.ToCharArray();
        Array.Reverse(result);
        return new string(result);
    }

    public static string FormatNumber(
        object? expression,
        int numDigitsAfterDecimal,
        int includeLeadingDigit,
        int useParensForNegativeNumbers,
        int groupDigits,
        VBCompatibilityProfile profile) =>
        FormatStandardNumber(
            expression,
            numDigitsAfterDecimal,
            includeLeadingDigit,
            useParensForNegativeNumbers,
            groupDigits,
            StandardNumberFormat.Number,
            profile);

    public static string FormatCurrency(
        object? expression,
        int numDigitsAfterDecimal,
        int includeLeadingDigit,
        int useParensForNegativeNumbers,
        int groupDigits,
        VBCompatibilityProfile profile) =>
        FormatStandardNumber(
            expression,
            numDigitsAfterDecimal,
            includeLeadingDigit,
            useParensForNegativeNumbers,
            groupDigits,
            StandardNumberFormat.Currency,
            profile);

    public static string FormatPercent(
        object? expression,
        int numDigitsAfterDecimal,
        int includeLeadingDigit,
        int useParensForNegativeNumbers,
        int groupDigits,
        VBCompatibilityProfile profile) =>
        FormatStandardNumber(
            expression,
            numDigitsAfterDecimal,
            includeLeadingDigit,
            useParensForNegativeNumbers,
            groupDigits,
            StandardNumberFormat.Percent,
            profile);

    public static string FormatDateTime(
        object? expression,
        int namedFormat,
        VBCompatibilityProfile profile)
    {
        var format = namedFormat switch
        {
            0 => "General Date",
            1 => "Long Date",
            2 => "Short Date",
            3 => "Long Time",
            4 => "Short Time",
            _ => throw new VB6RuntimeErrorException(5, $"Unsupported FormatDateTime format {namedFormat}.")
        };

        return FormatValue(ToDateValue(expression, profile), format, 0, 0, profile);
    }

    public static string Partition(int number, int start, int stop, int interval)
    {
        if (start < 0 || stop < start || interval < 1)
        {
            throw new VB6RuntimeErrorException(5, "Partition requires non-negative bounds and a positive interval.");
        }

        var width = stop.ToString(CultureInfo.InvariantCulture).Length;
        if (number < start)
        {
            return FormatPartitionRange(null, (long)start - 1, width);
        }

        if (number > stop)
        {
            return FormatPartitionRange((long)stop + 1, null, width);
        }

        var lower = (long)start + (((long)number - start) / interval * interval);
        var upper = Math.Min(lower + interval - 1, stop);
        return FormatPartitionRange(lower, upper, width);
    }

    private enum StandardNumberFormat
    {
        Number,
        Currency,
        Percent
    }

    private static string FormatStandardNumber(
        object? expression,
        int numDigitsAfterDecimal,
        int includeLeadingDigit,
        int useParensForNegativeNumbers,
        int groupDigits,
        StandardNumberFormat format,
        VBCompatibilityProfile profile)
    {
        var number = GetStandardFormatNumber(expression, profile);
        var numberFormat = (NumberFormatInfo)FormatCulture(profile).NumberFormat.Clone();
        if (profile == VBCompatibilityProfile.Deterministic)
        {
            numberFormat.CurrencySymbol = "$";
            numberFormat.PercentPositivePattern = 1;
            numberFormat.PercentNegativePattern = 1;
        }

        var digits = ResolveDecimalDigits(numDigitsAfterDecimal, numberFormat, format);
        var includeLeading = ResolveTriState(includeLeadingDigit, true, nameof(includeLeadingDigit));
        var useParens = ResolveTriState(
            useParensForNegativeNumbers,
            DefaultNegativeParentheses(numberFormat, format),
            nameof(useParensForNegativeNumbers));
        var group = ResolveTriState(groupDigits, true, nameof(groupDigits));
        var formatCharacter = ApplyStandardNumberFormat(numberFormat, digits, group, format);
        var result = number.ToString(formatCharacter + digits.ToString(CultureInfo.InvariantCulture), numberFormat) ?? string.Empty;

        if (useParens)
        {
            result = ParenthesizeNegativeResult(result, numberFormat.NegativeSign);
        }
        else
        {
            result = RemoveParentheses(result, numberFormat.NegativeSign);
        }

        return includeLeading ? result : RemoveLeadingZero(result, numberFormat, format);
    }

    private static IFormattable GetStandardFormatNumber(object? expression, VBCompatibilityProfile profile)
    {
        expression = VBVariantObject.ResolveDefaultValue(expression);
        VBVariants.ThrowIfMissing(expression);
        VBVariants.ThrowIfArray(expression);
        VBVariants.ThrowIfNull(expression);

        if (expression is null)
        {
            return (short)0;
        }

        if (TryGetFormatNumber(expression, out var number))
        {
            return number;
        }

        if (expression is string text && decimal.TryParse(
                text.Trim(),
                NumberStyles.Float | NumberStyles.AllowThousands,
                FormatCulture(profile),
                out var parsed))
        {
            return parsed;
        }

        throw new VB6TypeMismatchException("FormatNumber requires a numeric expression.");
    }

    private static int ResolveDecimalDigits(
        int digits,
        NumberFormatInfo numberFormat,
        StandardNumberFormat format)
    {
        if (digits < -1)
        {
            throw new VB6RuntimeErrorException(5, "The decimal-place argument must be -1 or greater.");
        }

        if (digits >= 0)
        {
            return digits;
        }

        return format switch
        {
            StandardNumberFormat.Number => numberFormat.NumberDecimalDigits,
            StandardNumberFormat.Currency => numberFormat.CurrencyDecimalDigits,
            StandardNumberFormat.Percent => numberFormat.PercentDecimalDigits,
            _ => throw new InvalidOperationException()
        };
    }

    private static bool ResolveTriState(int value, bool defaultValue, string parameterName) => value switch
    {
        -2 => defaultValue,
        -1 => true,
        0 => false,
        _ => throw new VB6RuntimeErrorException(5, $"{parameterName} must be vbUseDefault, vbTrue, or vbFalse.")
    };

    private static string ApplyStandardNumberFormat(
        NumberFormatInfo numberFormat,
        int digits,
        bool group,
        StandardNumberFormat format)
    {
        switch (format)
        {
            case StandardNumberFormat.Number:
                numberFormat.NumberDecimalDigits = digits;
                if (!group) numberFormat.NumberGroupSizes = [0];
                return "N";
            case StandardNumberFormat.Currency:
                numberFormat.CurrencyDecimalDigits = digits;
                if (!group) numberFormat.CurrencyGroupSizes = [0];
                return "C";
            case StandardNumberFormat.Percent:
                numberFormat.PercentDecimalDigits = digits;
                if (!group) numberFormat.PercentGroupSizes = [0];
                return "P";
            default:
                throw new InvalidOperationException();
        }
    }

    private static bool DefaultNegativeParentheses(NumberFormatInfo numberFormat, StandardNumberFormat format) => format switch
    {
        StandardNumberFormat.Number => numberFormat.NumberNegativePattern == 0,
        StandardNumberFormat.Currency => numberFormat.CurrencyNegativePattern is 0 or 4 or 14 or 15,
        StandardNumberFormat.Percent => false,
        _ => false
    };

    private static string ParenthesizeNegativeResult(string result, string negativeSign)
    {
        var index = result.IndexOf(negativeSign, StringComparison.Ordinal);
        return index < 0
            ? result
            : "(" + result.Remove(index, negativeSign.Length) + ")";
    }

    private static string RemoveParentheses(string result, string negativeSign)
    {
        if (result.Length >= 2 && result[0] == '(' && result[^1] == ')')
        {
            return negativeSign + result[1..^1];
        }

        return result;
    }

    private static string RemoveLeadingZero(
        string result,
        NumberFormatInfo numberFormat,
        StandardNumberFormat format)
    {
        var separator = format switch
        {
            StandardNumberFormat.Number => numberFormat.NumberDecimalSeparator,
            StandardNumberFormat.Currency => numberFormat.CurrencyDecimalSeparator,
            StandardNumberFormat.Percent => numberFormat.PercentDecimalSeparator,
            _ => string.Empty
        };
        var index = result.IndexOf(separator, StringComparison.Ordinal);
        if (index <= 0 || result[index - 1] != '0' ||
            (index > 1 && char.IsDigit(result[index - 2])))
        {
            return result;
        }

        return result.Remove(index - 1, 1);
    }

    private static VBDateValue ToDateValue(object? value, VBCompatibilityProfile profile)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);
        VBVariants.ThrowIfNull(value);

        return value switch
        {
            null => new VBDateValue(0d),
            VBDateValue date => date,
            DateTime date => new VBDateValue(date.ToOADate()),
            string text when DateTime.TryParse(
                text,
                FormatCulture(profile),
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                out var parsed) => new VBDateValue(parsed.ToOADate()),
            _ => new VBDateValue(VBConversions.CDate(value))
        };
    }

    private static string FormatPartitionRange(long? lower, long? upper, int width) =>
        (lower is null ? new string(' ', width) : lower.Value.ToString(CultureInfo.InvariantCulture).PadLeft(width)) +
        ":" +
        (upper is null ? new string(' ', width) : upper.Value.ToString(CultureInfo.InvariantCulture).PadLeft(width));

    private static string FormatNumber(
        IFormattable number,
        object? originalValue,
        string format,
        VBCompatibilityProfile profile)
    {
        if (format.Length == 0)
        {
            if (originalValue is bool boolean)
            {
                return boolean ? "True" : "False";
            }

            return number.ToString("G29", FormatCulture(profile)) ?? string.Empty;
        }

        var normalizedFormat = format.ToUpperInvariant();
        if (normalizedFormat is "YES/NO" or "TRUE/FALSE" or "ON/OFF")
        {
            var nonZero = VBConversions.CDbl(originalValue) != 0d;
            return normalizedFormat switch
            {
                "YES/NO" => nonZero ? "Yes" : "No",
                "TRUE/FALSE" => nonZero ? "True" : "False",
                _ => nonZero ? "On" : "Off"
            };
        }

        var numericFormat = normalizedFormat switch
        {
            "GENERAL NUMBER" => "G29",
            "CURRENCY" when profile == VBCompatibilityProfile.VB6Sp6 => "C2",
            "CURRENCY" => "$#,##0.00;($#,##0.00)",
            "FIXED" => "0.00",
            "STANDARD" => "#,##0.00",
            "PERCENT" => "0.00%",
            "SCIENTIFIC" => "0.00E+00",
            _ => NormalizeNumericSections(format)
        };

        try
        {
            return number.ToString(numericFormat, FormatCulture(profile)) ?? string.Empty;
        }
        catch (FormatException exception)
        {
            throw new NotSupportedException(
                $"Format mask '{format}' is outside the current numeric Format subset.",
                exception);
        }
    }

    private static string FormatDate(
        VBDateValue value,
        string format,
        int firstDayOfWeek,
        int firstWeekOfYear,
        VBCompatibilityProfile profile)
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
            return date.ToString("G", FormatCulture(profile));
        }

        var namedFormat = format.ToUpperInvariant();
        return namedFormat switch
        {
            "GENERAL DATE" => FormatGeneralDate(date, value.OADate, profile),
            "LONG DATE" => FormatNamedDate(date, "long-date", profile),
            "MEDIUM DATE" => FormatNamedDate(date, "medium-date", profile),
            "SHORT DATE" => FormatNamedDate(date, "short-date", profile),
            "LONG TIME" => FormatNamedDate(date, "long-time", profile),
            "MEDIUM TIME" => FormatNamedDate(date, "medium-time", profile),
            "SHORT TIME" => FormatNamedDate(date, "short-time", profile),
            _ => FormatDateTokens(date, format, firstDayOfWeek, firstWeekOfYear, profile)
        };
    }

    private static string FormatGeneralDate(
        DateTime value,
        double oaDate,
        VBCompatibilityProfile profile)
    {
        var hasDate = Math.Truncate(oaDate) != 0d;
        var hasTime = oaDate != Math.Truncate(oaDate);
        if (!hasDate)
        {
            return FormatNamedDate(value, "long-time", profile);
        }

        if (!hasTime)
        {
            return FormatNamedDate(value, "short-date", profile);
        }

        return FormatNamedDate(value, "short-date", profile) + " " +
               FormatNamedDate(value, "long-time", profile);
    }

    private static string FormatNamedDate(
        DateTime value,
        string namedFormat,
        VBCompatibilityProfile profile)
    {
        var culture = FormatCulture(profile);
        var pattern = namedFormat switch
        {
            "long-date" when profile == VBCompatibilityProfile.VB6Sp6 => culture.DateTimeFormat.LongDatePattern,
            "long-date" => "dddd, dd MMMM yyyy",
            "medium-date" => "dd-MMM-yy",
            "short-date" when profile == VBCompatibilityProfile.VB6Sp6 => culture.DateTimeFormat.ShortDatePattern,
            "short-date" => "yyyy-MM-dd",
            "long-time" when profile == VBCompatibilityProfile.VB6Sp6 => culture.DateTimeFormat.LongTimePattern,
            "long-time" => "HH:mm:ss",
            "medium-time" => "h:mm tt",
            "short-time" => "HH:mm",
            _ => throw new ArgumentOutOfRangeException(nameof(namedFormat), namedFormat, "Unknown named VB6 date format.")
        };
        return value.ToString(pattern, culture);
    }

    private static string FormatDateTokens(
        DateTime value,
        string format,
        int firstDayOfWeek,
        int firstWeekOfYear,
        VBCompatibilityProfile profile)
    {
        var weekStart = ToFirstDayOfWeek(firstDayOfWeek);
        var weekRule = ToCalendarWeekRule(firstWeekOfYear);
        var culture = FormatCulture(profile);
        var result = new System.Text.StringBuilder();
        var hasAmPm = ContainsAmPmToken(format);
        var previousToken = '\0';

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

            if (character == '\\')
            {
                if (index + 1 >= format.Length)
                {
                    throw new NotSupportedException($"Date Format mask '{format}' ends with an escape character.");
                }

                result.Append(format[index + 1]);
                index += 2;
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

            if (format.AsSpan(index).StartsWith("AMPM", StringComparison.OrdinalIgnoreCase))
            {
                result.Append(value.Hour < 12
                    ? culture.DateTimeFormat.AMDesignator
                    : culture.DateTimeFormat.PMDesignator);
                index += "AMPM".Length;
                continue;
            }

            if (character == ':')
            {
                result.Append(culture.DateTimeFormat.TimeSeparator);
                index++;
                continue;
            }

            if (character == '/')
            {
                result.Append(culture.DateTimeFormat.DateSeparator);
                index++;
                continue;
            }

            var token = char.ToLowerInvariant(character);
            if (token is not ('c' or 'y' or 'm' or 'd' or 'h' or 'n' or 's' or 'w' or 'q' or 't'))
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
                case 'c' when count == 1:
                    result.Append(FormatGeneralDate(value, value.ToOADate(), profile));
                    break;
                case 'c':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported general-date token length.");
                case 'y' when count == 1:
                    result.Append(value.DayOfYear.ToString(culture));
                    break;
                case 'y' when count == 2:
                    result.Append((value.Year % 100).ToString("D2", culture));
                    break;
                case 'y' when count == 4:
                    result.Append(value.Year.ToString("D4", culture));
                    break;
                case 'y':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported year token length.");
                case 'w' when count == 1:
                    result.Append(Weekday(value, weekStart).ToString(culture));
                    break;
                case 'w' when count == 2:
                    result.Append(
                        culture.Calendar
                            .GetWeekOfYear(value, weekRule, weekStart)
                            .ToString(culture));
                    break;
                case 'w':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported weekday token length.");
                case 'q' when count == 1:
                    result.Append(((value.Month - 1) / 3 + 1).ToString(culture));
                    break;
                case 'q':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported quarter token length.");
                case 'm' when previousToken == 'h' && count <= 2:
                    result.Append(value.Minute.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 'm' when count == 4:
                    result.Append(value.ToString("MMMM", culture));
                    break;
                case 'm' when count == 3:
                    result.Append(value.ToString("MMM", culture));
                    break;
                case 'm' when count is 1 or 2:
                    result.Append(value.Month.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 'm':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported month token length.");
                case 'd' when count == 6:
                    result.Append(FormatNamedDate(value, "long-date", profile));
                    break;
                case 'd' when count == 5:
                    result.Append(FormatNamedDate(value, "short-date", profile));
                    break;
                case 'd' when count == 4:
                    result.Append(value.ToString("dddd", culture));
                    break;
                case 'd' when count == 3:
                    result.Append(value.ToString("ddd", culture));
                    break;
                case 'd' when count is 1 or 2:
                    result.Append(value.Day.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 'd':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported day token length.");
                case 'h' when count is 1 or 2:
                    var hour = hasAmPm ? value.Hour % 12 : value.Hour;
                    if (hasAmPm && hour == 0)
                    {
                        hour = 12;
                    }

                    result.Append(hour.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 'h':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported hour token length.");
                case 'n' when count is 1 or 2:
                    result.Append(value.Minute.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 'n':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported minute token length.");
                case 's' when count is 1 or 2:
                    result.Append(value.Second.ToString(count == 2 ? "D2" : "D", culture));
                    break;
                case 's':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported second token length.");
                case 't' when count == 5:
                    result.Append(FormatNamedDate(value, "long-time", profile));
                    break;
                case 't':
                    throw new NotSupportedException(
                        $"Date Format mask '{format}' uses an unsupported complete-time token length.");
            }

            previousToken = token;
            index += count;
        }

        return result.ToString();
    }

    private static System.Globalization.CultureInfo FormatCulture(VBCompatibilityProfile profile) =>
        profile == VBCompatibilityProfile.VB6Sp6
            ? System.Globalization.CultureInfo.CurrentCulture
            : System.Globalization.CultureInfo.InvariantCulture;

    private static int Weekday(DateTime value, DayOfWeek firstDayOfWeek) =>
        ((int)value.DayOfWeek - (int)firstDayOfWeek + 7) % 7 + 1;

    /// <summary>
    /// Resolves a VB6 FirstDayOfWeek constant. <c>vbUseSystem</c> (0) deliberately follows the
    /// ambient culture — the caller asked for the system setting. Sanctioned exception to the
    /// invariant-culture rule; see docs/ROADMAP.md and VBDateTime.ResolveFirstDayOfWeek, which
    /// must stay in agreement with this.
    /// </summary>
    private static DayOfWeek ToFirstDayOfWeek(int value) => value switch
    {
        0 => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek,
        1 => DayOfWeek.Sunday,
        2 => DayOfWeek.Monday,
        3 => DayOfWeek.Tuesday,
        4 => DayOfWeek.Wednesday,
        5 => DayOfWeek.Thursday,
        6 => DayOfWeek.Friday,
        7 => DayOfWeek.Saturday,
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "VB6 FirstDayOfWeek must be vbUseSystem or a weekday value from 1 through 7.")
    };

    /// <summary>
    /// Resolves a VB6 FirstWeekOfYear constant. <c>vbUseSystem</c> (0) follows the ambient culture
    /// for the same reason as <see cref="ToFirstDayOfWeek"/>; explicit constants do not.
    /// </summary>
    private static System.Globalization.CalendarWeekRule ToCalendarWeekRule(int value) => value switch
    {
        0 => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule,
        1 => System.Globalization.CalendarWeekRule.FirstDay,
        2 => System.Globalization.CalendarWeekRule.FirstFourDayWeek,
        3 => System.Globalization.CalendarWeekRule.FirstFullWeek,
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "VB6 FirstWeekOfYear must be vbUseSystem or a value from 1 through 3.")
    };

    private static int CountToken(string format, int start, char token)
    {
        var count = 1;
        while (start + count < format.Length &&
               char.ToLowerInvariant(format[start + count]) == char.ToLowerInvariant(token))
        {
            count++;
        }

        return count;
    }

    private static bool ContainsAmPmToken(string format)
    {
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] is '\'' or '"')
            {
                var end = format.IndexOf(format[index], index + 1);
                if (end < 0)
                {
                    return false;
                }

                index = end;
                continue;
            }

            if (format[index] == '\\')
            {
                index++;
                continue;
            }

            if (format.AsSpan(index).StartsWith("AM/PM", StringComparison.OrdinalIgnoreCase) ||
                format.AsSpan(index).StartsWith("A/P", StringComparison.OrdinalIgnoreCase) ||
                format.AsSpan(index).StartsWith("AMPM", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumericFormat(string format)
    {
        if (IsNamedNumericFormat(format))
        {
            return true;
        }

        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] is '\'' or '"')
            {
                var end = format.IndexOf(format[index], index + 1);
                if (end < 0)
                {
                    return false;
                }

                index = end;
                continue;
            }

            if (format[index] == '\\')
            {
                index++;
                continue;
            }

            if (format[index] is '0' or '#' or '%')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDateFormat(string format)
    {
        if (format.ToUpperInvariant() is
            "GENERAL DATE" or "LONG DATE" or "MEDIUM DATE" or "SHORT DATE" or
            "LONG TIME" or "MEDIUM TIME" or "SHORT TIME")
        {
            return true;
        }

        var hasDateToken = false;
        for (var index = 0; index < format.Length; index++)
        {
            if (format[index] is '\'' or '"')
            {
                var end = format.IndexOf(format[index], index + 1);
                if (end < 0)
                {
                    return false;
                }

                index = end;
                continue;
            }

            if (format[index] == '\\')
            {
                index++;
                continue;
            }

            if (format[index] is '@' or '&' or '<' or '>' or '!')
            {
                return false;
            }

            hasDateToken |= char.ToLowerInvariant(format[index]) is
                'c' or 'd' or 'h' or 'm' or 'n' or 'q' or 's' or 't' or 'w' or 'y';
        }

        return hasDateToken;
    }

    private static bool IsNamedNumericFormat(string format) => format.ToUpperInvariant() is
        "GENERAL NUMBER" or "CURRENCY" or "FIXED" or "STANDARD" or "PERCENT" or "SCIENTIFIC" or
        "YES/NO" or "TRUE/FALSE" or "ON/OFF";

    private static string NormalizeNumericSections(string format)
    {
        var sections = SplitFormatSections(format, 4);
        return sections.Count == 4
            ? string.Join(';', sections.Take(3))
            : format;
    }

    private static IReadOnlyList<string> SplitFormatSections(string format, int maximumSections)
    {
        var sections = new List<string>();
        var section = new StringBuilder();
        var quote = '\0';
        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '\\' && index + 1 < format.Length)
            {
                section.Append(character);
                section.Append(format[++index]);
                continue;
            }

            if (character is '\'' or '"')
            {
                if (quote == '\0')
                {
                    quote = character;
                }
                else if (quote == character)
                {
                    quote = '\0';
                }

                section.Append(character);
                continue;
            }

            if (character == ';' && quote == '\0' && sections.Count < maximumSections - 1)
            {
                sections.Add(section.ToString());
                section.Clear();
                continue;
            }

            section.Append(character);
        }

        sections.Add(section.ToString());
        return sections;
    }

    private static string FormatString(string value, string format, bool isNull = false)
    {
        if (format.Length == 0)
        {
            return isNull ? "Null" : value;
        }

        var sections = SplitFormatSections(format, 2);
        var selectedFormat = value.Length == 0 || isNull
            ? sections.Count == 2 ? sections[1] : sections[0]
            : sections[0];
        var caseMode = 0;
        var leftToRight = false;
        var tokens = new List<(char Character, char Placeholder)>();
        for (var index = 0; index < selectedFormat.Length; index++)
        {
            var character = selectedFormat[index];
            if (character is '\'' or '"')
            {
                var end = selectedFormat.IndexOf(character, index + 1);
                if (end < 0)
                {
                    throw new NotSupportedException($"String Format mask '{format}' has an unterminated literal.");
                }

                for (var literalIndex = index + 1; literalIndex < end; literalIndex++)
                {
                    tokens.Add((selectedFormat[literalIndex], '\0'));
                }

                index = end;
                continue;
            }

            if (character == '\\')
            {
                if (index + 1 >= selectedFormat.Length)
                {
                    throw new NotSupportedException($"String Format mask '{format}' ends with an escape character.");
                }

                tokens.Add((selectedFormat[++index], '\0'));
                continue;
            }

            switch (character)
            {
                case '<':
                    caseMode = -1;
                    break;
                case '>':
                    caseMode = 1;
                    break;
                case '!':
                    leftToRight = true;
                    break;
                case '@':
                case '&':
                    tokens.Add(('\0', character));
                    break;
                default:
                    tokens.Add((character, '\0'));
                    break;
            }
        }

        var placeholderCount = tokens.Count(token => token.Placeholder != '\0');
        if (placeholderCount == 0)
        {
            var literal = tokens.Count == 0
                ? value
                : new string(tokens.Select(token => token.Character).ToArray());
            return ApplyStringCase(literal, caseMode < 0, caseMode > 0);
        }

        var characters = value.ToCharArray();
        var nextCharacter = leftToRight ? 0 : characters.Length - 1;
        var step = leftToRight ? 1 : -1;
        var placeholderPositions = tokens
            .Select((token, index) => (token, index))
            .Where(entry => entry.token.Placeholder != '\0')
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
                : tokens[position].Placeholder == '@' ? ' ' : null;
            if (hasCharacter)
            {
                nextCharacter += step;
            }
        }

        var result = new StringBuilder(tokens.Count);
        for (var position = 0; position < tokens.Count; position++)
        {
            var token = tokens[position];
            if (token.Placeholder == '\0')
            {
                result.Append(token.Character);
                continue;
            }

            if (replacements[position] is char replacement)
            {
                result.Append(replacement);
            }
        }

        return ApplyStringCase(result.ToString(), caseMode < 0, caseMode > 0);
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
            case bool booleanValue:
                number = booleanValue ? (short)-1 : (short)0;
                return true;
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
    public static bool IsNumeric(object? value) =>
        IsNumeric(value, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware numeric predicate using the selected decimal/thousands separators.</summary>
    public static bool IsNumeric(object? value, VBCompatibilityProfile compatibilityProfile)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        return value switch
        {
            null => false,
            bool => true,
            byte or short or int or long or float or double or decimal or VBCurrency => true,
            string text => double.TryParse(
                text.Trim(),
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                FormatCulture(compatibilityProfile),
                out _),
            _ => false
        };
    }

    /// <summary>
    /// Implements VB6 Like for the language wildcard subset: <c>?</c> matches one character,
    /// <c>*</c> matches zero or more, <c>#</c> matches an ASCII digit, and bracket character
    /// lists support negation and inclusive ranges. Matching is ordinal by default and
    /// case-insensitive for <c>Option Compare Text</c>.
    /// </summary>
    public static bool Like(object? value, object? pattern, bool textCompare)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        pattern = VBVariantObject.ResolveDefaultValue(pattern);
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

    /// <summary>
    /// Implements InStrB. The returned position is one-based and counts encoded bytes; textual
    /// comparisons still use the same ordinal, case-insensitive rule as InStr.
    /// </summary>
    public static int InStrB(
        int start,
        string string1,
        string string2,
        int compare,
        VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(string1);
        ArgumentNullException.ThrowIfNull(string2);
        if (start < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "VB6 InStrB uses one-based byte positions.");
        }

        var source = EncodeByteString(string1, profile);
        var match = EncodeByteString(string2, profile);
        if (start > source.Length)
        {
            return 0;
        }

        if (match.Length == 0)
        {
            return start;
        }

        if (compare != 1)
        {
            var index = IndexOfBytes(source, match, start - 1);
            return index < 0 ? 0 : index + 1;
        }

        // Text comparison is defined over characters, but the result remains a byte position.
        // Decode the source and calculate the returned offset from the same profile encoding.
        var sourceText = DecodeByteSlice(source, 0, source.Length, profile);
        var matchText = DecodeByteSlice(match, 0, match.Length, profile);
        var prefixText = DecodeByteSlice(source, 0, Math.Min(start - 1, source.Length), profile);
        var charStart = prefixText.Length;
        var charIndex = sourceText.IndexOf(matchText, charStart, StringComparison.OrdinalIgnoreCase);
        return charIndex < 0
            ? 0
            : checked(EncodeByteString(sourceText[..charIndex], profile).Length + 1);
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

    /// <summary>Implements VB6 StrComp with binary or ordinal text comparison.</summary>
    public static int StrComp(string string1, string string2, int compare)
    {
        ArgumentNullException.ThrowIfNull(string1);
        ArgumentNullException.ThrowIfNull(string2);

        var comparison = compare == 1 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Math.Sign(string.Compare(string1, string2, comparison));
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
    /// Implements the portable StrConv subset used by VB6 source. The existing overload keeps
    /// deterministic invariant casing for callers that do not select a compatibility profile.
    /// </summary>
    public static string StrConv(string value, int conversion, int lcid)
        => StrConv(value, conversion, lcid, VBCompatibilityProfile.Deterministic);

    /// <summary>
    /// Implements the profile-aware StrConv contract for casing, East-Asian width and Japanese
    /// kana conversions. The implementation stays Unicode-native: the active profile culture is
    /// used for casing and also decides whether the locale-gated width/kana flags are valid.
    /// </summary>
    public static string StrConv(
        string value,
        int conversion,
        int lcid,
        VBCompatibilityProfile compatibilityProfile)
    {
        ArgumentNullException.ThrowIfNull(value);
        var culture = compatibilityProfile == VBCompatibilityProfile.VB6Sp6
            ? ResolveStrConvCulture(lcid)
            : System.Globalization.CultureInfo.InvariantCulture;
        const int upperCase = 1;
        const int lowerCase = 2;
        const int wide = 4;
        const int narrow = 8;
        const int katakana = 16;
        const int hiragana = 32;
        const int unicode = 64;
        const int fromUnicode = 128;
        const int supportedFlags = upperCase | lowerCase | wide | narrow | katakana | hiragana | unicode | fromUnicode;

        if (conversion <= 0 || (conversion & ~supportedFlags) != 0)
        {
            throw new ArgumentException($"VB6 StrConv conversion '{conversion}' is invalid.", nameof(conversion));
        }

        if ((conversion & wide) != 0 && (conversion & narrow) != 0)
        {
            throw new ArgumentException("VB6 StrConv cannot combine vbWide and vbNarrow.", nameof(conversion));
        }

        if ((conversion & katakana) != 0 && (conversion & hiragana) != 0)
        {
            throw new ArgumentException("VB6 StrConv cannot combine vbKatakana and vbHiragana.", nameof(conversion));
        }

        if ((conversion & unicode) != 0 && (conversion & fromUnicode) != 0)
        {
            throw new ArgumentException("VB6 StrConv cannot combine vbUnicode and vbFromUnicode.", nameof(conversion));
        }

        var casing = conversion & (upperCase | lowerCase);
        var result = casing switch
        {
            upperCase => value.ToUpper(culture),
            lowerCase => value.ToLower(culture),
            upperCase | lowerCase => culture.TextInfo.ToTitleCase(value.ToLower(culture)),
            _ => value
        };

        var isEastAsian = culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ||
            culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ||
            culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if ((conversion & (wide | narrow)) != 0 && !isEastAsian)
        {
            throw new InvalidOperationException(
                "VB6 StrConv vbWide/vbNarrow requires an East-Asian compatibility locale.");
        }

        var isJapanese = culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        if ((conversion & (katakana | hiragana)) != 0 && !isJapanese)
        {
            throw new InvalidOperationException(
                "VB6 StrConv vbKatakana/vbHiragana requires a Japanese compatibility locale.");
        }

        if ((conversion & wide) != 0)
        {
            result = ToWide(result);
        }
        else if ((conversion & narrow) != 0)
        {
            result = ToNarrow(result);
        }

        if ((conversion & katakana) != 0)
        {
            result = ToKatakana(result);
        }
        else if ((conversion & hiragana) != 0)
        {
            result = ToHiragana(result);
        }

        // vbUnicode/vbFromUnicode are byte-array conversions in the original runtime. The
        // managed intrinsic has a String return type, so the Unicode text itself is preserved;
        // the profile-aware ANSI byte contract is exposed by LenB/Asc/Chr instead.
        return result;
    }

    private static System.Globalization.CultureInfo ResolveStrConvCulture(int lcid)
    {
        if (lcid == 0)
        {
            return System.Globalization.CultureInfo.CurrentCulture;
        }

        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(lcid);
        }
        catch (System.Globalization.CultureNotFoundException exception)
        {
            throw new ArgumentException($"VB6 StrConv LCID '{lcid}' is not installed.", nameof(lcid), exception);
        }
    }

    private static string ToWide(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '\u0021' and <= '\u007e')
            {
                builder.Append((char)(character + 0xfee0));
            }
            else if (character == '\u0020')
            {
                builder.Append('\u3000');
            }
            else if (character is >= '\uff61' and <= '\uff9f')
            {
                builder.Append(character.ToString().Normalize(System.Text.NormalizationForm.FormKC));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    private static string ToNarrow(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is >= '\uff01' and <= '\uff5e')
            {
                builder.Append((char)(character - 0xfee0));
            }
            else if (character == '\u3000')
            {
                builder.Append(' ');
            }
            else if (FullWidthKatakana.TryGetValue(character, out var halfWidth))
            {
                builder.Append(halfWidth);
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static readonly IReadOnlyDictionary<char, string> FullWidthKatakana =
        new Dictionary<char, string>
        {
            ['。'] = "｡", ['「'] = "｢", ['」'] = "｣", ['、'] = "､", ['・'] = "･",
            ['ァ'] = "ｧ", ['ア'] = "ｱ", ['ィ'] = "ｨ", ['イ'] = "ｲ", ['ゥ'] = "ｩ",
            ['ウ'] = "ｳ", ['ェ'] = "ｪ", ['エ'] = "ｴ", ['ォ'] = "ｫ", ['オ'] = "ｵ",
            ['カ'] = "ｶ", ['ガ'] = "ｶﾞ", ['キ'] = "ｷ", ['ギ'] = "ｷﾞ", ['ク'] = "ｸ",
            ['グ'] = "ｸﾞ", ['ケ'] = "ｹ", ['ゲ'] = "ｹﾞ", ['コ'] = "ｺ", ['ゴ'] = "ｺﾞ",
            ['サ'] = "ｻ", ['ザ'] = "ｻﾞ", ['シ'] = "ｼ", ['ジ'] = "ｼﾞ", ['ス'] = "ｽ",
            ['ズ'] = "ｽﾞ", ['セ'] = "ｾ", ['ゼ'] = "ｾﾞ", ['ソ'] = "ｿ", ['ゾ'] = "ｿﾞ",
            ['タ'] = "ﾀ", ['ダ'] = "ﾀﾞ", ['チ'] = "ﾁ", ['ヂ'] = "ﾁﾞ", ['ッ'] = "ｯ",
            ['ツ'] = "ﾂ", ['ヅ'] = "ﾂﾞ", ['テ'] = "ﾃ", ['デ'] = "ﾃﾞ", ['ト'] = "ﾄ",
            ['ド'] = "ﾄﾞ", ['ナ'] = "ﾅ", ['ニ'] = "ﾆ", ['ヌ'] = "ﾇ", ['ネ'] = "ﾈ",
            ['ノ'] = "ﾉ", ['ハ'] = "ﾊ", ['バ'] = "ﾊﾞ", ['パ'] = "ﾊﾟ", ['ヒ'] = "ﾋ",
            ['ビ'] = "ﾋﾞ", ['ピ'] = "ﾋﾟ", ['フ'] = "ﾌ", ['ブ'] = "ﾌﾞ", ['プ'] = "ﾌﾟ",
            ['ヘ'] = "ﾍ", ['ベ'] = "ﾍﾞ", ['ペ'] = "ﾍﾟ", ['ホ'] = "ﾎ", ['ボ'] = "ﾎﾞ",
            ['ポ'] = "ﾎﾟ", ['マ'] = "ﾏ", ['ミ'] = "ﾐ", ['ム'] = "ﾑ", ['メ'] = "ﾒ",
            ['モ'] = "ﾓ", ['ャ'] = "ｬ", ['ヤ'] = "ﾔ", ['ュ'] = "ｭ", ['ユ'] = "ﾕ",
            ['ョ'] = "ｮ", ['ヨ'] = "ﾖ", ['ラ'] = "ﾗ", ['リ'] = "ﾘ", ['ル'] = "ﾙ",
            ['レ'] = "ﾚ", ['ロ'] = "ﾛ", ['ヮ'] = "ﾜ", ['ワ'] = "ﾜ", ['ヰ'] = "ｲ",
            ['ヱ'] = "ｴ", ['ヲ'] = "ｦ", ['ン'] = "ﾝ", ['ヴ'] = "ｳﾞ", ['ヵ'] = "ｶ",
            ['ヶ'] = "ｹ", ['ー'] = "ｰ"
        };

    private static string ToKatakana(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is >= '\u3041' and <= '\u3096'
                ? (char)(character + 0x60)
                : character);
        }

        return builder.ToString();
    }

    private static string ToHiragana(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is >= '\u30a1' and <= '\u30f6'
                ? (char)(character - 0x60)
                : character);
        }

        return builder.ToString();
    }

    /// <summary>Returns the first byte of the VB6 AscB intrinsic's byte view of the string.</summary>
    public static int AscB(string value) => AscB(value, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware AscB over the same byte view LeftB, MidB and InStrB use.</summary>
    public static int AscB(string value, VBCompatibilityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            throw new ArgumentException("VB6 AscB requires a non-empty string.", nameof(value));
        }

        return EncodeByteString(value, profile)[0];
    }

    /// <summary>Returns the single-byte string of the VB6 ChrB intrinsic.</summary>
    public static string ChrB(int charCode) => ChrB(charCode, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware ChrB producing exactly one byte of the active byte view.</summary>
    public static string ChrB(int charCode, VBCompatibilityProfile profile)
    {
        if (charCode is < 0 or > 255)
        {
            throw new VB6RuntimeErrorException(5, "VB6 ChrB accepts byte values from 0 through 255 only.");
        }

        return DecodeByteSlice([(byte)charCode], 0, 1, profile);
    }

    private static byte[] EncodeByteString(string value, VBCompatibilityProfile profile) =>
        (profile == VBCompatibilityProfile.Deterministic
            ? Encoding.Unicode
            : GetAnsiEncoding(profile)).GetBytes(value);

    private static string DecodeByteSlice(
        byte[] bytes,
        int offset,
        int count,
        VBCompatibilityProfile profile)
    {
        if (count <= 0)
        {
            return string.Empty;
        }

        offset = Math.Clamp(offset, 0, bytes.Length);
        count = Math.Min(count, bytes.Length - offset);
        if (count <= 0)
        {
            return string.Empty;
        }

        if (profile == VBCompatibilityProfile.Deterministic)
        {
            // Deterministic strings are represented as UTF-16. A byte-oriented slice may end
            // halfway through a code unit; preserving the low byte with a zero high byte keeps
            // the operation byte-exact instead of silently dropping the requested byte.
            var slice = new byte[count + (count & 1)];
            Buffer.BlockCopy(bytes, offset, slice, 0, count);
            return Encoding.Unicode.GetString(slice);
        }

        return GetAnsiEncoding(profile).GetString(bytes, offset, count);
    }

    private static int IndexOfBytes(byte[] source, byte[] match, int start)
    {
        if (match.Length == 0)
        {
            return Math.Clamp(start, 0, source.Length);
        }

        var last = source.Length - match.Length;
        for (var index = Math.Max(0, start); index <= last; index++)
        {
            if (source.AsSpan(index, match.Length).SequenceEqual(match))
            {
                return index;
            }
        }

        return -1;
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

    /// <summary>Returns the Windows-1252 character represented by a VB6 Chr code.</summary>
    public static string Chr(int charCode)
        => Chr(charCode, VBCompatibilityProfile.Deterministic);

    /// <summary>Profile-aware Chr using the active Windows ANSI code page for VB6Sp6.</summary>
    public static string Chr(int charCode, VBCompatibilityProfile profile)
    {
        if (charCode is < 0 or > 255)
        {
            throw new NotSupportedException(
                "VB6 Chr accepts Windows-1252 byte values from 0 through 255 only.");
        }

        if (charCode <= 127)
        {
            return ((char)charCode).ToString();
        }

        try
        {
            var byteValue = (byte)charCode;
            if (IsUndefinedWindowsAnsiByte(byteValue))
            {
                throw new NotSupportedException(
                    $"VB6 Chr cannot map byte {charCode} in Windows-1252.");
            }

            return GetAnsiEncoding(profile).GetString(new[] { byteValue });
        }
        catch (DecoderFallbackException exception)
        {
            throw new NotSupportedException(
                $"VB6 Chr cannot map byte {charCode} in Windows-1252.",
                exception);
        }
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
