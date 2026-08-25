using System.Collections;
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

        return new VBComObject(className, pathName);
    }

    /// <summary>Process launching is delegated to the host; headless builds return a stable id.</summary>
    public static int Shell(string pathName, short windowStyle) => 0;

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

    /// <summary>Returns an empty command line in headless runs; hosts can supply process arguments.</summary>
    public static string Command() => string.Empty;

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

    /// <summary>Keyboard injection belongs to the UI host; headless execution intentionally does nothing.</summary>
    public static void SendKeys(string keys, bool wait)
    {
        _ = keys;
        _ = wait;
    }

    /// <summary>Context-menu display belongs to the UI host; headless execution intentionally does nothing.</summary>
    public static void PopupMenu(object? menu, int flags, float x, float y)
    {
        Host?.PopupMenu(menu, flags, x, y);
    }

    /// <summary>Headless controls use identity scaling; a UI host can supply its scale modes.</summary>
    public static float ScaleX(float value, int fromScale, int toScale) => value;

    /// <summary>Headless controls use identity scaling; a UI host can supply its scale modes.</summary>
    public static float ScaleY(float value, int fromScale, int toScale) => value;

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

    /// <summary>Optional host callback for unqualified control Print calls.</summary>
    public static Action<object?>? PrintSink { get; set; }

    /// <summary>Optional host callback supplying the controls exposed by a Form or UserControl.</summary>
    public static Func<object?, IEnumerable<object?>>? ControlEnumerationSink { get; set; }

    /// <summary>Optional host callback for the supported PaintPicture argument set.</summary>
    public static Action<VBPaintPicture>? PaintPictureSink { get; set; }

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
