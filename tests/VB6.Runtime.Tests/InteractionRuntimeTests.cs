namespace VB6.Runtime.Tests;

[TestClass]
public sealed class InteractionRuntimeTests
{
    [TestMethod]
    public void RGB_ClampsComponentsAndUsesWindowsColorLayout()
    {
        Assert.AreEqual(0x030201, VBFunctions.RGB(1, 2, 3));
        Assert.AreEqual(0x00FF00, VBFunctions.RGB(-1, 300, 0));
    }

    [TestMethod]
    public void Settings_AreCaseInsensitiveAndReturnDefaults()
    {
        VBInteraction.SaveSetting("RuntimeTests", "Settings", "Answer", "42");

        Assert.AreEqual(
            "42",
            VBInteraction.GetSetting("runtimetests", "settings", "answer", "missing"));
        Assert.AreEqual(
            "missing",
            VBInteraction.GetSetting("RuntimeTests", "Settings", "other", "missing"));
    }

    [TestMethod]
    public void PropertyBag_StoresValuesAndUsesFallbacks()
    {
        var bag = new VBPropertyBag();

        Assert.AreEqual("fallback", bag.ReadProperty("Name", "fallback"));
        bag.WriteProperty("Name", "value");
        Assert.AreEqual("value", bag.ReadProperty("name", "fallback"));
    }

    [TestMethod]
    public void Command_ReturnsEmptyValueInHeadlessRuntime()
    {
        Assert.AreEqual(string.Empty, VBInteraction.Command());
    }

    [TestMethod]
    public void ControlHostContracts_AreDeterministicInHeadlessRuntime()
    {
        Assert.AreEqual(12f, VBInteraction.ScaleX(12f, 0, 0));
        Assert.AreEqual(8f, VBInteraction.ScaleY(8f, 0, 0));
        Assert.AreEqual(5f, VBInteraction.TextWidth("hello"));
    }
}
