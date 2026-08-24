using System.Globalization;

namespace VB6.Runtime;

public static partial class VBOperators
{
    /// <summary>
    /// Dynamic multiplication used when at least one VB6 operand is Variant. The C# generator
    /// already emits MultiplyInteger for the binder's scalar arithmetic fallback; the direct
    /// Variant binder path selects this object overload when a Variant operand is present.
    /// </summary>
    public static object? MultiplyInteger(object? left, object? right)
    {
        VBVariants.ThrowIfMissing(left, right);
        VBVariants.ThrowIfArray(left, right);
        ThrowIfErrorOperand(left, right);

        var kind = PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte => MultiplyVariantByte(left, right),
            VariantNumericKind.Integer => MultiplyVariantInteger(left, right),
            VariantNumericKind.Long => MultiplyVariantLong(left, right),
            VariantNumericKind.LongLong => MultiplyVariantLongLong(left, right),
            VariantNumericKind.UShort => MultiplyUShort(VBConversions.CUShort(left), VBConversions.CUShort(right)),
            VariantNumericKind.UInteger => MultiplyUInteger(VBConversions.CUInt(left), VBConversions.CUInt(right)),
            VariantNumericKind.ULong => MultiplyULong(VBConversions.CULng(left), VBConversions.CULng(right)),
            VariantNumericKind.Single => MultiplyVariantSingle(left, right),
            VariantNumericKind.Currency => MultiplyCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
            VariantNumericKind.Decimal => checked(VariantDecimal(left) * VariantDecimal(right)),
            VariantNumericKind.Double => MultiplyVariantDouble(left, right),
            _ => throw new InvalidOperationException("Unexpected Variant numeric kind.")
        };
    }

    private static object MultiplyVariantByte(object? left, object? right)
    {
        try
        {
            return MultiplyByte(VBConversions.CByte(left), VBConversions.CByte(right));
        }
        catch (OverflowException)
        {
            return MultiplyVariantInteger(left, right);
        }
    }

    private static object MultiplyVariantInteger(object? left, object? right)
    {
        try
        {
            return MultiplyInteger(VBConversions.CInt(left), VBConversions.CInt(right));
        }
        catch (OverflowException)
        {
            return MultiplyLong(VBConversions.CLng(left), VBConversions.CLng(right));
        }
    }

    private static object MultiplyVariantLong(object? left, object? right)
    {
        try
        {
            return MultiplyLong(VBConversions.CLng(left), VBConversions.CLng(right));
        }
        catch (OverflowException)
        {
            return MultiplyVariantDouble(left, right);
        }
    }

    private static object MultiplyVariantLongLong(object? left, object? right)
    {
        try
        {
            return MultiplyLongLong(VBConversions.CLngLng(left), VBConversions.CLngLng(right));
        }
        catch (OverflowException)
        {
            return MultiplyVariantDouble(left, right);
        }
    }

    private static object MultiplyVariantSingle(object? left, object? right)
    {
        try
        {
            return MultiplySingle(VBConversions.CSng(left), VBConversions.CSng(right));
        }
        catch (OverflowException)
        {
            return MultiplyVariantDouble(left, right);
        }
    }

    private static object MultiplyVariantDouble(object? left, object? right)
    {
        var result = MultiplyDouble(VBConversions.CDbl(left), VBConversions.CDbl(right));
        if (double.IsInfinity(result))
        {
            throw new OverflowException("VB6 Variant Double multiplication overflowed.");
        }

        return result;
    }

    private static VariantNumericKind GetVariantNumericKind(object? value)
    {
        return value switch
        {
            null => VariantNumericKind.Integer,
            byte => VariantNumericKind.Byte,
            short => VariantNumericKind.Integer,
            int => VariantNumericKind.Long,
            ushort => VariantNumericKind.UShort,
            uint => VariantNumericKind.UInteger,
            long => VariantNumericKind.LongLong,
            ulong => VariantNumericKind.ULong,
            IntPtr => VariantNumericKind.LongLong,
            float => VariantNumericKind.Single,
            VBCurrency => VariantNumericKind.Currency,
            decimal => VariantNumericKind.Decimal,
            VBDateValue => VariantNumericKind.Double,
            double => VariantNumericKind.Double,
            bool => VariantNumericKind.Integer,
            string text when double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out _) => VariantNumericKind.Double,
            string => throw new InvalidCastException("VB6 Variant string is not numeric."),
            _ => throw new InvalidCastException(
                $"CLR value of type '{value.GetType().FullName}' is not a supported VB6 numeric Variant.")
        };
    }

    private static VariantNumericKind PromoteVariantMultiplyKind(
        VariantNumericKind left,
        VariantNumericKind right)
    {
        if (left == VariantNumericKind.Decimal || right == VariantNumericKind.Decimal)
        {
            return VariantNumericKind.Decimal;
        }

        if (left == VariantNumericKind.Double || right == VariantNumericKind.Double)
        {
            return VariantNumericKind.Double;
        }

        if (left == VariantNumericKind.Currency || right == VariantNumericKind.Currency)
        {
            return VariantNumericKind.Currency;
        }

        if (left == VariantNumericKind.Single || right == VariantNumericKind.Single)
        {
            var other = left == VariantNumericKind.Single ? right : left;
            return other is VariantNumericKind.Long or VariantNumericKind.LongLong or
                VariantNumericKind.UInteger or VariantNumericKind.ULong
                ? VariantNumericKind.Double
                : VariantNumericKind.Single;
        }

        if (left == VariantNumericKind.ULong || right == VariantNumericKind.ULong)
        {
            return left is VariantNumericKind.Long or VariantNumericKind.LongLong ||
                right is VariantNumericKind.Long or VariantNumericKind.LongLong
                ? VariantNumericKind.Decimal
                : VariantNumericKind.ULong;
        }

        if (left == VariantNumericKind.UInteger || right == VariantNumericKind.UInteger)
        {
            return left is VariantNumericKind.Long or VariantNumericKind.LongLong ||
                right is VariantNumericKind.Long or VariantNumericKind.LongLong
                ? VariantNumericKind.Decimal
                : VariantNumericKind.UInteger;
        }

        if (left == VariantNumericKind.UShort || right == VariantNumericKind.UShort)
        {
            return VariantNumericKind.UShort;
        }

        if (left == VariantNumericKind.LongLong || right == VariantNumericKind.LongLong)
        {
            return VariantNumericKind.LongLong;
        }

        if (left == VariantNumericKind.Long || right == VariantNumericKind.Long)
        {
            return VariantNumericKind.Long;
        }

        if (left == VariantNumericKind.Integer || right == VariantNumericKind.Integer)
        {
            return VariantNumericKind.Integer;
        }

        return VariantNumericKind.Byte;
    }

    private static VariantNumericKind PromoteVariantAddKind(
        VariantNumericKind left,
        VariantNumericKind right)
    {
        if (left == VariantNumericKind.Decimal || right == VariantNumericKind.Decimal)
        {
            return VariantNumericKind.Decimal;
        }

        if (left == VariantNumericKind.Double || right == VariantNumericKind.Double)
        {
            return VariantNumericKind.Double;
        }

        if (left == VariantNumericKind.Currency || right == VariantNumericKind.Currency)
        {
            return VariantNumericKind.Currency;
        }

        if (left == VariantNumericKind.Single || right == VariantNumericKind.Single)
        {
            var other = left == VariantNumericKind.Single ? right : left;
            return other is VariantNumericKind.Long or VariantNumericKind.LongLong or
                VariantNumericKind.UInteger or VariantNumericKind.ULong
                ? VariantNumericKind.Double
                : VariantNumericKind.Single;
        }

        if (left == VariantNumericKind.ULong || right == VariantNumericKind.ULong)
        {
            return left is VariantNumericKind.Long or VariantNumericKind.LongLong ||
                right is VariantNumericKind.Long or VariantNumericKind.LongLong
                ? VariantNumericKind.Decimal
                : VariantNumericKind.ULong;
        }

        if (left == VariantNumericKind.UInteger || right == VariantNumericKind.UInteger)
        {
            return left is VariantNumericKind.Long or VariantNumericKind.LongLong ||
                right is VariantNumericKind.Long or VariantNumericKind.LongLong
                ? VariantNumericKind.Decimal
                : VariantNumericKind.UInteger;
        }

        if (left == VariantNumericKind.UShort || right == VariantNumericKind.UShort)
        {
            return VariantNumericKind.UShort;
        }

        if (left == VariantNumericKind.LongLong || right == VariantNumericKind.LongLong)
        {
            return VariantNumericKind.LongLong;
        }

        if (left == VariantNumericKind.Long || right == VariantNumericKind.Long)
        {
            return VariantNumericKind.Long;
        }

        if (left == VariantNumericKind.Integer || right == VariantNumericKind.Integer)
        {
            return VariantNumericKind.Integer;
        }

        return VariantNumericKind.Byte;
    }

    private enum VariantNumericKind
    {
        Byte,
        Integer,
        Long,
        LongLong,
        UShort,
        UInteger,
        ULong,
        Single,
        Currency,
        Decimal,
        Double
    }
}
