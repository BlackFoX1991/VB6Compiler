namespace VB6.Runtime.Tests;

[TestClass]
public sealed class DateTimeRuntimeTests
{
    [TestMethod]
    public void DatePartFunctions_ReadOleAutomationDates()
    {
        const double date = 43832;
        const double time = 0.5;

        Assert.AreEqual((short)2020, VBDateTime.Year(date));
        Assert.AreEqual((short)1, VBDateTime.Month(date));
        Assert.AreEqual((short)2, VBDateTime.Day(date));
        Assert.AreEqual((short)12, VBDateTime.Hour(time));
        Assert.AreEqual((short)0, VBDateTime.Minute(time));
        Assert.AreEqual((short)0, VBDateTime.Second(time));

        var timer = VBDateTime.Timer();
        Assert.IsTrue(timer >= 0f && timer < 86_400f, $"Timer returned {timer} outside one day.");
    }

    [TestMethod]
    public void DateSerialAndDateArithmetic_UseSupportedIntervals()
    {
        Assert.AreEqual(43832d, VBDateTime.DateSerial(2020, 1, 2));
        Assert.AreEqual(43863d, VBDateTime.DateAdd("m", 1, 43832));
        Assert.AreEqual(43833d, VBDateTime.DateAdd("w", 1, 43832));
        Assert.AreEqual(43839d, VBDateTime.DateAdd("ww", 1, 43832));
        Assert.AreEqual(1, VBDateTime.DateDiff("d", 43832, 43833));
        Assert.AreEqual(1, VBDateTime.DateDiff("m", 43832, 43863));
        Assert.AreEqual(1, VBDateTime.DateDiff("w", 43832, 43839));
        Assert.AreEqual(1, VBDateTime.DateDiff("ww", 43832, 43835));
        Assert.AreEqual(0, VBDateTime.DateDiff("ww", 43832, 43835, 2));

        var time = DateTime.FromOADate(VBDateTime.TimeSerial(12, 30, 45));
        Assert.AreEqual(12, time.Hour);
        Assert.AreEqual(30, time.Minute);
        Assert.AreEqual(45, time.Second);
    }

    [TestMethod]
    public void DateValueAndTimeValue_NormalizeOleDateParts()
    {
        Assert.AreEqual(43832d, VBDateTime.DateValue(43832.75d));
        Assert.AreEqual(0.75d, VBDateTime.TimeValue(43832.75d));
    }

    [TestMethod]
    public void DatePart_ReturnsCalendarAndTimePartsWithWeekSettings()
    {
        Assert.AreEqual(2020, VBDateTime.DatePart("yyyy", 43832));
        Assert.AreEqual(1, VBDateTime.DatePart("q", 43832));
        Assert.AreEqual(1, VBDateTime.DatePart("m", 43832));
        Assert.AreEqual(2, VBDateTime.DatePart("y", 43832));
        Assert.AreEqual(2, VBDateTime.DatePart("d", 43832));
        Assert.AreEqual(5, VBDateTime.DatePart("w", 43832));
        Assert.AreEqual(1, VBDateTime.DatePart("w", 43832, 5));
        Assert.AreEqual(1, VBDateTime.DatePart("ww", 43832, 2, 2));
        const double halfPastNoon = 43832.5208333333;
        Assert.AreEqual(12, VBDateTime.DatePart("h", halfPastNoon));
        Assert.AreEqual(30, VBDateTime.DatePart("n", halfPastNoon));
        Assert.AreEqual(0, VBDateTime.DatePart("s", halfPastNoon));
    }
}
