namespace VB6.Runtime;

public static partial class VBOperators
{
    public static bool Equal(object? variant, byte value) => VariantEqualsScalar(variant, value);
    public static bool Equal(byte value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, short value) => VariantEqualsScalar(variant, value);
    public static bool Equal(short value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, int value) => VariantEqualsScalar(variant, value);
    public static bool Equal(int value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, long value) => VariantEqualsScalar(variant, value);
    public static bool Equal(long value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, float value) => VariantEqualsScalar(variant, value);
    public static bool Equal(float value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, double value) => VariantEqualsScalar(variant, value);
    public static bool Equal(double value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, bool value) => VariantEqualsScalar(variant, value);
    public static bool Equal(bool value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, string value) => VariantEqualsScalar(variant, value);
    public static bool Equal(string value, object? variant) => VariantEqualsScalar(variant, value);

    public static bool Equal(object? variant, VBCurrency value) => VariantEqualsScalar(variant, value);
    public static bool Equal(VBCurrency value, object? variant) => VariantEqualsScalar(variant, value);

    private static bool VariantEqualsScalar(object? variant, object scalar)
    {
        if (variant is null)
        {
            return scalar switch
            {
                string text => text.Length == 0,
                bool boolean => !boolean,
                VBCurrency currency => currency.ScaledValue == 0,
                byte value => value == 0,
                short value => value == 0,
                int value => value == 0,
                long value => value == 0,
                float value => value == 0f,
                double value => value == 0d,
                _ => false
            };
        }

        if (scalar is string scalarString)
        {
            return variant is string variantString &&
                   string.Equals(variantString, scalarString, StringComparison.Ordinal);
        }

        if (variant is string)
        {
            return false;
        }

        if (!IsSupportedVariantNumericValue(variant))
        {
            throw new InvalidCastException(
                $"CLR value of type '{variant.GetType().FullName}' is not a supported scalar VB6 Variant value.");
        }

        if (scalar is bool scalarBoolean)
        {
            return ToVariantComparisonDouble(variant) == (scalarBoolean ? -1d : 0d);
        }

        if (variant is bool variantBoolean)
        {
            return (variantBoolean ? -1d : 0d) == ToVariantComparisonDouble(scalar);
        }

        if (variant is float or double || scalar is float or double)
        {
            return ToVariantComparisonDouble(variant) == ToVariantComparisonDouble(scalar);
        }

        if (variant is VBCurrency || scalar is VBCurrency)
        {
            return ToVariantComparisonDouble(variant) == ToVariantComparisonDouble(scalar);
        }

        return VBConversions.CLngLng(variant) == VBConversions.CLngLng(scalar);
    }

    private static bool IsSupportedVariantNumericValue(object value) =>
        value is byte or short or int or long or float or double or bool or VBCurrency;

    private static double ToVariantComparisonDouble(object value) => value switch
    {
        bool boolean => boolean ? -1d : 0d,
        VBCurrency currency => currency.ToDouble(),
        _ => VBConversions.CDbl(value)
    };
}
