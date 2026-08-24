using System.Globalization;

namespace VB6.Runtime;

/// <summary>
/// VB6 conversions between strings and numbers use the invariant culture, so a compiled
/// program produces the same values on every machine. Classic VB6 resolved these against the
/// active locale, which would make <c>"2.5" * 2</c> yield 50 under a comma-decimal locale and 5
/// under a point-decimal one; determinism is worth more here than reproducing that. Anything
/// that genuinely needs locale-aware formatting belongs in the later <c>Format$</c> work, where
/// the locale is an explicit input rather than ambient thread state.
/// </summary>
public static class VBConversions
{
    public static byte CByte(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            var missingErrorNumber = VBVariants.MissingErrorNumber;
            return checked((byte)missingErrorNumber);
        }

        if (value is VBErrorValue error)
        {
            return checked((byte)error.Code);
        }

        if (value is IntPtr pointer)
        {
            return checked((byte)pointer.ToInt64());
        }

        if (value is VBCurrency currency)
        {
            return checked((byte)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? boolean ? byte.MaxValue : byte.MinValue
            : Convert.ToByte(value, CultureInfo.InvariantCulture);
    }

    public static short CInt(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return checked((short)VBVariants.MissingErrorNumber);
        }

        if (value is VBErrorValue error)
        {
            return checked((short)error.Code);
        }

        if (value is IntPtr pointer)
        {
            return checked((short)pointer.ToInt64());
        }

        if (value is VBCurrency currency)
        {
            return checked((short)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? (short)(boolean ? -1 : 0)
            : Convert.ToInt16(value, CultureInfo.InvariantCulture);
    }

    public static int CLng(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return error.Code;
        }

        if (value is IntPtr pointer)
        {
            return checked((int)pointer.ToInt64());
        }

        if (value is VBCurrency currency)
        {
            return checked((int)currency.ToRoundedInt64());
        }

        return value is bool boolean
            ? boolean ? -1 : 0
            : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public static long CLngLng(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return error.Code;
        }

        if (value is IntPtr pointer)
        {
            return pointer.ToInt64();
        }

        if (value is VBCurrency currency)
        {
            return currency.ToRoundedInt64();
        }

        return value is bool boolean
            ? boolean ? -1L : 0L
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public static IntPtr CLngPtr(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return new IntPtr(VBVariants.MissingErrorNumber);
        }

        if (value is VBErrorValue error)
        {
            return new IntPtr(error.Code);
        }

        if (value is IntPtr pointer)
        {
            return pointer;
        }

        var numeric = value is bool boolean
            ? boolean ? -1L : 0L
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        return new IntPtr(numeric);
    }

    public static ushort CUShort(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return checked((ushort)VBVariants.MissingErrorNumber);
        }

        if (value is VBErrorValue error)
        {
            return checked((ushort)error.Code);
        }

        if (value is ushort number)
        {
            return number;
        }

        if (value is IntPtr pointer)
        {
            return checked((ushort)pointer.ToInt64());
        }

        if (value is bool boolean)
        {
            return boolean ? ushort.MaxValue : (ushort)0;
        }

        if (value is VBCurrency currency)
        {
            return checked((ushort)currency.ToRoundedInt64());
        }

        return Convert.ToUInt16(value, CultureInfo.InvariantCulture);
    }

    public static uint CUInt(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return checked((uint)error.Code);
        }

        if (value is uint number)
        {
            return number;
        }

        if (value is IntPtr pointer)
        {
            return checked((uint)pointer.ToInt64());
        }

        if (value is bool boolean)
        {
            return boolean ? uint.MaxValue : 0u;
        }

        if (value is VBCurrency currency)
        {
            return checked((uint)currency.ToRoundedInt64());
        }

        return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
    }

    public static ulong CULng(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return checked((ulong)error.Code);
        }

        if (value is ulong number)
        {
            return number;
        }

        if (value is IntPtr pointer)
        {
            return checked((ulong)pointer.ToInt64());
        }

        if (value is bool boolean)
        {
            return boolean ? ulong.MaxValue : 0UL;
        }

        if (value is VBCurrency currency)
        {
            return checked((ulong)currency.ToRoundedInt64());
        }

        return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
    }

    public static VBCurrency CCur(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBCurrency.FromDecimal(VBVariants.MissingErrorNumber);
        }

        if (value is VBErrorValue error)
        {
            return VBCurrency.FromDecimal(error.Code);
        }

        if (value is IntPtr pointer)
        {
            return VBCurrency.FromDecimal(pointer.ToInt64());
        }

        if (value is VBCurrency currency)
        {
            return currency;
        }

        if (value is bool boolean)
        {
            return VBCurrency.FromScaled(boolean ? -VBCurrency.Scale : 0L);
        }

        var decimalValue = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        return VBCurrency.FromDecimal(decimalValue);
    }

    public static object CDec(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        if (VBVariants.IsMissing(value))
        {
            return (decimal)VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return (decimal)error.Code;
        }

        if (value is VBCurrency currency)
        {
            return currency.ToDecimal();
        }

        if (value is IntPtr pointer)
        {
            return pointer.ToInt64();
        }

        if (value is VBDateValue date)
        {
            return Convert.ToDecimal(date.OADate, CultureInfo.InvariantCulture);
        }

        if (value is bool boolean)
        {
            return boolean ? -1m : 0m;
        }

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
    }

    public static object CVErr(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return new VBErrorValue(
            VBVariants.IsMissing(value)
                ? VBVariants.MissingErrorNumber
                : Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    public static float CSng(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsMissing(value))
        {
            return VBVariants.MissingErrorNumber;
        }

        if (value is VBErrorValue error)
        {
            return CheckSingle(error.Code);
        }

        var result = value switch
        {
            IntPtr pointer => pointer.ToInt64(),
            VBCurrency currency => currency.ToSingle(),
            bool boolean => boolean ? -1f : 0f,
            _ => Convert.ToSingle(value, CultureInfo.InvariantCulture)
        };
        return CheckSingle(result);
    }

    public static double CDbl(object? value) => VBVariantObject.ResolveDefaultValue(value) switch
    {
        _ when VBVariants.IsArray(value) => throw new VB6TypeMismatchException("Array Variant values cannot be converted to Double."),
        VBErrorValue error => error.Code,
        _ when VBVariants.IsMissing(value) => VBVariants.MissingErrorNumber,
        IntPtr pointer => pointer.ToInt64(),
        VBCurrency currency => currency.ToDouble(),
        VBDateValue date => date.OADate,
        bool boolean => boolean ? -1d : 0d,
        _ => Convert.ToDouble(value, CultureInfo.InvariantCulture)
    };

    public static object DateToVariant(double value) => new VBDateValue(value);

    public static object CVDate(object? value) => new VBDateValue(CDate(value));

    public static double CDate(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);
        VBVariants.ThrowIfMissing(value);

        if (value is VBErrorValue error)
        {
            throw new VB6TypeMismatchException($"Cannot convert Error Variant {error.Code} to Date.");
        }

        if (value is VBDateValue date)
        {
            return date.OADate;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToOADate();
        }

        if (value is string text)
        {
            return DateTime.Parse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal)
                .ToOADate();
        }

        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    public static bool CBool(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);
        VBVariants.ThrowIfMissing(value);
        return value switch
        {
            VBErrorValue error => error.Code != 0,
            IntPtr pointer => pointer != IntPtr.Zero,
            VBCurrency currency => currency.ScaledValue != 0,
            _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture)
        };
    }

    public static string CStr(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfArray(value);
        VBVariants.ThrowIfMissing(value);

        if (VBVariants.IsNull(value))
        {
            throw new InvalidCastException("VB6 CStr cannot convert Null to String.");
        }

        return value switch
        {
            VBErrorValue error => $"Error {error.Code}",
            IntPtr pointer => pointer.ToInt64().ToString(CultureInfo.InvariantCulture),
            VBCurrency currency => currency.ToString(),
            VBDateValue date => date.OADate.ToString("G15", CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString("G29", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    public static object? CVar(object? value) => value;

    public static byte ConvertCByte(object? value)
    {
        RejectImplicitError(value, nameof(Byte));
        return CByte(value);
    }

    public static short ConvertCInt(object? value)
    {
        RejectImplicitError(value, nameof(Int16));
        return CInt(value);
    }

    public static int ConvertCLng(object? value)
    {
        RejectImplicitError(value, nameof(Int32));
        return CLng(value);
    }

    public static long ConvertCLngLng(object? value)
    {
        RejectImplicitError(value, nameof(Int64));
        return CLngLng(value);
    }

    public static IntPtr ConvertCLngPtr(object? value)
    {
        RejectImplicitError(value, nameof(IntPtr));
        return CLngPtr(value);
    }

    public static ushort ConvertCUShort(object? value)
    {
        RejectImplicitError(value, nameof(UInt16));
        return CUShort(value);
    }

    public static uint ConvertCUInt(object? value)
    {
        RejectImplicitError(value, nameof(UInt32));
        return CUInt(value);
    }

    public static ulong ConvertCULng(object? value)
    {
        RejectImplicitError(value, nameof(UInt64));
        return CULng(value);
    }

    public static VBCurrency ConvertCCur(object? value)
    {
        RejectImplicitError(value, nameof(VBCurrency));
        return CCur(value);
    }

    public static double ConvertCDate(object? value)
    {
        RejectImplicitError(value, nameof(DateTime));
        return CDate(value);
    }

    public static float ConvertCSng(object? value)
    {
        RejectImplicitError(value, nameof(Single));
        return CSng(value);
    }

    public static double ConvertCDbl(object? value)
    {
        RejectImplicitError(value, nameof(Double));
        return CDbl(value);
    }

    public static bool ConvertCBool(object? value)
    {
        RejectImplicitError(value, nameof(Boolean));
        return CBool(value);
    }

    public static string ConvertCStr(object? value)
    {
        RejectImplicitError(value, nameof(String));
        return CStr(value);
    }

    private static void RejectImplicitError(object? value, string targetType)
    {
        VBVariants.ThrowIfMissing(value);

        if (value is VBErrorValue error)
        {
            throw new VB6TypeMismatchException(
                $"Cannot implicitly convert Error Variant {error.Code} to {targetType}.");
        }
    }

    /// <summary>Implements VB6 Int, including floor semantics for negative fractional values.</summary>
    public static object Int(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);
        RejectImplicitError(value, "Double");

        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        var numeric = value is null ? 0d : VBConversions.CDbl(value);
        return Math.Floor(numeric);
    }

    private static float CheckSingle(float value)
    {
        if (float.IsInfinity(value))
        {
            throw new OverflowException("Value is outside the range of VB6 Single.");
        }

        return value;
    }
}

public static partial class VBOperators
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

    public static IntPtr AddLongPtr(IntPtr left, IntPtr right) => FromLongPtr(checked(left.ToInt64() + right.ToInt64()));

    public static IntPtr SubtractLongPtr(IntPtr left, IntPtr right) => FromLongPtr(checked(left.ToInt64() - right.ToInt64()));

    public static IntPtr MultiplyLongPtr(IntPtr left, IntPtr right) => FromLongPtr(checked(left.ToInt64() * right.ToInt64()));

    public static IntPtr NegateLongPtr(IntPtr value) => FromLongPtr(checked(-value.ToInt64()));

    public static IntPtr IntegerDivideLongPtr(IntPtr left, IntPtr right) => FromLongPtr(checked(left.ToInt64() / right.ToInt64()));

    public static IntPtr ModLongPtr(IntPtr left, IntPtr right) => FromLongPtr(checked(left.ToInt64() % right.ToInt64()));

    public static ushort AddUShort(ushort left, ushort right) => checked((ushort)(left + right));

    public static ushort SubtractUShort(ushort left, ushort right) => checked((ushort)(left - right));

    public static ushort MultiplyUShort(ushort left, ushort right) => checked((ushort)(left * right));

    public static ushort NegateUShort(ushort value) => checked((ushort)(0 - value));

    public static ushort IntegerDivideUShort(ushort left, ushort right) => (ushort)(left / right);

    public static ushort ModUShort(ushort left, ushort right) => (ushort)(left % right);

    public static uint AddUInteger(uint left, uint right) => checked(left + right);

    public static uint SubtractUInteger(uint left, uint right) => checked(left - right);

    public static uint MultiplyUInteger(uint left, uint right) => checked(left * right);

    public static uint NegateUInteger(uint value) => checked(0u - value);

    public static uint IntegerDivideUInteger(uint left, uint right) => left / right;

    public static uint ModUInteger(uint left, uint right) => left % right;

    public static ulong AddULong(ulong left, ulong right) => checked(left + right);

    public static ulong SubtractULong(ulong left, ulong right) => checked(left - right);

    public static ulong MultiplyULong(ulong left, ulong right) => checked(left * right);

    public static ulong NegateULong(ulong value) => checked(0UL - value);

    public static ulong IntegerDivideULong(ulong left, ulong right) => left / right;

    public static ulong ModULong(ulong left, ulong right) => left % right;

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

    public static double Power(double number, double exponent)
    {
        if (number < 0d && exponent != Math.Truncate(exponent))
        {
            throw new ArgumentException("VB6 exponentiation requires an integer exponent for a negative base.", nameof(exponent));
        }

        var result = Math.Pow(number, exponent);
        if (double.IsInfinity(result))
        {
            throw new OverflowException("VB6 exponentiation result is outside the Double range.");
        }

        if (double.IsNaN(result))
        {
            throw new ArithmeticException("VB6 exponentiation produced an invalid numeric result.");
        }

        return result;
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

    public static IntPtr NotLongPtr(IntPtr value) => FromLongPtr(~value.ToInt64());

    public static ushort NotUShort(ushort value) => (ushort)~value;

    public static uint NotUInteger(uint value) => ~value;

    public static ulong NotULong(ulong value) => ~value;

    public static byte AndByte(byte left, byte right) => (byte)(left & right);

    public static short AndInteger(short left, short right) => (short)(left & right);

    public static int AndLong(int left, int right) => left & right;

    public static long AndLongLong(long left, long right) => left & right;

    public static IntPtr AndLongPtr(IntPtr left, IntPtr right) => FromLongPtr(left.ToInt64() & right.ToInt64());

    public static ushort AndUShort(ushort left, ushort right) => (ushort)(left & right);

    public static uint AndUInteger(uint left, uint right) => left & right;

    public static ulong AndULong(ulong left, ulong right) => left & right;

    public static byte OrByte(byte left, byte right) => (byte)(left | right);

    public static short OrInteger(short left, short right) => (short)(left | right);

    public static int OrLong(int left, int right) => left | right;

    public static long OrLongLong(long left, long right) => left | right;

    public static IntPtr OrLongPtr(IntPtr left, IntPtr right) => FromLongPtr(left.ToInt64() | right.ToInt64());

    public static ushort OrUShort(ushort left, ushort right) => (ushort)(left | right);

    public static uint OrUInteger(uint left, uint right) => left | right;

    public static ulong OrULong(ulong left, ulong right) => left | right;

    public static byte XorByte(byte left, byte right) => (byte)(left ^ right);

    public static short XorInteger(short left, short right) => (short)(left ^ right);

    public static int XorLong(int left, int right) => left ^ right;

    public static long XorLongLong(long left, long right) => left ^ right;

    public static IntPtr XorLongPtr(IntPtr left, IntPtr right) => FromLongPtr(left.ToInt64() ^ right.ToInt64());

    public static ushort XorUShort(ushort left, ushort right) => (ushort)(left ^ right);

    public static uint XorUInteger(uint left, uint right) => left ^ right;

    public static ulong XorULong(ulong left, ulong right) => left ^ right;

    public static short EqvInteger(short left, short right) => unchecked((short)~(left ^ right));

    public static int EqvLong(int left, int right) => ~(left ^ right);

    public static long EqvLongLong(long left, long right) => ~(left ^ right);

    public static IntPtr EqvLongPtr(IntPtr left, IntPtr right) => FromLongPtr(~(left.ToInt64() ^ right.ToInt64()));

    public static ushort EqvUShort(ushort left, ushort right) => (ushort)~(left ^ right);

    public static uint EqvUInteger(uint left, uint right) => ~(left ^ right);

    public static ulong EqvULong(ulong left, ulong right) => ~(left ^ right);

    public static short ImpInteger(short left, short right) =>
        unchecked((short)((~left & 0xFFFF) | (right & 0xFFFF)));

    public static int ImpLong(int left, int right) => ~left | right;

    public static long ImpLongLong(long left, long right) => ~left | right;

    public static IntPtr ImpLongPtr(IntPtr left, IntPtr right) => FromLongPtr(~left.ToInt64() | right.ToInt64());

    public static ushort ImpUShort(ushort left, ushort right) => (ushort)(~left | right);

    public static uint ImpUInteger(uint left, uint right) => ~left | right;

    public static ulong ImpULong(ulong left, ulong right) => ~left | right;

    private static IntPtr FromLongPtr(long value) => new(value);

    public static string Concat(object? left, object? right) => VBConversions.CStr(left) + VBConversions.CStr(right);

    public static string ConcatVariant(object? left, object? right)
    {
        left = VBVariantObject.ResolveDefaultValue(left);
        right = VBVariantObject.ResolveDefaultValue(right);
        VBVariants.ThrowIfMissing(left, right);
        VBVariants.ThrowIfArray(left, right);
        ThrowIfErrorOperand(left, right);

        return (VBVariants.IsNull(left) ? string.Empty : VBConversions.CStr(left)) +
            (VBVariants.IsNull(right) ? string.Empty : VBConversions.CStr(right));
    }

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
    /// <summary>
    /// Formats the scalar values accepted by Debug.Print. VB6 reserves one leading column for the
    /// sign of positive numeric values, while strings, Boolean values and Null are printed as-is.
    /// </summary>
    public static string Format(object? value)
    {
        value = VBVariantObject.ResolveDefaultValue(value);
        VBVariants.ThrowIfMissing(value);
        VBVariants.ThrowIfArray(value);

        if (VBVariants.IsNull(value))
        {
            return "Null";
        }

        return value switch
        {
            null => string.Empty,
            VBErrorValue error => $"Error {error.Code}",
            string text => text,
            bool boolean => boolean ? "True" : "False",
            byte number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            short number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            int number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            long number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            ushort number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            uint number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            ulong number => FormatNumeric(number.ToString(CultureInfo.InvariantCulture)),
            IntPtr pointer => FormatNumeric(pointer.ToInt64().ToString(CultureInfo.InvariantCulture)),
            float number => FormatNumeric(number.ToString("G15", CultureInfo.InvariantCulture)),
            double number => FormatNumeric(number.ToString("G15", CultureInfo.InvariantCulture)),
            decimal number => FormatNumeric(number.ToString("G15", CultureInfo.InvariantCulture)),
            VBCurrency currency => FormatNumeric(currency.ToDecimal().ToString("G15", CultureInfo.InvariantCulture)),
            VBDateValue date => FormatNumeric(date.OADate.ToString("G15", CultureInfo.InvariantCulture)),
            _ => VBConversions.CStr(value)
        };
    }

    public static void Print(object? value) => Console.WriteLine(Format(value));

    private static string FormatNumeric(string value) =>
        value.StartsWith("-", StringComparison.Ordinal) ? value : $" {value}";
}
