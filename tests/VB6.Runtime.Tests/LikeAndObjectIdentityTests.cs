using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Reflection;

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
    public void Like_ResolvesDefaultValuesBeforeNullAndStringChecks()
    {
        Assert.IsTrue(VBStrings.Like(new TextDefaultObject(), "a*", textCompare: false));
        Assert.IsFalse(VBStrings.Like(new NullDefaultObject(), "a*", textCompare: false));
    }

    [DefaultMember(nameof(Value))]
    private sealed class TextDefaultObject
    {
        public string Value => "abc";
    }

    [DefaultMember(nameof(Value))]
    private sealed class NullDefaultObject
    {
        public object Value => VBVariants.NullValue();
    }

    [TestMethod]
    public void ObjectIdentity_UsesReferenceEquality()
    {
        var value = new object();
        Assert.IsTrue(VBObjectIdentity.IsSame(value, value));
        Assert.IsFalse(VBObjectIdentity.IsSame(value, new object()));
        Assert.IsTrue(VBObjectIdentity.IsSame(null, null));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ObjectIdentity_UsesComIUnknownIdentityAcrossRcws()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM identity test requires Windows.");
            return;
        }

        var comType = Type.GetTypeFromProgID("htmlfile", throwOnError: false);
        if (comType is null)
        {
            Assert.Inconclusive("The htmlfile COM class is not available.");
            return;
        }

        var first = VBInteraction.CreateObject("htmlfile", string.Empty);
        var second = VBInteraction.CreateObject("htmlfile", string.Empty);
        Assert.IsTrue(Marshal.IsComObject(first));
        Assert.IsTrue(Marshal.IsComObject(second));
        Assert.IsFalse(VBObjectIdentity.IsSame(first, second));

        nint unknown = IntPtr.Zero;
        nint dispatch = IntPtr.Zero;
        try
        {
            unknown = Marshal.GetIUnknownForObject(first);
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            Marshal.QueryInterface(unknown, in dispatchId, out dispatch);
            var secondWrapper = Marshal.GetObjectForIUnknown(dispatch);

            Assert.IsTrue(Marshal.IsComObject(secondWrapper));
            Assert.IsTrue(VBObjectIdentity.IsSame(first, secondWrapper));
        }
        finally
        {
            if (unknown != IntPtr.Zero)
            {
                Marshal.Release(unknown);
            }

            if (dispatch != IntPtr.Zero)
            {
                Marshal.Release(dispatch);
            }
        }
    }
}
