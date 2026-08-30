using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Headless, deterministic implementations of VB6 interaction intrinsics.</summary>
public static class VBInteraction
{
    private static readonly Dictionary<string, string> Settings = new(StringComparer.Ordinal);
    private static readonly object SettingsGate = new();
    private static readonly VBApplication ApplicationValue = VBApplication.Create();
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
    /// Returns the default affirmative/first button in headless builds. A GUI host can replace this
    /// service at the application boundary without changing generated code.
    /// </summary>
    public static short MsgBox(string prompt, int buttons, string title) => buttons switch
    {
        4 => 6, // vbYesNo: deterministic default is Yes.
        3 => 6, // vbYesNoCancel: deterministic default is Yes.
        5 => 4, // vbRetryCancel: deterministic default is Retry.
        _ => 1 // vbOKOnly and all message-style flags.
    };

    /// <summary>
    /// Headless InputBox contract. A UI host can replace this implementation and keep the
    /// generated call signature stable; compiler and CI runs return the supplied default.
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
        _ = prompt;
        _ = title;
        _ = xpos;
        _ = ypos;
        _ = helpFile;
        _ = context;
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
        var settingKey = MakeSettingKey(appName, section, key);
        lock (SettingsGate)
        {
            Settings[settingKey] = setting;
        }
    }

    /// <summary>Forwards keyboard injection to the UI host; headless execution does nothing.</summary>
    public static void SendKeys(string keys, bool wait)
    {
        ArgumentNullException.ThrowIfNull(keys);
        Host?.SendKeys(keys, wait);
    }

    /// <summary>Reads clipboard text through a configured sink or the active UI host.</summary>
    public static string ClipboardGetText()
    {
        if (ClipboardTextSink is { } sink)
        {
            return sink() ?? string.Empty;
        }

        return Host?.TryGetClipboardText(out var text) == true
            ? text ?? string.Empty
            : string.Empty;
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

    private static string MakeSettingKey(string appName, string section, string key) =>
        string.Join(
            '\u001f',
            appName.ToUpperInvariant(),
            section.ToUpperInvariant(),
            key.ToUpperInvariant());
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
}
