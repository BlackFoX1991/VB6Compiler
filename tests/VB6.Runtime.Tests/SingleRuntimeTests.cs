namespace VB6.Runtime.Tests;

[TestClass]
public sealed class SingleRuntimeTests
{
    [TestMethod]
    public void CSng_UsesVbBooleanAndFloatingConversion()
    {
        Assert.AreEqual(-1f, VBConversions.CSng(true));
        Assert.AreEqual(0f, VBConversions.CSng(false));
        Assert.AreEqual(1.5f, VBConversions.CSng(1.5d));
    }

    [TestMethod]
    public void SingleArithmetic_UsesFloatPrecision()
    {
        Assert.AreEqual(2.5f, VBOperators.AddSingle(1.5f, 1f));
        Assert.AreEqual(0.5f, VBOperators.DivideSingle(1f, 2f));
    }

    [TestMethod]
    public void SingleArithmetic_ReportsOverflowAndDivisionByZero()
    {
        Assert.ThrowsException<OverflowException>(() => VBOperators.MultiplySingle(float.MaxValue, 2f));
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.DivideSingle(1f, 0f));
    }
}
