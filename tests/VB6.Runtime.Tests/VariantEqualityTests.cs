using VB6.Runtime;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VariantEqualityTests
{
    [TestMethod]
    public void Equal_ScalarVariantOverloadsApplyEmptyComparisonContext()
    {
        Assert.IsTrue(VBOperators.Equal((object?)null, (short)0));
        Assert.IsTrue(VBOperators.Equal((short)0, (object?)null));
        Assert.IsFalse(VBOperators.Equal((object?)null, (short)1));

        Assert.IsTrue(VBOperators.Equal((object?)null, string.Empty));
        Assert.IsTrue(VBOperators.Equal(string.Empty, (object?)null));
        Assert.IsFalse(VBOperators.Equal((object?)null, "x"));

        Assert.IsTrue(VBOperators.Equal((object?)null, false));
        Assert.IsFalse(VBOperators.Equal((object?)null, true));
    }

    [TestMethod]
    public void Equal_ScalarVariantOverloadsCompareNumericSubtypesAndBooleanValues()
    {
        Assert.IsTrue(VBOperators.Equal((object?)3, (short)3));
        Assert.IsTrue(VBOperators.Equal((short)3, (object?)3L));
        Assert.IsTrue(VBOperators.Equal((object?)3f, 3d));
        Assert.IsTrue(VBOperators.Equal((object?)VBConversions.CCur(3m), (short)3));
        Assert.IsTrue(VBOperators.Equal((object?)true, (short)-1));
        Assert.IsTrue(VBOperators.Equal((object?)false, (short)0));
        Assert.IsFalse(VBOperators.Equal((object?)3, (short)4));
    }

    [TestMethod]
    public void Equal_ScalarVariantOverloadsKeepStringAndNumericComparisonDomainsDistinct()
    {
        Assert.IsTrue(VBOperators.Equal((object?)"abc", "abc"));
        Assert.IsFalse(VBOperators.Equal((object?)"abc", "ABC"));
        Assert.IsFalse(VBOperators.Equal((object?)"3", (short)3));
        Assert.IsFalse(VBOperators.Equal((object?)3, "3"));
    }

    [TestMethod]
    public void Equal_ScalarVariantOverloadsRejectUnsupportedPayloads()
    {
        Assert.ThrowsException<InvalidCastException>(() =>
            VBOperators.Equal((object?)new object(), (short)0));
    }
}
