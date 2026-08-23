using System.Globalization;

namespace VB6.Runtime;

/// <summary>Portable Date/Time intrinsics represented by OLE Automation doubles.</summary>
public static class VBDateTime
{
    public static object Date() => new VBDateValue(DateTime.Today.ToOADate());

    public static object Time()
    {
        var time = DateTime.Now.TimeOfDay;
        return new VBDateValue(new DateTime(1899, 12, 30).Add(time).ToOADate());
    }

    public static double Now() => DateTime.Now.ToOADate();

    public static double DateValue(object? value) =>
        FromOleDate(VBConversions.CDate(value)).Date.ToOADate();

    public static double TimeValue(object? value)
    {
        var time = FromOleDate(VBConversions.CDate(value)).TimeOfDay;
        return new DateTime(1899, 12, 30).Add(time).ToOADate();
    }

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
            "y" or "d" or "w" => date.AddDays(count).ToOADate(),
            "ww" => date.AddDays(checked(count * 7)).ToOADate(),
            "h" => date.AddHours(count).ToOADate(),
            "n" => date.AddMinutes(count).ToOADate(),
            "s" => date.AddSeconds(count).ToOADate(),
            _ => throw UnsupportedInterval(interval)
        };
    }

    public static int DateDiff(
        string interval,
        double firstValue,
        double secondValue,
        int firstDayOfWeek = 1,
        int firstWeekOfYear = 1)
    {
        ArgumentNullException.ThrowIfNull(interval);
        var first = FromOleDate(firstValue);
        var second = FromOleDate(secondValue);
        var weekStart = ResolveFirstDayOfWeek(firstDayOfWeek);
        ValidateFirstWeekOfYear(firstWeekOfYear);
        return NormalizeInterval(interval) switch
        {
            "yyyy" => second.Year - first.Year,
            "q" => checked((second.Year * 4 + (second.Month - 1) / 3) -
                (first.Year * 4 + (first.Month - 1) / 3)),
            "m" => checked((second.Year * 12 + second.Month) - (first.Year * 12 + first.Month)),
            "y" or "d" => checked((int)(second.Date - first.Date).TotalDays),
            "w" => CountWeekdayBoundaries(first.Date, second.Date, first.DayOfWeek),
            "ww" => CountWeekdayBoundaries(first.Date, second.Date, weekStart),
            "h" => checked((int)(second - first).TotalHours),
            "n" => checked((int)(second - first).TotalMinutes),
            "s" => checked((int)(second - first).TotalSeconds),
            _ => throw UnsupportedInterval(interval)
        };
    }

    public static int DatePart(
        string interval,
        double value,
        int firstDayOfWeek = 1,
        int firstWeekOfYear = 1)
    {
        ArgumentNullException.ThrowIfNull(interval);
        var date = FromOleDate(value);
        var firstDay = ResolveFirstDayOfWeek(firstDayOfWeek);
        var weekRule = ResolveFirstWeekRule(firstWeekOfYear);
        return NormalizeInterval(interval) switch
        {
            "yyyy" => date.Year,
            "q" => (date.Month - 1) / 3 + 1,
            "m" => date.Month,
            "y" => date.DayOfYear,
            "d" => date.Day,
            "w" => 1 + ((int)date.DayOfWeek - (int)firstDay + 7) % 7,
            "ww" => new GregorianCalendar().GetWeekOfYear(date, weekRule, firstDay),
            "h" => date.Hour,
            "n" => date.Minute,
            "s" => date.Second,
            _ => throw UnsupportedInterval(interval)
        };
    }

    public static short Weekday(double value, int firstDayOfWeek = 1)
    {
        var firstDay = ResolveFirstDayOfWeek(firstDayOfWeek);
        var date = FromOleDate(value);
        return checked((short)(1 + ((int)date.DayOfWeek - (int)firstDay + 7) % 7));
    }

    public static string WeekdayName(int weekday, bool abbreviate = false, int firstDayOfWeek = 1)
    {
        var firstDay = ResolveFirstDayOfWeek(firstDayOfWeek);
        if (weekday is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(weekday), "Weekday must be between 1 and 7.");
        }

        var day = (DayOfWeek)(((int)firstDay + weekday - 1) % 7);
        return abbreviate
            ? CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedDayName(day)
            : CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day);
    }

    public static string MonthName(int month, bool abbreviate = false)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        return abbreviate
            ? CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month)
            : CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);
    }

    private static DateTime FromOleDate(double value) => DateTime.FromOADate(value);

    private static int WholeIntervalCount(double value) =>
        checked((int)Math.Truncate(value));

    private static string NormalizeInterval(string interval) => interval.Trim().ToLowerInvariant();

    private static DayOfWeek ResolveFirstDayOfWeek(int value) => value switch
    {
        0 or 1 => DayOfWeek.Sunday,
        2 => DayOfWeek.Monday,
        3 => DayOfWeek.Tuesday,
        4 => DayOfWeek.Wednesday,
        5 => DayOfWeek.Thursday,
        6 => DayOfWeek.Friday,
        7 => DayOfWeek.Saturday,
        _ => throw new ArgumentOutOfRangeException(nameof(value), "FirstDayOfWeek must be between 0 and 7.")
    };

    private static void ValidateFirstWeekOfYear(int value)
    {
        if (value is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "FirstWeekOfYear must be between 0 and 3.");
        }
    }

    private static CalendarWeekRule ResolveFirstWeekRule(int value) => value switch
    {
        0 or 1 => CalendarWeekRule.FirstDay,
        2 => CalendarWeekRule.FirstFourDayWeek,
        3 => CalendarWeekRule.FirstFullWeek,
        _ => throw new ArgumentOutOfRangeException(nameof(value), "FirstWeekOfYear must be between 0 and 3.")
    };

    private static int CountWeekdayBoundaries(DateTime first, DateTime second, DayOfWeek weekday)
    {
        if (second >= first)
        {
            return CountForwardWeekdayBoundaries(first, second, weekday);
        }

        return -CountForwardWeekdayBoundaries(second, first, weekday);
    }

    private static int CountForwardWeekdayBoundaries(DateTime first, DateTime second, DayOfWeek weekday)
    {
        var daysUntilBoundary = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        if (daysUntilBoundary == 0)
        {
            daysUntilBoundary = 7;
        }

        var elapsedDays = checked((int)(second - first).TotalDays);
        return elapsedDays < daysUntilBoundary
            ? 0
            : checked(1 + (elapsedDays - daysUntilBoundary) / 7);
    }

    private static NotSupportedException UnsupportedInterval(string interval) =>
        new($"Date interval '{interval}' is outside the current DateAdd/DateDiff subset.");
}
