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
    public void Choose_ReturnsSelectedChoiceOrNullOutsideTheChoiceRange()
    {
        var choices = new VBArray<object>(new VBArrayBound(0, 2));
        choices[0] = "one";
        choices[1] = "two";
        choices[2] = "three";

        Assert.AreEqual("one", VBFunctions.Choose(1, choices));
        Assert.AreEqual("three", VBFunctions.Choose(3, choices));
        Assert.IsTrue(VBVariants.IsNull(VBFunctions.Choose(0, choices)));
        Assert.IsTrue(VBVariants.IsNull(VBFunctions.Choose(4, choices)));
    }

    [TestMethod]
    public void Switch_ReturnsVariantNullWhenNoConditionMatches()
    {
        var arguments = new VBArray<object>(new VBArrayBound(0, 3));
        arguments[0] = false;
        arguments[1] = "first";
        arguments[2] = false;
        arguments[3] = "second";

        Assert.IsTrue(VBVariants.IsNull(VBFunctions.Switch(arguments)));
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
    public void LoadPicture_ExposesDeterministicHostMetadataDefaults()
    {
        var picture = VBInteraction.LoadPicture(string.Empty);

        Assert.AreEqual(string.Empty, picture.FileName);
        Assert.AreEqual(0, picture.Width);
        Assert.AreEqual(0, picture.Height);
        Assert.AreEqual(0, picture.Type);
    }

    [TestMethod]
    public void ControlHostContracts_AreDeterministicInHeadlessRuntime()
    {
        Assert.AreEqual(12f, VBInteraction.ScaleX(12f, 0, 0));
        Assert.AreEqual(8f, VBInteraction.ScaleY(8f, 0, 0));
        Assert.AreEqual(5f, VBInteraction.TextWidth("hello"));
        Assert.AreEqual(1f, VBInteraction.TextHeight("hello"));
        Assert.AreEqual(0f, VBInteraction.TextHeight(string.Empty));
    }

    [TestMethod]
    public void ControlEnumeration_UsesHostSinkAndPreservesOrder()
    {
        var previousSink = VBInteraction.ControlEnumerationSink;
        try
        {
            VBInteraction.ControlEnumerationSink = _ => new object?[] { "first", "second" };
            var values = VBInteraction.EnumerateControls(new object());

            Assert.AreEqual(2, values.Length);
            Assert.AreEqual("first", values[0]);
            Assert.AreEqual("second", values[1]);
        }
        finally
        {
            VBInteraction.ControlEnumerationSink = previousSink;
        }
    }

    [TestMethod]
    public void ControlHostDrawingContracts_ForwardToHostSinks()
    {
        object? printed = null;
        VBPaintPicture? painted = null;
        var previousPrintSink = VBInteraction.PrintSink;
        var previousPaintSink = VBInteraction.PaintPictureSink;
        try
        {
            VBInteraction.PrintSink = value => printed = value;
            VBInteraction.PaintPictureSink = value => painted = value;
            VBInteraction.Print("caption");
            VBInteraction.PaintPicture("icon", 1f, 2f, 3f, 4f);
        }
        finally
        {
            VBInteraction.PrintSink = previousPrintSink;
            VBInteraction.PaintPictureSink = previousPaintSink;
        }

        Assert.AreEqual("caption", printed);
        Assert.IsNotNull(painted);
        Assert.AreEqual("icon", painted!.Picture);
        Assert.AreEqual(1f, painted.X);
        Assert.AreEqual(4f, painted.Height);
    }

    [TestMethod]
    public void GraphicsLine_ForwardsTypedOperationToHostSink()
    {
        VBGraphicsLine? captured = null;
        var previousSink = VBInteraction.GraphicsLineSink;
        try
        {
            VBInteraction.GraphicsLineSink = line => captured = line;
            VBInteraction.GraphicsLine(10f, 2f, 13f, 4f, 255, false, true, true);
        }
        finally
        {
            VBInteraction.GraphicsLineSink = previousSink;
        }

        Assert.IsNotNull(captured);
        Assert.AreEqual(10f, captured!.StartX);
        Assert.AreEqual(2f, captured.StartY);
        Assert.AreEqual(13f, captured.EndX);
        Assert.AreEqual(4f, captured.EndY);
        Assert.AreEqual(255, captured.Color);
        Assert.IsFalse(captured.IsStep);
        Assert.IsTrue(captured.DrawBox);
        Assert.IsTrue(captured.Fill);
    }
}
