using System.Globalization;
using System.Text;

namespace VB6.Runtime;

/// <summary>
/// How VB6 turns a number into text when no format pattern says otherwise.
///
/// This is the one renderer behind <c>CStr</c>, <c>Format(…, "General Number")</c>,
/// <c>Debug.Print</c>/<c>Print #</c> and <c>Str</c>. They differ only in their culture and in two
/// cosmetic rules that <c>Str</c> adds on top; the digits themselves are decided here, once.
///
/// Measured against VB6 SP6 on 2026-09-11 over the thresholds from 10^-20 to 10^18 in both types,
/// plus non-round mantissas on either side of each boundary. The rule that came out is **not** the
/// one .NET's <c>G</c> specifier implements, and the difference is the whole reason this class
/// exists:
///
/// <list type="bullet">
/// <item>Round to the type's precision -- seven significant digits for a Single, fifteen for a
/// Double -- and drop trailing zeros. Call the result <c>s</c> digits at decimal exponent
/// <c>e</c>.</item>
/// <item>For a value of at least one, write it out while <c>e + 1 &lt;= P</c>; that is, while the
/// integer part fits the precision.</item>
/// <item>Below one, write it out while <c>(-e - 1) + s &lt;= P</c> -- the leading zeros after the
/// separator count against the same budget as the digits.</item>
/// <item>Otherwise use exponential notation with the same digits and an exponent of at least two
/// places, always signed.</item>
/// </list>
///
/// The second rule is where .NET disagrees: <c>G</c> goes exponential as soon as the exponent
/// drops below -5, so <c>CStr(CDbl(0.00001))</c> came out as <c>1E-05</c> where the original writes
/// <c>0,00001</c> -- and the original keeps writing it out all the way down to <c>10^-15</c>. The
/// first rule matters in the other direction: a Single of <c>0.00000123</c> is exponential although
/// its exponent is only -6, because five leading zeros plus three digits exceed the budget of
/// seven. An exponent threshold alone cannot express either case.
///
/// Currency is not part of that: it is a scaled integer with four decimals, so it is exact, never
/// exponential, and its trailing zeros are simply trimmed.
/// </summary>
public static class VBNumberText
{
    /// <summary>Significant digits VB6 shows for a Single.</summary>
    public const int SinglePrecision = 7;

    /// <summary>Significant digits VB6 shows for a Double, a Currency and a Date serial.</summary>
    public const int DoublePrecision = 15;

    /// <summary>Significant digits VB6 shows for the Decimal Variant subtype.</summary>
    public const int DecimalPrecision = 29;

    /// <summary>
    /// The culture a profile renders numbers in. <c>VB6Sp6</c> follows the system LCID like the
    /// original; <c>Deterministic</c> stays invariant by decision.
    /// </summary>
    public static CultureInfo CultureFor(VBCompatibilityProfile profile) =>
        profile == VBCompatibilityProfile.VB6Sp6
            ? CultureInfo.CurrentCulture
            : CultureInfo.InvariantCulture;

    public static string FromSingle(float value, IFormatProvider culture) =>
        FromDouble(value, SinglePrecision, culture);

    public static string FromDouble(double value, IFormatProvider culture) =>
        FromDouble(value, DoublePrecision, culture);

    /// <summary>
    /// Renders a floating-point value with <paramref name="precision"/> significant digits.
    ///
    /// NaN and the infinities pass through .NET's own text: VB6 cannot produce them through
    /// arithmetic -- it raises errors 6 and 11 first -- so there is no original behaviour to match,
    /// and inventing one would be worse than saying what the value is.
    /// </summary>
    public static string FromDouble(double value, int precision, IFormatProvider culture)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        if (value == 0d)
        {
            return "0";
        }

        var negative = value < 0d;
        var (digits, exponent) = Significand(Math.Abs(value), precision);
        var separator = Separator(culture);
        var text = FitsFixedNotation(digits.Length, exponent, precision)
            ? FixedNotation(digits, exponent, separator)
            : ExponentialNotation(digits, exponent, separator);

        return negative ? NegativeSign(culture) + text : text;
    }

    /// <summary>
    /// Renders a Currency. Its four decimals are exact by construction, so there is nothing to
    /// round and nothing that could overflow into exponential notation -- the whole range fits
    /// nineteen digits.
    /// </summary>
    public static string FromCurrency(VBCurrency value, IFormatProvider culture) =>
        FromExactDecimal(value.ToDecimal(), culture);

    /// <summary>Renders the Decimal Variant subtype, which is exact in the same way.</summary>
    public static string FromDecimal(decimal value, IFormatProvider culture) =>
        FromExactDecimal(value, culture);

    private static string FromExactDecimal(decimal value, IFormatProvider culture)
    {
        // 'G29' auf einem Decimal ist verlustfrei und laesst nachlaufende Nullen weg; nur der
        // Trenner haengt an der Kultur.
        var text = value.ToString("G29", CultureInfo.InvariantCulture);
        var separator = Separator(culture);
        return separator == "."
            ? text
            : text.Replace(".", separator, StringComparison.Ordinal);
    }

    /// <summary>
    /// The digits and the decimal exponent of a positive value, rounded to
    /// <paramref name="precision"/> significant digits with trailing zeros removed.
    ///
    /// The round trip goes through the <c>E</c> specifier because it is the only one that rounds to
    /// a *significant* digit count; rounding by hand would have to reimplement the carry, and a
    /// carry is exactly where this gets interesting (1999999999999999 becomes 2E+15, not
    /// 1,99999999999999E+15).
    /// </summary>
    private static (string Digits, int Exponent) Significand(double value, int precision)
    {
        var scientific = value.ToString("E" + (precision - 1).ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture);
        var marker = scientific.IndexOf('E');
        var exponent = int.Parse(
            scientific[(marker + 1)..],
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);

        var mantissa = scientific[..marker].Replace(".", string.Empty, StringComparison.Ordinal);
        var digits = mantissa.TrimEnd('0');
        return (digits.Length == 0 ? "0" : digits, exponent);
    }

    private static bool FitsFixedNotation(int significantDigits, int exponent, int precision) =>
        exponent >= 0
            ? exponent + 1 <= precision
            : -exponent - 1 + significantDigits <= precision;

    private static string FixedNotation(string digits, int exponent, string separator)
    {
        if (exponent < 0)
        {
            return "0" + separator + new string('0', -exponent - 1) + digits;
        }

        var integerLength = exponent + 1;
        if (integerLength >= digits.Length)
        {
            return digits + new string('0', integerLength - digits.Length);
        }

        return digits[..integerLength] + separator + digits[integerLength..];
    }

    private static string ExponentialNotation(string digits, int exponent, string separator)
    {
        var text = new StringBuilder();
        text.Append(digits[0]);
        if (digits.Length > 1)
        {
            text.Append(separator).Append(digits, 1, digits.Length - 1);
        }

        // Immer ein Vorzeichen und mindestens zwei Stellen -- '1E+16', '1E-08', '1E-100'.
        text.Append('E').Append(exponent < 0 ? '-' : '+');
        text.Append(Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static string Separator(IFormatProvider culture) =>
        NumberFormatInfo.GetInstance(culture).NumberDecimalSeparator;

    private static string NegativeSign(IFormatProvider culture) =>
        NumberFormatInfo.GetInstance(culture).NegativeSign;
}
