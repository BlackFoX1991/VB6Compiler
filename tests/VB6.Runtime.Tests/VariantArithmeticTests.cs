namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VariantArithmeticTests
{
    [TestMethod]
    public void Arithmetic_PreservesVariantPromotionAndHandlesEmpty()
    {
        Assert.AreEqual((short)3, VBOperators.AddVariant((short)1, (short)2));
        Assert.AreEqual(60000, VBOperators.AddVariant((short)30000, (short)30000));
        Assert.AreEqual((short)-1, VBOperators.SubtractVariant(null, (short)1));
        Assert.AreEqual(2.5d, VBOperators.DivideVariant(5, 2));
        Assert.AreEqual(2, VBOperators.IntegerDivideVariant(5, 2));
        Assert.AreEqual(1, VBOperators.ModVariant(5, 2));
        Assert.AreEqual(8d, VBOperators.PowerVariant(2, 3));
        Assert.AreEqual((short)-2, VBOperators.NegateVariant((short)2));
    }

    [TestMethod]
    public void Arithmetic_PropagatesNull()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBOperators.AddVariant(nullValue, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.DivideVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.NegateVariant(nullValue)));
    }

    [TestMethod]
    public void DateVariants_PreserveDateSubtypeForAdditionAndSingleDateSubtraction()
    {
        var date = VBConversions.DateToVariant(43832d);

        var next = VBOperators.AddVariant(date, 1);
        Assert.IsInstanceOfType<VBDateValue>(next);
        Assert.AreEqual(43833d, ((VBDateValue)next!).OADate);

        var previous = VBOperators.SubtractVariant(date, 1);
        Assert.IsInstanceOfType<VBDateValue>(previous);
        Assert.AreEqual(43831d, ((VBDateValue)previous!).OADate);

        Assert.AreEqual(1d, VBOperators.SubtractVariant(next, date));
    }

    [TestMethod]
    public void DecimalVariant_PreservesDecimalPrecisionAndUsesDoubleForPower()
    {
        var decimalValue = VBConversions.CDec("7922816251426433759354395033.5");

        Assert.AreEqual(7922816251426433759354395034.5m, VBOperators.AddVariant(decimalValue, 1));
        Assert.AreEqual(2.5m, VBOperators.MultiplyInteger(VBConversions.CDec("1.25"), (object?)2));
        Assert.AreEqual(0.625m, VBOperators.DivideVariant(VBConversions.CDec("1.25"), 2));
        Assert.AreEqual(8d, VBOperators.PowerVariant(VBConversions.CDec("2"), 3));
        Assert.AreEqual(2.25m, VBOperators.AddVariant(VBConversions.CDec("1.25"), 1d));
        var currencyResult = VBOperators.AddVariant(VBConversions.CCur(1m), 0.5d);
        Assert.IsInstanceOfType<VBCurrency>(currencyResult);
        Assert.AreEqual(1.5m, ((VBCurrency)currencyResult!).ToDecimal());
        Assert.AreEqual(-1.25m, VBOperators.NegateVariant(VBConversions.CDec("1.25")));
        Assert.AreEqual(2, VBOperators.IntegerDivideVariant(VBConversions.CDec("5.1"), 2));
        Assert.AreEqual(-2, VBOperators.NotVariant(VBConversions.CDec("1.1")));
        Assert.AreEqual(0, VBOperators.AndVariant(VBConversions.CDec("5.1"), 2));
    }

    [TestMethod]
    public void CDec_ProducesVariantDecimalAndPreservesNull()
    {
        Assert.AreEqual(1.25m, VBConversions.CDec("1.25"));
        Assert.IsTrue(VBVariants.IsNull(VBConversions.CDec(VBVariants.NullValue())));
    }

    [TestMethod]
    public void VariantConcatenation_TreatsNullAsEmptyString()
    {
        var nullValue = VBVariants.NullValue();

        Assert.AreEqual("x", VBOperators.ConcatVariant(nullValue, "x"));
        Assert.AreEqual("x", VBOperators.ConcatVariant("x", nullValue));
    }

    [TestMethod]
    public void Addition_DistinguishesVariantStringAndEmptySemantics()
    {
        Assert.AreEqual("ab", VBOperators.AddVariant("a", "b"));
        Assert.AreEqual("a", VBOperators.AddVariant("a", null));
        Assert.AreEqual(2d, VBOperators.AddVariant("1", 1));
        Assert.AreEqual("a1", VBOperators.AddStringVariant("a", 1));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.AddStringVariant("a", VBVariants.NullValue())));
    }

    [TestMethod]
    public void VariantComparisons_PropagateNull()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBOperators.VariantEqual(nullValue, 1)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.VariantNotEqual(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.VariantLess(nullValue, 1)));
    }

    [TestMethod]
    public void VariantComparisons_UseStringRulesForStringVariantsAndEmpty()
    {
        Assert.IsTrue((bool)VBOperators.VariantLess("10", "2"));
        Assert.IsTrue((bool)VBOperators.VariantEqual(null, string.Empty));
        Assert.IsTrue((bool)VBOperators.VariantLess(null, "a"));
        Assert.IsTrue((bool)VBOperators.StringVariantLess("10", 2));
        Assert.IsTrue((bool)VBOperators.StringVariantEqual("10", 10));
    }

    [TestMethod]
    public void DecimalComparisons_PreservePrecisionAgainstFloatingVariants()
    {
        var value = VBConversions.CDec("0.100000000000000005");

        Assert.IsTrue((bool)VBOperators.VariantGreater(value, 0.1d));
        Assert.IsFalse((bool)VBOperators.VariantEqual(value, 0.1d));
    }

    [TestMethod]
    public void VariantComparisons_UseVb6CurrencyAndSinglePrecisionRules()
    {
        var currency = VBConversions.CCur(1m);

        Assert.IsTrue((bool)VBOperators.VariantEqual(currency, 1.00004d));
        Assert.IsFalse((bool)VBOperators.VariantEqual(currency, 1.00006d));
        Assert.IsTrue((bool)VBOperators.VariantEqual(0.1f, 0.1d));
    }
}
