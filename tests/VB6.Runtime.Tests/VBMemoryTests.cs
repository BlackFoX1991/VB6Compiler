using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime.Tests;

[TestClass]
public sealed class VBMemoryTests
{
    [TestMethod]
    public void ObjPtr_MapsEmptyAndNothingToNull()
    {
        Assert.AreEqual(IntPtr.Zero, VBMemory.ObjPtr(null));
        Assert.AreEqual(IntPtr.Zero, VBMemory.ObjPtr(VBVariants.NothingValue()));
    }

    [TestMethod]
    public void ObjPtr_RejectsScalarVariants()
    {
        Assert.ThrowsException<VB6TypeMismatchException>(() => VBMemory.ObjPtr(42));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void ObjPtr_UsesTheComControllingIUnknown()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The COM ObjPtr test requires Windows.");
            return;
        }

        var comType = Type.GetTypeFromProgID("htmlfile", throwOnError: false);
        if (comType is null)
        {
            Assert.Inconclusive("The htmlfile COM class is not available.");
            return;
        }

        var value = VBInteraction.CreateObject("htmlfile", string.Empty);
        Assert.IsTrue(Marshal.IsComObject(value));

        nint expected = IntPtr.Zero;
        try
        {
            expected = Marshal.GetIUnknownForObject(value);
            Assert.AreNotEqual(IntPtr.Zero, expected);
            Assert.AreEqual(expected, VBMemory.ObjPtr(value));
        }
        finally
        {
            if (expected != IntPtr.Zero)
            {
                Marshal.Release(expected);
            }

            Marshal.FinalReleaseComObject(value);
        }
    }
}
