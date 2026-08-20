namespace VB6.Runtime.Tests;

[TestClass]
public sealed class DecimalRuntimeTests
{
    [TestMethod]
    public void CDec_UsesVbBooleanAndCurrencyConversion()
    {
        Assert.AreEqual(-1m, VBConversions.CDec(true));
        Assert.AreEqual(0m, VBConversions.CDec(false));
        Assert.AreEqual(1.2345m, VBConversions.CDec(VBConversions.CCur(1.2345m)));
    }

    [TestMethod]
    public void DecimalArithmetic_UsesCheckedDecimalOperators()
    {
        Assert.AreEqual(3.75m, VBOperators.AddDecimal(1.25m, 2.5m));
        Assert.AreEqual(3.125m, VBOperators.MultiplyDecimal(1.25m, 2.5m));
        Assert.AreEqual(0.5m, VBOperators.DivideDecimal(1m, 2m));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddDecimal(decimal.MaxValue, 1m));
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.DivideDecimal(1m, 0m));
    }
}
