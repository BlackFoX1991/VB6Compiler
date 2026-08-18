namespace VB6.Runtime.Tests;

[TestClass]
public sealed class LongRuntimeTests
{
    [TestMethod]
    public void CLng_UsesVbBooleanAndBankersRoundingSemantics()
    {
        Assert.AreEqual(-1, VBConversions.CLng(true));
        Assert.AreEqual(0, VBConversions.CLng(false));
        Assert.AreEqual(2, VBConversions.CLng(1.5d));
        Assert.AreEqual(2, VBConversions.CLng(2.5d));
    }

    [TestMethod]
    public void LongArithmetic_UsesCheckedInt32Range()
    {
        Assert.AreEqual(60000, VBOperators.AddLong(40000, 20000));
        Assert.AreEqual(80000, VBOperators.MultiplyLong(40000, 2));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddLong(int.MaxValue, 1));
    }

    [TestMethod]
    public void LongIntegerDivisionAndMod_PreserveLongRange()
    {
        Assert.AreEqual(20000, VBOperators.IntegerDivideLong(60000, 3));
        Assert.AreEqual(2, VBOperators.ModLong(60002, 3));
    }
}
