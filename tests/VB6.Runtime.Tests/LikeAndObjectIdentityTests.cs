namespace VB6.Runtime.Tests;

[TestClass]
public sealed class LikeAndObjectIdentityTests
{
    [TestMethod]
    public void Like_SupportsWildcardsDigitsListsAndRanges()
    {
        Assert.IsTrue(VBStrings.Like("abc", "a*", false));
        Assert.IsTrue(VBStrings.Like("a5", "a#", false));
        Assert.IsTrue(VBStrings.Like("ac", "a[!d]", false));
        Assert.IsTrue(VBStrings.Like("m", "[a-z]", false));
        Assert.IsFalse(VBStrings.Like("A", "a", false));
        Assert.IsTrue(VBStrings.Like("A", "a", true));
    }

    [TestMethod]
    public void ObjectIdentity_UsesReferenceEquality()
    {
        var value = new object();
        Assert.IsTrue(VBObjectIdentity.IsSame(value, value));
        Assert.IsFalse(VBObjectIdentity.IsSame(value, new object()));
        Assert.IsTrue(VBObjectIdentity.IsSame(null, null));
    }
}
