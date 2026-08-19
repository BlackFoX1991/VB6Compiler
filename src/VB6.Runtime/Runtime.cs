using System.Globalization;

namespace VB6.Runtime;

public static class VBConversions
{
    public static byte CByte(object? value)
    {
        if (value is VBCurrency currency)
        {
            return checked((byte)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? boolean ? byte.MaxValue : byte.MinValue
            : Convert.ToByte(value, CultureInfo.CurrentCulture);
    }

    public static short CInt(object? value)
    {
        if (value is VBCurrency currency)
        {
            return checked((short)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? (short)(boolean ? -1 : 0)
            : Convert.ToInt16(value, CultureInfo.CurrentCulture);
    }

    public static int CLng(object? value)
    {
        if (value is VBCurrency currency)
        {
            return checked((int)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? boolean ? -1 : 0
            : Convert.ToInt32(value, CultureInfo.CurrentCulture);
    }

    public static long CLngLng(object? value)
    {
        if (value is VBCurrency currency)
        {
            return currency.ToRoundedInt64();
        }

        return value is bool boolean
            ? boolean ? -1L : 0L
            : Convert.ToInt64(value, CultureInfo.CurrentCulture);
    }

    public static VBCurrency CCur(object? value)
    {
        if (value is VBCurrency currency)
        {
            return currency;
        }

        if (value is bool boolean)
        {
            return VBCurrency.FromScaled(boolean ? -VBCurrency.Scale : 0L);
        }

        var decimalValue = Convert.ToDecimal(value, CultureInfo.CurrentCulture);
        return VBCurrency.FromDecimal(decimalValue);
    }

    public static float CSng(object? value)
    {
        var result = value switch
        {
            VBCurrency currency => currency.ToSingle(),
            bool boolean => boolean ? -1f : 0f,
            _ => Convert.ToSingle(value, CultureInfo.CurrentCulture)
        };
        return CheckSingle(result);
    }

    public static double CDbl(object? value) => value switch
    {
        VBCurrency currency => currency.ToDouble(),
        bool boolean => boolean ? -1d : 0d,
        _ => Convert.ToDouble(value, CultureInfo.CurrentCulture)
    };

    public static bool CBool(object? value) => value is VBCurrency currency
        ? currency.ScaledValue != 0
        : Convert.ToBoolean(value, CultureInfo.CurrentCulture);

    public static string CStr(object? value) => value is VBCurrency currency
        ? currency.ToString()
        : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;

    private static float CheckSingle(float value)
    {
        if (float.IsInfinity(value))
        {
            throw new OverflowException("Value is outside the range of VB6 Single.");
        }

        return value;
    }
}

public static class VBOperators
{
    public static byte AddByte(byte left, byte right) => checked((byte)(left + right));

    public static byte SubtractByte(byte left, byte right) => checked((byte)(left - right));

    public static byte MultiplyByte(byte left, byte right) => checked((byte)(left * right));

    public static byte IntegerDivideByte(byte left, byte right) => checked((byte)(left / right));

    public static byte ModByte(byte left, byte right) => checked((byte)(left % right));

    public static short AddInteger(short left, short right) => checked((short)(left + right));

    public static short SubtractInteger(short left, short right) => checked((short)(left - right));

    public static short MultiplyInteger(short left, short right) => checked((short)(left * right));

    public static short NegateInteger(short value) => checked((short)-value);

    public static short IntegerDivide(short left, short right) => checked((short)(left / right));

    public static short ModInteger(short left, short right) => checked((short)(left % right));

    public static int AddLong(int left, int right) => checked(left + right);

    public static int SubtractLong(int left, int right) => checked(left - right);

    public static int MultiplyLong(int left, int right) => checked(left * right);

    public static int NegateLong(int value) => checked(-value);

    public static int IntegerDivideLong(int left, int right) => checked(left / right);

    public static int ModLong(int left, int right) => checked(left % right);

    public static long AddLongLong(long left, long right) => checked(left + right);

    public static long SubtractLongLong(long left, long right) => checked(left - right);

    public static long MultiplyLongLong(long left, long right) => checked(left * right);

    public static long NegateLongLong(long value) => checked(-value);

    public static long IntegerDivideLongLong(long left, long right) => checked(left / right);

    public static long ModLongLong(long left, long right) => checked(left % right);

    public static VBCurrency AddCurrency(VBCurrency left, VBCurrency right) =>
        VBCurrency.FromScaled(checked(left.ScaledValue + right.ScaledValue));

    public static VBCurrency SubtractCurrency(VBCurrency left, VBCurrency right) =>
        VBCurrency.FromScaled(checked(left.ScaledValue - right.ScaledValue));

    public static VBCurrency MultiplyCurrency(VBCurrency left, VBCurrency right) =>
        VBCurrency.Multiply(left, right);

    public static VBCurrency NegateCurrency(VBCurrency value) =>
        VBCurrency.FromScaled(checked(-value.ScaledValue));

    public static float AddSingle(float left, float right) => CheckSingle(left + right);

    public static float SubtractSingle(float left, float right) => CheckSingle(left - right);

    public static float MultiplySingle(float left, float right) => CheckSingle(left * right);

    public static float NegateSingle(float value) => CheckSingle(-value);

    public static float DivideSingle(float left, float right)
    {
        if (right == 0f)
        {
            if (left == 0f)
            {
                throw new OverflowException("VB6 floating-point division 0 / 0 causes overflow.");
            }

            throw new DivideByZeroException();
        }

        return CheckSingle(left / right);
    }

    public static double AddDouble(double left, double right) => left + right;

    public static double SubtractDouble(double left, double right) => left - right;

    public static double MultiplyDouble(double left, double right) => left * right;

    public static double NegateDouble(double value) => -value;

    public static double DivideDouble(double left, double right)
    {
        if (right == 0d)
        {
            if (left == 0d)
            {
                throw new OverflowException("VB6 floating-point division 0 / 0 causes overflow.");
            }

            throw new DivideByZeroException();
        }

        return left / right;
    }

    public static bool NotBoolean(bool value) => !value;

    public static bool AndBoolean(bool left, bool right) => left & right;

    public static bool OrBoolean(bool left, bool right) => left | right;

    public static bool XorBoolean(bool left, bool right) => left ^ right;

    public static bool EqvBoolean(bool left, bool right) => left == right;

    public static bool ImpBoolean(bool left, bool right) => !left || right;

    public static short NotInteger(short value) => unchecked((short)~value);

    public static int NotLong(int value) => ~value;

    public static long NotLongLong(long value) => ~value;

    public static byte AndByte(byte left, byte right) => (byte)(left & right);

    public static short AndInteger(short left, short right) => (short)(left & right);

    public static int AndLong(int left, int right) => left & right;

    public static long AndLongLong(long left, long right) => left & right;

    public static byte OrByte(byte left, byte right) => (byte)(left | right);

    public static short OrInteger(short left, short right) => (short)(left | right);

    public static int OrLong(int left, int right) => left | right;

    public static long OrLongLong(long left, long right) => left | right;

    public static byte XorByte(byte left, byte right) => (byte)(left ^ right);

    public static short XorInteger(short left, short right) => (short)(left ^ right);

    public static int XorLong(int left, int right) => left ^ right;

    public static long XorLongLong(long left, long right) => left ^ right;

    public static short EqvInteger(short left, short right) => unchecked((short)~(left ^ right));

    public static int EqvLong(int left, int right) => ~(left ^ right);

    public static long EqvLongLong(long left, long right) => ~(left ^ right);

    public static short ImpInteger(short left, short right) =>
        unchecked((short)((~left & 0xFFFF) | (right & 0xFFFF)));

    public static int ImpLong(int left, int right) => ~left | right;

    public static long ImpLongLong(long left, long right) => ~left | right;

    public static string Concat(object? left, object? right) => VBConversions.CStr(left) + VBConversions.CStr(right);

    public static bool Equal(object? left, object? right) => Compare(left, right) == 0;

    public static bool NotEqual(object? left, object? right) => Compare(left, right) != 0;

    public static bool Less(object? left, object? right) => Compare(left, right) < 0;

    public static bool LessOrEqual(object? left, object? right) => Compare(left, right) <= 0;

    public static bool Greater(object? left, object? right) => Compare(left, right) > 0;

    public static bool GreaterOrEqual(object? left, object? right) => Compare(left, right) >= 0;

    private static float CheckSingle(float value)
    {
        if (float.IsInfinity(value))
        {
            throw new OverflowException("Value is outside the range of VB6 Single.");
        }

        return value;
    }

    private static int Compare(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        if (left is string leftString && right is string rightString)
        {
            return string.CompareOrdinal(leftString, rightString);
        }

        if (left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        throw new InvalidOperationException($"Values of type '{left.GetType()}' cannot be compared.");
    }
}

public static class VBDebug
{
    public static void Print(object? value) => Console.WriteLine(value);
}
