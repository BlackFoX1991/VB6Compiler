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
    public void FloatingDivision_UsesVb6RuntimeErrors()
    {
        Assert.ThrowsException<OverflowException>(() => VBOperators.MultiplySingle(float.MaxValue, 2f));
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.DivideSingle(1f, 0f));
        Assert.ThrowsException<OverflowException>(() => VBOperators.DivideSingle(0f, 0f));
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.DivideDouble(1d, 0d));
        Assert.ThrowsException<OverflowException>(() => VBOperators.DivideDouble(0d, 0d));
    }
}
