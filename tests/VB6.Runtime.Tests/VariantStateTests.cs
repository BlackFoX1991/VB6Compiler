namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VariantStateTests
{
    [TestMethod]
    public void StatePredicates_DistinguishEmptyNullNothingAndMissing()
    {
        Assert.IsTrue(VBVariants.IsEmpty(VBVariants.EmptyValue()));
        Assert.IsFalse(VBVariants.IsNull(VBVariants.EmptyValue()));

        var nullValue = VBVariants.NullValue();
        Assert.IsTrue(VBVariants.IsNull(nullValue));
        Assert.IsFalse(VBVariants.IsEmpty(nullValue));

        var nothingValue = VBVariants.NothingValue();
        Assert.IsFalse(VBVariants.IsEmpty(nothingValue));
        Assert.IsFalse(VBVariants.IsNull(nothingValue));

        var missingValue = VBVariants.MissingValue();
        Assert.IsTrue(VBVariants.IsMissing(missingValue));
        Assert.IsFalse(VBVariants.IsNull(missingValue));
    }

    [TestMethod]
    public void VarType_ReturnsVb6VariantCodesForSupportedRuntimeValues()
    {
        Assert.AreEqual((short)0, VBVariants.VarType(VBVariants.EmptyValue()));
        Assert.AreEqual((short)1, VBVariants.VarType(VBVariants.NullValue()));
        Assert.AreEqual((short)2, VBVariants.VarType((short)1));
        Assert.AreEqual((short)3, VBVariants.VarType(1));
        Assert.AreEqual((short)6, VBVariants.VarType(VBConversions.CCur(1m)));
        Assert.AreEqual((short)14, VBVariants.VarType(VBConversions.CDec(1.25m)));
        Assert.AreEqual((short)8, VBVariants.VarType("value"));
        Assert.AreEqual((short)9, VBVariants.VarType(VBVariants.NothingValue()));
        Assert.AreEqual((short)10, VBVariants.VarType(VBVariants.MissingValue()));
        Assert.AreEqual((short)11, VBVariants.VarType(true));
        Assert.AreEqual((short)17, VBVariants.VarType((byte)1));
        Assert.AreEqual((short)20, VBVariants.VarType((long)1));
        Assert.AreEqual((short)8200, VBVariants.VarType(new VBArray<string>(new VBArrayBound(0, 1))));
        Assert.AreEqual((short)8204, VBVariants.VarType(new VBArray<object>(new VBArrayBound(0, 1))));
    }
}
