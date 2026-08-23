namespace VB6.Runtime.Tests;

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
    public void ErrorVariantConversions_DistinguishExplicitAndImplicitPaths()
    {
        var error = new VBErrorValue(2001);

        Assert.AreEqual((short)2001, VBConversions.CInt(error));
        Assert.AreEqual(2001d, VBConversions.CDbl(error));
        Assert.AreEqual(2001m, VBConversions.CDec(error));
        Assert.AreEqual("Error 2001", VBConversions.CStr(error));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBConversions.ConvertCInt(error));
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBConversions.ConvertCStr(error));
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
    public void ErrSource_TracksRaisedAndRuntimeErrors()
    {
        try
        {
            VBErrors.Raise(5, "unit", "message", string.Empty, 0);
        }
        catch (VB6RaisedError)
        {
            Assert.AreEqual("unit", VBErrors.SourceValue());
            Assert.AreEqual("message", VBErrors.DescriptionValue());
        }

        VBErrors.Set(new InvalidOperationException("failure"));
        try
        {
            Assert.AreEqual("InvalidOperationException", VBErrors.SourceValue());
        }
        finally
        {
            VBErrors.Clear();
        }
    }
}
