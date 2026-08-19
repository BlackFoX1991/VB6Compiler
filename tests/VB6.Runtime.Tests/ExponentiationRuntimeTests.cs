using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ExponentiationRuntimeTests
{
    [TestMethod]
    public void Power_ComputesDoubleExponentiation()
    {
        Assert.AreEqual(8d, VBOperators.Power(2d, 3d));
        Assert.AreEqual(0.125d, VBOperators.Power(2d, -3d));
        Assert.AreEqual(-125d, VBOperators.Power(-5d, 3d));
    }

    [TestMethod]
    public void Power_RejectsFractionalExponentForNegativeBase()
    {
        Assert.ThrowsException<ArgumentException>(() => VBOperators.Power(-4d, 0.5d));
    }

    [TestMethod]
    public void Power_ReportsDoubleOverflow()
    {
        Assert.ThrowsException<OverflowException>(() => VBOperators.Power(double.MaxValue, 2d));
    }
}