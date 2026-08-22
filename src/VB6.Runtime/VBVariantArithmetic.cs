namespace VB6.Runtime;

public static partial class VBOperators
{
    public static object? AddVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        if (left is null)
        {
            return right ?? (short)0;
        }

        if (right is null)
        {
            return left;
        }

        if (left is string leftString && right is string rightString)
        {
            return leftString + rightString;
        }

        var kind = PromoteVariantAddKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte => AddVariantByte(left, right),
            VariantNumericKind.Integer => AddVariantInteger(left, right),
            VariantNumericKind.Long => AddVariantLong(left, right),
            VariantNumericKind.LongLong => AddLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
            VariantNumericKind.Single => AddSingle(VBConversions.CSng(left), VBConversions.CSng(right)),
            VariantNumericKind.Currency => AddCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
            VariantNumericKind.Decimal => checked(VariantDecimal(left) + VariantDecimal(right)),
            VariantNumericKind.Double => AddDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)),
            _ => throw new InvalidOperationException("Unexpected Variant numeric kind.")
        };
    }

    public static object? AddStringVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        return VBConversions.CStr(left) + VBConversions.CStr(right);
    }

    public static object? SubtractVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        var kind = PromoteVariantAddKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte => SubtractVariantByte(left, right),
            VariantNumericKind.Integer => SubtractVariantInteger(left, right),
            VariantNumericKind.Long => SubtractVariantLong(left, right),
            VariantNumericKind.LongLong => SubtractLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
            VariantNumericKind.Single => SubtractSingle(VBConversions.CSng(left), VBConversions.CSng(right)),
            VariantNumericKind.Currency => SubtractCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
            VariantNumericKind.Decimal => checked(VariantDecimal(left) - VariantDecimal(right)),
            VariantNumericKind.Double => SubtractDouble(VBConversions.CDbl(left), VBConversions.CDbl(right)),
            _ => throw new InvalidOperationException("Unexpected Variant numeric kind.")
        };
    }

    public static object? DivideVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        return PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right)) == VariantNumericKind.Decimal
            ? checked(VariantDecimal(left) / VariantDecimal(right))
            : DivideDouble(VBConversions.CDbl(left), VBConversions.CDbl(right));
    }

    public static object? IntegerDivideVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        var kind = PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte or VariantNumericKind.Integer =>
                (object)IntegerDivide(VBConversions.CInt(left), VBConversions.CInt(right)),
            VariantNumericKind.Long =>
                (object)IntegerDivideLong(VBConversions.CLng(left), VBConversions.CLng(right)),
            VariantNumericKind.LongLong =>
                (object)IntegerDivideLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
            VariantNumericKind.Decimal => (object)IntegerDivideLong(VBConversions.CLng(left), VBConversions.CLng(right)),
            _ => (object)IntegerDivideLong(VBConversions.CLng(left), VBConversions.CLng(right))
        };
    }

    public static object? ModVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        var kind = PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte or VariantNumericKind.Integer =>
                (object)ModInteger(VBConversions.CInt(left), VBConversions.CInt(right)),
            VariantNumericKind.Long =>
                (object)ModLong(VBConversions.CLng(left), VBConversions.CLng(right)),
            VariantNumericKind.LongLong =>
                (object)ModLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
            VariantNumericKind.Decimal => VariantDecimal(left) % VariantDecimal(right),
            _ => (object)ModLong(VBConversions.CLng(left), VBConversions.CLng(right))
        };
    }

    public static object? PowerVariant(object? left, object? right)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        return Power(VBConversions.CDbl(left), VBConversions.CDbl(right));
    }

    public static object? NegateVariant(object? value)
    {
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        return GetVariantNumericKind(value) switch
        {
            VariantNumericKind.Byte or VariantNumericKind.Integer =>
                (object)NegateInteger(VBConversions.CInt(value)),
            VariantNumericKind.Long => (object)NegateLong(VBConversions.CLng(value)),
            VariantNumericKind.LongLong => (object)NegateLongLong(VBConversions.CLngLng(value)),
            VariantNumericKind.Single => (object)NegateSingle(VBConversions.CSng(value)),
            VariantNumericKind.Currency => (object)NegateCurrency(VBConversions.CCur(value)),
            VariantNumericKind.Decimal => checked(-VariantDecimal(value)),
            VariantNumericKind.Double => (object)NegateDouble(VBConversions.CDbl(value)),
            _ => throw new InvalidOperationException("Unexpected Variant numeric kind.")
        };
    }

    public static object? NotVariant(object? value)
    {
        if (VBVariants.IsNull(value))
        {
            return VBVariants.NullValue();
        }

        if (value is bool boolean)
        {
            return NotBoolean(boolean);
        }

        return GetVariantNumericKind(value) switch
        {
            VariantNumericKind.Byte or VariantNumericKind.Integer =>
                (object)NotInteger(VBConversions.CInt(value)),
            VariantNumericKind.Long => (object)NotLong(VBConversions.CLng(value)),
            VariantNumericKind.LongLong => (object)NotLongLong(VBConversions.CLngLng(value)),
            VariantNumericKind.Single or VariantNumericKind.Currency or VariantNumericKind.Decimal or VariantNumericKind.Double =>
                (object)NotLong(VBConversions.CLng(value)),
            _ => (object)NotInteger(VBConversions.CInt(value))
        };
    }

    public static object? AndVariant(object? left, object? right) => ApplyVariantBitwise(left, right, AndBoolean, AndByte, AndInteger, AndLong, AndLongLong);

    public static object? OrVariant(object? left, object? right) => ApplyVariantBitwise(left, right, OrBoolean, OrByte, OrInteger, OrLong, OrLongLong);

    public static object? XorVariant(object? left, object? right) => ApplyVariantBitwise(left, right, XorBoolean, XorByte, XorInteger, XorLong, XorLongLong);

    public static object? EqvVariant(object? left, object? right) => ApplyVariantBitwise(left, right, EqvBoolean, null, EqvInteger, EqvLong, EqvLongLong);

    public static object? ImpVariant(object? left, object? right) => ApplyVariantBitwise(left, right, ImpBoolean, null, ImpInteger, ImpLong, ImpLongLong);

    public static object VariantEqual(object? left, object? right) => CompareVariant(left, right, comparison => comparison == 0);

    public static object VariantNotEqual(object? left, object? right) => CompareVariant(left, right, comparison => comparison != 0);

    public static object VariantLess(object? left, object? right) => CompareVariant(left, right, comparison => comparison < 0);

    public static object VariantLessOrEqual(object? left, object? right) => CompareVariant(left, right, comparison => comparison <= 0);

    public static object VariantGreater(object? left, object? right) => CompareVariant(left, right, comparison => comparison > 0);

    public static object VariantGreaterOrEqual(object? left, object? right) => CompareVariant(left, right, comparison => comparison >= 0);

    public static object StringVariantEqual(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison == 0);

    public static object StringVariantNotEqual(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison != 0);

    public static object StringVariantLess(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison < 0);

    public static object StringVariantLessOrEqual(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison <= 0);

    public static object StringVariantGreater(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison > 0);

    public static object StringVariantGreaterOrEqual(object? left, object? right) =>
        CompareStringVariant(left, right, comparison => comparison >= 0);

    private static object AddVariantByte(object? left, object? right)
    {
        try
        {
            return AddByte(VBConversions.CByte(left), VBConversions.CByte(right));
        }
        catch (OverflowException)
        {
            return AddVariantInteger(left, right);
        }
    }

    private static object AddVariantInteger(object? left, object? right)
    {
        try
        {
            return AddInteger(VBConversions.CInt(left), VBConversions.CInt(right));
        }
        catch (OverflowException)
        {
            return AddVariantLong(left, right);
        }
    }

    private static object AddVariantLong(object? left, object? right)
    {
        try
        {
            return AddLong(VBConversions.CLng(left), VBConversions.CLng(right));
        }
        catch (OverflowException)
        {
            return AddDouble(VBConversions.CDbl(left), VBConversions.CDbl(right));
        }
    }

    private static object SubtractVariantByte(object? left, object? right)
    {
        try
        {
            return SubtractByte(VBConversions.CByte(left), VBConversions.CByte(right));
        }
        catch (OverflowException)
        {
            return SubtractVariantInteger(left, right);
        }
    }

    private static object SubtractVariantInteger(object? left, object? right)
    {
        try
        {
            return SubtractInteger(VBConversions.CInt(left), VBConversions.CInt(right));
        }
        catch (OverflowException)
        {
            return SubtractVariantLong(left, right);
        }
    }

    private static object SubtractVariantLong(object? left, object? right)
    {
        try
        {
            return SubtractLong(VBConversions.CLng(left), VBConversions.CLng(right));
        }
        catch (OverflowException)
        {
            return SubtractDouble(VBConversions.CDbl(left), VBConversions.CDbl(right));
        }
    }

    private static decimal VariantDecimal(object? value) => VBConversions.CDec(value) is decimal decimalValue
        ? decimalValue
        : throw new InvalidCastException("VB6 Variant value is not a Decimal subtype.");

    private static bool HasNullOperand(object? left, object? right) =>
        VBVariants.IsNull(left) || VBVariants.IsNull(right);

    private static object CompareVariant(object? left, object? right, Func<int, bool> predicate) =>
        HasNullOperand(left, right)
            ? VBVariants.NullValue()
            : predicate(CompareVariantValues(left, right));

    private static object CompareStringVariant(object? left, object? right, Func<int, bool> predicate) =>
        HasNullOperand(left, right)
            ? VBVariants.NullValue()
            : predicate(string.CompareOrdinal(VBConversions.CStr(left), VBConversions.CStr(right)));

    private static int CompareVariantValues(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (VBVariants.IsNull(left) || VBVariants.IsNull(right))
        {
            return VBVariants.IsNull(left) ? -1 : 1;
        }

        if (left is string leftString && right is string rightString)
        {
            return string.CompareOrdinal(leftString, rightString);
        }

        if (left is null && right is string rightText)
        {
            return string.CompareOrdinal(string.Empty, rightText);
        }

        if (left is string leftText && right is null)
        {
            return string.CompareOrdinal(leftText, string.Empty);
        }

        if (TryGetVariantDecimalValue(left, out var leftDecimal) &&
            TryGetVariantDecimalValue(right, out var rightDecimal))
        {
            return leftDecimal.CompareTo(rightDecimal);
        }

        if (TryGetVariantNumericValue(left, out var leftNumber) &&
            TryGetVariantNumericValue(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return Compare(left, right);
    }

    private static bool TryGetVariantNumericValue(object? value, out double number)
    {
        if (VBVariants.IsNull(value) || VBVariants.IsMissing(value))
        {
            number = 0d;
            return false;
        }

        switch (value)
        {
            case null:
                number = 0d;
                return true;
            case string text when double.TryParse(
                text,
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out number):
                return true;
            case string:
                number = 0d;
                return false;
            case byte or short or int or long or float or double or decimal or VBCurrency or bool:
                number = VBConversions.CDbl(value);
                return true;
            default:
                number = 0d;
                return false;
        }
    }

    private static bool TryGetVariantDecimalValue(object? value, out decimal number)
    {
        if (VBVariants.IsNull(value) || VBVariants.IsMissing(value))
        {
            number = 0m;
            return false;
        }

        switch (value)
        {
            case null:
                number = 0m;
                return true;
            case decimal decimalValue:
                number = decimalValue;
                return true;
            case VBCurrency currency:
                number = currency.ToDecimal();
                return true;
            case byte or short or int or long or bool:
                number = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            case string text when decimal.TryParse(
                text,
                System.Globalization.NumberStyles.Float | System.Globalization.NumberStyles.AllowThousands,
                System.Globalization.CultureInfo.InvariantCulture,
                out number):
                return true;
            default:
                number = 0m;
                return false;
        }
    }

    private static object? ApplyVariantBitwise(
        object? left,
        object? right,
        Func<bool, bool, bool> booleanOperation,
        Func<byte, byte, byte>? byteOperation,
        Func<short, short, short> integerOperation,
        Func<int, int, int> longOperation,
        Func<long, long, long> longLongOperation)
    {
        if (HasNullOperand(left, right))
        {
            return VBVariants.NullValue();
        }

        if (left is bool leftBoolean && right is bool rightBoolean)
        {
            return booleanOperation(leftBoolean, rightBoolean);
        }

        var kind = PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte when byteOperation is not null =>
                (object)byteOperation(VBConversions.CByte(left), VBConversions.CByte(right)),
            VariantNumericKind.Integer =>
                (object)integerOperation(VBConversions.CInt(left), VBConversions.CInt(right)),
            VariantNumericKind.Long =>
                (object)longOperation(VBConversions.CLng(left), VBConversions.CLng(right)),
            VariantNumericKind.LongLong =>
                (object)longLongOperation(VBConversions.CLngLng(left), VBConversions.CLngLng(right)),
            VariantNumericKind.Single or VariantNumericKind.Currency or VariantNumericKind.Decimal or VariantNumericKind.Double =>
                (object)longOperation(VBConversions.CLng(left), VBConversions.CLng(right)),
            _ => (object)integerOperation(VBConversions.CInt(left), VBConversions.CInt(right))
        };
    }
}
