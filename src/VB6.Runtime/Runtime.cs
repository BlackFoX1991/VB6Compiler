using System.Globalization;

namespace VB6.Runtime;

public enum VBVariantKind
{
    Empty,
    Null,
    Nothing,
    Missing,
    Error,
    Value
}

public readonly record struct VBVariant(VBVariantKind Kind, object? Value)
{
    public static readonly VBVariant Empty = new(VBVariantKind.Empty, null);
    public static readonly VBVariant Null = new(VBVariantKind.Null, null);
    public static readonly VBVariant Nothing = new(VBVariantKind.Nothing, null);
    public static readonly VBVariant Missing = new(VBVariantKind.Missing, null);

    public static VBVariant FromError(int errorCode) => new(VBVariantKind.Error, errorCode);

    public static VBVariant From(object? value) =>
        value is VBVariant variant ? variant : new VBVariant(VBVariantKind.Value, value);

    public bool IsEmpty => Kind == VBVariantKind.Empty;
    public bool IsNull => Kind == VBVariantKind.Null;
    public bool IsMissing => Kind == VBVariantKind.Missing;
    public bool IsError => Kind == VBVariantKind.Error;

    public object? Unwrap() => Kind == VBVariantKind.Value ? Value : null;

    public string ToDisplayString() => Kind switch
    {
        VBVariantKind.Value => VBConversions.CStr(Value),
        _ => string.Empty
    };

    public override string ToString() => ToDisplayString();

    public static implicit operator VBVariant(byte value) => From(value);
    public static implicit operator VBVariant(short value) => From(value);
    public static implicit operator VBVariant(int value) => From(value);
    public static implicit operator VBVariant(long value) => From(value);
    public static implicit operator VBVariant(float value) => From(value);
    public static implicit operator VBVariant(double value) => From(value);
    public static implicit operator VBVariant(decimal value) => From(value);
    public static implicit operator VBVariant(bool value) => From(value);
    public static implicit operator VBVariant(string value) => From(value);
    public static implicit operator VBVariant(VBCurrency value) => From(value);
}

public static class VBVariantFunctions
{
    public static short VarType(object? value)
    {
        if (value is VBVariant variant)
        {
            return variant.Kind switch
            {
                VBVariantKind.Empty => 0,
                VBVariantKind.Null => 1,
                VBVariantKind.Nothing => 9,
                VBVariantKind.Error => 10,
                VBVariantKind.Missing => 10,
                VBVariantKind.Value => VarType(variant.Value),
                _ => 12
            };
        }

        return value switch
        {
            null => 0,
            short => 2,
            int => 3,
            float => 4,
            double => 5,
            VBCurrency => 6,
            string => 8,
            bool => 11,
            decimal => 14,
            byte => 17,
            long => 20,
            _ => 12
        };
    }

    public static bool IsEmpty(object? value) =>
        value is null || value is VBVariant { Kind: VBVariantKind.Empty };

    public static bool IsNull(object? value) =>
        value is VBVariant { Kind: VBVariantKind.Null };

    public static bool IsError(object? value) =>
        value is VBVariant { Kind: VBVariantKind.Error };

    public static bool IsMissing(object? value) =>
        value is VBVariant { Kind: VBVariantKind.Missing };

    public static VBVariant CVErr(object? errorNumber) =>
        VBVariant.FromError(VBConversions.CLng(errorNumber));

    public static bool IsNumeric(object? value)
    {
        if (value is VBVariant variant)
        {
            value = variant.Unwrap();
        }

        return value is byte or short or int or long or float or double or decimal or VBCurrency;
    }
}

public static class VBVariantOperators
{
    public static VBVariant Add(object? left, object? right)
    {
        if (HasNull(left, right))
        {
            return VBVariant.Null;
        }

        left = Unwrap(left);
        right = Unwrap(right);
        if (left is string || right is string)
        {
            return VBVariant.From(VBConversions.CStr(left) + VBConversions.CStr(right));
        }

        return Numeric(left, right, "Add");
    }

    public static VBVariant Subtract(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : Numeric(Unwrap(left), Unwrap(right), "Subtract");

    public static VBVariant Multiply(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : Numeric(Unwrap(left), Unwrap(right), "Multiply");

    public static VBVariant Divide(object? left, object? right) =>
        HasNull(left, right)
            ? VBVariant.Null
            : DivideNumeric(Unwrap(left), Unwrap(right));

    public static VBVariant IntegerDivide(object? left, object? right) =>
        HasNull(left, right)
            ? VBVariant.Null
            : VBVariant.From(VBOperators.IntegerDivideLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)));

    public static VBVariant Mod(object? left, object? right) =>
        HasNull(left, right)
            ? VBVariant.Null
            : VBVariant.From(VBOperators.ModLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)));

    public static VBVariant Power(object? left, object? right) =>
        HasNull(left, right)
            ? VBVariant.Null
            : VBVariant.From(VBOperators.Power(VBConversions.CDbl(left), VBConversions.CDbl(right)));

    public static VBVariant Concat(object? left, object? right) =>
        HasNull(left, right)
            ? VBVariant.Null
            : VBVariant.From(VBConversions.CStr(left) + VBConversions.CStr(right));

    public static VBVariant Negate(object? value)
    {
        if (IsNull(value))
        {
            return VBVariant.Null;
        }

        value = Unwrap(value);
        return GetNumericKind(value, null) switch
        {
            VariantNumericKind.Currency => VBVariant.From(VBOperators.NegateCurrency(VBConversions.CCur(value))),
            VariantNumericKind.Decimal => VBVariant.From(VBOperators.NegateDecimal(VBConversions.CDec(value))),
            VariantNumericKind.Double => VBVariant.From(VBOperators.NegateDouble(VBConversions.CDbl(value))),
            VariantNumericKind.Single => VBVariant.From(VBOperators.NegateSingle(VBConversions.CSng(value))),
            VariantNumericKind.LongLong => VBVariant.From(VBOperators.NegateLongLong(VBConversions.CLngLng(value))),
            VariantNumericKind.Long => VBVariant.From(VBOperators.NegateLong(VBConversions.CLng(value))),
            _ => VBVariant.From(VBOperators.NegateInteger(VBConversions.CInt(value)))
        };
    }

    public static VBVariant Not(object? value)
    {
        if (IsNull(value))
        {
            return VBVariant.Null;
        }

        value = Unwrap(value);
        if (value is bool)
        {
            return VBVariant.From(VBOperators.NotBoolean(VBConversions.CBool(value)));
        }

        return GetNumericKind(value, null) switch
        {
            VariantNumericKind.LongLong => VBVariant.From(VBOperators.NotLongLong(VBConversions.CLngLng(value))),
            VariantNumericKind.Long or VariantNumericKind.Currency or VariantNumericKind.Decimal or
            VariantNumericKind.Single or VariantNumericKind.Double =>
                VBVariant.From(VBOperators.NotLong(VBConversions.CLng(value))),
            _ => VBVariant.From(VBOperators.NotInteger(VBConversions.CInt(value)))
        };
    }

    public static VBVariant And(object? left, object? right)
    {
        if (TryBooleanLogical(left, right, "And", out var result))
        {
            return result;
        }

        return HasNull(left, right) ? VBVariant.Null : IntegerLogical(Unwrap(left), Unwrap(right), "And");
    }

    public static VBVariant Or(object? left, object? right)
    {
        if (TryBooleanLogical(left, right, "Or", out var result))
        {
            return result;
        }

        return HasNull(left, right) ? VBVariant.Null : IntegerLogical(Unwrap(left), Unwrap(right), "Or");
    }

    public static VBVariant Xor(object? left, object? right)
    {
        if (TryBooleanLogical(left, right, "Xor", out var result))
        {
            return result;
        }

        return HasNull(left, right) ? VBVariant.Null : IntegerLogical(Unwrap(left), Unwrap(right), "Xor");
    }

    public static VBVariant Eqv(object? left, object? right)
    {
        if (TryBooleanLogical(left, right, "Eqv", out var result))
        {
            return result;
        }

        return HasNull(left, right) ? VBVariant.Null : IntegerLogical(Unwrap(left), Unwrap(right), "Eqv");
    }

    public static VBVariant Imp(object? left, object? right)
    {
        if (TryBooleanLogical(left, right, "Imp", out var result))
        {
            return result;
        }

        return HasNull(left, right) ? VBVariant.Null : IntegerLogical(Unwrap(left), Unwrap(right), "Imp");
    }

    public static VBVariant Equal(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) == 0);

    public static VBVariant NotEqual(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) != 0);

    public static VBVariant Less(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) < 0);

    public static VBVariant LessOrEqual(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) <= 0);

    public static VBVariant Greater(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) > 0);

    public static VBVariant GreaterOrEqual(object? left, object? right) =>
        HasNull(left, right) ? VBVariant.Null : VBVariant.From(Compare(Unwrap(left), Unwrap(right)) >= 0);

    private static VBVariant Numeric(object? left, object? right, string operation)
    {
        return GetNumericKind(left, right) switch
        {
            VariantNumericKind.Currency => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
                "Subtract" => VBOperators.SubtractCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
                "Multiply" => VBOperators.MultiplyCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Currency operation '{operation}'.")
            }),
            VariantNumericKind.Decimal => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddDecimal(VBConversions.CDec(left), VBConversions.CDec(right)),
                "Subtract" => VBOperators.SubtractDecimal(VBConversions.CDec(left), VBConversions.CDec(right)),
                "Multiply" => VBOperators.MultiplyDecimal(VBConversions.CDec(left), VBConversions.CDec(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Decimal operation '{operation}'.")
            }),
            VariantNumericKind.Double => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)),
                "Subtract" => VBOperators.SubtractDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)),
                "Multiply" => VBOperators.MultiplyDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Double operation '{operation}'.")
            }),
            VariantNumericKind.Single => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddSingle(VBConversions.CSng(left), VBConversions.CSng(right)),
                "Subtract" => VBOperators.SubtractSingle(VBConversions.CSng(left), VBConversions.CSng(right)),
                "Multiply" => VBOperators.MultiplySingle(VBConversions.CSng(left), VBConversions.CSng(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Single operation '{operation}'.")
            }),
            VariantNumericKind.LongLong => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Subtract" => VBOperators.SubtractLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Multiply" => VBOperators.MultiplyLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant LongLong operation '{operation}'.")
            }),
            VariantNumericKind.Long => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Subtract" => VBOperators.SubtractLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Multiply" => VBOperators.MultiplyLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Long operation '{operation}'.")
            }),
            _ => VBVariant.From(operation switch
            {
                "Add" => VBOperators.AddInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Subtract" => VBOperators.SubtractInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Multiply" => VBOperators.MultiplyInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Integer operation '{operation}'.")
            })
        };
    }

    private static VBVariant DivideNumeric(object? left, object? right)
    {
        return GetNumericKind(left, right) switch
        {
            VariantNumericKind.Decimal => VBVariant.From(VBOperators.DivideDecimal(VBConversions.CDec(left), VBConversions.CDec(right))),
            _ => VBVariant.From(VBOperators.DivideDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)))
        };
    }

    private static VBVariant IntegerLogical(object? left, object? right, string operation)
    {
        return GetIntegerLogicalKind(left, right) switch
        {
            VariantNumericKind.LongLong => VBVariant.From(operation switch
            {
                "And" => VBOperators.AndLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Or" => VBOperators.OrLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Xor" => VBOperators.XorLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Eqv" => VBOperators.EqvLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                "Imp" => VBOperators.ImpLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant LongLong logical operation '{operation}'.")
            }),
            VariantNumericKind.Long => VBVariant.From(operation switch
            {
                "And" => VBOperators.AndLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Or" => VBOperators.OrLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Xor" => VBOperators.XorLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Eqv" => VBOperators.EqvLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                "Imp" => VBOperators.ImpLong(VBConversions.CLng(left), VBConversions.CLng(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Long logical operation '{operation}'.")
            }),
            _ => VBVariant.From(operation switch
            {
                "And" => VBOperators.AndInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Or" => VBOperators.OrInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Xor" => VBOperators.XorInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Eqv" => VBOperators.EqvInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                "Imp" => VBOperators.ImpInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
                _ => throw new InvalidOperationException($"Unsupported Variant Integer logical operation '{operation}'.")
            })
        };
    }

    private static bool TryBooleanLogical(object? left, object? right, string operation, out VBVariant result)
    {
        var leftIsNull = IsNull(left);
        var rightIsNull = IsNull(right);
        left = Unwrap(left);
        right = Unwrap(right);
        if (!leftIsNull && left is null)
        {
            left = false;
        }

        if (!rightIsNull && right is null)
        {
            right = false;
        }

        var leftBoolean = left is bool;
        var rightBoolean = right is bool;

        if (!leftIsNull && !rightIsNull && (!leftBoolean || !rightBoolean))
        {
            result = VBVariant.Empty;
            return false;
        }

        if (!leftIsNull && !leftBoolean)
        {
            result = VBVariant.Empty;
            return false;
        }

        if (!rightIsNull && !rightBoolean)
        {
            result = VBVariant.Empty;
            return false;
        }

        result = operation switch
        {
            "And" => BooleanAnd(leftIsNull ? null : VBConversions.CBool(left), rightIsNull ? null : VBConversions.CBool(right)),
            "Or" => BooleanOr(leftIsNull ? null : VBConversions.CBool(left), rightIsNull ? null : VBConversions.CBool(right)),
            "Xor" => leftIsNull || rightIsNull
                ? VBVariant.Null
                : VBVariant.From(VBOperators.XorBoolean(VBConversions.CBool(left), VBConversions.CBool(right))),
            "Eqv" => leftIsNull || rightIsNull
                ? VBVariant.Null
                : VBVariant.From(VBOperators.EqvBoolean(VBConversions.CBool(left), VBConversions.CBool(right))),
            "Imp" => BooleanImp(leftIsNull ? null : VBConversions.CBool(left), rightIsNull ? null : VBConversions.CBool(right)),
            _ => throw new InvalidOperationException($"Unsupported Variant Boolean logical operation '{operation}'.")
        };
        return true;
    }

    private static VBVariant BooleanAnd(bool? left, bool? right)
    {
        if (left == false || right == false)
        {
            return VBVariant.From(false);
        }

        return left is null || right is null
            ? VBVariant.Null
            : VBVariant.From(true);
    }

    private static VBVariant BooleanOr(bool? left, bool? right)
    {
        if (left == true || right == true)
        {
            return VBVariant.From(true);
        }

        return left is null || right is null
            ? VBVariant.Null
            : VBVariant.From(false);
    }

    private static VBVariant BooleanImp(bool? left, bool? right)
    {
        if (left == false || right == true)
        {
            return VBVariant.From(true);
        }

        return left is null || right is null
            ? VBVariant.Null
            : VBVariant.From(false);
    }

    private static int Compare(object? left, object? right)
    {
        if (IsNumericLike(left) && IsNumericLike(right))
        {
            if (left is decimal or VBCurrency || right is decimal or VBCurrency)
            {
                return VBConversions.CDec(left).CompareTo(VBConversions.CDec(right));
            }

            return VBConversions.CDbl(left).CompareTo(VBConversions.CDbl(right));
        }

        if (left is string || right is string)
        {
            return string.CompareOrdinal(VBConversions.CStr(left), VBConversions.CStr(right));
        }

        return VBOperators.Equal(left, right)
            ? 0
            : throw new InvalidOperationException($"Values of type '{left?.GetType()}' and '{right?.GetType()}' cannot be compared.");
    }

    private static VariantNumericKind GetNumericKind(object? left, object? right)
    {
        if (left is double || right is double)
        {
            return VariantNumericKind.Double;
        }

        if (left is decimal || right is decimal)
        {
            return VariantNumericKind.Decimal;
        }

        if (left is VBCurrency || right is VBCurrency)
        {
            return VariantNumericKind.Currency;
        }

        if (left is float || right is float)
        {
            return VariantNumericKind.Single;
        }

        if (left is long || right is long)
        {
            return VariantNumericKind.LongLong;
        }

        if (left is int || right is int)
        {
            return VariantNumericKind.Long;
        }

        return VariantNumericKind.Integer;
    }

    private static VariantNumericKind GetIntegerLogicalKind(object? left, object? right)
    {
        if (left is long || right is long)
        {
            return VariantNumericKind.LongLong;
        }

        if (left is int || right is int ||
            left is float or double or decimal or VBCurrency ||
            right is float or double or decimal or VBCurrency)
        {
            return VariantNumericKind.Long;
        }

        return VariantNumericKind.Integer;
    }

    private static bool HasNull(object? left, object? right) => IsNull(left) || IsNull(right);

    private static bool IsNull(object? value) => value is VBVariant { Kind: VBVariantKind.Null };

    private static object? Unwrap(object? value) => value is VBVariant variant ? variant.Unwrap() : value;

    private static bool IsNumericLike(object? value) =>
        value is byte or short or int or long or float or double or decimal or VBCurrency or bool;

    private enum VariantNumericKind
    {
        Integer,
        Long,
        LongLong,
        Currency,
        Decimal,
        Single,
        Double
    }
}

public static class VBConversions
{
    public static byte CByte(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

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
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

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
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

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
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

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
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

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

    public static decimal CDec(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

        return value switch
        {
            VBCurrency currency => currency.ToDecimal(),
            bool boolean => boolean ? -1m : 0m,
            _ => Convert.ToDecimal(value, CultureInfo.CurrentCulture)
        };
    }

    public static float CSng(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

        var result = value switch
        {
            VBCurrency currency => currency.ToSingle(),
            bool boolean => boolean ? -1f : 0f,
            _ => Convert.ToSingle(value, CultureInfo.CurrentCulture)
        };
        return CheckSingle(result);
    }

    public static double CDbl(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

        return value switch
        {
            VBCurrency currency => currency.ToDouble(),
            bool boolean => boolean ? -1d : 0d,
            _ => Convert.ToDouble(value, CultureInfo.CurrentCulture)
        };
    }

    public static bool CBool(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            value = variant.Unwrap();
        }

        return value is VBCurrency currency
            ? currency.ScaledValue != 0
            : Convert.ToBoolean(value, CultureInfo.CurrentCulture);
    }

    public static string CStr(object? value)
    {
        if (value is VBVariant variant)
        {
            ThrowIfInvalidVariantConversion(variant);
            return variant.ToDisplayString();
        }

        return value is VBCurrency currency
            ? currency.ToString()
            : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private static void ThrowIfInvalidVariantConversion(VBVariant variant)
    {
        if (variant.IsNull)
        {
            throw new InvalidOperationException("Invalid use of Null.");
        }

        if (variant.IsError)
        {
            throw new InvalidOperationException("Type mismatch.");
        }
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

public static class VBStrings
{
    public static string FixedLength(object? value, int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Fixed-length strings cannot have a negative length.");
        }

        var text = VBConversions.CStr(value);
        return text.Length > length
            ? text[..length]
            : text.PadRight(length);
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

    public static decimal AddDecimal(decimal left, decimal right) => checked(left + right);

    public static decimal SubtractDecimal(decimal left, decimal right) => checked(left - right);

    public static decimal MultiplyDecimal(decimal left, decimal right) => checked(left * right);

    public static decimal NegateDecimal(decimal value) => checked(-value);

    public static decimal DivideDecimal(decimal left, decimal right) => checked(left / right);

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
    public static void Print(object? value) => Console.WriteLine(Format(value));

    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        VBVariant { Kind: VBVariantKind.Value } variant => Format(variant.Value),
        VBVariant => string.Empty,
        VBCurrency currency => currency.ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
}
