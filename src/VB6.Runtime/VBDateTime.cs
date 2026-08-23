namespace VB6.Runtime;

/// <summary>Portable Date/Time intrinsics represented by OLE Automation doubles.</summary>
public static class VBDateTime
{
    public static double Now() => DateTime.Now.ToOADate();

    public static short Year(double value) => checked((short)FromOleDate(value).Year);

    public static short Month(double value) => checked((short)FromOleDate(value).Month);

    public static short Day(double value) => checked((short)FromOleDate(value).Day);

    public static short Hour(double value) => checked((short)FromOleDate(value).Hour);

    public static short Minute(double value) => checked((short)FromOleDate(value).Minute);

    public static short Second(double value) => checked((short)FromOleDate(value).Second);

    /// <summary>Seconds elapsed since local midnight, matching the VB6 Timer range.</summary>
    public static float Timer() => (float)(DateTime.Now - DateTime.Today).TotalSeconds;

    public static double DateSerial(short year, short month, short day)
    {
        var fullYear = year is >= 0 and <= 99
            ? year >= 30 ? year + 1900 : year + 2000
            : year;
        return new DateTime(fullYear, 1, 1)
            .AddMonths(month - 1)
            .AddDays(day - 1)
            .ToOADate();
    }

    public static double TimeSerial(short hour, short minute, short second) =>
        new DateTime(1899, 12, 30)
            .AddHours(hour)
            .AddMinutes(minute)
            .AddSeconds(second)
            .ToOADate();

    public static double DateAdd(string interval, double number, double value)
    {
        ArgumentNullException.ThrowIfNull(interval);
        var date = FromOleDate(value);
        var count = WholeIntervalCount(number);
        return NormalizeInterval(interval) switch
        {
            "yyyy" => date.AddYears(count).ToOADate(),
            "q" => date.AddMonths(checked(count * 3)).ToOADate(),
            "m" => date.AddMonths(count).ToOADate(),
            "y" or "d" => date.AddDays(count).ToOADate(),
            "h" => date.AddHours(count).ToOADate(),
            "n" => date.AddMinutes(count).ToOADate(),
            "s" => date.AddSeconds(count).ToOADate(),
            _ => throw UnsupportedInterval(interval)
        };
    }

    public static int DateDiff(string interval, double firstValue, double secondValue)
    {
        ArgumentNullException.ThrowIfNull(interval);
        var first = FromOleDate(firstValue);
        var second = FromOleDate(secondValue);
        return NormalizeInterval(interval) switch
        {
            "yyyy" => second.Year - first.Year,
            "q" => checked((second.Year * 4 + (second.Month - 1) / 3) -
                (first.Year * 4 + (first.Month - 1) / 3)),
            "m" => checked((second.Year * 12 + second.Month) - (first.Year * 12 + first.Month)),
            "y" or "d" => checked((int)(second.Date - first.Date).TotalDays),
            "h" => checked((int)(second - first).TotalHours),
            "n" => checked((int)(second - first).TotalMinutes),
            "s" => checked((int)(second - first).TotalSeconds),
            _ => throw UnsupportedInterval(interval)
        };
    }

    private static DateTime FromOleDate(double value) => DateTime.FromOADate(value);

    private static int WholeIntervalCount(double value) =>
        checked((int)Math.Truncate(value));

    private static string NormalizeInterval(string interval) => interval.Trim().ToLowerInvariant();

    private static NotSupportedException UnsupportedInterval(string interval) =>
        new($"Date interval '{interval}' is outside the current DateAdd/DateDiff subset.");
}
