using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VariantMultiplyTests
{
    [TestMethod]
    public void MultiplyInteger_VariantOverloadPreservesAndPromotesNumericSubtypes()
    {
        var byteResult = VBOperators.MultiplyInteger((object?)(byte)2, (object?)(byte)3);
        Assert.IsInstanceOfType<byte>(byteResult);
        Assert.AreEqual((byte)6, byteResult);

        var integerPromotion = VBOperators.MultiplyInteger((object?)(byte)200, (object?)(byte)2);
        Assert.IsInstanceOfType<short>(integerPromotion);
        Assert.AreEqual((short)400, integerPromotion);

        var longPromotion = VBOperators.MultiplyInteger((object?)(short)300, (object?)(short)200);
        Assert.IsInstanceOfType<int>(longPromotion);
        Assert.AreEqual(60000, longPromotion);

        var doublePromotion = VBOperators.MultiplyInteger((object?)50000, (object?)50000);
        Assert.IsInstanceOfType<double>(doublePromotion);
        Assert.AreEqual(2500000000d, doublePromotion);
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadHandlesEmptyBooleanStringAndMixedPrecision()
    {
        Assert.AreEqual(0, VBOperators.MultiplyInteger(null, (object?)7));
        Assert.AreEqual((short)-2, VBOperators.MultiplyInteger((object?)true, (object?)(short)2));
        Assert.AreEqual(5d, VBOperators.MultiplyInteger((object?)"2.5", (object?)(short)2));
        Assert.AreEqual(6d, VBOperators.MultiplyInteger((object?)2f, (object?)3));

        var currencyAndInteger = VBOperators.MultiplyInteger(
            (object?)VBConversions.CCur(1.5m),
            (object?)(short)2);
        var currencyAndDouble = VBOperators.MultiplyInteger(
            (object?)VBConversions.CCur(1.5m),
            (object?)2d);
        Assert.IsInstanceOfType<VBCurrency>(currencyAndInteger);
        Assert.AreEqual(VBConversions.CCur(3m), currencyAndInteger);
        Assert.IsInstanceOfType<double>(currencyAndDouble);
        Assert.AreEqual(3d, currencyAndDouble);
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadPropagatesNull()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBOperators.MultiplyInteger(nullValue, 2)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.MultiplyInteger(2, nullValue)));
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadRejectsInvalidValuesAndDoubleOverflow()
    {
        Assert.ThrowsException<InvalidCastException>(() =>
            VBOperators.MultiplyInteger((object?)"not-a-number", (object?)(short)2));
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.MultiplyInteger((object?)double.MaxValue, (object?)2d));
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadUsesSingleCurrencyAndDecimalPromotionRules()
    {
        var singleAndInteger = VBOperators.MultiplyInteger((object?)1f, (object?)(short)2);
        var singleAndLong = VBOperators.MultiplyInteger((object?)1f, (object?)2);
        var currencyAndSingle = VBOperators.MultiplyInteger(
            (object?)VBConversions.CCur(1.25m),
            (object?)0.5f);
        var decimalAndDouble = VBOperators.MultiplyInteger(
            VBConversions.CDec("1.25"),
            (object?)0.5d);

        Assert.IsInstanceOfType<float>(singleAndInteger);
        Assert.IsInstanceOfType<double>(singleAndLong);
        Assert.IsInstanceOfType<VBCurrency>(currencyAndSingle);
        Assert.IsInstanceOfType<decimal>(decimalAndDouble);
        Assert.AreEqual(2f, singleAndInteger);
        Assert.AreEqual(2d, singleAndLong);
        Assert.AreEqual(0.625m, ((VBCurrency)currencyAndSingle!).ToDecimal());
        Assert.AreEqual(0.625m, decimalAndDouble);
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadPromotesUnsignedOverflowWithoutWrapping()
    {
        var unsignedShort = VBOperators.MultiplyInteger(
            (object?)ushort.MaxValue,
            (object?)(ushort)ushort.MaxValue);
        var unsignedInteger = VBOperators.MultiplyInteger(
            (object?)uint.MaxValue,
            (object?)uint.MaxValue);
        var unsignedLong = VBOperators.MultiplyInteger(
            (object?)ulong.MaxValue,
            (object?)2UL);

        Assert.IsInstanceOfType<uint>(unsignedShort);
        Assert.AreEqual(4_294_836_225u, unsignedShort);
        Assert.IsInstanceOfType<ulong>(unsignedInteger);
        Assert.AreEqual(18_446_744_065_119_617_025UL, unsignedInteger);
        Assert.IsInstanceOfType<decimal>(unsignedLong);
        Assert.AreEqual(36_893_488_147_419_103_230m, unsignedLong);
    }
}
