using System.Reflection;

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

    [TestMethod]
    public void TypePredicates_DistinguishArraysDatesAndObjects()
    {
        Assert.IsTrue(VBVariants.IsArray(new VBArray<int>(new VBArrayBound(0, 1))));
        Assert.IsFalse(VBVariants.IsArray("value"));

        Assert.IsTrue(VBVariants.IsDate(new VBDateValue(43832d)));
        Assert.IsTrue(VBVariants.IsDate("April 28, 2014"));
        Assert.IsFalse(VBVariants.IsDate("not a date"));

        Assert.IsTrue(VBVariants.IsObject(VBVariants.NothingValue()));
        Assert.IsTrue(VBVariants.IsObject(new object()));
        Assert.IsFalse(VBVariants.IsObject(VBVariants.EmptyValue()));
        Assert.IsFalse(VBVariants.IsObject(VBVariants.NullValue()));
        Assert.IsFalse(VBVariants.IsObject(new VBArray<object>(new VBArrayBound(0, 0))));
    }

    [TestMethod]
    public void IsDate_ResolvesAnObjectDefaultValue()
    {
        Assert.IsTrue(VBVariants.IsDate(new DateDefaultObject()));
        Assert.IsFalse(VBVariants.IsDate(new InvalidDateDefaultObject()));
    }

    [TestMethod]
    public void ToBoolean_ResolvesObjectDefaultValuesAndPreservesVariantStates()
    {
        Assert.IsTrue(VBVariants.ToBoolean(new TrueDefaultObject()));
        Assert.IsFalse(VBVariants.ToBoolean(new FalseDefaultObject()));
        Assert.IsFalse(VBVariants.ToBoolean(new NullDefaultObject()));
        Assert.ThrowsException<VB6MissingArgumentException>(
            () => VBVariants.ToBoolean(new MissingDefaultObject()));
    }

    [DefaultMember(nameof(Value))]
    private sealed class DateDefaultObject
    {
        public string Value => "April 28, 2014";
    }

    [DefaultMember(nameof(Value))]
    private sealed class InvalidDateDefaultObject
    {
        public string Value => "not a date";
    }

    [DefaultMember(nameof(Value))]
    private sealed class TrueDefaultObject
    {
        public int Value => 1;
    }

    [DefaultMember(nameof(Value))]
    private sealed class FalseDefaultObject
    {
        public int Value => 0;
    }

    [DefaultMember(nameof(Value))]
    private sealed class NullDefaultObject
    {
        public object Value => VBVariants.NullValue();
    }

    [DefaultMember(nameof(Value))]
    private sealed class MissingDefaultObject
    {
        public object Value => VBVariants.MissingValue();
    }

    [TestMethod]
    public void TypeName_ReportsVb6ArrayElementNames()
    {
        Assert.AreEqual("Long()", VBFunctions.TypeName(new VBArray<int>(new VBArrayBound(0, 1))));
        Assert.AreEqual("String()", VBFunctions.TypeName(new VBArray<string>(new VBArrayBound(0, 1))));
        Assert.AreEqual("Variant()", VBFunctions.TypeName(new VBArray<object>(new VBArrayBound(0, 1))));
    }

    [TestMethod]
    public void ArrayIntrinsic_ReturnsItsVariantArguments()
    {
        var values = (VBArray<object>)VBFunctions.Array(new VBArray<object>(
            new VBArrayBound(0, 2)));

        Assert.AreEqual(0, values.LBound());
        Assert.AreEqual(2, values.UBound());
    }
}
