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

    private static DateTime FromOleDate(double value) => DateTime.FromOADate(value);
}
