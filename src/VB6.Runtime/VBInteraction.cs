namespace VB6.Runtime;

/// <summary>Headless, deterministic implementations of VB6 interaction intrinsics.</summary>
public static class VBInteraction
{
    private static readonly Dictionary<string, string> Settings = new(StringComparer.Ordinal);
    private static readonly object SettingsGate = new();

    /// <summary>Yielding to a UI message pump is a host concern; the compiler runtime has no pump.</summary>
    public static void DoEvents()
    {
    }

    /// <summary>Form loading is supplied by the UI host; headless compilation has no form store.</summary>
    public static void Load(object? value)
    {
    }

    /// <summary>Form unloading is supplied by the UI host; headless compilation has no form store.</summary>
    public static void Unload(object? value)
    {
    }

    /// <summary>
    /// Creates a host-owned COM object placeholder. Native/COM hosts can replace this contract
    /// with IDispatch activation without changing generated call sites.
    /// </summary>
    public static object CreateObject(string className, string serverName) =>
        new VBComObject(className, serverName);

    public static object GetObject(string pathName, string className) =>
        new VBComObject(className, pathName);

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
        _ = menu;
        _ = flags;
        _ = x;
        _ = y;
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
        var values = ControlEnumerationSink?.Invoke(target)?.ToArray() ??
            (target is VBCollection collection
                ? VBCollection.EnumerateValues(collection).EnumerateValues().Cast<object?>().ToArray()
                : target is System.Collections.IEnumerable enumerable
                ? enumerable.Cast<object?>().ToArray()
                : Array.Empty<object?>());
        var result = new VBArray<object>(new VBArrayBound(0, values.Length - 1));
        for (var index = 0; index < values.Length; index++)
        {
            result[index] = values[index]!;
        }

        return result;
    }

    /// <summary>Forwards an unqualified control Print call to an optional host sink.</summary>
    public static void Print(object? value) => PrintSink?.Invoke(value);

    /// <summary>Forwards a control PaintPicture call to an optional host sink.</summary>
    public static void PaintPicture(object? picture, float x, float y, float width, float height) =>
        PaintPictureSink?.Invoke(new VBPaintPicture(picture, x, y, width, height));

    /// <summary>Optional host callback for unqualified control Print calls.</summary>
    public static Action<object?>? PrintSink { get; set; }

    /// <summary>Optional host callback supplying the controls exposed by a Form or UserControl.</summary>
    public static Func<object?, IEnumerable<object?>>? ControlEnumerationSink { get; set; }

    /// <summary>Optional host callback for the supported PaintPicture argument set.</summary>
    public static Action<VBPaintPicture>? PaintPictureSink { get; set; }

    public static VBPicture LoadPicture(string fileName) => new(fileName);

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
        GraphicsLineSink?.Invoke(new VBGraphicsLine(
            startX,
            startY,
            endX,
            endY,
            color is null ? null : VBConversions.CLng(color),
            isStep,
            drawBox,
            fill));
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
