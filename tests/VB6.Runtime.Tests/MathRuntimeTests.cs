namespace VB6.Runtime.Tests;

[TestClass]
public sealed class MathRuntimeTests
{
    [TestMethod]
    public void ExtendedMathIntrinsics_UseRadiansAndNaturalLogarithms()
    {
        Assert.AreEqual(Math.E, VBMath.Exp(1d), 1e-12);
        Assert.AreEqual(1d, VBMath.Log(Math.E), 1e-12);
        Assert.AreEqual(1d, VBMath.Sin(Math.PI / 2d), 1e-12);
        Assert.AreEqual(1d, VBMath.Cos(0d), 1e-12);
        Assert.AreEqual(0d, VBMath.Tan(0d), 1e-12);
        Assert.AreEqual(Math.PI / 4d, VBMath.Atn(1d), 1e-12);
    }

    [TestMethod]
    public void VariantMath_PreservesNullAndTreatsEmptyAsZero()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBMath.Abs(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Fix(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Round(nullValue, 0)));

        Assert.AreEqual((short)0, VBMath.Abs(null));
        Assert.AreEqual((short)0, VBMath.Fix(null));
        Assert.AreEqual((short)0, VBMath.Round(null, 0));
    }
}
