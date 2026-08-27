namespace VB6.Runtime;

/// <summary>
/// Core VB6 financial intrinsics. The formulas follow the ordinary end-/beginning-of-period
/// annuity definitions used by the VB runtime; all values are Double-compatible.
/// </summary>
public static class VBFinancial
{
    public static double FV(double rate, double nper, double pmt, double pv, double type)
    {
        ValidatePeriodCount(nper);
        return FutureValueCore(rate, nper, pmt, pv, NormalizeType(type));
    }

    public static double PV(double rate, double nper, double pmt, double fv, double type)
    {
        ValidatePeriodCount(nper);
        var factor = GrowthFactor(rate, nper);
        if (rate == 0d)
        {
            return -(fv + pmt * nper);
        }

        return -(fv + pmt * (1d + rate * NormalizeType(type)) * (factor - 1d) / rate) / factor;
    }

    public static double PMT(double rate, double nper, double pv, double fv, double type)
    {
        ValidatePeriodCount(nper);
        var normalizedType = NormalizeType(type);
        var factor = GrowthFactor(rate, nper);
        if (rate == 0d)
        {
            return -(pv + fv) / nper;
        }

        var denominator = (1d + rate * normalizedType) * (factor - 1d);
        if (denominator == 0d)
        {
            throw new DivideByZeroException("VB6 PMT has a zero annuity denominator.");
        }

        return -(pv * factor + fv) * rate / denominator;
    }

    public static double IPMT(double rate, double period, double nper, double pv, double fv, double type)
    {
        ValidatePaymentPeriod(period, nper);
        var normalizedType = NormalizeType(type);
        if (rate == 0d || (normalizedType == 1d && period == 1d))
        {
            return 0d;
        }

        var payment = PMT(rate, nper, pv, fv, normalizedType);
        var balance = normalizedType == 1d
            ? FutureValueCore(rate, period - 2d, payment, pv, normalizedType) - payment
            : FutureValueCore(rate, period - 1d, payment, pv, normalizedType);
        return balance * rate;
    }

    public static double PPMT(double rate, double period, double nper, double pv, double fv, double type) =>
        PMT(rate, nper, pv, fv, type) - IPMT(rate, period, nper, pv, fv, type);

    public static double NPER(double rate, double pmt, double pv, double fv, double type)
    {
        var normalizedType = NormalizeType(type);
        if (!double.IsFinite(rate) || rate <= -1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate),
                rate,
                "VB6 NPer requires a finite rate greater than -1.");
        }

        if (rate == 0d)
        {
            if (pmt == 0d)
            {
                throw new DivideByZeroException("VB6 NPer requires a non-zero payment when Rate is zero.");
            }

            return -(pv + fv) / pmt;
        }

        var paymentTerm = pmt * (1d + rate * normalizedType) / rate;
        var denominator = pv + paymentTerm;
        var ratio = (paymentTerm - fv) / denominator;
        if (!double.IsFinite(ratio) || ratio <= 0d || denominator == 0d)
        {
            throw new ArgumentException("VB6 NPer arguments do not describe a solvable annuity.");
        }

        var result = Math.Log(ratio) / Math.Log(1d + rate);
        if (!double.IsFinite(result))
        {
            throw new InvalidOperationException("VB6 NPer did not produce a finite period count.");
        }

        return result;
    }

    public static double RATE(double nper, double pmt, double pv, double fv, double type, double guess)
    {
        ValidatePositivePeriodCount(nper);
        var normalizedType = NormalizeType(type);
        if (!double.IsFinite(guess) || guess <= -1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(guess),
                guess,
                "VB6 Rate Guess must be finite and greater than -1.");
        }

        var rate = guess;
        var tolerance = 1e-12d * Math.Max(1d, Math.Max(Math.Abs(pv), Math.Abs(fv)));
        if (Math.Abs(EvaluateAnnuity(0d, nper, pmt, pv, fv, normalizedType)) <= tolerance)
        {
            return 0d;
        }

        for (var iteration = 0; iteration < 80; iteration++)
        {
            var value = EvaluateAnnuity(rate, nper, pmt, pv, fv, normalizedType);
            if (double.IsFinite(value) && Math.Abs(value) <= tolerance)
            {
                return rate;
            }

            var step = Math.Max(1e-8d, Math.Abs(rate) * 1e-6d);
            var lower = Math.Max(-0.999999999999d, rate - step);
            var upper = rate + step;
            var lowerValue = EvaluateAnnuity(lower, nper, pmt, pv, fv, normalizedType);
            var upperValue = EvaluateAnnuity(upper, nper, pmt, pv, fv, normalizedType);
            var derivative = (upperValue - lowerValue) / (upper - lower);
            if (!double.IsFinite(derivative) || Math.Abs(derivative) < 1e-18d)
            {
                break;
            }

            var next = rate - value / derivative;
            if (!double.IsFinite(next) || next <= -1d)
            {
                break;
            }

            if (Math.Abs(next - rate) <= 1e-13d)
            {
                return next;
            }

            rate = next;
        }

        var bracketLower = -0.999999999999d;
        var bracketLowerValue = EvaluateAnnuity(bracketLower, nper, pmt, pv, fv, normalizedType);
        var bracketUpper = Math.Max(0.1d, guess + 0.1d);
        var bracketUpperValue = EvaluateAnnuity(bracketUpper, nper, pmt, pv, fv, normalizedType);
        for (var expansion = 0;
             expansion < 64 && (!double.IsFinite(bracketUpperValue) || Math.Sign(bracketLowerValue) == Math.Sign(bracketUpperValue));
             expansion++)
        {
            bracketUpper = bracketUpper * 2d + 0.1d;
            bracketUpperValue = EvaluateAnnuity(bracketUpper, nper, pmt, pv, fv, normalizedType);
        }

        if (!double.IsFinite(bracketLowerValue) || !double.IsFinite(bracketUpperValue) ||
            Math.Sign(bracketLowerValue) == Math.Sign(bracketUpperValue))
        {
            throw new InvalidOperationException("VB6 Rate could not bracket an annuity root.");
        }

        for (var iteration = 0; iteration < 160; iteration++)
        {
            var middle = (bracketLower + bracketUpper) / 2d;
            var middleValue = EvaluateAnnuity(middle, nper, pmt, pv, fv, normalizedType);
            if (Math.Abs(middleValue) <= tolerance)
            {
                return middle;
            }

            if (Math.Sign(middleValue) == Math.Sign(bracketLowerValue))
            {
                bracketLower = middle;
                bracketLowerValue = middleValue;
            }
            else
            {
                bracketUpper = middle;
            }
        }

        return (bracketLower + bracketUpper) / 2d;
    }

    public static double NPV(double rate, VBArray<object> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= -1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rate),
                rate,
                "VB6 NPV requires a finite rate greater than -1.");
        }

        var discount = 1d + rate;
        var result = 0d;
        var period = 1;
        foreach (var value in values.EnumerateValues())
        {
            result += VBConversions.CDbl(value) / Math.Pow(discount, period++);
        }

        return result;
    }

    public static double IRR(VBArray<object> values, double guess)
    {
        ArgumentNullException.ThrowIfNull(values);
        var cashFlows = values.EnumerateValues()
            .Select(value => VBConversions.CDbl(value))
            .ToArray();
        if (cashFlows.Length < 2)
        {
            throw new ArgumentException("VB6 IRR requires at least two cash flows.", nameof(values));
        }

        var rate = double.IsFinite(guess) && guess > -1d ? guess : 0.1d;
        for (var iteration = 0; iteration < 50; iteration++)
        {
            var value = EvaluateCashFlows(cashFlows, rate, out var derivative);
            if (Math.Abs(value) < 1e-12)
            {
                return rate;
            }

            if (Math.Abs(derivative) < 1e-18 || !double.IsFinite(derivative))
            {
                break;
            }

            var next = rate - value / derivative;
            if (!double.IsFinite(next) || next <= -1d)
            {
                break;
            }

            rate = next;
        }

        var lower = -0.999999999999d;
        var lowerValue = EvaluateCashFlows(cashFlows, lower, out _);
        var upper = Math.Max(0.1d, rate + 0.1d);
        var upperValue = EvaluateCashFlows(cashFlows, upper, out _);
        for (var expansion = 0; expansion < 64 && Math.Sign(lowerValue) == Math.Sign(upperValue); expansion++)
        {
            upper = upper * 2d + 0.1d;
            upperValue = EvaluateCashFlows(cashFlows, upper, out _);
            if (!double.IsFinite(upperValue))
            {
                break;
            }
        }

        if (!double.IsFinite(lowerValue) || !double.IsFinite(upperValue) ||
            Math.Sign(lowerValue) == Math.Sign(upperValue))
        {
            throw new InvalidOperationException("VB6 IRR could not bracket a cash-flow root.");
        }

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var middle = (lower + upper) / 2d;
            var middleValue = EvaluateCashFlows(cashFlows, middle, out _);
            if (Math.Abs(middleValue) < 1e-12)
            {
                return middle;
            }

            if (Math.Sign(middleValue) == Math.Sign(lowerValue))
            {
                lower = middle;
                lowerValue = middleValue;
            }
            else
            {
                upper = middle;
                upperValue = middleValue;
            }
        }

        return (lower + upper) / 2d;
    }

    public static double MIRR(VBArray<object> values, double financeRate, double reinvestRate)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateDiscountRate(financeRate, nameof(financeRate), "MIRR FinanceRate");
        ValidateDiscountRate(reinvestRate, nameof(reinvestRate), "MIRR ReinvestRate");

        var cashFlows = values.EnumerateValues()
            .Select(value => VBConversions.CDbl(value))
            .ToArray();
        if (cashFlows.Length < 2)
        {
            throw new ArgumentException("VB6 MIRR requires at least two cash flows.", nameof(values));
        }

        var futurePositive = 0d;
        var presentNegative = 0d;
        for (var period = 0; period < cashFlows.Length; period++)
        {
            var cashFlow = cashFlows[period];
            if (cashFlow > 0d)
            {
                futurePositive += cashFlow * Math.Pow(1d + reinvestRate, cashFlows.Length - period - 1);
            }
            else if (cashFlow < 0d)
            {
                presentNegative += cashFlow / Math.Pow(1d + financeRate, period);
            }
        }

        if (futurePositive == 0d || presentNegative == 0d)
        {
            throw new ArgumentException("VB6 MIRR requires at least one positive and one negative cash flow.", nameof(values));
        }

        var result = Math.Pow(futurePositive / -presentNegative, 1d / (cashFlows.Length - 1d)) - 1d;
        if (!double.IsFinite(result))
        {
            throw new InvalidOperationException("VB6 MIRR did not produce a finite rate.");
        }

        return result;
    }

    public static double SLN(double cost, double salvage, double life)
    {
        ValidateLife(life);
        return (cost - salvage) / life;
    }

    public static double SYD(double cost, double salvage, double life, double period)
    {
        ValidateLife(life);
        var wholePeriod = ValidatePeriod(period, life);
        return (cost - salvage) * (life - wholePeriod + 1d) * 2d / (life * (life + 1d));
    }

    public static double DDB(double cost, double salvage, double life, double period, double factor)
    {
        ValidateLife(life);
        var wholePeriod = ValidatePeriod(period, life);
        if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(factor),
                factor,
                "VB6 DDB Factor must be a finite positive value.");
        }

        var bookValue = cost;
        var depreciation = 0d;
        for (var current = 1d; current <= wholePeriod; current++)
        {
            var remaining = bookValue - salvage;
            depreciation = Math.Max(0d, Math.Min(bookValue * factor / life, remaining));
            bookValue -= depreciation;
        }

        return depreciation;
    }

    private static double EvaluateCashFlows(
        IReadOnlyList<double> cashFlows,
        double rate,
        out double derivative)
    {
        var factor = 1d + rate;
        var value = cashFlows[0];
        derivative = 0d;
        for (var period = 1; period < cashFlows.Count; period++)
        {
            var denominator = Math.Pow(factor, period);
            value += cashFlows[period] / denominator;
            derivative -= period * cashFlows[period] / (denominator * factor);
        }

        return value;
    }

    private static double EvaluateAnnuity(
        double rate,
        double nper,
        double pmt,
        double pv,
        double fv,
        double type)
    {
        if (Math.Abs(rate) < 1e-12d)
        {
            return pv + pmt * nper + fv;
        }

        var factor = Math.Pow(1d + rate, nper);
        return pv * factor + pmt * (1d + rate * type) * (factor - 1d) / rate + fv;
    }

    private static double FutureValueCore(double rate, double nper, double pmt, double pv, double type)
    {
        if (rate == 0d)
        {
            return -(pv + pmt * nper);
        }

        var factor = GrowthFactor(rate, nper);
        return -(pv * factor + pmt * (1d + rate * type) * (factor - 1d) / rate);
    }

    private static void ValidateLife(double life)
    {
        if (double.IsNaN(life) || double.IsInfinity(life) || life <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(life),
                life,
                "VB6 depreciation Life must be a finite positive value.");
        }
    }

    private static void ValidatePaymentPeriod(double period, double nper)
    {
        ValidatePositivePeriodCount(nper);
        if (!double.IsFinite(period) || period < 1d || period > nper)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "VB6 IPmt/PPmt Period must be between 1 and NPer.");
        }
    }

    private static void ValidateDiscountRate(double rate, string parameterName, string displayName)
    {
        if (!double.IsFinite(rate) || rate <= -1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rate,
                $"VB6 {displayName} must be finite and greater than -1.");
        }
    }

    private static double ValidatePeriod(double period, double life)
    {
        if (double.IsNaN(period) || double.IsInfinity(period) ||
            period < 1d || period > life || period != Math.Truncate(period))
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "VB6 depreciation Period must be a whole number between 1 and Life.");
        }

        return period;
    }

    private static double GrowthFactor(double rate, double nper)
    {
        var factor = Math.Pow(1d + rate, nper);
        if (double.IsNaN(factor) || double.IsInfinity(factor))
        {
            throw new OverflowException("VB6 financial result is outside the range of Double.");
        }

        return factor;
    }

    private static double NormalizeType(double type) => type switch
    {
        0d => 0d,
        1d => 1d,
        _ => throw new ArgumentOutOfRangeException(
            nameof(type),
            type,
            "VB6 financial Type must be 0 (end of period) or 1 (beginning of period).")
    };

    private static void ValidatePeriodCount(double nper)
    {
        if (nper == 0d || double.IsNaN(nper) || double.IsInfinity(nper))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nper),
                nper,
                "VB6 financial NPer must be a finite, non-zero value.");
        }
    }

    private static void ValidatePositivePeriodCount(double nper)
    {
        if (!double.IsFinite(nper) || nper <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nper),
                nper,
                "VB6 financial NPer must be a finite positive value.");
        }
    }
}
