namespace VB6.Runtime.Tests;

using System.Globalization;

[TestClass]
public sealed class RuntimeTests
{
    [TestMethod]
    public void CInt_UsesBankersRounding()
    {
        Assert.AreEqual((short)2, VBConversions.CInt(1.5d));
        Assert.AreEqual((short)2, VBConversions.CInt(2.5d));
    }

    [TestMethod]
    public void BooleanNumericConversions_UseVbTrueValue()
    {
        Assert.AreEqual((short)-1, VBConversions.CInt(true));
        Assert.AreEqual((short)0, VBConversions.CInt(false));
        Assert.AreEqual(-1d, VBConversions.CDbl(true));
        Assert.AreEqual(0d, VBConversions.CDbl(false));
    }

    [TestMethod]
    public void BooleanLogicalOperators_UseVbTruthTables()
    {
        Assert.IsFalse(VBOperators.NotBoolean(true));
        Assert.IsTrue(VBOperators.NotBoolean(false));

        Assert.IsTrue(VBOperators.AndBoolean(true, true));
        Assert.IsFalse(VBOperators.AndBoolean(true, false));

        Assert.IsTrue(VBOperators.OrBoolean(false, true));
        Assert.IsFalse(VBOperators.OrBoolean(false, false));

        Assert.IsTrue(VBOperators.XorBoolean(true, false));
        Assert.IsFalse(VBOperators.XorBoolean(true, true));

        Assert.IsTrue(VBOperators.EqvBoolean(true, true));
        Assert.IsFalse(VBOperators.EqvBoolean(true, false));

        Assert.IsFalse(VBOperators.ImpBoolean(true, false));
        Assert.IsTrue(VBOperators.ImpBoolean(false, false));
        Assert.IsTrue(VBOperators.ImpBoolean(true, true));
    }

    [TestMethod]
    public void IntegerAddition_ThrowsOnOverflow()
    {
        Assert.ThrowsException<OverflowException>(() =>
            VBOperators.AddInteger(short.MaxValue, 1));
    }

    [TestMethod]
    public void StringComparison_IsBinaryByDefault()
    {
        Assert.IsTrue(VBOperators.Less("A", "a"));
    }

    [TestMethod]
    public void FixedLengthString_TruncatesAndPadsRight()
    {
        Assert.AreEqual("ABC", VBStrings.FixedLength("ABCDE", 3));
        Assert.AreEqual("A  ", VBStrings.FixedLength("A", 3));
        Assert.AreEqual("   ", VBStrings.FixedLength(null, 3));
    }

    [TestMethod]
    public void Variant_EmptyAndPrimitiveValuesConvertThroughRuntime()
    {
        Assert.IsTrue(VBVariant.Empty.IsEmpty);
        Assert.AreEqual(string.Empty, VBVariant.Empty.ToDisplayString());
        Assert.AreEqual((short)10, VBConversions.CInt(VBVariant.From(10)));
        Assert.AreEqual("ok", VBConversions.CStr(VBVariant.From("ok")));
        Assert.AreEqual("ok", VBDebug.Format(VBVariant.From("ok")));
    }

    [TestMethod]
    public void Variant_BuiltinsReportStateAndType()
    {
        Assert.AreEqual((short)0, VBVariantFunctions.VarType(VBVariant.Empty));
        Assert.AreEqual((short)1, VBVariantFunctions.VarType(VBVariant.Null));
        Assert.AreEqual((short)2, VBVariantFunctions.VarType(VBVariant.From((short)10)));
        Assert.AreEqual((short)8, VBVariantFunctions.VarType(VBVariant.From("ok")));
        Assert.AreEqual((short)14, VBVariantFunctions.VarType(VBVariant.From(1.25m)));
        Assert.IsTrue(VBVariantFunctions.IsEmpty(VBVariant.Empty));
        Assert.IsTrue(VBVariantFunctions.IsNull(VBVariant.Null));
        Assert.IsTrue(VBVariantFunctions.IsError(VBVariantFunctions.CVErr(5)));
        Assert.IsTrue(VBVariantFunctions.IsMissing(VBVariant.Missing));
        Assert.AreEqual((short)10, VBVariantFunctions.VarType(VBVariantFunctions.CVErr(5)));
        Assert.IsTrue(VBVariantFunctions.IsNumeric(VBVariant.From(10)));
        Assert.IsFalse(VBVariantFunctions.IsNumeric(VBVariant.From("ok")));
        Assert.ThrowsException<InvalidOperationException>(() => VBConversions.CInt(VBVariantFunctions.CVErr(5)));
    }

    /// <summary>
    /// IsNumeric asks whether a value can be read as a number. Validating text is what legacy
    /// code uses it for, so numeric strings must count.
    /// </summary>
    [TestMethod]
    public void Variant_IsNumericAcceptsNumericStringsBooleansAndEmpty()
    {
        Assert.IsTrue(VBVariantFunctions.IsNumeric("123"));
        Assert.IsTrue(VBVariantFunctions.IsNumeric("  -42  "));
        Assert.IsTrue(VBVariantFunctions.IsNumeric(VBVariant.From("7")));
        Assert.IsTrue(VBVariantFunctions.IsNumeric("&HFF"));
        Assert.IsTrue(VBVariantFunctions.IsNumeric("&O17"));
        Assert.IsTrue(VBVariantFunctions.IsNumeric(true));
        Assert.IsTrue(VBVariantFunctions.IsNumeric(VBVariant.Empty));

        Assert.IsFalse(VBVariantFunctions.IsNumeric("abc"));
        Assert.IsFalse(VBVariantFunctions.IsNumeric(string.Empty));
        Assert.IsFalse(VBVariantFunctions.IsNumeric("   "));
        Assert.IsFalse(VBVariantFunctions.IsNumeric("&HZZ"));
        Assert.IsFalse(VBVariantFunctions.IsNumeric("&O9"));
        Assert.IsFalse(VBVariantFunctions.IsNumeric(VBVariant.Null));
        Assert.IsFalse(VBVariantFunctions.IsNumeric(VBVariant.Nothing));
        Assert.IsFalse(VBVariantFunctions.IsNumeric(VBVariantFunctions.CVErr(5)));
    }

    /// <summary>
    /// VB6 '+' concatenates only when no operand is numeric. One number makes it an addition
    /// and converts the string — 'Total = Total + Text1.Text' depends on it. Empty stays on the
    /// string side and keeps concatenating.
    /// </summary>
    [TestMethod]
    public void Variant_AddIsNumericWhenOneOperandIsANumber()
    {
        Assert.AreEqual(3d, VBVariantOperators.Add(VBVariant.From(1L), "2").Unwrap());
        Assert.AreEqual(3d, VBVariantOperators.Add(VBVariant.From("1"), 2L).Unwrap());
        Assert.AreEqual("12", VBVariantOperators.Add(VBVariant.From("1"), "2").Unwrap());
        Assert.AreEqual("x", VBVariantOperators.Add(VBVariant.Empty, "x").Unwrap());
        Assert.AreEqual((short)0, VBVariantOperators.Add(VBVariant.Empty, VBVariant.Empty).Unwrap());
        Assert.IsTrue(VBVariantOperators.Add(VBVariant.Null, "1").IsNull);

        // Concatenation is unaffected: '&' never adds.
        Assert.AreEqual("12", VBVariantOperators.Concat(VBVariant.From(1L), 2L).Unwrap());
    }

    [TestMethod]
    public void Variant_OperatorsUseRuntimePromotionAndNullPropagation()
    {
        Assert.AreEqual(5L, VBVariantOperators.Add(VBVariant.From(2L), 3L).Unwrap());
        Assert.AreEqual(3.5m, VBVariantOperators.Add(VBVariant.From(1.25m), VBConversions.CCur(2.25m)).Unwrap());
        Assert.AreEqual(0.5m, VBVariantOperators.Divide(VBVariant.From(1m), 2m).Unwrap());
        Assert.AreEqual(2.5d, VBVariantOperators.Divide(VBVariant.From(5L), 2L).Unwrap());
        Assert.AreEqual("2x", VBVariantOperators.Concat(VBVariant.From(2L), "x").Unwrap());
        Assert.AreEqual(true, VBVariantOperators.Equal(VBVariant.From(2L), 2L).Unwrap());
        Assert.IsTrue(VBVariantOperators.Add(VBVariant.Null, 1L).IsNull);
        Assert.IsTrue(VBVariantOperators.Equal(VBVariant.Null, 1L).IsNull);
        Assert.ThrowsException<InvalidOperationException>(() => VBConversions.CBool(VBVariant.Null));
    }

    [TestMethod]
    public void Variant_LogicalOperatorsPreserveBooleanTriStateAndNumericBitwiseBehavior()
    {
        Assert.AreEqual(false, VBVariantOperators.And(VBVariant.Null, false).Unwrap());
        Assert.IsTrue(VBVariantOperators.And(VBVariant.Null, true).IsNull);
        Assert.AreEqual(true, VBVariantOperators.Or(VBVariant.Null, true).Unwrap());
        Assert.IsTrue(VBVariantOperators.Or(VBVariant.Null, false).IsNull);
        Assert.IsTrue(VBVariantOperators.Xor(VBVariant.Null, true).IsNull);
        Assert.AreEqual(true, VBVariantOperators.Imp(VBVariant.Null, true).Unwrap());
        Assert.IsTrue(VBVariantOperators.Imp(true, VBVariant.Null).IsNull);
        Assert.AreEqual(false, VBVariantOperators.And(VBVariant.Empty, true).Unwrap());

        Assert.AreEqual(2L, VBVariantOperators.And(VBVariant.From(6L), 3L).Unwrap());
        Assert.AreEqual(7L, VBVariantOperators.Or(VBVariant.From(6L), 3L).Unwrap());
        Assert.AreEqual(5L, VBVariantOperators.Xor(VBVariant.From(6L), 3L).Unwrap());
    }

    [TestMethod]
    public void DebugPrintFormatsNumbersInvariantly()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");

            Assert.AreEqual("0.125", VBDebug.Format(0.125d));
            Assert.AreEqual("1.25", VBDebug.Format(1.25f));
            Assert.AreEqual("3.5", VBDebug.Format(VBVariant.From(3.5m)));
            Assert.AreEqual("12.3456", VBDebug.Format(VBConversions.CCur(12.3456m)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }
}
