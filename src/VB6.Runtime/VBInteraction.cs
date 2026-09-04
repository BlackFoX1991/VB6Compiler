using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Headless, deterministic implementations of VB6 interaction intrinsics.</summary>
public static class VBInteraction
{
    private const int ClipboardTextFormat = 1;
    private static readonly Dictionary<SettingKey, string> Settings = new(SettingKeyComparer.Instance);
    private static readonly object SettingsGate = new();
    private static readonly Dictionary<int, object?> ClipboardData = new();
    private static readonly object ClipboardGate = new();
    private static readonly VBApplication ApplicationValue = VBApplication.Create();
    private static readonly VBScreen ScreenValue = new();
    private static readonly object ScreenGate = new();
    private static VBScreenState _headlessScreen = VBScreenState.Headless;
    private static readonly VBPrinter PrinterValue = new();
    private static readonly object PrinterGate = new();
    private static VBPrinterState _headlessPrinter = VBPrinterState.Headless;
    private static string _commandLine = string.Empty;
    private static bool _commandLineSetByHost;

    /// <summary>
    /// Optional UI host. Generated code can be executed headless when this is null; UI-sensitive
    /// operations then remain deterministic no-ops or use the portable control proxy.
    /// </summary>
    public static IVB6Host? Host { get; set; }

    /// <summary>
    /// Optional host activation hook for <c>CreateObject</c>. Returning <see langword="null"/>
    /// lets the runtime continue with its Windows COM activation or deterministic placeholder.
    /// </summary>
    public static Func<string, string, object?>? CreateObjectSink { get; set; }

    /// <summary>
    /// Optional host activation hook for <c>GetObject</c>. Returning <see langword="null"/>
    /// lets the runtime continue with its Windows moniker activation or deterministic placeholder.
    /// </summary>
    public static Func<string, string, object?>? GetObjectSink { get; set; }

    /// <summary>Yields to the configured UI host's message pump.</summary>
    public static void DoEvents()
    {
        Host?.DoEvents();
    }

    /// <summary>
    /// Activates the optional WinForms host assembly for a generated Form startup. Reflection is
    /// intentional here: the portable runtime must remain usable without WindowsDesktop.
    /// </summary>
    public static void StartWinFormsHost() => InvokeOptionalWinFormsHost(nameof(StartWinFormsHost));

    /// <summary>Runs the optional WinForms message loop and returns its process result.</summary>
    public static int RunWinFormsMessageLoop() =>
        Convert.ToInt32(InvokeOptionalWinFormsHost(nameof(RunWinFormsMessageLoop)) ?? 0, CultureInfo.InvariantCulture);

    private static object? InvokeOptionalWinFormsHost(string methodName)
    {
        var hostType = Type.GetType(
            "VB6.Runtime.WinForms.WinFormsApplicationHost, VB6.Runtime.WinForms",
            throwOnError: false);
        var method = hostType?.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        if (method is null)
        {
            return null;
        }

        try
        {
            return method.Invoke(null, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    /// <summary>Loads a form/control through the configured UI host.</summary>
    public static void Load(object? value)
    {
        if (value is not null)
        {
            Host?.Load(value);
        }
    }

    /// <summary>Unloads a form/control through the configured UI host.</summary>
    public static void Unload(object? value)
    {
        if (value is not null)
        {
            Host?.Unload(value);
        }
    }

    /// <summary>
    /// <c>Load ctlButton(index)</c> on a control array. VB6 grows the array to reach the index,
    /// clones the designer-created element and puts the copy in the slot. The grown array is
    /// returned so the caller writes it back into the same place, the way ReDim Preserve does —
    /// every other holder of the array sees the new element through that field.
    /// </summary>
    public static VBArray<object?>? LoadControlArrayElement(
        VBArray<object?>? array,
        int index,
        string name,
        object owner)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(owner);
        if (array is null)
        {
            return null;
        }

        if (index < array.LBound())
        {
            VBErrors.Raise(9, name, "Subscript out of range", string.Empty, 0);
        }

        if (index > array.UBound())
        {
            array = array.ReDimPreserve(new VBArrayBound(array.LBound(), index));
        }
        else if (array[index] is not null)
        {
            // VB6 refuses to load an element that already exists.
            VBErrors.Raise(360, name, "Object already loaded", string.Empty, 0);
        }

        array[index] = Host?.LoadControlArrayElement(owner, name, index, FindTemplate(array));
        return array;
    }

    /// <summary>
    /// <c>Unload ctlButton(index)</c> on a control array. The slot is cleared but the array keeps
    /// its bounds, as in VB6: the index stays addressable and can be loaded again.
    /// </summary>
    public static VBArray<object?>? UnloadControlArrayElement(
        VBArray<object?>? array,
        int index,
        string name,
        object owner)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(owner);
        if (array is null)
        {
            return null;
        }

        if (index < array.LBound() || index > array.UBound())
        {
            VBErrors.Raise(9, name, "Subscript out of range", string.Empty, 0);
        }

        Host?.UnloadControlArrayElement(owner, name, index, array[index]);
        array[index] = null;
        return array;
    }

    /// <summary>
    /// The element a loaded copy is cloned from. VB6 uses the lowest existing index, which is the
    /// one the designer created.
    /// </summary>
    private static object? FindTemplate(VBArray<object?> array)
    {
        for (var index = array.LBound(); index <= array.UBound(); index++)
        {
            if (array[index] is { } element)
            {
                return element;
            }
        }

        return null;
    }

    /// <summary>Shows a form/control through the configured UI host.</summary>
    public static void Show(object? value)
    {
        if (value is not null)
        {
            Host?.TryInvokeMember(value, "Show", Array.Empty<object?>(), out _);
        }
    }

    /// <summary>Creates a designer control through the host or a portable headless proxy.</summary>
    public static object CreateControl(object owner, string name, string typeName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        return Host?.CreateControl(owner, name, typeName)
            ?? new VBControlProxy(name, typeName, owner);
    }

    /// <summary>Applies a scalar value from a Form/UserControl designer envelope through the host.</summary>
    public static void SetMember(object target, string memberName, object? value)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        Host?.EnsureForm(target);
        Host?.TrySetMember(target, memberName, Array.Empty<object?>(), value);
    }

    /// <summary>Closes the designer envelope of a Form/UserControl. See the host contract.</summary>
    public static void CompleteDesignerInitialization(object target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Host?.CompleteDesignerInitialization(target);
    }

    /// <summary>
    /// Activates a registered coclass by class id, as VB6 does for New on an imported class. The
    /// ProgID path below cannot serve this: a coclass need not have one, and the type library
    /// names the class by GUID.
    /// </summary>
    public static object CreateComInstance(string classId, string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(classId);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        if (!OperatingSystem.IsWindows())
        {
            VBErrors.Raise(429, typeName, $"ActiveX component cannot create object: {typeName}", string.Empty, 0);
            return new VBComObject(typeName, string.Empty);
        }

        var comType = Type.GetTypeFromCLSID(Guid.Parse(classId), throwOnError: false);
        object? instance = null;
        try
        {
            instance = comType is null ? null : Activator.CreateInstance(comType);
        }
        catch (COMException)
        {
            instance = null;
        }
        catch (TargetInvocationException)
        {
            instance = null;
        }

        if (instance is null)
        {
            // VB6 reports 429 when the component cannot be created -- a registration that is
            // missing, or in the other process architecture. Answering a placeholder instead would
            // move the failure to the first member access and hide its cause.
            VBErrors.Raise(429, typeName, $"ActiveX component cannot create object: {typeName}", string.Empty, 0);
        }

        return instance!;
    }

    /// <summary>
    /// Creates a COM object through the host hook or Windows ProgID activation. Unknown ProgIDs
    /// remain a deterministic placeholder so headless compiler tests do not require a COM server.
    /// </summary>
    public static object CreateObject(string className, string serverName)
    {
        var resolved = CreateObjectSink?.Invoke(className, serverName);
        if (resolved is not null)
        {
            return resolved;
        }

        if (OperatingSystem.IsWindows())
        {
            var comType = string.IsNullOrWhiteSpace(serverName)
                ? Type.GetTypeFromProgID(className, throwOnError: false)
                : Type.GetTypeFromProgID(className, serverName, throwOnError: false);
            if (comType is not null)
            {
                return Activator.CreateInstance(comType)
                    ?? throw new COMException($"COM class '{className}' could not be activated.");
            }
        }

        return new VBComObject(className, serverName);
    }

    /// <summary>Gets a running COM object through a host hook or a Windows moniker.</summary>
    public static object GetObject(string pathName, string className)
    {
        var resolved = GetObjectSink?.Invoke(pathName, className);
        if (resolved is not null)
        {
            return resolved;
        }

        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(pathName))
        {
            return Marshal.BindToMoniker(pathName)
                ?? throw new COMException($"COM moniker '{pathName}' could not be resolved.");
        }

        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(className))
        {
            var comType = Type.GetTypeFromProgID(className, throwOnError: false);
            if (comType is not null && comType.GUID != Guid.Empty)
            {
                var classId = comType.GUID;
                var hresult = GetActiveObject(ref classId, IntPtr.Zero, out var activeObject);
                if (hresult >= 0 && activeObject is not null)
                {
                    return activeObject;
                }

                Marshal.ThrowExceptionForHR(hresult);
            }
        }

        return new VBComObject(className, pathName);
    }

    [DllImport("oleaut32.dll", PreserveSig = true)]
    [return: MarshalAs(UnmanagedType.I4)]
    private static extern int GetActiveObject(
        ref Guid classId,
        IntPtr reserved,
        [MarshalAs(UnmanagedType.Interface)] out object? activeObject);

    /// <summary>
    /// Starts a Windows process using the VB6 window-style contract. Portable headless hosts keep
    /// the historical deterministic zero result instead of attempting platform-specific launch.
    /// </summary>
    public static int Shell(string pathName, short windowStyle)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(pathName))
        {
            throw new ArgumentException("Shell requires a non-empty command line.", nameof(pathName));
        }

        var command = SplitShellCommand(pathName);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            UseShellExecute = true,
            WindowStyle = ToProcessWindowStyle(windowStyle)
        });
        return process?.Id ?? 0;
    }

    private static ProcessWindowStyle ToProcessWindowStyle(short windowStyle) => windowStyle switch
    {
        0 => ProcessWindowStyle.Hidden,
        2 or 6 => ProcessWindowStyle.Minimized,
        3 => ProcessWindowStyle.Maximized,
        _ => ProcessWindowStyle.Normal
    };

    private static (string FileName, string Arguments) SplitShellCommand(string commandLine)
    {
        var value = commandLine.Trim();
        if (value[0] == '"')
        {
            var closingQuote = value.IndexOf('"', 1);
            if (closingQuote > 0)
            {
                return (value[1..closingQuote], value[(closingQuote + 1)..].TrimStart());
            }
        }

        if (File.Exists(value))
        {
            return (value, string.Empty);
        }

        foreach (var extension in new[] { ".exe", ".com", ".bat", ".cmd" })
        {
            var boundary = value.IndexOf(extension, StringComparison.OrdinalIgnoreCase);
            if (boundary >= 0)
            {
                var end = boundary + extension.Length;
                if (end == value.Length || char.IsWhiteSpace(value[end]))
                {
                    return (value[..end], value[end..].TrimStart());
                }
            }
        }

        var separator = value.IndexOfAny(new[] { ' ', '\t' });
        return separator < 0
            ? (value, string.Empty)
            : (value[..separator], value[separator..].TrimStart());
    }

    /// <summary>
    /// Returns the host result when one is available, otherwise the default affirmative/first
    /// button in headless builds.
    /// </summary>
    public static short MsgBox(string prompt, int buttons, string title)
    {
        if (Host is { } host && host.TryShowMessageBox(prompt, buttons, title, out var result))
        {
            return result;
        }

        return buttons switch
        {
            4 => 6, // vbYesNo: deterministic default is Yes.
            3 => 6, // vbYesNoCancel: deterministic default is Yes.
            5 => 4, // vbRetryCancel: deterministic default is Retry.
            _ => 1 // vbOKOnly and all message-style flags.
        };
    }

    /// <summary>
    /// Uses an explicit host InputBox service when available. Compiler and CI runs stay headless
    /// and return the supplied default.
    /// </summary>
    public static string InputBox(
        string prompt,
        string title,
        string defaultResponse,
        float xpos,
        float ypos,
        string helpFile,
        int context)
    {
        if (Host is { } host && host.TryShowInputBox(
                prompt,
                title,
                defaultResponse,
                xpos,
                ypos,
                helpFile,
                context,
                out var response))
        {
            return response ?? string.Empty;
        }

        return defaultResponse;
    }

    /// <summary>Initializes <c>Command</c> from the current process unless a host supplied arguments.</summary>
    public static void InitializeCommandLine()
    {
        if (!_commandLineSetByHost)
        {
            _commandLine = ExtractCommandLineTail(Environment.CommandLine);
        }
    }

    /// <summary>Provides command-line arguments when a host invokes a generated application.</summary>
    public static void SetCommandLineArguments(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _commandLine = string.Join(" ", arguments.Select(QuoteCommandArgument));
        _commandLineSetByHost = true;
    }

    /// <summary>Clears a command-line override supplied by an external application runner.</summary>
    public static void ClearCommandLineArguments()
    {
        _commandLine = string.Empty;
        _commandLineSetByHost = false;
    }

    /// <summary>Returns the command-line tail initialized by the generated entry point or host.</summary>
    public static string Command() => _commandLine;

    private static string ExtractCommandLineTail(string commandLine)
    {
        var value = commandLine.TrimStart();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var index = 0;
        if (value[0] == '"')
        {
            index = 1;
            while (index < value.Length && value[index] != '"')
            {
                index++;
            }

            if (index < value.Length)
            {
                index++;
            }
        }
        else
        {
            while (index < value.Length && !char.IsWhiteSpace(value[index]))
            {
                index++;
            }
        }

        return value[index..].TrimStart();
    }

    private static string QuoteCommandArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return argument.Length > 0 && argument.All(character => !char.IsWhiteSpace(character) && character != '"')
            ? argument
            : '"' + argument.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
    }

    /// <summary>
    /// Returns an environment value by name or a complete <c>NAME=VALUE</c> entry by one-based
    /// numeric position. The numeric snapshot is sorted by environment name so generated code has
    /// stable behavior across managed hosts.
    /// </summary>
    public static string Environ(object? expression)
    {
        if (expression is string name)
        {
            return EnvironmentEntries()
                .FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty;
        }

        if (expression is null || VBVariants.IsNull(expression))
        {
            return string.Empty;
        }

        var index = VBConversions.CLng(expression);
        if (index <= 0)
        {
            return string.Empty;
        }

        var entries = EnvironmentEntries();
        return index <= entries.Length ? entries[index - 1].Text : string.Empty;
    }

    private static EnvironmentEntry[] EnvironmentEntries() =>
        Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(entry => new EnvironmentEntry(
                Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(entry.Value, CultureInfo.InvariantCulture) ?? string.Empty))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();

    private sealed record EnvironmentEntry(string Name, string Value)
    {
        public string Text => $"{Name}={Value}";
    }

    /// <summary>Returns the process application object used by the built-in <c>App</c> global.</summary>
    public static VBApplication Application() => ApplicationValue;

    public static string ApplicationExeName() => ApplicationValue.EXEName;

    public static string ApplicationPath() => ApplicationValue.Path;

    public static string ApplicationTitle() => ApplicationValue.Title;

    public static int ApplicationHInstance() => ApplicationValue.hInstance;

    public static int ApplicationMajor() => ApplicationValue.Major;

    public static int ApplicationMinor() => ApplicationValue.Minor;

    public static int ApplicationRevision() => ApplicationValue.Revision;

    /// <summary>Returns the runtime facade behind the built-in <c>Screen</c> global.</summary>
    public static VBScreen Screen() => ScreenValue;

    /// <summary>Returns the active form, or null for the deterministic headless fallback.</summary>
    public static object? ScreenActiveForm() => CurrentScreenState().ActiveForm;

    /// <summary>Returns the active control, or null for the deterministic headless fallback.</summary>
    public static object? ScreenActiveControl() => CurrentScreenState().ActiveControl;

    /// <summary>Returns the horizontal twips-per-pixel conversion factor.</summary>
    public static float ScreenTwipsPerPixelX() => CurrentScreenState().TwipsPerPixelX;

    /// <summary>Returns the vertical twips-per-pixel conversion factor.</summary>
    public static float ScreenTwipsPerPixelY() => CurrentScreenState().TwipsPerPixelY;

    /// <summary>Returns the process-wide VB6 mouse-pointer value.</summary>
    public static int ScreenMousePointer() => CurrentScreenState().MousePointer;

    /// <summary>Sets the process-wide VB6 mouse-pointer value through the host or headless state.</summary>
    public static void ScreenSetMousePointer(int mousePointer)
    {
        if (Host?.TrySetScreenMousePointer(mousePointer) == true)
        {
            return;
        }

        lock (ScreenGate)
        {
            _headlessScreen = _headlessScreen with { MousePointer = mousePointer };
        }
    }

    private static VBScreenState CurrentScreenState()
    {
        if (Host?.TryGetScreenState(out var screen) == true && screen is not null)
        {
            return screen;
        }

        lock (ScreenGate)
        {
            return _headlessScreen;
        }
    }

    /// <summary>Returns the runtime facade behind the built-in <c>Printer</c> global.</summary>
    public static VBPrinter Printer() => PrinterValue;

    public static string PrinterGetString(string propertyName) => propertyName.ToUpperInvariant() switch
    {
        "DEVICENAME" => CurrentPrinterState().DeviceName,
        "DRIVERNAME" => CurrentPrinterState().DriverName,
        "PORT" => CurrentPrinterState().Port,
        "DOCUMENTNAME" => CurrentPrinterState().DocumentName,
        "OUTPUTFILE" => CurrentPrinterState().OutputFile,
        _ => throw UnsupportedPrinterProperty(propertyName)
    };

    public static void PrinterSetString(string propertyName, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        UpdatePrinterState(propertyName, state => propertyName.ToUpperInvariant() switch
        {
            "DEVICENAME" => state with { DeviceName = value },
            "DRIVERNAME" => state with { DriverName = value },
            "PORT" => state with { Port = value },
            "DOCUMENTNAME" => state with { DocumentName = value },
            "OUTPUTFILE" => state with { OutputFile = value },
            _ => throw UnsupportedPrinterProperty(propertyName)
        });
    }

    public static int PrinterGetLong(string propertyName) => propertyName.ToUpperInvariant() switch
    {
        "COLORMODE" => CurrentPrinterState().ColorMode,
        "COPIES" => CurrentPrinterState().Copies,
        "DRAWMODE" => CurrentPrinterState().DrawMode,
        "DRAWSTYLE" => CurrentPrinterState().DrawStyle,
        "DRAWWIDTH" => CurrentPrinterState().DrawWidth,
        "DUPLEX" => CurrentPrinterState().Duplex,
        "FILLCOLOR" => CurrentPrinterState().FillColor,
        "FILLSTYLE" => CurrentPrinterState().FillStyle,
        "FORECOLOR" => CurrentPrinterState().ForeColor,
        "HDC" => CurrentPrinterState().Hdc,
        "HEIGHT" => CurrentPrinterState().Height,
        "ORIENTATION" => CurrentPrinterState().Orientation,
        "PAGE" => CurrentPrinterState().Page,
        "PAPERBIN" => CurrentPrinterState().PaperBin,
        "PAPERSIZE" => CurrentPrinterState().PaperSize,
        "PRINTQUALITY" => CurrentPrinterState().PrintQuality,
        "SCALEMODE" => CurrentPrinterState().ScaleMode,
        "WIDTH" => CurrentPrinterState().Width,
        "ZOOM" => CurrentPrinterState().Zoom,
        _ => throw UnsupportedPrinterProperty(propertyName)
    };

    public static void PrinterSetLong(string propertyName, int value)
    {
        UpdatePrinterState(propertyName, state => propertyName.ToUpperInvariant() switch
        {
            "COLORMODE" => state with { ColorMode = value },
            "COPIES" => state with { Copies = value },
            "DRAWMODE" => state with { DrawMode = value },
            "DRAWSTYLE" => state with { DrawStyle = value },
            "DRAWWIDTH" => state with { DrawWidth = value },
            "DUPLEX" => state with { Duplex = value },
            "FILLCOLOR" => state with { FillColor = value },
            "FILLSTYLE" => state with { FillStyle = value },
            "FORECOLOR" => state with { ForeColor = value },
            "HEIGHT" => state with { Height = value, ScaleHeight = value },
            "ORIENTATION" => state with { Orientation = value },
            "PAPERBIN" => state with { PaperBin = value },
            "PAPERSIZE" => state with { PaperSize = value },
            "PRINTQUALITY" => state with { PrintQuality = value },
            "SCALEMODE" when value is >= 0 and <= 7 => state with { ScaleMode = value },
            "SCALEMODE" => throw new ArgumentOutOfRangeException(nameof(value), "VB6 scale modes are between 0 and 7."),
            "WIDTH" => state with { Width = value, ScaleWidth = value },
            "ZOOM" when value > 0 => state with { Zoom = value },
            "ZOOM" => throw new ArgumentOutOfRangeException(nameof(value), "Printer.Zoom must be positive."),
            _ => throw UnsupportedPrinterProperty(propertyName)
        });
    }

    public static float PrinterGetSingle(string propertyName) => propertyName.ToUpperInvariant() switch
    {
        "CURRENTX" => CurrentPrinterState().CurrentX,
        "CURRENTY" => CurrentPrinterState().CurrentY,
        "SCALEHEIGHT" => CurrentPrinterState().ScaleHeight,
        "SCALELEFT" => CurrentPrinterState().ScaleLeft,
        "SCALETOP" => CurrentPrinterState().ScaleTop,
        "SCALEWIDTH" => CurrentPrinterState().ScaleWidth,
        "TWIPSPERPIXELX" => CurrentPrinterState().TwipsPerPixelX,
        "TWIPSPERPIXELY" => CurrentPrinterState().TwipsPerPixelY,
        _ => throw UnsupportedPrinterProperty(propertyName)
    };

    public static void PrinterSetSingle(string propertyName, float value)
    {
        UpdatePrinterState(propertyName, state => propertyName.ToUpperInvariant() switch
        {
            "CURRENTX" => state with { CurrentX = value },
            "CURRENTY" => state with { CurrentY = value },
            "SCALEHEIGHT" => state with { ScaleHeight = value, ScaleMode = 0 },
            "SCALELEFT" => state with { ScaleLeft = value, ScaleMode = 0 },
            "SCALETOP" => state with { ScaleTop = value, ScaleMode = 0 },
            "SCALEWIDTH" => state with { ScaleWidth = value, ScaleMode = 0 },
            _ => throw UnsupportedPrinterProperty(propertyName)
        });
    }

    public static bool PrinterGetBoolean(string propertyName) => propertyName.ToUpperInvariant() switch
    {
        "TRACKDEFAULT" => CurrentPrinterState().TrackDefault,
        "ISDEFAULTPRINTER" => CurrentPrinterState().IsDefaultPrinter,
        _ => throw UnsupportedPrinterProperty(propertyName)
    };

    public static void PrinterSetBoolean(string propertyName, bool value)
    {
        UpdatePrinterState(propertyName, state => propertyName.ToUpperInvariant() switch
        {
            "TRACKDEFAULT" => state with { TrackDefault = value },
            _ => throw UnsupportedPrinterProperty(propertyName)
        });
    }

    public static object? PrinterGetObject(string propertyName) => propertyName.ToUpperInvariant() switch
    {
        "FONT" => CurrentPrinterState().Font,
        _ => throw UnsupportedPrinterProperty(propertyName)
    };

    public static void PrinterSetObject(string propertyName, object? value)
    {
        UpdatePrinterState(propertyName, state => propertyName.ToUpperInvariant() switch
        {
            "FONT" => state with { Font = value },
            _ => throw UnsupportedPrinterProperty(propertyName)
        });
    }

    /// <summary>Appends a text line to the selected printer document without requiring a desktop.</summary>
    public static void PrinterPrint(object? value)
    {
        var text = VBConversions.CStr(value);
        Host?.TryWritePrinterText(text);
        UpdatePrinterState("Print", state => state with
        {
            Page = state.Page == 0 ? 1 : state.Page,
            CurrentX = 0f,
            CurrentY = state.CurrentY + 1f
        });
    }

    public static void PrinterNewPage()
    {
        Host?.TryAdvancePrinterPage();
        UpdatePrinterState("NewPage", state => state with
        {
            Page = state.Page == 0 ? 2 : state.Page + 1,
            CurrentX = 0f,
            CurrentY = 0f
        });
    }

    public static void PrinterEndDoc() => CompletePrinterDocument(abort: false);

    public static void PrinterKillDoc() => CompletePrinterDocument(abort: true);

    public static float PrinterTextWidth(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Host?.TryMeasurePrinterText(text, out var width, out _) == true
            ? width
            : TextWidth(text);
    }

    public static float PrinterTextHeight(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Host?.TryMeasurePrinterText(text, out _, out var height) == true
            ? height
            : TextHeight(text);
    }

    public static float PrinterScaleX(float value, int fromScale, int toScale) => ScaleX(value, fromScale, toScale);

    public static float PrinterScaleY(float value, int fromScale, int toScale) => ScaleY(value, fromScale, toScale);

    public static void PrinterPaintPicture(object? picture, float x, float y, float width, float height)
    {
        Host?.TryPaintPrinter(new VBPaintPicture(picture, x, y, width, height));
        UpdatePrinterState("PaintPicture", state => state with
        {
            Page = state.Page == 0 ? 1 : state.Page,
            CurrentX = x,
            CurrentY = y
        });
    }

    private static void CompletePrinterDocument(bool abort)
    {
        Host?.TryCompletePrinterDocument(abort);
        UpdatePrinterState(abort ? "KillDoc" : "EndDoc", state => state with
        {
            Page = 0,
            CurrentX = 0f,
            CurrentY = 0f
        });
    }

    private static VBPrinterState CurrentPrinterState()
    {
        if (Host?.TryGetPrinterState(out var printer) == true && printer is not null)
        {
            return printer;
        }

        lock (PrinterGate)
        {
            return _headlessPrinter;
        }
    }

    private static void UpdatePrinterState(string operation, Func<VBPrinterState, VBPrinterState> update)
    {
        var current = CurrentPrinterState();
        var next = update(current);
        if (Host is { } host && host.TryGetPrinterState(out var hostState) && hostState is not null)
        {
            if (host.TrySetPrinterState(next))
            {
                return;
            }

            throw new VB6RuntimeErrorException(383, $"Printer host rejected {operation}.");
        }

        lock (PrinterGate)
        {
            _headlessPrinter = next;
        }
    }

    private static VB6RuntimeErrorException UnsupportedPrinterProperty(string propertyName) =>
        new(438, $"Printer does not support property '{propertyName}'.");

    /// <summary>
    /// Provides a deterministic process-local replacement for the VB6 registry settings API.
    /// Hosts may replace this store at their boundary without changing generated call sites.
    /// </summary>
    public static string GetSetting(
        string appName,
        string section,
        string key,
        string defaultValue)
    {
        if (Host is { } host && host.TryGetSetting(appName, section, key, out var hostValue))
        {
            return hostValue ?? string.Empty;
        }

        var settingKey = MakeSettingKey(appName, section, key);
        lock (SettingsGate)
        {
            return Settings.TryGetValue(settingKey, out var value) ? value : defaultValue;
        }
    }

    public static void SaveSetting(
        string appName,
        string section,
        string key,
        string setting)
    {
        if (Host is { } host && host.TrySaveSetting(appName, section, key, setting))
        {
            return;
        }

        var settingKey = MakeSettingKey(appName, section, key);
        lock (SettingsGate)
        {
            Settings[settingKey] = setting;
        }
    }

    /// <summary>
    /// Deletes a registry key, section, or complete application entry through the host when it
    /// provides one. The deterministic fallback mirrors VB6's hierarchy without touching the
    /// interactive user's registry.
    /// </summary>
    public static void DeleteSetting(string appName, object? section = null, object? key = null)
    {
        ArgumentNullException.ThrowIfNull(appName);
        var (hasSection, sectionName) = ReadOptionalSettingPart(section, "Section");
        var (hasKey, keyName) = ReadOptionalSettingPart(key, "Key");
        if (hasKey && !hasSection)
        {
            throw new VB6RuntimeErrorException(5, "DeleteSetting requires a section when a key is supplied.");
        }

        if (Host is { } host && host.TryDeleteSetting(appName, hasSection, sectionName, hasKey, keyName))
        {
            return;
        }

        lock (SettingsGate)
        {
            var matches = Settings.Keys
                .Where(candidate =>
                    SettingKeyComparer.NameEquals(candidate.AppName, appName) &&
                    (!hasSection || SettingKeyComparer.NameEquals(candidate.Section, sectionName!)) &&
                    (!hasKey || SettingKeyComparer.NameEquals(candidate.Key, keyName!)))
                .ToArray();
            if (matches.Length == 0)
            {
                throw new VB6RuntimeErrorException(5, "DeleteSetting could not find the requested settings entry.");
            }

            foreach (var match in matches)
            {
                Settings.Remove(match);
            }
        }
    }

    /// <summary>
    /// Returns the VB6 two-column Variant array of key/value pairs for one application section.
    /// A missing application or section returns an uninitialized Variant, represented by null.
    /// </summary>
    public static object? GetAllSettings(string appName, string section)
    {
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(section);
        if (Host is { } host && host.TryGetAllSettings(appName, section, out var hostSettings))
        {
            return hostSettings;
        }

        lock (SettingsGate)
        {
            var matches = Settings
                .Where(pair =>
                    SettingKeyComparer.NameEquals(pair.Key.AppName, appName) &&
                    SettingKeyComparer.NameEquals(pair.Key.Section, section))
                .OrderBy(pair => pair.Key.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (matches.Length == 0)
            {
                return null;
            }

            var result = new VBArray<object>(
                new VBArrayBound(0, matches.Length - 1),
                new VBArrayBound(0, 1));
            for (var index = 0; index < matches.Length; index++)
            {
                result[index, 0] = matches[index].Key.Key;
                result[index, 1] = matches[index].Value;
            }

            return result;
        }
    }

    /// <summary>Forwards keyboard injection to the UI host; headless execution does nothing.</summary>
    public static void SendKeys(string keys, bool wait)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Host?.SendKeys(keys, wait);
    }

    /// <summary>Reads clipboard text through a configured sink, the active UI host, or the deterministic fallback.</summary>
    public static string ClipboardGetText(int format = ClipboardTextFormat)
    {
        if (format == ClipboardTextFormat && ClipboardTextSink is { } sink)
        {
            return sink() ?? string.Empty;
        }

        if (Host?.TryGetClipboardText(format, out var text) == true)
        {
            return text ?? string.Empty;
        }

        lock (ClipboardGate)
        {
            return ClipboardData.TryGetValue(format, out var data) && data is string storedText
                ? storedText
                : string.Empty;
        }
    }

    /// <summary>Writes text under one VB6 clipboard format without requiring a desktop session.</summary>
    public static void ClipboardSetText(string text, int format = ClipboardTextFormat)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Host?.TrySetClipboardText(text, format) == true)
        {
            return;
        }

        lock (ClipboardGate)
        {
            ClipboardData[format] = text;
        }
    }

    /// <summary>Returns opaque clipboard data or null when the requested format is unavailable.</summary>
    public static object? ClipboardGetData(int format = 0)
    {
        if (Host?.TryGetClipboardData(format, out var hostData) == true)
        {
            return hostData;
        }

        lock (ClipboardGate)
        {
            if (format == 0)
            {
                return ClipboardData.OrderBy(pair => pair.Key).Select(pair => pair.Value).FirstOrDefault();
            }

            return ClipboardData.TryGetValue(format, out var data) ? data : null;
        }
    }

    /// <summary>Writes opaque clipboard data under the requested format or an inferred default.</summary>
    public static void ClipboardSetData(object? data, int format = 0)
    {
        var effectiveFormat = format == 0 ? InferClipboardFormat(data) : format;
        if (Host?.TrySetClipboardData(data, effectiveFormat) == true)
        {
            return;
        }

        lock (ClipboardGate)
        {
            ClipboardData[effectiveFormat] = data;
        }
    }

    /// <summary>Returns whether the requested clipboard format is currently available.</summary>
    public static bool ClipboardGetFormat(int format)
    {
        if (format == ClipboardTextFormat && ClipboardTextSink is not null)
        {
            return true;
        }

        if (Host?.TryGetClipboardFormat(format, out var available) == true)
        {
            return available;
        }

        lock (ClipboardGate)
        {
            return format == 0 ? ClipboardData.Count != 0 : ClipboardData.ContainsKey(format);
        }
    }

    /// <summary>Clears every clipboard format through the configured host or deterministic fallback.</summary>
    public static void ClipboardClear()
    {
        if (Host?.TryClearClipboard() == true)
        {
            return;
        }

        lock (ClipboardGate)
        {
            ClipboardData.Clear();
        }
    }

    /// <summary>Context-menu display belongs to the UI host; headless execution intentionally does nothing.</summary>
    public static void PopupMenu(object? menu, int flags, float x, float y)
    {
        Host?.PopupMenu(menu, flags, x, y);
    }

    /// <summary>
    /// Converts a horizontal VB6 coordinate between explicit scale modes. A headless process has
    /// no control-specific <c>ScaleWidth</c>/<c>ScaleHeight</c>, so <c>vbUser</c> follows the
    /// documented twips fallback; all fixed modes use their documented units-per-inch values.
    /// </summary>
    public static float ScaleX(float value, int fromScale, int toScale) =>
        Scale(value, fromScale, toScale, vertical: false);

    /// <summary>Vertical counterpart of <see cref="ScaleX"/> (character mode is six units/inch).</summary>
    public static float ScaleY(float value, int fromScale, int toScale) =>
        Scale(value, fromScale, toScale, vertical: true);

    private static float Scale(float value, int fromScale, int toScale, bool vertical)
    {
        ValidateScaleMode(fromScale);
        ValidateScaleMode(toScale);
        if (fromScale == toScale)
        {
            return value;
        }

        var fromUnits = ScaleUnitsPerInch(fromScale, vertical);
        var toUnits = ScaleUnitsPerInch(toScale, vertical);
        return value / fromUnits * toUnits;
    }

    private static void ValidateScaleMode(int scaleMode)
    {
        if (scaleMode is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scaleMode),
                "VB6 scale modes must be between vbUser (0) and vbCentimeter (7).");
        }
    }

    private static float ScaleUnitsPerInch(int scaleMode, bool vertical) => scaleMode switch
    {
        1 => 1440f, // vbTwips
        2 => 72f,   // vbPoints
        3 => 96f,   // vbPixels at the deterministic 96-DPI headless baseline
        4 => vertical ? 6f : 12f, // vbCharacters
        5 => 1f,    // vbInches
        6 => 25.4f, // vbMillimeters
        7 => 2.54f, // vbCentimeters
        0 => 1440f, // vbUser falls back to twips without a control-specific scale
        _ => 1f
    };

    /// <summary>Returns a deterministic character-width approximation for headless control code.</summary>
    public static float TextWidth(string text) => text.Length;

    /// <summary>Returns a deterministic line-height approximation for headless control code.</summary>
    public static float TextHeight(string text) => text.Length == 0 ? 0f : 1f;

    /// <summary>
    /// Enumerates controls supplied by a UI or COM host. Headless runs return an empty snapshot;
    /// the callback keeps generated code independent of a concrete Forms implementation.
    /// </summary>
    public static VBArray<object> EnumerateControls(object? target)
    {
        var isComCollection = target is not null && Marshal.IsComObject(target);
        var values = ControlEnumerationSink?.Invoke(target)?.ToArray() ??
            Host?.EnumerateControls(target)?.ToArray() ??
            (target is VBCollection collection
                ? VBCollection.EnumerateValues(collection).EnumerateValues().Cast<object?>().ToArray()
                : target is System.Collections.IEnumerable enumerable
                ? EnumerateHostValues(enumerable, isComCollection)
                : Array.Empty<object?>());
        var result = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = values[index]!;
        }

        return result;
    }

    /// <summary>
    /// Enumerates whatever a Variant or a late-bound object turns out to hold: an array, a
    /// Collection, or a COM object with _NewEnum. Unlike the control snapshot above, an empty
    /// answer is not a legitimate outcome here -- a value with no enumerator is error 438, and a
    /// Nothing is 91, exactly as VB6 answers them.
    /// </summary>
    public static VBArray<object> EnumerateObjectValues(object? target)
    {
        if (target is null || VBVariants.IsNothing(target))
        {
            VBErrors.Raise(91, "For Each", "Object variable or With block variable not set", string.Empty, 0);
        }

        switch (target)
        {
            case VBArray<object> variantArray:
                return variantArray;
            case VBCollection collection:
                return VBCollection.EnumerateValues(collection);
        }

        var values = target is System.Collections.IEnumerable enumerable
            ? EnumerateHostValues(enumerable, OperatingSystem.IsWindows() && Marshal.IsComObject(target))
            : null;
        if (values is null)
        {
            VBErrors.Raise(
                438,
                "For Each",
                "Object does not support this property or method",
                string.Empty,
                0);
            values = Array.Empty<object?>();
        }

        var result = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = values[index]!;
        }

        return result;
    }

    private static object?[] EnumerateHostValues(
        System.Collections.IEnumerable enumerable,
        bool isComCollection)
    {
        var values = enumerable.Cast<object?>();
        // Some legacy IEnumVARIANT implementations expose a trailing VT_EMPTY entry through
        // the RCW enumerator even though the collection Count excludes it. It is not a control
        // element and must not become an extra Variant item in a VB6 For Each loop.
        return (isComCollection ? values.Where(value => value is not null) : values).ToArray();
    }

    /// <summary>Forwards an unqualified control Print call to an optional host sink.</summary>
    public static void Print(object? value) => PrintSink?.Invoke(value);

    /// <summary>Sends a supported control PaintPicture call to the active host or headless sink.</summary>
    public static void PaintPicture(object? picture, float x, float y, float width, float height)
    {
        var operation = new VBPaintPicture(picture, x, y, width, height);
        if (Host is { } host)
        {
            host.PaintPicture(operation);
        }
        else
        {
            PaintPictureSink?.Invoke(operation);
        }
    }

    /// <summary>Clears the active VB6 drawing surface through the optional UI host.</summary>
    public static void Cls()
    {
        if (Host is { } host)
        {
            host.GraphicsClear(null);
        }
        else
        {
            ClsSink?.Invoke();
        }
    }

    /// <summary>Optional host callback for unqualified control Print calls.</summary>
    public static Action<object?>? PrintSink { get; set; }

    /// <summary>Optional host callback supplying the controls exposed by a Form or UserControl.</summary>
    public static Func<object?, IEnumerable<object?>>? ControlEnumerationSink { get; set; }

    /// <summary>Optional host callback for the supported PaintPicture argument set.</summary>
    public static Action<VBPaintPicture>? PaintPictureSink { get; set; }

    /// <summary>Optional headless callback for an unqualified <c>Cls</c> operation.</summary>
    public static Action? ClsSink { get; set; }

    /// <summary>Optional host-independent clipboard text source for headless execution.</summary>
    public static Func<string?>? ClipboardTextSink { get; set; }

    internal static bool TryGetHostMember(
        object? target,
        string memberName,
        object?[] arguments,
        out object? value)
    {
        if (target is not null && Host is not null)
        {
            return Host.TryGetMember(target, memberName, arguments, out value);
        }

        value = null;
        return false;
    }

    internal static bool TrySetHostMember(
        object? target,
        string memberName,
        object?[] arguments,
        object? value)
    {
        return target is not null &&
               Host is not null &&
               Host.TrySetMember(target, memberName, arguments, value);
    }

    internal static bool TryInvokeHostMember(
        object? target,
        string memberName,
        object?[] arguments,
        out object? result)
    {
        if (target is not null && Host is not null)
        {
            return Host.TryInvokeMember(target, memberName, arguments, out result);
        }

        result = null;
        return false;
    }

    public static VBPicture LoadPicture(string fileName) => new(fileName);

    /// <summary>Creates the host-neutral object behind VB6 Font/StdFont values.</summary>
    public static VBFont CreateFont() => new();

    /// <summary>Signals a changed UserControl property to a host; headless execution has no sink.</summary>
    public static void PropertyChanged(string propertyName) => _ = propertyName;

    /// <summary>
    /// Sends a VB6 graphics Line operation to the active UI host. Headless runs keep the contract
    /// deterministic and perform no drawing when no sink is installed.
    /// </summary>
    public static void GraphicsLine(
        float startX,
        float startY,
        float endX,
        float endY,
        object? color,
        bool isStep,
        bool drawBox,
        bool fill)
    {
        var line = new VBGraphicsLine(
            startX,
            startY,
            endX,
            endY,
            color is null ? null : VBConversions.CLng(color),
            isStep,
            drawBox,
            fill);
        if (Host is { } host)
        {
            host.GraphicsLine(line);
        }
        else
        {
            GraphicsLineSink?.Invoke(line);
        }
    }

    /// <summary>Sends a graphics Line operation to a specific Form or control host target.</summary>
    public static void GraphicsLine(
        object? target,
        float startX,
        float startY,
        float endX,
        float endY,
        object? color,
        bool isStep,
        bool drawBox,
        bool fill)
    {
        var line = new VBGraphicsLine(
            startX,
            startY,
            endX,
            endY,
            color is null ? null : VBConversions.CLng(color),
            isStep,
            drawBox,
            fill);
        if (Host is { } host)
        {
            host.GraphicsLine(target, line);
        }
        else
        {
            GraphicsLineSink?.Invoke(line);
        }
    }

    /// <summary>Host callback for drawing operations; null means a headless no-op backend.</summary>
    public static Action<VBGraphicsLine>? GraphicsLineSink { get; set; }

    /// <summary>Sets a single pixel on the active host surface.</summary>
    public static void GraphicsPSet(float x, float y, object? color, bool isStep)
    {
        var point = new VBGraphicsPoint(x, y, color is null ? null : VBConversions.CLng(color), isStep);
        if (Host is { } host)
        {
            host.GraphicsPSet(point);
        }
        else
        {
            GraphicsPSetSink?.Invoke(point);
        }
    }

    /// <summary>Sets a single pixel on a specific Form or control host target.</summary>
    public static void GraphicsPSet(object? target, float x, float y, object? color, bool isStep)
    {
        var point = new VBGraphicsPoint(x, y, color is null ? null : VBConversions.CLng(color), isStep);
        if (Host is { } host)
        {
            host.GraphicsPSet(target, point);
        }
        else
        {
            GraphicsPSetSink?.Invoke(point);
        }
    }

    public static Action<VBGraphicsPoint>? GraphicsPSetSink { get; set; }

    private static VBGraphicsCircle CreateCircle(
        float x,
        float y,
        float radius,
        object? color,
        object? start,
        object? end,
        object? aspect,
        bool isStep) =>
        new(
            x,
            y,
            radius,
            color is null ? null : VBConversions.CLng(color),
            start is null ? null : VBConversions.CSng(start),
            end is null ? null : VBConversions.CSng(end),
            aspect is null ? null : VBConversions.CSng(aspect),
            isStep);

    /// <summary>Draws a VB6 Circle, arc or segment on the active host surface.</summary>
    public static void GraphicsCircle(
        float x,
        float y,
        float radius,
        object? color,
        object? start,
        object? end,
        object? aspect,
        bool isStep)
    {
        var circle = CreateCircle(x, y, radius, color, start, end, aspect, isStep);
        if (Host is { } host)
        {
            host.GraphicsCircle(circle);
        }
        else
        {
            GraphicsCircleSink?.Invoke(circle);
        }
    }

    /// <summary>Draws a Circle on a specific Form or control host target.</summary>
    public static void GraphicsCircle(
        object? target,
        float x,
        float y,
        float radius,
        object? color,
        object? start,
        object? end,
        object? aspect,
        bool isStep)
    {
        var circle = CreateCircle(x, y, radius, color, start, end, aspect, isStep);
        if (Host is { } host)
        {
            host.GraphicsCircle(target, circle);
        }
        else
        {
            GraphicsCircleSink?.Invoke(circle);
        }
    }

    public static Action<VBGraphicsCircle>? GraphicsCircleSink { get; set; }

    /// <summary>
    /// Reads the colour of one pixel of the active drawing surface. VB6 answers -1 for a point
    /// outside the surface, and that is also what a host without a drawing surface reports.
    /// </summary>
    /// <summary>
    /// The input-method state of the active window. VB6 answers 0 -- vbIMEModeNoControl -- on a
    /// system without an East Asian IME, and that is the only answer this host can give: it never
    /// installs one. Reporting an error instead would break code that merely asks.
    /// </summary>
    public static short IMEStatus() => 0;

    public static int GraphicsPoint(float x, float y) =>
        Host is { } host && host.TryGetGraphicsPoint(x, y, out var color) ? color : -1;

    private static SettingKey MakeSettingKey(string appName, string section, string key) =>
        new(appName, section, key);

    private static (bool HasValue, string? Value) ReadOptionalSettingPart(object? value, string parameterName)
    {
        if (value is null || VBVariants.IsMissing(value))
        {
            return (false, null);
        }

        if (VBVariants.IsNull(value))
        {
            throw new VB6RuntimeErrorException(94, $"Invalid use of Null for DeleteSetting {parameterName}.");
        }

        return (true, VBConversions.CStr(value));
    }

    private static int InferClipboardFormat(object? data) => data is string ? ClipboardTextFormat : 0;

    private readonly record struct SettingKey(string AppName, string Section, string Key);

    private sealed class SettingKeyComparer : IEqualityComparer<SettingKey>
    {
        public static readonly SettingKeyComparer Instance = new();

        public bool Equals(SettingKey left, SettingKey right) =>
            NameEquals(left.AppName, right.AppName) &&
            NameEquals(left.Section, right.Section) &&
            NameEquals(left.Key, right.Key);

        public int GetHashCode(SettingKey value) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.AppName),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Section),
            StringComparer.OrdinalIgnoreCase.GetHashCode(value.Key));

        public static bool NameEquals(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Managed storage for the VB6 Font/StdFont host contract.</summary>
public sealed class VBFont
{
    public string Name { get; set; } = string.Empty;
    public float Size { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }
    public int Weight { get; set; }
    public short Charset { get; set; }
    public int hFont { get; set; }
}

/// <summary>
/// Host-neutral application metadata exposed through VB6's global <c>App</c> object. A UI or
/// native host can supply a richer instance without changing generated member access.
/// </summary>
public sealed class VBApplication
{
    private VBApplication(
        string exeName,
        string path,
        string title,
        int major,
        int minor,
        int revision)
    {
        EXEName = exeName;
        Path = path;
        Title = title;
        Major = major;
        Minor = minor;
        Revision = revision;
    }

    public string EXEName { get; }

    public string Path { get; }

    public string Title { get; }

    public int hInstance => 0;

    public int Major { get; }

    public int Minor { get; }

    public int Revision { get; }

    internal static VBApplication Create()
    {
        var assembly = Assembly.GetEntryAssembly();
        var assemblyPath = assembly?.Location;
        var fullPath = string.IsNullOrWhiteSpace(assemblyPath)
            ? Environment.ProcessPath
            : assemblyPath;
        var exeName = string.IsNullOrWhiteSpace(fullPath)
            ? assembly?.GetName().Name ?? string.Empty
            : System.IO.Path.GetFileNameWithoutExtension(fullPath);
        var path = string.IsNullOrWhiteSpace(fullPath)
            ? AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
            : System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty;
        var assemblyName = assembly?.GetName();
        var version = assemblyName?.Version;
        var title = assembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;

        return new VBApplication(
            exeName,
            path,
            string.IsNullOrEmpty(title) ? assemblyName?.Name ?? exeName : title,
            version?.Major ?? 0,
            version?.Minor ?? 0,
            version?.Revision ?? 0);
    }
}

/// <summary>
/// A VB6 <c>PSet</c> operation. <paramref name="IsStep"/> makes the coordinates relative to the
/// current drawing position, exactly as it does for <c>Line</c>; a null colour means the surface
/// keeps its current ForeColor.
/// </summary>
/// <summary>
/// A VB6 <c>Circle</c> operation. A null optional keeps the documented default: the current
/// ForeColor, a full circle from 0 to two pi, and an aspect ratio of one.
/// </summary>
public sealed record VBGraphicsCircle(
    float X,
    float Y,
    float Radius,
    int? Color,
    float? Start,
    float? End,
    float? Aspect,
    bool IsStep);

public sealed record VBGraphicsPoint(
    float X,
    float Y,
    int? Color,
    bool IsStep);

public sealed record VBGraphicsLine(
    float StartX,
    float StartY,
    float EndX,
    float EndY,
    int? Color,
    bool IsStep,
    bool DrawBox,
    bool Fill);

public sealed record VBPaintPicture(
    object? Picture,
    float X,
    float Y,
    float Width,
    float Height);

public sealed record VBComObject(string ClassName, string ServerName);

public sealed record VBPicture(string FileName)
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Type { get; init; }
}

/// <summary>Minimal host-neutral PropertyBag implementation used by ActiveX UserControls.</summary>
public sealed class VBPropertyBag
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public object? ReadProperty(string name, object? defaultValue = null) =>
        _values.TryGetValue(name, out var value) ? value : defaultValue;

    public void WriteProperty(string name, object? value, object? defaultValue = null)
    {
        _ = defaultValue;
        _values[name] = value;
    }

    /// <summary>
    /// True while the bag holds nothing. VB6 decides on exactly this whether a UserControl gets
    /// <c>InitProperties</c> -- it is new -- or <c>ReadProperties</c>, which restores a control
    /// that was saved before.
    /// </summary>
    public bool IsEmpty => _values.Count == 0;
}
