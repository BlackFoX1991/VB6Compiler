namespace VB6.Runtime.Tests;

[TestClass]
public sealed class UnsignedIntegerRuntimeTests
{
    [TestMethod]
    public void UIntegerConversionAndOperatorsPreserveUInt32Range()
    {
        var value = VBConversions.CUInt(4000000000L);

        Assert.AreEqual(4000000000u, value);
        Assert.AreEqual(uint.MaxValue, VBOperators.AddUInteger(value, 294967295u));
        Assert.AreEqual(2000000000u, VBOperators.IntegerDivideUInteger(value, 2u));
        Assert.AreEqual(1u, VBOperators.ModUInteger(value, 3u));
        Assert.AreEqual(0xE0000000u, VBOperators.AndUInteger(value, 0xF0000000u));
        Assert.AreEqual(uint.MaxValue, VBOperators.NotUInteger(0u));
        Assert.AreEqual("4000000000", VBConversions.CStr(value));
        Assert.AreEqual((short)20, VBVariants.VarType(value));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddUInteger(uint.MaxValue, 1u));
    }
}
