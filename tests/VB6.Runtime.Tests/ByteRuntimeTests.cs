namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ByteRuntimeTests
{
    [TestMethod]
    public void CByte_UsesUnsignedRangeAndVbBooleanConversion()
    {
        Assert.AreEqual((byte)255, VBConversions.CByte(true));
        Assert.AreEqual((byte)0, VBConversions.CByte(false));
        Assert.AreEqual((byte)126, VBConversions.CByte(125.5678d));
        Assert.ThrowsException<OverflowException>(() => VBConversions.CByte(256));
        Assert.ThrowsException<OverflowException>(() => VBConversions.CByte(-1));
    }

    [TestMethod]
    public void ByteArithmetic_UsesCheckedUInt8Range()
    {
        Assert.AreEqual((byte)220, VBOperators.AddByte(200, 20));
        Assert.AreEqual((byte)200, VBOperators.MultiplyByte(100, 2));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddByte(255, 1));
        Assert.ThrowsException<OverflowException>(() => VBOperators.SubtractByte(0, 1));
    }

    [TestMethod]
    public void ByteIntegerDivisionAndMod_PreserveByteRange()
    {
        Assert.AreEqual((byte)20, VBOperators.IntegerDivideByte(100, 5));
        Assert.AreEqual((byte)4, VBOperators.ModByte(100, 6));
    }
}
