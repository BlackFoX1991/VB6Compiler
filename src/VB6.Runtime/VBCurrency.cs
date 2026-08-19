using System.Globalization;
using System.Numerics;

namespace VB6.Runtime;

public readonly struct VBCurrency : IComparable, IComparable<VBCurrency>, IEquatable<VBCurrency>
{
    internal const long Scale = 10_000L;

    private VBCurrency(long scaledValue)
    {
        ScaledValue = scaledValue;
    }

    internal long ScaledValue { get; }

    internal static VBCurrency FromScaled(long scaledValue) => new(scaledValue);

    internal static VBCurrency FromDecimal(decimal value)
    {
        var rounded = decimal.Round(value, 4, MidpointRounding.ToEven);
        var scaled = rounded * Scale;
        if (scaled < long.MinValue || scaled > long.MaxValue)
        {
            throw new OverflowException("Value is outside the range of VB6 Currency.");
        }

        return new VBCurrency((long)scaled);
    }

    internal static VBCurrency Multiply(VBCurrency left, VBCurrency right)
    {
        var product = (BigInteger)left.ScaledValue * right.ScaledValue;
        var quotient = BigInteger.DivRem(product, Scale, out var remainder);
        var absoluteRemainder = BigInteger.Abs(remainder);
        var twiceRemainder = absoluteRemainder * 2;

        if (twiceRemainder > Scale || (twiceRemainder == Scale && !quotient.IsEven))
        {
            quotient += product.Sign;
        }

        if (quotient < long.MinValue || quotient > long.MaxValue)
        {
            throw new OverflowException("VB6 Currency multiplication overflowed.");
        }

        return new VBCurrency((long)quotient);
    }

    internal long ToRoundedInt64()
    {
        var quotient = ScaledValue / Scale;
        var remainder = Math.Abs(ScaledValue % Scale);

        if (remainder > Scale / 2 || (remainder == Scale / 2 && (quotient & 1L) != 0))
        {
            quotient = checked(quotient + Math.Sign(ScaledValue));
        }

        return quotient;
    }

    public decimal ToDecimal() => ScaledValue / 10_000m;

    public double ToDouble() => ScaledValue / 10_000d;

    public float ToSingle() => ScaledValue / 10_000f;

    public int CompareTo(VBCurrency other) => ScaledValue.CompareTo(other.ScaledValue);

    public int CompareTo(object? obj) => obj is VBCurrency other
        ? CompareTo(other)
        : throw new ArgumentException("Object must be a VBCurrency value.", nameof(obj));

    public bool Equals(VBCurrency other) => ScaledValue == other.ScaledValue;

    public override bool Equals(object? obj) => obj is VBCurrency other && Equals(other);

    public override int GetHashCode() => ScaledValue.GetHashCode();

    public override string ToString() => ToDecimal().ToString("0.####", CultureInfo.CurrentCulture);
}
