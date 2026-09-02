using System.Reflection;

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
        Assert.IsTrue(VBVariants.IsNull(VBMath.Sgn(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Fix(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Round(nullValue, 0)));

        Assert.AreEqual((short)0, VBMath.Abs(null));
        Assert.AreEqual((short)0, VBMath.Sgn(null));
        Assert.AreEqual((short)0, VBMath.Fix(null));
        Assert.AreEqual((short)0, VBMath.Round(null, 0));
    }

    [TestMethod]
    public void Int_UsesVariantStateAndCentralConversions()
    {
        var nullValue = VBVariants.NullValue();
        var array = new VBArray<object>(new VBArrayBound(0, 0));

        Assert.IsTrue(VBVariants.IsNull(VBConversions.Int(nullValue)));
        Assert.AreEqual((short)0, VBConversions.Int(null));

        var currency = VBConversions.Int(VBConversions.CCur(1.75m));
        Assert.IsInstanceOfType<VBCurrency>(currency);
        Assert.AreEqual(1m, ((VBCurrency)currency).ToDecimal());

        var date = VBConversions.Int(new VBDateValue(43832.75d));
        Assert.IsInstanceOfType<VBDateValue>(date);
        Assert.AreEqual(43832d, ((VBDateValue)date).OADate);
        Assert.ThrowsException<VB6MissingArgumentException>(
            () => VBConversions.Int(VBVariants.MissingValue()));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBConversions.Int(array));
    }

    [TestMethod]
    public void IntFixAndAbs_PreserveNumericVariantSubtypes()
    {
        var negativeCurrency = VBConversions.CCur(-1.75m);
        var fixedCurrency = VBMath.Fix(negativeCurrency);
        var flooredCurrency = VBConversions.Int(negativeCurrency);
        var absoluteCurrency = VBMath.Abs(negativeCurrency);
        var date = new VBDateValue(43832.75d);
        var negativeDate = new VBDateValue(-1.75d);

        Assert.IsInstanceOfType<VBCurrency>(fixedCurrency);
        Assert.AreEqual(-1m, ((VBCurrency)fixedCurrency).ToDecimal());
        Assert.IsInstanceOfType<VBCurrency>(flooredCurrency);
        Assert.AreEqual(-2m, ((VBCurrency)flooredCurrency).ToDecimal());
        Assert.IsInstanceOfType<VBCurrency>(absoluteCurrency);
        Assert.AreEqual(1.75m, ((VBCurrency)absoluteCurrency).ToDecimal());

        Assert.AreEqual(new VBDateValue(43832d), VBMath.Fix(date));
        Assert.AreEqual(new VBDateValue(43832d), VBConversions.Int(date));
        Assert.AreEqual(new VBDateValue(1.75d), VBMath.Abs(negativeDate));
        Assert.AreEqual(new VBDateValue(-1d), VBMath.Fix(negativeDate));
        Assert.AreEqual(new VBDateValue(-2d), VBConversions.Int(negativeDate));

        Assert.IsInstanceOfType<float>(VBMath.Fix(-1.75f));
        Assert.IsInstanceOfType<float>(VBConversions.Int(-1.75f));
        Assert.IsInstanceOfType<decimal>(VBMath.Fix(-1.75m));
        Assert.IsInstanceOfType<decimal>(VBConversions.Int(-1.75m));
    }

    [TestMethod]
    public void MathIntrinsics_UseBankersRoundingAndDomainErrors()
    {
        Assert.AreEqual(0.1234m, VBMath.Round(0.12345m, 4));
        Assert.AreEqual(0.1236m, VBMath.Round(0.12355m, 4));
        Assert.AreEqual(-1.8m, VBMath.Round(VBConversions.CCur(-1.75m), 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBMath.Sqr(-1d));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBMath.Log(0d));
        Assert.ThrowsException<OverflowException>(() => VBMath.Exp(1000d));
    }

    [TestMethod]
    public void VariantMath_UsesStandardMissingAndArrayErrors()
    {
        var missing = VBVariants.MissingValue();
        var array = new VBArray<object>(new VBArrayBound(0, 0));

        Assert.ThrowsException<VB6MissingArgumentException>(() => VBMath.Abs(missing));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBMath.Fix(missing));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBMath.Round(missing, 0));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBMath.Abs(array));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBMath.Fix(array));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBMath.Round(array, 0));
    }

    [TestMethod]
    public void ObjectVariants_ResolveDefaultValuesForVariantMathAndErrorConversion()
    {
        var number = new NumericDefaultObject();
        var errorNumber = new ErrorNumberDefaultObject();
        var nullValue = new NullDefaultObject();

        Assert.AreEqual(1.75d, VBMath.Abs(number));
        Assert.AreEqual((short)-1, VBMath.Sgn(number));
        Assert.AreEqual(-1d, VBMath.Fix(number));
        Assert.AreEqual(-1.8m, VBMath.Round(number, 1));
        Assert.AreEqual(-2d, VBConversions.Int(number));
        Assert.AreEqual(new VBErrorValue(2001), VBConversions.CVErr(errorNumber));

        Assert.IsTrue(VBVariants.IsNull(VBMath.Abs(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Sgn(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Fix(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBMath.Round(nullValue, 0)));
        Assert.IsTrue(VBVariants.IsNull(VBConversions.Int(nullValue)));
    }

    [DefaultMember(nameof(Value))]
    private sealed class NumericDefaultObject
    {
        public double Value => -1.75d;
    }

    [DefaultMember(nameof(Value))]
    private sealed class ErrorNumberDefaultObject
    {
        public int Value => 2001;
    }

    [DefaultMember(nameof(Value))]
    private sealed class NullDefaultObject
    {
        public object Value => VBVariants.NullValue();
    }

    [TestMethod]
    public void Rnd_UsesTheVB6SequenceAndHonorsArgumentModes()
    {
        Assert.AreEqual(0.7055475f, VBMath.Rnd(), 0.0000001f);
        var second = VBMath.Rnd();
        Assert.AreEqual(0.533424f, second, 0.000001f);
        Assert.AreEqual(second, VBMath.Rnd(0f));

        var seeded = VBMath.Rnd(-1f);
        Assert.AreEqual(seeded, VBMath.Rnd(-1f));
    }

    [TestMethod]
    public void Randomize_RepeatsASequenceForTheSameNumericSeed()
    {
        VBMath.Rnd(-1f);
        VBMath.Randomize(1d);
        var first = VBMath.Rnd();

        VBMath.Rnd(-1f);
        VBMath.Randomize(1d);
        Assert.AreEqual(first, VBMath.Rnd());
    }
}
