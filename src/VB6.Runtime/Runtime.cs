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

    public static VBCurrency CCur(object? value) => VBCurrency.From(value);

    public static float CSng(object? value) => Convert.ToSingle(value, CultureInfo.CurrentCulture);
    public static double CDbl(object? value) => Convert.ToDouble(value, CultureInfo.CurrentCulture);
    public static string CStr(object? value) => Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;

    public static bool CBool(object? value)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        if (value is VBCurrency currency)
        {
            return currency.ScaledValue != 0;
        }

        if (value is null)
        {
            return false;
        }

        if (value is string text)
        {
            if (bool.TryParse(text, out var parsedBoolean))
            {
                return parsedBoolean;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsedNumber))
            {
                return parsedNumber != 0.0;
            }
        }

        return Convert.ToDouble(value, CultureInfo.CurrentCulture) != 0.0;
    }
}

public static partial class VBOperators
{
    public static double Power(double left, double right) => Math.Pow(left, right);

    public static bool Equal(object? left, object? right) => Compare(left, right) == 0;
    public static bool NotEqual(object? left, object? right) => Compare(left, right) != 0;
    public static bool Less(object? left, object? right) => Compare(left, right) < 0;
    public static bool LessOrEqual(object? left, object? right) => Compare(left, right) <= 0;
    public static bool Greater(object? left, object? right) => Compare(left, right) > 0;
    public static bool GreaterOrEqual(object? left, object? right) => Compare(left, right) >= 0;

    public static byte AddByte(byte left, byte right) => checked((byte)(left + right));
    public static short AddInteger(short left, short right) => checked((short)(left + right));
    public static int AddLong(int left, int right) => checked(left + right);
    public static long AddLongLong(long left, long right) => checked(left + right);
    public static VBCurrency AddCurrency(VBCurrency left, VBCurrency right) => VBCurrency.Add(left, right);
    public static float AddSingle(float left, float right) => CheckFinite(left + right);
    public static double AddDouble(double left, double right) => left + right;

    public static byte SubtractByte(byte left, byte right) => checked((byte)(left - right));
    public static short SubtractInteger(short left, short right) => checked((short)(left - right));
    public static int SubtractLong(int left, int right) => checked(left - right);
    public static long SubtractLongLong(long left, long right) => checked(left - right);
    public static VBCurrency SubtractCurrency(VBCurrency left, VBCurrency right) => VBCurrency.Subtract(left, right);
    public static float SubtractSingle(float left, float right) => CheckFinite(left - right);
    public static double SubtractDouble(double left, double right) => left - right;

    public static byte MultiplyByte(byte left, byte right) => checked((byte)(left * right));
    public static short MultiplyInteger(short left, short right) => checked((short)(left * right));
    public static int MultiplyLong(int left, int right) => checked(left * right);
    public static long MultiplyLongLong(long left, long right) => checked(left * right);
    public static VBCurrency MultiplyCurrency(VBCurrency left, VBCurrency right) => VBCurrency.Multiply(left, right);
    public static float MultiplySingle(float left, float right) => CheckFinite(left * right);
    public static double MultiplyDouble(double left, double right) => left * right;

    public static byte IntegerDivideByte(byte left, byte right) => checked((byte)(left / right));
    public static short IntegerDivide(short left, short right) => checked((short)(left / right));
    public static int IntegerDivideLong(int left, int right) => checked(left / right);
    public static long IntegerDivideLongLong(long left, long right) => checked(left / right);

    public static byte ModByte(byte left, byte right) => checked((byte)(left % right));
    public static short ModInteger(short left, short right) => checked((short)(left % right));
    public static int ModLong(int left, int right) => left % right;
    public static long ModLongLong(long left, long right) => left % right;

    public static byte AndByte(byte left, byte right) => (byte)(left & right);
    public static short AndInteger(short left, short right) => (short)(left & right);
    public static int AndLong(int left, int right) => left & right;
    public static long AndLongLong(long left, long right) => left & right;
    public static bool AndBoolean(bool left, bool right) => left & right;

    public static byte OrByte(byte left, byte right) => (byte)(left | right);
    public static short OrInteger(short left, short right) => (short)(left | right);
    public static int OrLong(int left, int right) => left | right;
    public static long OrLongLong(long left, long right) => left | right;
    public static bool OrBoolean(bool left, bool right) => left | right;

    public static byte XorByte(byte left, byte right) => (byte)(left ^ right);
    public static short XorInteger(short left, short right) => (short)(left ^ right);
    public static int XorLong(int left, int right) => left ^ right;
    public static long XorLongLong(long left, long right) => left ^ right;
    public static bool XorBoolean(bool left, bool right) => left ^ right;

    public static byte EqvByte(byte left, byte right) => (byte)~(left ^ right);
    public static short EqvInteger(short left, short right) => (short)~(left ^ right);
    public static int EqvLong(int left, int right) => ~(left ^ right);
    public static long EqvLongLong(long left, long right) => ~(left ^ right);
    public static bool EqvBoolean(bool left, bool right) => left == right;

    public static byte ImpByte(byte left, byte right) => (byte)(~left | right);
    public static short ImpInteger(short left, short right) => (short)(~left | right);
    public static int ImpLong(int left, int right) => ~left | right;
    public static long ImpLongLong(long left, long right) => ~left | right;
    public static bool ImpBoolean(bool left, bool right) => !left || right;

    public static byte NotByte(byte operand) => (byte)~operand;
    public static short NotInteger(short operand) => (short)~operand;
    public static int NotLong(int operand) => ~operand;
    public static long NotLongLong(long operand) => ~operand;
    public static bool NotBoolean(bool operand) => !operand;

    public static byte NegateByte(byte operand) => checked((byte)-operand);
    public static short NegateInteger(short operand) => checked((short)-operand);
    public static int NegateLong(int operand) => checked(-operand);
    public static long NegateLongLong(long operand) => checked(-operand);
    public static VBCurrency NegateCurrency(VBCurrency operand) => VBCurrency.Negate(operand);
    public static float NegateSingle(float operand) => -operand;
    public static double NegateDouble(double operand) => -operand;

    public static float DivideSingle(float left, float right)
    {
        if (right == 0f)
        {
            throw new DivideByZeroException();
        }

        return CheckFinite(left / right);
    }

    public static double DivideDouble(double left, double right)
    {
        if (right == 0d)
        {
            throw new DivideByZeroException();
        }

        return left / right;
    }

    public static string Concat(object? left, object? right) =>
        VBConversions.CStr(left) + VBConversions.CStr(right);

    private static int Compare(object? left, object? right)
    {
        if (left is string || right is string)
        {
            return string.Compare(
                VBConversions.CStr(left),
                VBConversions.CStr(right),
                StringComparison.Ordinal);
        }

        if (left is bool || right is bool)
        {
            return VBConversions.CBool(left).CompareTo(VBConversions.CBool(right));
        }

        if (left is VBCurrency || right is VBCurrency)
        {
            return VBConversions.CCur(left).CompareTo(VBConversions.CCur(right));
        }

        return VBConversions.CDbl(left).CompareTo(VBConversions.CDbl(right));
    }

    private static float CheckFinite(float value)
    {
        if (float.IsInfinity(value))
        {
            throw new OverflowException();
        }

        return value;
    }
}

public static class VBDebug
{
    public static void Print(object? value)
    {
        Console.WriteLine(VBConversions.CStr(value));
    }
}
