using System.Globalization;

namespace VB6.Runtime;

/// <summary>Scalar math intrinsics with VB6-compatible Variant preservation where applicable.</summary>
public static class VBMath
{
    public static object? Abs(object? value) => value switch
    {
        null => null,
        byte number => number,
        short number => checked((short)Math.Abs(number)),
        int number => Math.Abs(number),
        long number => Math.Abs(number),
        float number => Math.Abs(number),
        double number => Math.Abs(number),
        decimal number => Math.Abs(number),
        VBCurrency currency => VBCurrency.FromScaled(Math.Abs(currency.ScaledValue)),
        _ => throw Unsupported(value, nameof(Abs))
    };

    public static short Sgn(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return number < 0 ? (short)-1 : number > 0 ? (short)1 : (short)0;
    }

    public static object? Fix(object? value) => value switch
    {
        null => null,
        byte number => number,
        short number => number,
        int number => number,
        long number => number,
        float number => MathF.Truncate(number),
        double number => Math.Truncate(number),
        decimal number => decimal.Truncate(number),
        VBCurrency currency => currency,
        _ => throw Unsupported(value, nameof(Fix))
    };

    public static object? Round(object? value, short digits)
    {
        if (value is null)
        {
            return null;
        }

        var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return decimal.Round(number, digits, MidpointRounding.ToEven);
    }

    public static double Sqr(double value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "VB6 Sqr requires a non-negative value.");
        }

        return Math.Sqrt(value);
    }

    public static double Exp(double value)
    {
        var result = Math.Exp(value);
        return double.IsInfinity(result)
            ? throw new OverflowException("VB6 Exp result is outside the range of Double.")
            : result;
    }

    public static double Log(double value)
    {
        if (value <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "VB6 Log requires a positive value.");
        }

        return Math.Log(value);
    }

    public static double Sin(double value) => Math.Sin(value);

    public static double Cos(double value) => Math.Cos(value);

    public static double Tan(double value) => Math.Tan(value);

    public static double Atn(double value) => Math.Atan(value);

    private static InvalidCastException Unsupported(object value, string function) =>
        new($"VB6 {function} does not support CLR value type '{value.GetType().FullName}'.");
}
