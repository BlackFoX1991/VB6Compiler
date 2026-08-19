namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ModRuntimeTests
{
    [TestMethod]
    public void ModInteger_ReturnsRemainderWithDividendSign()
    {
        Assert.AreEqual((short)2, VBOperators.ModInteger(17, 5));
        Assert.AreEqual((short)-2, VBOperators.ModInteger(-17, 5));
        Assert.AreEqual((short)2, VBOperators.ModInteger(17, -5));
    }

    [TestMethod]
    public void ModInteger_ThrowsForZeroDivisor()
    {
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.ModInteger(17, 0));
    }
}
