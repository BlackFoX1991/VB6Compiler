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
}
