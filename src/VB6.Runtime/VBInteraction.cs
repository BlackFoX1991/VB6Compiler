namespace VB6.Runtime;

/// <summary>Headless, deterministic implementations of VB6 interaction intrinsics.</summary>
public static class VBInteraction
{
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
}

public sealed record VBComObject(string ClassName, string ServerName);
