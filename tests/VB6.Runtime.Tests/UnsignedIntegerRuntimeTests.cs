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

    [TestMethod]
    public void UnsignedWidthConversionsAndOperatorsPreserveRanges()
    {
        var small = VBConversions.CUShort(65534);
        var wide = VBConversions.CULng("18446744073709551614");

        Assert.AreEqual((ushort)65534, small);
        Assert.AreEqual(ushort.MaxValue, VBOperators.AddUShort(small, 1));
        Assert.AreEqual((ushort)0xF0F0, VBOperators.AndUShort(0xFFFF, 0xF0F0));
        Assert.AreEqual((ushort)65535, VBOperators.NotUShort(0));
        Assert.AreEqual(ulong.MaxValue, VBOperators.AddULong(wide, 1));
        Assert.AreEqual(ulong.MaxValue, VBOperators.NotULong(0));
        Assert.AreEqual("18446744073709551615", VBConversions.CStr(ulong.MaxValue));
        Assert.AreEqual((short)18, VBVariants.VarType(small));
        Assert.AreEqual((short)21, VBVariants.VarType(wide));
        Assert.AreEqual((ushort)65535, VBOperators.AddVariant(small, (ushort)1));
        Assert.AreEqual(uint.MaxValue, VBOperators.AddVariant(4000000000u, 294967295u));
        Assert.AreEqual(ulong.MaxValue, VBOperators.AddVariant(wide, 1UL));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddULong(ulong.MaxValue, 1));
    }
}
