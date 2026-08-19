namespace VB6.Runtime.Tests;

[TestClass]
public sealed class BitwiseRuntimeTests
{
    [TestMethod]
    public void And_MasksBits()
    {
        Assert.AreEqual((byte)8, VBOperators.AndByte(12, 10));
        Assert.AreEqual((short)8, VBOperators.AndInteger(12, 10));
        Assert.AreEqual(8, VBOperators.AndLong(12, 10));
        Assert.AreEqual(8L, VBOperators.AndLongLong(12, 10));
    }

    [TestMethod]
    public void Or_SetsBits()
    {
        Assert.AreEqual((byte)14, VBOperators.OrByte(12, 10));
        Assert.AreEqual((short)14, VBOperators.OrInteger(12, 10));
        Assert.AreEqual(14, VBOperators.OrLong(12, 10));
        Assert.AreEqual(14L, VBOperators.OrLongLong(12, 10));
    }

    [TestMethod]
    public void Xor_TogglesBits()
    {
        Assert.AreEqual((byte)6, VBOperators.XorByte(12, 10));
        Assert.AreEqual((short)6, VBOperators.XorInteger(12, 10));
        Assert.AreEqual(6, VBOperators.XorLong(12, 10));
        Assert.AreEqual(6L, VBOperators.XorLongLong(12, 10));
    }

    [TestMethod]
    public void Not_ComplementsAllBits()
    {
        Assert.AreEqual((short)-2, VBOperators.NotInteger(1));
        Assert.AreEqual(-2, VBOperators.NotLong(1));
        Assert.AreEqual(-2L, VBOperators.NotLongLong(1));
        Assert.AreEqual((short)-1, VBOperators.NotInteger(0));
    }

    [TestMethod]
    public void Eqv_IsTheComplementOfXor()
    {
        Assert.AreEqual((short)-7, VBOperators.EqvInteger(12, 10));
        Assert.AreEqual(-7, VBOperators.EqvLong(12, 10));
        Assert.AreEqual(-7L, VBOperators.EqvLongLong(12, 10));

        // Equal operands leave every bit set, which is VB6 True.
        Assert.AreEqual((short)-1, VBOperators.EqvInteger(5, 5));
    }

    [TestMethod]
    public void Imp_IsImplication()
    {
        Assert.AreEqual((short)-1, VBOperators.ImpInteger(0, 0));
        Assert.AreEqual((short)-1, VBOperators.ImpInteger(0, -1));
        Assert.AreEqual((short)0, VBOperators.ImpInteger(-1, 0));
        Assert.AreEqual((short)-1, VBOperators.ImpInteger(-1, -1));
        Assert.AreEqual(-1, VBOperators.ImpLong(0, 0));
        Assert.AreEqual(-1L, VBOperators.ImpLongLong(0, 0));
    }

    [TestMethod]
    public void Imp_StaysWithinSixteenBitsForInteger()
    {
        // ~left must not sign-extend past the Integer width.
        Assert.AreEqual((short)-4, VBOperators.ImpInteger(11, 12));
    }
}
