using System.Globalization;

namespace VB6.Runtime;

/// <summary>Scalar math intrinsics with VB6-compatible Variant preservation where applicable.</summary>
public static class VBMath
{
    private const long RandomModulus = 1L << 24;
    private const long RandomMultiplier = 1140671485L;
    private const long RandomIncrement = 12820163L;
    private const int InitialRandomSeed = 0x50000;
    private static readonly object RandomGate = new();
    private static int randomSeed = InitialRandomSeed;

    /// <summary>
    /// Implements the VB6 24-bit linear-congruential generator. The seed and constants match the
    /// documented VB6 runtime so programs that depend on the legacy sequence remain repeatable.
    /// </summary>
    public static float Rnd() => Rnd(1f);

    public static float Rnd(float number)
    {
        lock (RandomGate)
        {
            var seed = randomSeed;
            if (number != 0f)
            {
                if (number < 0f)
                {
                    var bits = unchecked((uint)BitConverter.SingleToInt32Bits(number));
                    seed = unchecked((int)((bits + (bits >> 24)) & (RandomModulus - 1)));
                }

                seed = unchecked((int)(((long)seed * RandomMultiplier + RandomIncrement) & (RandomModulus - 1)));
            }

            randomSeed = seed;
            return seed / (float)RandomModulus;
        }
    }

    /// <summary>
    /// Seeds the VB6 generator. An omitted Variant argument uses the system timer; a numeric seed
    /// follows the legacy runtime's high-word mixing before the next Rnd call advances the state.
    /// </summary>
    public static void Randomize(object? number)
    {
        lock (RandomGate)
        {
            if (VBVariants.IsMissing(number))
            {
                var timer = (float)(DateTime.Now - DateTime.Today).TotalSeconds;
                randomSeed = MixRandomizeValue(BitConverter.SingleToInt32Bits(timer));
                return;
            }

            var value = VBConversions.CDbl(number);
            var bits = BitConverter.DoubleToInt64Bits(value);
            randomSeed = MixRandomizeValue(unchecked((int)(bits >> 32)));
        }
    }

    private static int MixRandomizeValue(int value)
    {
        var mixed = unchecked(((value & ushort.MaxValue) ^ (value >> 16)) << 8);
        return (randomSeed & ~0x00FFFF00) | mixed;
    }

    public static object? Abs(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return value switch
        {
            null => (short)0,
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
    }

    public static object? Sgn(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        if (value is null)
        {
            return (short)0;
        }

        var number = VBConversions.CDec(value) is decimal decimalValue
            ? decimalValue
            : throw new InvalidCastException("VB6 Sgn received a non-numeric Variant value.");
        return number < 0 ? (short)-1 : number > 0 ? (short)1 : (short)0;
    }

    public static object? Fix(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return value switch
        {
            null => (short)0,
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
    }

    public static object? Round(object? value, short digits)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        if (value is null)
        {
            return (short)0;
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
