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
    public void QBColor_UsesTheQbasicPaletteAndRejectsNumbersOutsideIt()
    {
        Assert.AreEqual(0x000000, VBFunctions.QBColor(0));
        Assert.AreEqual(0xFF0000, VBFunctions.QBColor(9));
        Assert.AreEqual(0x0000FF, VBFunctions.QBColor(12));
        Assert.AreEqual(0xFFFFFF, VBFunctions.QBColor(15));
        var exception = Assert.ThrowsException<VB6RuntimeErrorException>(() => VBFunctions.QBColor(16));
        Assert.AreEqual(5, exception.Number);
    }

    [TestMethod]
    public void CallByName_UsesTheExistingDynamicMemberDispatchForMethodsAndProperties()
    {
        var target = new CallByNameTarget { Value = 4 };
        var noArguments = new VBArray<object>(new VBArrayBound(0, -1));
        var methodArguments = new VBArray<object>(new VBArrayBound(0, 0));
        methodArguments[0] = 3;
        var setterArguments = new VBArray<object>(new VBArrayBound(0, 0));
        setterArguments[0] = 9;

        Assert.AreEqual(7, VBFunctions.CallByName(target, "Add", 1, methodArguments));
        Assert.AreEqual(4, VBFunctions.CallByName(target, "Value", 2, noArguments));
        Assert.IsNull(VBFunctions.CallByName(target, "Value", 4, setterArguments));
        Assert.AreEqual(9, target.Value);
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
    public void Settings_GetAllAndDeletePreserveTheVB6RegistryHierarchy()
    {
        var appName = "RegistryRuntime_" + Guid.NewGuid().ToString("N");
        VBInteraction.SaveSetting(appName, "General", "Zebra", "last");
        VBInteraction.SaveSetting(appName, "General", "Alpha", "first");
        VBInteraction.SaveSetting(appName, "Other", "Retained", "yes");

        var settingsValue = VBInteraction.GetAllSettings(appName.ToLowerInvariant(), "general");
        Assert.IsInstanceOfType<VBArray<object>>(settingsValue);
        var settings = (VBArray<object>)settingsValue!;
        Assert.AreEqual(2, settings.Rank);
        Assert.AreEqual(0, settings.LBound(1));
        Assert.AreEqual(1, settings.UBound(1));
        Assert.AreEqual(0, settings.LBound(2));
        Assert.AreEqual(1, settings.UBound(2));
        Assert.AreEqual("Alpha", settings[0, 0]);
        Assert.AreEqual("first", settings[0, 1]);
        Assert.AreEqual("Zebra", settings[1, 0]);
        Assert.AreEqual("last", settings[1, 1]);

        VBInteraction.DeleteSetting(appName, "General", "alpha");
        Assert.AreEqual("missing", VBInteraction.GetSetting(appName, "General", "Alpha", "missing"));
        Assert.IsNotNull(VBInteraction.GetAllSettings(appName, "General"));

        VBInteraction.DeleteSetting(appName, "General");
        Assert.IsNull(VBInteraction.GetAllSettings(appName, "General"));
        Assert.AreEqual("yes", VBInteraction.GetSetting(appName, "Other", "Retained", "missing"));

        VBInteraction.DeleteSetting(appName);
        Assert.IsNull(VBInteraction.GetAllSettings(appName, "Other"));
        var exception = Assert.ThrowsException<VB6RuntimeErrorException>(
            () => VBInteraction.DeleteSetting(appName, "Other"));
        Assert.AreEqual(5, exception.Number);
    }

    [TestMethod]
    public void InteractionServices_UseInjectedHostBeforeHeadlessFallback()
    {
        var previousHost = VBInteraction.Host;
        var host = new InteractionServiceHost();
        try
        {
            VBInteraction.Host = host;

            Assert.AreEqual((short)7, VBInteraction.MsgBox("Proceed?", 4, "Confirm"));
            Assert.AreEqual(
                "host response",
                VBInteraction.InputBox("Prompt", "Title", "default", 1f, 2f, "help.chm", 3));

            VBInteraction.SaveSetting("App", "Section", "Key", "persisted");
            Assert.AreEqual("persisted", VBInteraction.GetSetting("App", "Section", "Key", "fallback"));
            Assert.AreEqual("fallback", VBInteraction.GetSetting("App", "Section", "other", "fallback"));

            var settingsValue = VBInteraction.GetAllSettings("App", "Section");
            Assert.IsInstanceOfType<VBArray<object>>(settingsValue);
            var settings = (VBArray<object>)settingsValue!;
            Assert.AreEqual("Key", settings[0, 0]);
            Assert.AreEqual("persisted", settings[0, 1]);
            VBInteraction.DeleteSetting("App", "Section", "Key");
            Assert.IsNull(VBInteraction.GetAllSettings("App", "Section"));

            VBInteraction.ClipboardSetText("host clipboard", 13);
            Assert.IsTrue(VBInteraction.ClipboardGetFormat(13));
            Assert.AreEqual("host clipboard", VBInteraction.ClipboardGetText(13));
            VBInteraction.ClipboardSetData("host data", 9001);
            Assert.AreEqual("host data", VBInteraction.ClipboardGetData(9001));
            VBInteraction.ClipboardClear();
            Assert.IsFalse(VBInteraction.ClipboardGetFormat(13));
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
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
    public void ClipboardServices_ProvideDeterministicMultiFormatHeadlessFallbacks()
    {
        var previousHost = VBInteraction.Host;
        var previousSink = VBInteraction.ClipboardTextSink;
        try
        {
            VBInteraction.Host = null;
            VBInteraction.ClipboardTextSink = null;
            VBInteraction.ClipboardClear();

            VBInteraction.ClipboardSetText("plain", 1);
            VBInteraction.ClipboardSetText("{\\rtf1 rich}", -16639);
            var picture = new VBPicture("picture.bmp");
            VBInteraction.ClipboardSetData(picture, 2);

            Assert.IsTrue(VBInteraction.ClipboardGetFormat(1));
            Assert.IsTrue(VBInteraction.ClipboardGetFormat(-16639));
            Assert.IsTrue(VBInteraction.ClipboardGetFormat(2));
            Assert.AreEqual("plain", VBInteraction.ClipboardGetText());
            Assert.AreEqual("{\\rtf1 rich}", VBInteraction.ClipboardGetText(-16639));
            Assert.AreSame(picture, VBInteraction.ClipboardGetData(2));

            VBInteraction.ClipboardClear();
            Assert.IsFalse(VBInteraction.ClipboardGetFormat(1));
            Assert.AreEqual(string.Empty, VBInteraction.ClipboardGetText());
            Assert.IsNull(VBInteraction.ClipboardGetData(2));
        }
        finally
        {
            VBInteraction.ClipboardClear();
            VBInteraction.Host = previousHost;
            VBInteraction.ClipboardTextSink = previousSink;
        }
    }

    [TestMethod]
    public void ScreenServices_ProvideDeterministicHeadlessStateAndAnInjectedHost()
    {
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = null;
            var previousPointer = VBInteraction.ScreenMousePointer();
            try
            {
                Assert.IsNull(VBInteraction.ScreenActiveForm());
                Assert.IsNull(VBInteraction.ScreenActiveControl());
                Assert.AreEqual(15f, VBInteraction.ScreenTwipsPerPixelX());
                Assert.AreEqual(15f, VBInteraction.ScreenTwipsPerPixelY());
                Assert.AreSame(VBInteraction.Screen(), VBInteraction.Screen());

                VBInteraction.ScreenSetMousePointer(11);
                Assert.AreEqual(11, VBInteraction.ScreenMousePointer());
                Assert.AreEqual(11, VBInteraction.Screen().MousePointer);
            }
            finally
            {
                VBInteraction.ScreenSetMousePointer(previousPointer);
            }

            var activeForm = new object();
            var activeControl = new object();
            var host = new InteractionServiceHost();
            host.SetScreenState(new VBScreenState(activeForm, activeControl, 12f, 13.5f, 2));
            VBInteraction.Host = host;

            Assert.AreSame(activeForm, VBInteraction.ScreenActiveForm());
            Assert.AreSame(activeControl, VBInteraction.ScreenActiveControl());
            Assert.AreEqual(12f, VBInteraction.ScreenTwipsPerPixelX());
            Assert.AreEqual(13.5f, VBInteraction.ScreenTwipsPerPixelY());
            Assert.AreEqual(2, VBInteraction.ScreenMousePointer());

            VBInteraction.ScreenSetMousePointer(11);
            Assert.AreEqual(11, VBInteraction.ScreenMousePointer());
            Assert.AreEqual(11, VBInteraction.Screen().MousePointer);
        }
        finally
        {
            VBInteraction.Host = previousHost;
        }
    }

    [TestMethod]
    public void PrinterServices_ProvideDeterministicDocumentStateAndAnInjectedHost()
    {
        var previousHost = VBInteraction.Host;
        try
        {
            VBInteraction.Host = null;
            var printer = VBInteraction.Printer();
            printer.EndDoc();
            printer.DocumentName = "Headless report";
            printer.Copies = 2;
            printer.CurrentX = 3f;
            printer.CurrentY = 4f;

            Assert.AreSame(printer, VBInteraction.Printer());
            Assert.AreEqual(15f, printer.TwipsPerPixelX);
            Assert.AreEqual(15f, printer.TwipsPerPixelY);
            Assert.AreEqual(2, printer.Copies);
            Assert.AreEqual(3f, printer.CurrentX);
            Assert.AreEqual(4f, printer.CurrentY);
            Assert.AreEqual(1440f, printer.ScaleX(1f, 5, 1));

            printer.Print("first line");
            Assert.AreEqual(1, printer.Page);
            Assert.AreEqual(5f, printer.CurrentY);
            printer.NewPage();
            Assert.AreEqual(2, printer.Page);
            Assert.AreEqual(0f, printer.CurrentX);
            Assert.AreEqual(0f, printer.CurrentY);
            printer.EndDoc();
            Assert.AreEqual(0, printer.Page);

            var host = new InteractionServiceHost();
            host.SetPrinterState(VBPrinterState.Headless with
            {
                DeviceName = "Test printer",
                DriverName = "Test driver",
                Port = "TEST:",
                DocumentName = "Host report",
                Hdc = 123,
                TwipsPerPixelX = 12f,
                TwipsPerPixelY = 13f
            });
            VBInteraction.Host = host;
            printer = VBInteraction.Printer();

            Assert.AreEqual("Test printer", printer.DeviceName);
            Assert.AreEqual(123, printer.hDC);
            Assert.AreEqual(12f, printer.TwipsPerPixelX);
            Assert.AreEqual(13f, printer.TwipsPerPixelY);
            printer.Copies = 3;
            printer.Print("host line");
            Assert.AreEqual(3, printer.Copies);
            CollectionAssert.AreEqual(new[] { "host line" }, host.PrinterText);
            Assert.AreEqual(1, printer.Page);
            printer.NewPage();
            Assert.AreEqual(1, host.AdvancedPageCount);
            Assert.AreEqual(2, printer.Page);
            Assert.AreEqual(12f, printer.TextWidth("measure"));
            Assert.AreEqual(3f, printer.TextHeight("measure"));
            printer.EndDoc();
            Assert.IsFalse(host.LastPrinterCompletionWasAbort);
            Assert.AreEqual(0, printer.Page);
        }
        finally
        {
            VBInteraction.Host = null;
            VBInteraction.PrinterEndDoc();
            VBInteraction.Host = previousHost;
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
        Assert.AreEqual(1f, VBInteraction.ScaleX(1440f, 1, 5));
        Assert.AreEqual(1f, VBInteraction.ScaleY(1440f, 1, 5));
        Assert.AreEqual(1f, VBInteraction.ScaleX(1440f, 0, 5));
        Assert.AreEqual(1f, VBInteraction.ScaleY(1440f, 0, 5));
        Assert.AreEqual(96f, VBInteraction.ScaleX(1440f, 1, 3));
        Assert.AreEqual(96f, VBInteraction.ScaleY(1440f, 1, 3));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => VBInteraction.ScaleX(1f, 8, 1));
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
    public void Cls_ForwardsToHeadlessHostSink()
    {
        var calls = 0;
        var previousSink = VBInteraction.ClsSink;
        try
        {
            VBInteraction.ClsSink = () => calls++;
            VBInteraction.Cls();
        }
        finally
        {
            VBInteraction.ClsSink = previousSink;
        }

        Assert.AreEqual(1, calls);
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

    public sealed class CallByNameTarget
    {
        public int Value { get; set; }

        public int Add(int value) => Value + value;
    }

    private sealed class InteractionServiceHost : IVB6Host
    {
        private readonly List<HostSetting> _settings = [];
        private readonly Dictionary<int, object?> _clipboard = [];
        private VBScreenState _screen = VBScreenState.Headless;
        private VBPrinterState _printer = VBPrinterState.Headless;

        public void SetScreenState(VBScreenState screen) => _screen = screen;

        public List<string> PrinterText { get; } = [];

        public int AdvancedPageCount { get; private set; }

        public bool LastPrinterCompletionWasAbort { get; private set; }

        public void SetPrinterState(VBPrinterState printer) => _printer = printer;

        public void DoEvents()
        {
        }

        public bool TryShowMessageBox(string prompt, int buttons, string title, out short result)
        {
            Assert.AreEqual("Proceed?", prompt);
            Assert.AreEqual(4, buttons);
            Assert.AreEqual("Confirm", title);
            result = 7;
            return true;
        }

        public bool TryShowInputBox(
            string prompt,
            string title,
            string defaultResponse,
            float xpos,
            float ypos,
            string helpFile,
            int context,
            out string? response)
        {
            Assert.AreEqual("Prompt", prompt);
            Assert.AreEqual("Title", title);
            Assert.AreEqual("default", defaultResponse);
            Assert.AreEqual(1f, xpos);
            Assert.AreEqual(2f, ypos);
            Assert.AreEqual("help.chm", helpFile);
            Assert.AreEqual(3, context);
            response = "host response";
            return true;
        }

        public bool TryGetSetting(string appName, string section, string key, out string? value)
        {
            var setting = _settings.FirstOrDefault(candidate =>
                NameEquals(candidate.AppName, appName) &&
                NameEquals(candidate.Section, section) &&
                NameEquals(candidate.Key, key));
            value = setting?.Value;
            return setting is not null;
        }

        public bool TrySaveSetting(string appName, string section, string key, string value)
        {
            var existing = _settings.FindIndex(candidate =>
                NameEquals(candidate.AppName, appName) &&
                NameEquals(candidate.Section, section) &&
                NameEquals(candidate.Key, key));
            if (existing >= 0)
            {
                _settings[existing] = new HostSetting(appName, section, key, value);
            }
            else
            {
                _settings.Add(new HostSetting(appName, section, key, value));
            }

            return true;
        }

        public bool TryDeleteSetting(string appName, bool hasSection, string? section, bool hasKey, string? key)
        {
            var removed = _settings.RemoveAll(candidate =>
                NameEquals(candidate.AppName, appName) &&
                (!hasSection || NameEquals(candidate.Section, section!)) &&
                (!hasKey || NameEquals(candidate.Key, key!)));
            return removed > 0;
        }

        public bool TryGetAllSettings(string appName, string section, out VBArray<object>? settings)
        {
            var matches = _settings
                .Where(candidate =>
                    NameEquals(candidate.AppName, appName) &&
                    NameEquals(candidate.Section, section))
                .OrderBy(candidate => candidate.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (matches.Length == 0)
            {
                settings = null;
                return true;
            }

            settings = new VBArray<object>(
                new VBArrayBound(0, matches.Length - 1),
                new VBArrayBound(0, 1));
            for (var index = 0; index < matches.Length; index++)
            {
                settings[index, 0] = matches[index].Key;
                settings[index, 1] = matches[index].Value;
            }

            return true;
        }

        public bool TryGetClipboardText(int format, out string? text)
        {
            text = _clipboard.TryGetValue(format, out var data) ? data as string : null;
            return text is not null;
        }

        public bool TrySetClipboardText(string text, int format)
        {
            _clipboard[format] = text;
            return true;
        }

        public bool TryGetClipboardData(int format, out object? data) =>
            _clipboard.TryGetValue(format, out data);

        public bool TrySetClipboardData(object? data, int format)
        {
            _clipboard[format] = data;
            return true;
        }

        public bool TryGetClipboardFormat(int format, out bool available)
        {
            available = _clipboard.ContainsKey(format);
            return true;
        }

        public bool TryClearClipboard()
        {
            _clipboard.Clear();
            return true;
        }

        public bool TryGetScreenState(out VBScreenState? screen)
        {
            screen = _screen;
            return true;
        }

        public bool TrySetScreenMousePointer(int mousePointer)
        {
            _screen = _screen with { MousePointer = mousePointer };
            return true;
        }

        public bool TryGetPrinterState(out VBPrinterState? printer)
        {
            printer = _printer;
            return true;
        }

        public bool TrySetPrinterState(VBPrinterState printer)
        {
            _printer = printer;
            return true;
        }

        public bool TryWritePrinterText(string text)
        {
            PrinterText.Add(text);
            return true;
        }

        public bool TryAdvancePrinterPage()
        {
            AdvancedPageCount++;
            return true;
        }

        public bool TryCompletePrinterDocument(bool abort)
        {
            LastPrinterCompletionWasAbort = abort;
            return true;
        }

        public bool TryMeasurePrinterText(string text, out float width, out float height)
        {
            Assert.AreEqual("measure", text);
            width = 12f;
            height = 3f;
            return true;
        }

        public void Load(object target)
        {
        }

        public void Unload(object target)
        {
        }

        public object? CreateControl(object owner, string name, string typeName) => null;

        public bool TryGetMember(object target, string memberName, object?[] arguments, out object? value)
        {
            value = null;
            return false;
        }

        public bool TrySetMember(object target, string memberName, object?[] arguments, object? value) => false;

        public bool TryInvokeMember(object target, string memberName, object?[] arguments, out object? result)
        {
            result = null;
            return false;
        }

        public IEnumerable<object?>? EnumerateControls(object? target) => [];

        private static bool NameEquals(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

        private sealed record HostSetting(string AppName, string Section, string Key, string Value);
    }
}
