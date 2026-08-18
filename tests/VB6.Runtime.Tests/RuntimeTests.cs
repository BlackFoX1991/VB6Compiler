namespace VB6.Runtime.Tests;

[TestClass]
public sealed class RuntimeTests
{
    [TestMethod]
    public void CInt_UsesBankersRounding()
    {
        Assert.AreEqual((short)2, VBConversions.CInt(1.5d));
        Assert.AreEqual((short)2, VBConversions.CInt(2.5d));
    }

    [TestMethod]
    public void IntegerAddition_ThrowsOnOverflow()
    {
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.AddInteger(short.MaxValue, 1));
    }

    [TestMethod]
    public void StringComparison_IsBinaryByDefault()
    {
        Assert.IsTrue(VBOperators.Less("A", "a"));
    }
}
