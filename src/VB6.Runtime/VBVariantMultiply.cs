using System.Globalization;

namespace VB6.Runtime;

public static partial class VBOperators
{
    /// <summary>
    /// Dynamic multiplication used when at least one VB6 operand is Variant. The C# generator
    /// already emits MultiplyInteger for the binder's arithmetic fallback; overload resolution
    /// selects this object overload only after VariantMultiplyLowerer restores a Variant operand.
    /// </summary>
    public static object? MultiplyInteger(object? left, object? right)
    {
        var kind = PromoteVariantMultiplyKind(GetVariantNumericKind(left), GetVariantNumericKind(right));
        return kind switch
        {
            VariantNumericKind.Byte => MultiplyVariantByte(left, right),
            VariantNumericKind.Integer => MultiplyVariantInteger(left, right),
            VariantNumericKind.Long => MultiplyVariantLong(left, right),
            VariantNumericKind.LongLong => MultiplyVariantLongLong(left, right),
            VariantNumericKind.Single => MultiplyVariantSingle(left, right),
            VariantNumericKind.Currency => MultiplyCurrency(VBConversions.CCur(left), VBConversions.CCur(right)),
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
            long => VariantNumericKind.LongLong,
            float => VariantNumericKind.Single,
            VBCurrency => VariantNumericKind.Currency,
            double => VariantNumericKind.Double,
            bool => VariantNumericKind.Integer,
            string text when double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
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
            return other is VariantNumericKind.Long or VariantNumericKind.LongLong
                ? VariantNumericKind.Double
                : VariantNumericKind.Single;
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
        Single,
        Currency,
        Double
    }
}
