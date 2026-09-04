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
/// control comes up with its defaults, silently. Measured against the registered stock controls,
/// three values are not reachable any other way at all: <c>_ExtentX</c>, <c>_ExtentY</c> and
/// <c>_Version</c> stand in every <c>.frm</c> and are refused by every control over IDispatch.
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
    ///
    /// Names carry their <c>BeginProperty</c> nesting as a dotted path
    /// (<c>Images.ListImage1.Picture</c>); the bag turns that back into the sub-objects the
    /// control asks for.
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

        persist.Load(new VBDesignerPropertyBag(VBDesignerStateGroup.Build(values)), IntPtr.Zero);
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

/// <summary>
/// One level of the designer envelope: the values written directly at this level, and the
/// <c>BeginProperty</c> groups nested inside it.
/// </summary>
internal sealed class VBDesignerStateGroup
{
    public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, VBDesignerStateGroup> Groups { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Turns the dotted paths back into the nesting the designer wrote.</summary>
    public static VBDesignerStateGroup Build(IReadOnlyDictionary<string, object?> values)
    {
        var root = new VBDesignerStateGroup();
        foreach (var pair in values)
        {
            var segments = pair.Key.Split('.');
            var group = root;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (!group.Groups.TryGetValue(segments[index], out var nested))
                {
                    nested = new VBDesignerStateGroup();
                    group.Groups[segments[index]] = nested;
                }

                group = nested;
            }

            group.Values[segments[^1]] = pair.Value;
        }

        return root;
    }
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[SupportedOSPlatform("windows")]
internal sealed class VBDesignerPropertyBag : IVBPropertyBag
{
    private const int ErrorNotFound = unchecked((int)0x80070490); // E_PROP_ID_UNSUPPORTED shape

    private readonly VBDesignerStateGroup _state;

    public VBDesignerPropertyBag(VBDesignerStateGroup state) => _state = state;

    /// <summary>Every property the control actually asked for.</summary>
    public List<string> ReadProperties { get; } = new();

    public int Read(string propertyName, ref object? value, IntPtr errorLog)
    {
        _ = errorLog;
        var requested = value;
        ReadProperties.Add(propertyName);

        if (!_state.Values.TryGetValue(propertyName, out var stored))
        {
            return TryLoadNestedObject(propertyName, requested);
        }

        // The caller passes the type it expects in value; converting to it is what the container
        // owes the control, and it is the reason a bag is typed at all. Measured against the stock
        // OCX: a control announces Int32, Int16, Single, Boolean -- and null wherever it wants an
        // object, which is where there is nothing to convert to.
        value = stored;
        if (stored is not null && requested is not null && stored.GetType() != requested.GetType())
        {
            try
            {
                value = Convert.ChangeType(
                    stored,
                    requested.GetType(),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (
                exception is InvalidCastException or FormatException or OverflowException)
            {
                // A designer value the control cannot use is its own decision to make. Failing the
                // read here would abort the whole Load and cost every other property with it.
                value = stored;
            }
        }

        return 0;
    }

    public int Write(string propertyName, ref object? value)
    {
        _ = propertyName;
        _ = value;

        // Saving belongs to a designer. This host runs programs; there is nothing to write back to.
        return unchecked((int)0x80004001); // E_NOTIMPL
    }

    /// <summary>
    /// A BeginProperty group is read as an object, not as a value: the control creates the
    /// collection or sub-object itself, passes it in, and expects the container to fill it from
    /// the nested state. That is how an ImageList gets its images and a Toolbar its buttons.
    ///
    /// A control that passes null instead expects the container to create the object. There is no
    /// general way to do that from a designer envelope, so the group stays unread and the control
    /// keeps its default — reported as not-found rather than as an empty success.
    /// </summary>
    private int TryLoadNestedObject(string propertyName, object? requested)
    {
        if (requested is not IVBPersistPropertyBag nested ||
            !_state.Groups.TryGetValue(propertyName, out var group))
        {
            // A control asks for everything it might have saved. A name the designer never wrote
            // is not an error — the control keeps the default it already has.
            return ErrorNotFound;
        }

        nested.Load(new VBDesignerPropertyBag(group), IntPtr.Zero);
        return 0;
    }
}
