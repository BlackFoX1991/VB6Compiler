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

        var currency = VBOperators.MultiplyInteger(
            (object?)VBConversions.CCur(1.5m),
            (object?)(short)2);
        Assert.IsInstanceOfType<VBCurrency>(currency);
        Assert.AreEqual(VBConversions.CCur(3m), currency);
    }

    [TestMethod]
    public void MultiplyInteger_VariantOverloadRejectsInvalidValuesAndDoubleOverflow()
    {
        Assert.ThrowsException<InvalidCastException>(() =>
            VBOperators.MultiplyInteger((object?)"not-a-number", (object?)(short)2));
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.MultiplyInteger((object?)double.MaxValue, (object?)2d));
    }
}
