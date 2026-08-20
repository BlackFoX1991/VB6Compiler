namespace VB6.Runtime.Tests;

[TestClass]
public sealed class ByRefTemporaryTests
{
    [TestMethod]
    public void Temp_ExposesTheValueByReference()
    {
        ref var slot = ref VBByRef.Temp(41L);
        slot += 1;

        Assert.AreEqual(42L, slot);
    }

    [TestMethod]
    public void Temp_GivesEachCallItsOwnStorage()
    {
        ref var first = ref VBByRef.Temp(1);
        ref var second = ref VBByRef.Temp(1);
        second = 99;

        Assert.AreEqual(1, first, "Temporaries must not share a slot, or recursion would corrupt them.");
        Assert.AreEqual(99, second);
    }

    [TestMethod]
    public void Temp_WorksForReferenceTypesAndStructs()
    {
        ref var text = ref VBByRef.Temp("vb");
        text += "6";
        Assert.AreEqual("vb6", text);

        ref var currency = ref VBByRef.Temp(VBConversions.CCur(1.5m));
        Assert.AreEqual(VBConversions.CCur(1.5m), currency);
    }
}
