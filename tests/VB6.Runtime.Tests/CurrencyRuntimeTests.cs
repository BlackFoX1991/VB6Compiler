namespace VB6.Runtime.Tests;

[TestClass]
public sealed class CurrencyRuntimeTests
{
    [TestMethod]
    public void CCur_UsesFourDecimalBankersRoundingAndVbBooleanValues()
    {
        Assert.AreEqual(1.2344m, VBConversions.CCur(1.23445m).ToDecimal());
        Assert.AreEqual(1.2346m, VBConversions.CCur(1.23455m).ToDecimal());
        Assert.AreEqual(-1m, VBConversions.CCur(true).ToDecimal());
        Assert.AreEqual(0m, VBConversions.CCur(false).ToDecimal());
    }

    [TestMethod]
    public void CurrencyArithmetic_UsesScaledCheckedRange()
    {
        var left = VBConversions.CCur(1.2345m);
        var right = VBConversions.CCur(1.2345m);

        Assert.AreEqual(2.469m, VBOperators.AddCurrency(left, right).ToDecimal());
        Assert.AreEqual(1.524m, VBOperators.MultiplyCurrency(left, right).ToDecimal());

        var maximum = VBConversions.CCur(922337203685477.5807m);
        var smallestStep = VBConversions.CCur(0.0001m);
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddCurrency(maximum, smallestStep));
    }

    [TestMethod]
    public void CurrencyToIntegralConversions_UseBankersRounding()
    {
        Assert.AreEqual((short)2, VBConversions.CInt(VBConversions.CCur(2.5m)));
        Assert.AreEqual((short)4, VBConversions.CInt(VBConversions.CCur(3.5m)));
        Assert.AreEqual(4L, VBConversions.CLngLng(VBConversions.CCur(3.5m)));
    }
}
