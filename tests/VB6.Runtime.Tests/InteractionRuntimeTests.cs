using System.Collections;
using System.Diagnostics;

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
    public void Command_UsesArgumentsProvidedByHost()
    {
        VBInteraction.SetCommandLineArguments(new[] { "first", "two words" });
        try
        {
            Assert.AreEqual("first \"two words\"", VBInteraction.Command());
        }
        finally
        {
            VBInteraction.ClearCommandLineArguments();
        }
    }

    [TestMethod]
    public void ClipboardGetText_UsesConfiguredHeadlessSink()
    {
        var previousSink = VBInteraction.ClipboardTextSink;
        try
        {
            VBInteraction.ClipboardTextSink = () => "clipboard value";
            Assert.AreEqual("clipboard value", VBInteraction.ClipboardGetText());
        }
        finally
        {
            VBInteraction.ClipboardTextSink = previousSink;
        }
    }

    [TestMethod]
    public void Shell_UsesHeadlessContractOrStartsAWindowsProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.AreEqual(0, VBInteraction.Shell("ignored-command", 1));
            return;
        }

        var processId = VBInteraction.Shell("cmd.exe /c exit 0", 0);
        Assert.IsTrue(processId > 0);
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.WaitForExit(5000))
            {
                process.Kill();
            }
        }
        catch (ArgumentException)
        {
            // The short-lived command may have exited before its process handle was observed.
        }
    }

    [TestMethod]
    public void Environ_ResolvesNamesAndStableNumericEntries()
    {
        var name = "VB6COMPILER_ENV_TEST_" + Guid.NewGuid().ToString("N");
        var previous = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, "compiled");

            Assert.AreEqual("compiled", VBInteraction.Environ(name));
            Assert.AreEqual(string.Empty, VBInteraction.Environ(name + "_MISSING"));

            var entries = Environment.GetEnvironmentVariables()
                .Cast<DictionaryEntry>()
                .Select(entry => new
                {
                    Name = Convert.ToString(entry.Key) ?? string.Empty,
                    Value = Convert.ToString(entry.Value) ?? string.Empty
                })
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                .Select(entry => $"{entry.Name}={entry.Value}")
                .ToArray();
            var index = Array.FindIndex(entries, entry => entry.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(index >= 0);
            Assert.AreEqual(entries[index], VBInteraction.Environ(index + 1.1));
            Assert.AreEqual(string.Empty, VBInteraction.Environ(0));
            Assert.AreEqual(string.Empty, VBInteraction.Environ(entries.Length + 1));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }

    [TestMethod]
    public void Application_ProvidesStableHeadlessMetadata()
    {
        var application = VBInteraction.Application();

        Assert.IsFalse(string.IsNullOrWhiteSpace(application.EXEName));
        Assert.IsFalse(string.IsNullOrWhiteSpace(application.Path));
        Assert.IsFalse(string.IsNullOrWhiteSpace(application.Title));
        Assert.AreEqual(0, application.hInstance);
        Assert.IsTrue(application.Major >= 0);
        Assert.IsTrue(application.Minor >= 0);
        Assert.IsTrue(application.Revision >= 0);
    }

    [TestMethod]
    public void ComActivation_UsesHostSinksBeforePlatformFallback()
    {
        var created = new object();
        var running = new object();
        var previousCreateSink = VBInteraction.CreateObjectSink;
        var previousGetSink = VBInteraction.GetObjectSink;
        try
        {
            VBInteraction.CreateObjectSink = (className, serverName) =>
                className == "Host.Widget" && serverName == "" ? created : null;
            VBInteraction.GetObjectSink = (pathName, className) =>
                pathName == "Host.Widget" && className == "" ? running : null;

            Assert.AreSame(created, VBInteraction.CreateObject("Host.Widget", ""));
            Assert.AreSame(running, VBInteraction.GetObject("Host.Widget", ""));
        }
        finally
        {
            VBInteraction.CreateObjectSink = previousCreateSink;
            VBInteraction.GetObjectSink = previousGetSink;
        }
    }

    [TestMethod]
    public void ComActivation_UsesDeterministicPlaceholderForUnknownHeadlessClass()
    {
        var value = VBInteraction.CreateObject(
            "VB6Compiler.Unknown." + Guid.NewGuid().ToString("N"),
            "");

        Assert.IsInstanceOfType<VBComObject>(value);
    }

    [TestMethod]
    public void ComActivation_UsesDeterministicPlaceholderForUnknownRunningClass()
    {
        var value = VBInteraction.GetObject(
            "",
            "VB6Compiler.Unknown." + Guid.NewGuid().ToString("N"));

        Assert.IsInstanceOfType<VBComObject>(value);
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
