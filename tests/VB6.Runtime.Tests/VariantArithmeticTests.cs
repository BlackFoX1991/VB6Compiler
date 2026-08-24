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
    public void Division_PromotesByteIntegerAndSingleVariantsToSingle()
    {
        var integerResult = VBOperators.DivideVariant((short)5, (short)2);
        var singleResult = VBOperators.DivideVariant(5f, 2f);

        Assert.IsInstanceOfType<float>(integerResult);
        Assert.AreEqual(2.5f, integerResult);
        Assert.IsInstanceOfType<float>(singleResult);
        Assert.AreEqual(2.5f, singleResult);
        Assert.IsInstanceOfType<double>(VBOperators.DivideVariant(5, 2));
    }

    [TestMethod]
    public void Arithmetic_PromotesSingleAndIntegerOverflowToTheNextVariantWidth()
    {
        var singleAdd = VBOperators.AddVariant(float.MaxValue, float.MaxValue);
        var singleSubtract = VBOperators.SubtractVariant(float.MinValue, float.MaxValue);
        var singleDivide = VBOperators.DivideVariant(float.MaxValue, 0.5f);
        var integerNegate = VBOperators.NegateVariant(short.MinValue);
        var longNegate = VBOperators.NegateVariant(int.MinValue);

        Assert.IsInstanceOfType<double>(singleAdd);
        Assert.IsInstanceOfType<double>(singleSubtract);
        Assert.IsInstanceOfType<double>(singleDivide);
        Assert.AreEqual(2 * (double)float.MaxValue, singleAdd);
        Assert.AreEqual((double)float.MinValue - float.MaxValue, singleSubtract);
        Assert.AreEqual((double)float.MaxValue / 0.5d, singleDivide);
        Assert.AreEqual(32768, integerNegate);
        Assert.AreEqual(2147483648d, longNegate);
    }

    [TestMethod]
    public void Arithmetic_RejectsVariantDoubleOverflow()
    {
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.AddVariant(double.MaxValue, double.MaxValue));
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.SubtractVariant(double.MinValue, double.MaxValue));
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.DivideVariant(double.MaxValue, double.Epsilon));
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
        Assert.IsInstanceOfType<double>(currencyResult);
        Assert.AreEqual(1.5d, currencyResult);
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
    public void Multiplication_PromotesCurrencyBeforeDouble()
    {
        var result = VBOperators.MultiplyInteger(VBConversions.CCur(1m), 0.5d);

        Assert.IsInstanceOfType<VBCurrency>(result);
        Assert.AreEqual(0.5m, ((VBCurrency)result!).ToDecimal());
    }

    [TestMethod]
    public void VariantComparisons_UseVb6CurrencyAndSinglePrecisionRules()
    {
        var currency = VBConversions.CCur(1m);

        Assert.IsTrue((bool)VBOperators.VariantEqual(currency, 1.00004d));
        Assert.IsFalse((bool)VBOperators.VariantEqual(currency, 1.00006d));
        Assert.IsTrue((bool)VBOperators.VariantEqual(0.1f, 0.1d));
    }

    [TestMethod]
    public void ErrorVariants_CompareByCodeButRejectOtherOperators()
    {
        var first = new VBErrorValue(2001);
        var same = new VBErrorValue(2001);
        var later = new VBErrorValue(2002);

        Assert.IsTrue((bool)VBOperators.VariantEqual(first, same));
        Assert.IsFalse((bool)VBOperators.VariantNotEqual(first, same));
        Assert.IsTrue((bool)VBOperators.VariantLess(first, later));
        Assert.IsTrue((bool)VBOperators.VariantGreater(later, first));

        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.VariantEqual(first, 2001));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AddVariant(first, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.MultiplyInteger(first, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.ConcatVariant(first, "value"));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.NotVariant(first));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AndVariant(first, 1));
    }

    [TestMethod]
    public void MissingVariants_RaiseError448ForVariantOperations()
    {
        var missing = VBVariants.MissingValue();

        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.AddVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.MultiplyInteger(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.ConcatVariant(missing, "value"));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.VariantEqual(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.NotVariant(missing));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.AndVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBDebug.Format(missing));
    }

    [TestMethod]
    public void ArrayVariants_RaiseTypeMismatchForScalarOperations()
    {
        var array = new VBArray<object>(new VBArrayBound(0, 1));

        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AddVariant(array, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.MultiplyInteger(array, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.ConcatVariant(array, "value"));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.VariantEqual(array, array));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.NotVariant(array));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AndVariant(array, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBConversions.CInt(array));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBDebug.Format(array));
    }
}
