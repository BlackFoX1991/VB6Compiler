using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// How a third-party ActiveX control gets its designer state back.
///
/// VB6 does not set an OCX's properties one by one. It hands the whole persisted state to the
/// control and lets the control read what it knows — through <c>IPersistPropertyBag</c>, with the
/// container supplying the bag. Most stock and third-party controls keep their state that way, so
/// a container that only assigns the properties it happens to recognise loses everything else: the
/// control comes up with its defaults, silently.
///
/// The container half is this file's job, and it is the half that can be tested here. A control
/// that does not implement the interface is not an error — it simply keeps its state elsewhere,
/// and the ordinary property assignment covers it.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VBComPersistence
{
    /// <summary>
    /// Gives a control its persisted designer state. Returns <see langword="false"/> when the
    /// control does not use property-bag persistence, which leaves the caller free to fall back
    /// to assigning the properties it knows.
    /// </summary>
    public static bool TryApplyDesignerState(
        object control,
        IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(values);

        if (control is not IVBPersistPropertyBag persist)
        {
            return false;
        }

        // InitNew first: a control has to be initialised before it is told what it should hold,
        // and VB6 calls it for a control that has no persisted state at all.
        persist.InitNew();
        if (values.Count == 0)
        {
            return true;
        }

        persist.Load(new VBDesignerPropertyBag(values), IntPtr.Zero);
        return true;
    }
}

/// <summary>
/// The container's property bag. A control reads from it by name and takes whatever it recognises;
/// a name it does not ask for simply stays unread.
/// </summary>
[ComVisible(true)]
[Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBPropertyBag
{
    [PreserveSig]
    int Read(
        [MarshalAs(UnmanagedType.LPWStr)] string propertyName,
        [MarshalAs(UnmanagedType.Struct)] ref object? value,
        IntPtr errorLog);

    [PreserveSig]
    int Write(
        [MarshalAs(UnmanagedType.LPWStr)] string propertyName,
        [MarshalAs(UnmanagedType.Struct)] ref object? value);
}

/// <summary>
/// The control's side. It derives from IPersist, so <c>GetClassID</c> holds the first slot — a
/// detail that is invisible in C# and fatal to get wrong in the vtable.
/// </summary>
[ComVisible(true)]
[Guid("37D84F60-42CB-11CE-8135-00AA004BB851")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBPersistPropertyBag
{
    void GetClassID(out Guid classId);

    void InitNew();

    void Load(IVBPropertyBag propertyBag, IntPtr errorLog);

    void Save(
        IVBPropertyBag propertyBag,
        [MarshalAs(UnmanagedType.Bool)] bool clearDirty,
        [MarshalAs(UnmanagedType.Bool)] bool saveAllProperties);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[SupportedOSPlatform("windows")]
internal sealed class VBDesignerPropertyBag : IVBPropertyBag
{
    private const int ErrorNotFound = unchecked((int)0x80070490); // E_PROP_ID_UNSUPPORTED shape

    private readonly IReadOnlyDictionary<string, object?> _values;

    public VBDesignerPropertyBag(IReadOnlyDictionary<string, object?> values) => _values = values;

    /// <summary>Every property the control actually asked for.</summary>
    public List<string> ReadProperties { get; } = new();

    public int Read(string propertyName, ref object? value, IntPtr errorLog)
    {
        _ = errorLog;
        ReadProperties.Add(propertyName);
        if (!_values.TryGetValue(propertyName, out var stored))
        {
            // A control asks for everything it might have saved. A name the designer never wrote
            // is not an error -- the control keeps the default it already has.
            return ErrorNotFound;
        }

        // The caller passes the type it expects in value; converting to it is what the container
        // owes the control, and it is the reason a bag is typed at all.
        value = stored is null || value is null
            ? stored
            : Convert.ChangeType(stored, value.GetType(), System.Globalization.CultureInfo.InvariantCulture);
        return 0;
    }

    public int Write(string propertyName, ref object? value)
    {
        _ = propertyName;
        _ = value;

        // Saving belongs to a designer. This host runs programs; there is nothing to write back to.
        return unchecked((int)0x80004001); // E_NOTIMPL
    }
}
