using System.Reflection;

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
        Assert.IsTrue(VBVariants.IsNull(VBOperators.SubtractVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.MultiplyInteger(nullValue, 2)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.DivideVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.IntegerDivideVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.ModVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.PowerVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.NegateVariant(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.NotVariant(nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.AndVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.OrVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.XorVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.EqvVariant(1, nullValue)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.ImpVariant(1, nullValue)));
    }

    [TestMethod]
    public void LogicalOperators_ResolveTheAbsorbingCasesOfTheNullTruthTable()
    {
        var nullValue = VBVariants.NullValue();

        // And ist False, sobald eine Seite False ist -- die unbekannte Seite entscheidet nichts
        // mehr. Der bestimmende Operand kommt unveraendert zurueck, damit False Boolean bleibt
        // und 0 seinen numerischen Untertyp behaelt.
        Assert.AreEqual(false, VBOperators.AndVariant(nullValue, false));
        Assert.AreEqual(false, VBOperators.AndVariant(false, nullValue));
        Assert.AreEqual((short)0, VBOperators.AndVariant(nullValue, (short)0));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.AndVariant(nullValue, true)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.AndVariant(nullValue, 1)));

        // Or ist True, sobald eine Seite True ist. Numerisch entscheidet nur der Wert mit allen
        // gesetzten Bits; eine beliebige Zahl laesst das Ergebnis Null.
        Assert.AreEqual(true, VBOperators.OrVariant(nullValue, true));
        Assert.AreEqual(true, VBOperators.OrVariant(true, nullValue));
        Assert.AreEqual(-1, VBOperators.OrVariant(nullValue, -1));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.OrVariant(nullValue, false)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.OrVariant(nullValue, 1)));

        // Imp ist True, sobald der Vordersatz False oder der Nachsatz True ist.
        Assert.AreEqual(true, VBOperators.ImpVariant(false, nullValue));
        Assert.AreEqual(true, VBOperators.ImpVariant(nullValue, true));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.ImpVariant(nullValue, false)));

        // Xor, Eqv und Not haben keinen absorbierenden Fall.
        Assert.IsTrue(VBVariants.IsNull(VBOperators.XorVariant(nullValue, true)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.EqvVariant(nullValue, true)));
        Assert.IsTrue(VBVariants.IsNull(VBOperators.NotVariant(nullValue)));
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
    public void Division_TreatsEmptyAsIntegerWhenPromotingWithSingle()
    {
        var emptyLeft = VBOperators.DivideVariant(null, 2f);

        Assert.IsInstanceOfType<float>(emptyLeft);
        Assert.AreEqual(0f, emptyLeft);
        Assert.ThrowsException<DivideByZeroException>(() => VBOperators.DivideVariant(2f, null));
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
    public void Arithmetic_RejectsVariantPowerOverflow()
    {
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.PowerVariant(double.MaxValue, 2));
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
    public void DateTimeVariants_UseOleDateConversionsAndPreserveDateArithmetic()
    {
        var date = new DateTime(2020, 1, 1);

        Assert.AreEqual(43831d, VBConversions.CDbl(date));
        Assert.AreEqual(43831m, VBConversions.CDec(date));

        var next = VBOperators.AddVariant(date, 1);
        Assert.IsInstanceOfType<VBDateValue>(next);
        Assert.AreEqual(43832d, ((VBDateValue)next!).OADate);

        var previous = VBOperators.SubtractVariant(date, 1);
        Assert.IsInstanceOfType<VBDateValue>(previous);
        Assert.AreEqual(43830d, ((VBDateValue)previous!).OADate);
    }

    [TestMethod]
    public void EmptyAddition_PreservesTheRemainingDateOperandAndDateOverflowPromotesToDouble()
    {
        var date = new DateTime(2020, 1, 1);

        var rightEmpty = VBOperators.AddVariant(date, VBVariants.EmptyValue());
        var leftEmpty = VBOperators.AddVariant(VBVariants.EmptyValue(), date);

        Assert.IsInstanceOfType<DateTime>(rightEmpty);
        Assert.IsInstanceOfType<DateTime>(leftEmpty);
        Assert.AreEqual(date, rightEmpty);
        Assert.AreEqual(date, leftEmpty);

        var overflowingDate = new VBDateValue(2_958_465d);
        var result = VBOperators.AddVariant(overflowingDate, 1d);

        Assert.IsInstanceOfType<double>(result);
        Assert.AreEqual(2_958_466d, result);
    }

    [TestMethod]
    public void VariantArithmetic_UsesVb6PrecisionOrderForSingleCurrencyAndDecimal()
    {
        var currency = VBConversions.CCur(1.25m);

        var singleAndInteger = VBOperators.AddVariant(1f, (short)2);
        var singleAndLong = VBOperators.AddVariant(1f, 2);
        var currencyAndSingle = VBOperators.AddVariant(currency, 0.5f);
        var currencyAndDouble = VBOperators.AddVariant(currency, 0.5d);
        var decimalAndDouble = VBOperators.AddVariant(VBConversions.CDec("1.25"), 0.5d);

        Assert.IsInstanceOfType<float>(singleAndInteger);
        Assert.IsInstanceOfType<double>(singleAndLong);
        Assert.IsInstanceOfType<VBCurrency>(currencyAndSingle);
        Assert.IsInstanceOfType<double>(currencyAndDouble);
        Assert.IsInstanceOfType<decimal>(decimalAndDouble);
        Assert.AreEqual(3f, singleAndInteger);
        Assert.AreEqual(3d, singleAndLong);
        Assert.AreEqual(1.75m, ((VBCurrency)currencyAndSingle!).ToDecimal());
        Assert.AreEqual(1.75d, currencyAndDouble);
        Assert.AreEqual(1.75m, decimalAndDouble);
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
        Assert.AreEqual(1, VBOperators.ModVariant(VBConversions.CDec("5.1"), 2));
        Assert.AreEqual(-2, VBOperators.NotVariant(VBConversions.CDec("1.1")));
        Assert.AreEqual(0, VBOperators.AndVariant(VBConversions.CDec("5.1"), 2));
    }

    [TestMethod]
    public void ModVariant_UsesVb6IntegerRoundingForFloatingOperands()
    {
        Assert.AreEqual(0, VBOperators.ModVariant(12, 4.3d));
        Assert.AreEqual(3, VBOperators.ModVariant(12.6d, 5));
        Assert.AreEqual(3, VBOperators.ModVariant(12.6f, 5));
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
    public void VariantConcatenation_PropagatesNullWhenBothOperandsAreNull()
    {
        var nullValue = VBVariants.NullValue();

        Assert.IsTrue(VBVariants.IsNull(VBOperators.ConcatVariant(nullValue, nullValue)));
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
    public void ObjectVariants_ResolveDefaultValuesForTypeOperatorsAndConversions()
    {
        var value = new NumericDefaultObject();

        Assert.AreEqual((short)3, VBVariants.VarType(value));
        Assert.AreEqual(8, VBOperators.AddVariant(value, 1));
        Assert.AreEqual(14, VBOperators.MultiplyInteger(value, 2));
        Assert.AreEqual("7x", VBOperators.ConcatVariant(value, "x"));
        Assert.AreEqual((short)7, VBConversions.CInt(value));
        Assert.AreEqual("7", VBConversions.CStr(value));
    }

    [TestMethod]
    public void ObjectVariants_PropagateDefaultGetterFailures()
    {
        // Die Ausnahme des Getters kommt unverpackt heraus. Vorher stand hier die
        // TargetInvocationException der Reflexion -- die traegt keine VB6-Nummer und landete in
        // VBErrors.Set im Sammelwert 5, wodurch etwa die 9 von Collection.Item verschwand.
        Assert.ThrowsException<InvalidOperationException>(
            () => VBVariants.VarType(new ThrowingDefaultObject()));
    }

    [TestMethod]
    public void ObjectVariants_WithoutAScalarDefaultMemberRaiseTypeMismatchInVariantExpressions()
    {
        var collection = new VBCollection();

        // Collection.Item is a parameterized default member.  It cannot supply a scalar for
        // an operator expression without an index, so every operator reports error 13.
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AddVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AddStringVariant(collection, "x"));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.SubtractVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.MultiplyInteger(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.DivideVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.IntegerDivideVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.ModVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.PowerVariant(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.ConcatVariant(collection, "x"));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.VariantEqual(collection, 1));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.StringVariantEqual(collection, "x"));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.NegateVariant(collection));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.NotVariant(collection));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBOperators.AndVariant(collection, 1));
    }

    [DefaultMember(nameof(Value))]
    private sealed class NumericDefaultObject
    {
        public int Value => 7;
    }

    [DefaultMember(nameof(Value))]
    private sealed class ThrowingDefaultObject
    {
        public int Value => throw new InvalidOperationException("default getter failed");
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
    public void VariantComparisons_OrderNumericValuesBeforeNonNumericStrings()
    {
        Assert.IsTrue((bool)VBOperators.VariantLess(1, "abc"));
        Assert.IsFalse((bool)VBOperators.VariantEqual(1, "abc"));
        Assert.IsTrue((bool)VBOperators.VariantGreater("abc", 1));
        Assert.IsTrue((bool)VBOperators.VariantLess(new VBDateValue(1), "abc"));
    }

    [TestMethod]
    public void DecimalComparisons_PreservePrecisionAgainstFloatingVariants()
    {
        var value = VBConversions.CDec("0.100000000000000005");

        Assert.IsTrue((bool)VBOperators.VariantGreater(value, 0.1d));
        Assert.IsFalse((bool)VBOperators.VariantEqual(value, 0.1d));
    }

    [TestMethod]
    public void Multiplication_PreservesCurrencyBeforeDoublePromotion()
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
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.SubtractVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.MultiplyInteger(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.DivideVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.IntegerDivideVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.ModVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.PowerVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.NegateVariant(missing));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.ConcatVariant(missing, "value"));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.VariantEqual(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.NotVariant(missing));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBOperators.AndVariant(missing, 1));
        Assert.ThrowsException<VB6MissingArgumentException>(() => VBDebug.Format(missing));
    }

    [TestMethod]
    public void UnsignedVariantArithmetic_PromotesAcrossUnsignedAndSignedBoundaries()
    {
        var mixedUShort = VBOperators.AddVariant(ushort.MaxValue, (short)1);
        var unsignedShortOverflow = VBOperators.AddVariant(ushort.MaxValue, (ushort)1);
        var unsignedIntegerOverflow = VBOperators.AddVariant(uint.MaxValue, 1u);
        var unsignedLongOverflow = VBOperators.AddVariant(ulong.MaxValue, 1UL);
        var mixedUShortSubtraction = VBOperators.SubtractVariant((ushort)1, (short)2);
        var unsignedIntegerSubtraction = VBOperators.SubtractVariant(1u, 2u);
        var unsignedLongSubtraction = VBOperators.SubtractVariant(1UL, 2UL);

        Assert.IsInstanceOfType<int>(mixedUShort);
        Assert.AreEqual(65536, mixedUShort);
        Assert.IsInstanceOfType<uint>(unsignedShortOverflow);
        Assert.AreEqual(65536u, unsignedShortOverflow);
        Assert.IsInstanceOfType<ulong>(unsignedIntegerOverflow);
        Assert.AreEqual(4_294_967_296UL, unsignedIntegerOverflow);
        Assert.IsInstanceOfType<decimal>(unsignedLongOverflow);
        Assert.AreEqual(18_446_744_073_709_551_616m, unsignedLongOverflow);
        Assert.AreEqual(-1, mixedUShortSubtraction);
        Assert.AreEqual(-1L, unsignedIntegerSubtraction);
        Assert.AreEqual(-1m, unsignedLongSubtraction);

        Assert.AreEqual(-1, VBOperators.NegateVariant((ushort)1));
        Assert.AreEqual(-4_294_967_295L, VBOperators.NegateVariant(uint.MaxValue));
        Assert.AreEqual(-18_446_744_073_709_551_615m, VBOperators.NegateVariant(ulong.MaxValue));
    }

    [TestMethod]
    public void ByteVariantIntegerDivisionAndModPreserveByteSubtype()
    {
        var quotient = VBOperators.IntegerDivideVariant((byte)5, (byte)2);
        var remainder = VBOperators.ModVariant((byte)5, (byte)2);

        Assert.IsInstanceOfType<byte>(quotient);
        Assert.IsInstanceOfType<byte>(remainder);
        Assert.AreEqual((byte)2, quotient);
        Assert.AreEqual((byte)1, remainder);
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
