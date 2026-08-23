namespace VB6.Runtime.Tests;

[TestClass]
public sealed class Int64RuntimeTests
{
    [TestMethod]
    public void CLngLng_UsesVbBooleanAndInt64Range()
    {
        Assert.AreEqual(-1L, VBConversions.CLngLng(true));
        Assert.AreEqual(0L, VBConversions.CLngLng(false));
        Assert.AreEqual(3000000000L, VBConversions.CLngLng(3000000000L));
    }

    [TestMethod]
    public void LongLongArithmetic_UsesCheckedInt64Range()
    {
        Assert.AreEqual(6000000000L, VBOperators.AddLongLong(3000000000L, 3000000000L));
        Assert.AreEqual(9000000000L, VBOperators.MultiplyLongLong(3000000000L, 3L));
        Assert.ThrowsException<OverflowException>(() => VBOperators.AddLongLong(long.MaxValue, 1L));
    }

    [TestMethod]
    public void LongLongIntegerDivisionAndMod_PreserveInt64Range()
    {
        Assert.AreEqual(3000000000L, VBOperators.IntegerDivideLongLong(9000000000L, 3L));
        Assert.AreEqual(2L, VBOperators.ModLongLong(9000000002L, 3L));
    }

    [TestMethod]
    public void LongPtrConversionAndArithmeticUseNativePointerWidth()
    {
        var value = VBConversions.CLngPtr(42L);

        Assert.AreEqual(new IntPtr(42), value);
        Assert.AreEqual(new IntPtr(84), VBOperators.AddLongPtr(value, value));
        Assert.AreEqual(new IntPtr(0x20), VBOperators.AndLongPtr(value, new IntPtr(0x60)));
        Assert.AreEqual(42L, VBConversions.CLngLng(value));
        Assert.AreEqual("42", VBConversions.CStr(value));
        Assert.AreEqual(IntPtr.Size == 8 ? (short)20 : (short)3, VBVariants.VarType(value));

        if (IntPtr.Size == 8)
        {
            Assert.AreEqual(new IntPtr(long.MaxValue), VBConversions.CLngPtr(long.MaxValue));
        }
    }
}
