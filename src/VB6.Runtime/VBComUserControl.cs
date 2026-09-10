using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace VB6.Runtime;

/// <summary>
/// What a generated UserControl is to a foreign container.
///
/// An ActiveX control is not one interface but a set, and a container decides what it may do with
/// a control by asking for them one at a time. Measured before any of this existed: a compiled
/// <c>.ctl</c> activated reg-free out of its own manifest answered <c>IDispatch</c>,
/// <c>IProvideClassInfo</c> and <c>IConnectionPointContainer</c> -- all three from the CLR -- and
/// <c>E_NOINTERFACE</c> to every OLE control interface there is. It was an Automation object that
/// happened to come from a <c>.ctl</c>, not a control.
///
/// This class is that missing half, and it sits on <see cref="VBComEventSource"/> because a control
/// is an event source first: its events are the reason a container hosts it rather than calls it.
///
/// The division of labour is the project's usual one. Everything that is decidable without a
/// screen -- identity, extent, persistence, ambient properties, the client site, the advise list --
/// is answered here, deterministically and without a host. Everything that needs a window or a
/// device context is asked of an <see cref="IVBControlPresentation"/> the host attaches, and
/// without one those members say so instead of pretending: a container that is told a control
/// activated when it did not lays out around a window that never appears.
/// </summary>
[ComVisible(true)]
[SupportedOSPlatform("windows")]
public abstract class VBComUserControl
    : VBComEventSource,
      IVBOleObject,
      IVBOleControl,
      IVBOleWindow,
      IVBOleInPlaceObject,
      IVBOleInPlaceActiveObject,
      IVBViewObject,
      IVBViewObject2,
      IVBPersistStreamInit,
      IVBPersistPropertyBag
{
    private const int SOk = 0;
    private const int SFalse = 1;
    private const int ENotImpl = unchecked((int)0x80004001);
    private const int EFail = unchecked((int)0x80004005);
    private const int EInvalidArg = unchecked((int)0x80070057);
    private const int OleENotRunning = unchecked((int)0x80040005);
    private const int OleEBlank = unchecked((int)0x80040007);
    private const int OleObjSCannotDoVerbNow = 0x00040181;

    /// <summary>
    /// What a container learns about this control before it creates one. The combination is the
    /// one a VB6 UserControl reports: it is drawn inside the container's window
    /// (<c>INSIDEOUT</c> plus <c>ACTIVATEWHENVISIBLE</c>), it redraws rather than scales when
    /// resized, it cannot be the inside of a link, and it wants its client site before it is
    /// loaded -- the last one is why <c>SETCLIENTSITEFIRST</c> matters: without it a container
    /// loads the control's state first, and every ambient property the control reads while
    /// restoring is missing.
    /// </summary>
    private const int ControlMiscStatus =
        0x00000001 |   // OLEMISC_RECOMPOSEONRESIZE
        0x00000010 |   // OLEMISC_CANTLINKINSIDE
        0x00000080 |   // OLEMISC_INSIDEOUT
        0x00000100 |   // OLEMISC_ACTIVATEWHENVISIBLE
        0x00020000;    // OLEMISC_SETCLIENTSITEFIRST

    private readonly Dictionary<int, IntPtr> _adviseSinks = new();
    private readonly object _sync = new();
    private VBPropertyBag _propertyBag = new();
    private IntPtr _clientSite;
    private int _nextAdviseConnection = 1;
    private int _frozenEvents;
    private IVBControlPresentation? _presentation;
    private bool _presentationResolved;
    private bool _dirty;
    private bool _initialised;

    /// <summary>
    /// Resolves the presentation before the generated class's own constructor body runs.
    ///
    /// The order is not a preference, it is forced: the designer envelope of a generated control
    /// stands in **its** constructor, which runs immediately after this one, and it creates the
    /// control's children through the ambient host. A container activates the class with
    /// <c>CoCreateInstance</c>, so there is no earlier moment to put a host in place -- and
    /// without one the envelope runs against nothing and the control ends up with no children at
    /// all, silently.
    ///
    /// A process with no presentation companion pays one failed assembly load, once for the
    /// lifetime of the process.
    /// </summary>
    protected VBComUserControl()
    {
        _presentation = VBControlPresentationHost.TryCreate(this);
        _presentationResolved = true;
    }

    /// <summary>
    /// The size the designer gave this control, in HIMETRIC. Zero means the designer never spoke,
    /// and <see cref="IVBOleObject.GetExtent"/> then reports failure rather than inventing one --
    /// a container that is handed a made-up size lays out around it, and the mistake surfaces as a
    /// layout defect somewhere else entirely.
    /// </summary>
    protected VBOleSize DesignExtent { get; set; }

    /// <summary>
    /// The window and drawing side, when a host attached one or a companion could be found.
    ///
    /// It resolves on first need rather than in the constructor: a headless process must not load
    /// a UI framework because a control object happened to be created, and a container that only
    /// reads properties never asks for a window at all. Null stays the ordinary state, and every
    /// member that needs one says so.
    /// </summary>
    protected IVBControlPresentation? Presentation
    {
        get
        {
            if (_presentation is null && !_presentationResolved)
            {
                _presentationResolved = true;
                _presentation = VBControlPresentationHost.TryCreate(this);
            }

            return _presentation;
        }
    }

    /// <summary>
    /// Takes the designer size from the <c>.ctl</c>, in twips, and stores it as the extent OLE
    /// asks for.
    ///
    /// The two units are the whole reason this is a method rather than a property the emitter
    /// fills: VB6 designs in twips (1/1440 inch) and OLE measures in HIMETRIC (1/100 mm), and a
    /// container handed twips lays out a control roughly 1.76 times too small. Converting here
    /// keeps the arithmetic in one place instead of in emitted IL.
    /// </summary>
    protected void SetDesignExtentFromTwips(int widthTwips, int heightTwips)
    {
        const int HiMetricPerInch = 2540;
        const int TwipsPerInch = 1440;

        if (widthTwips <= 0 || heightTwips <= 0)
        {
            return;
        }

        DesignExtent = new VBOleSize
        {
            Width = (int)((long)widthTwips * HiMetricPerInch / TwipsPerInch),
            Height = (int)((long)heightTwips * HiMetricPerInch / TwipsPerInch)
        };
    }

    /// <summary>Attaches the host's window and drawing implementation to this control.</summary>
    public void AttachPresentation(IVBControlPresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        _presentation = presentation;
        _presentationResolved = true;
    }

    /// <summary>
    /// Records that the control's persisted state no longer matches what was saved. A container
    /// asks before it saves, and a control that always says yes makes every close dirty.
    /// </summary>
    public void MarkDirty() => _dirty = true;

    // ---- IPersist / IPersistStreamInit -------------------------------------------------------

    void IVBPersistStreamInit.GetClassID(out Guid classId) => classId = GetClassId();

    int IVBPersistStreamInit.IsDirty() => _dirty ? SOk : SFalse;

    /// <summary>
    /// Restores the control from the block it wrote earlier.
    ///
    /// The bytes are this control's own -- the container keeps them and hands them back unchanged,
    /// which is the whole point of the stream contract. What travels inside is the property bag,
    /// because that is what VB6 persists: <c>UserControl_WriteProperties</c> fills a bag and
    /// <c>UserControl_ReadProperties</c> reads it back, and a control that was saved gets
    /// <c>ReadProperties</c> while a new one gets <c>InitProperties</c>.
    /// </summary>
    void IVBPersistStreamInit.Load(IStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _propertyBag = VBPropertyBagSerializer.Read(ReadAll(stream));
        _initialised = true;
        _dirty = false;
        if (_propertyBag.IsEmpty)
        {
            InvokeLifecycle("UserControl_InitProperties");
            return;
        }

        InvokeLifecycle("UserControl_ReadProperties", _propertyBag);
    }

    void IVBPersistStreamInit.Save(IStream stream, bool clearDirty)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bag = new VBPropertyBag();
        InvokeLifecycle("UserControl_WriteProperties", bag);
        _propertyBag = bag;

        var block = VBPropertyBagSerializer.Write(bag);
        stream.Write(block, block.Length, IntPtr.Zero);
        if (clearDirty)
        {
            _dirty = false;
        }
    }

    /// <summary>
    /// Every measured stock control answers this with <c>E_NOTIMPL</c>, and so does this one. The
    /// honest reason is the same: the size is only known once the control has written, and a
    /// container that sized a buffer from a guess would truncate the state of the one control that
    /// guessed wrong.
    /// </summary>
    void IVBPersistStreamInit.GetSizeMax(out long size)
    {
        size = 0;
        throw new COMException(nameof(IVBPersistStreamInit.GetSizeMax), ENotImpl);
    }

    void IVBPersistStreamInit.InitNew()
    {
        _propertyBag = new VBPropertyBag();
        _initialised = true;
        _dirty = false;
        InvokeLifecycle("UserControl_InitProperties");
    }

    // ---- IOleObject --------------------------------------------------------------------------

    /// <summary>
    /// Takes the container's site.
    ///
    /// The reference is kept as a raw pointer with a reference of its own: the site outlives
    /// individual calls, and a container is entitled to release its own copy as soon as this
    /// returns. Passing null is how a container detaches, and it must release what was held.
    /// </summary>
    int IVBOleObject.SetClientSite(IntPtr clientSite)
    {
        lock (_sync)
        {
            if (_clientSite != IntPtr.Zero)
            {
                Marshal.Release(_clientSite);
            }

            _clientSite = clientSite;
            if (_clientSite != IntPtr.Zero)
            {
                Marshal.AddRef(_clientSite);
            }
        }

        InvokeLifecycle("UserControl_Initialize");
        return SOk;
    }

    int IVBOleObject.GetClientSite(out IntPtr clientSite)
    {
        lock (_sync)
        {
            clientSite = _clientSite;
            if (clientSite != IntPtr.Zero)
            {
                Marshal.AddRef(clientSite);
            }
        }

        return SOk;
    }

    /// <summary>
    /// The names a container would put in a window title while editing the object in place. A
    /// control is edited in its container, never in a window of its own, so there is nothing to
    /// title -- VB6 controls answer S_OK and keep nothing.
    /// </summary>
    int IVBOleObject.SetHostNames(string containerApplication, string containerObject) => SOk;

    int IVBOleObject.Close(int saveOption)
    {
        // OLECLOSE_SAVEIFDIRTY is 0, and a container that closes a dirty control expects the state
        // to be asked for before the object goes.
        if (saveOption == 0 && _dirty && _clientSite != IntPtr.Zero)
        {
            RequestSaveFromSite();
        }

        Presentation?.Deactivate();
        InvokeLifecycle("UserControl_Terminate");
        lock (_sync)
        {
            if (_clientSite != IntPtr.Zero)
            {
                Marshal.Release(_clientSite);
                _clientSite = IntPtr.Zero;
            }
        }

        return SOk;
    }

    // A control is not a linkable object: it has no moniker, cannot be the source of a link, and
    // cannot be created from transferred data. Saying E_NOTIMPL is the contract's own answer for
    // that, and it is what a container tests for before offering paste-link.
    int IVBOleObject.SetMoniker(int whichMoniker, IntPtr moniker) => ENotImpl;

    int IVBOleObject.GetMoniker(int assign, int whichMoniker, out IntPtr moniker)
    {
        moniker = IntPtr.Zero;
        return ENotImpl;
    }

    int IVBOleObject.InitFromData(IntPtr dataObject, bool creation, int reserved) => ENotImpl;

    int IVBOleObject.GetClipboardData(int reserved, out IntPtr dataObject)
    {
        dataObject = IntPtr.Zero;
        return ENotImpl;
    }

    /// <summary>
    /// Runs a verb. For a control the only ones that mean anything are show, hide and the two
    /// activations, and the primary verb of a control is to activate it in place.
    ///
    /// Without a presentation there is no window to activate, and the answer is
    /// <c>OLEOBJ_S_CANNOT_DOVERB_NOW</c> -- a success code that says the verb is real but not
    /// possible right now. Returning plain S_OK would tell the container it had an activated
    /// control and let it lay out around a window that does not exist.
    /// </summary>
    int IVBOleObject.DoVerb(
        int verb,
        IntPtr message,
        IntPtr activeSite,
        int index,
        IntPtr parentWindow,
        IntPtr positionRectangle)
    {
        const int VerbPrimary = 0;
        const int VerbShow = -1;
        const int VerbHide = -3;
        const int VerbUiActivate = -4;
        const int VerbInPlaceActivate = -5;

        if (Presentation is not { } presentation)
        {
            return OleObjSCannotDoVerbNow;
        }

        switch (verb)
        {
            case VerbHide:
                presentation.Hide();
                return SOk;
            case VerbPrimary:
            case VerbShow:
            case VerbUiActivate:
            case VerbInPlaceActivate:
                return presentation.Activate(parentWindow, positionRectangle, uiActive: verb != VerbInPlaceActivate)
                    ? SOk
                    : OleObjSCannotDoVerbNow;
            default:
                return ENotImpl;
        }
    }

    int IVBOleObject.EnumVerbs(out IntPtr enumerator)
    {
        // OLEOBJ_E_NOVERBS would be a lie -- the verbs above exist. A container that cannot
        // enumerate them falls back to the registry's verb list, which is where a control's verbs
        // belong anyway.
        enumerator = IntPtr.Zero;
        return ENotImpl;
    }

    int IVBOleObject.Update() => SOk;

    int IVBOleObject.IsUpToDate() => SOk;

    int IVBOleObject.GetUserClassID(out Guid classId)
    {
        classId = GetClassId();
        return SOk;
    }

    /// <summary>
    /// The name a container shows for this control. It is the VB6 class name, which is also the
    /// name in the type library and in the ProgID -- three places that must agree, or an object
    /// browser lists one control under two names.
    /// </summary>
    int IVBOleObject.GetUserType(int formOfType, out IntPtr userType)
    {
        userType = Marshal.StringToCoTaskMemUni(GetControlName());
        return SOk;
    }

    int IVBOleObject.SetExtent(int drawAspect, ref VBOleSize size)
    {
        const int DvAspectContent = 1;
        if (drawAspect != DvAspectContent)
        {
            return EInvalidArg;
        }

        if (size.Width <= 0 || size.Height <= 0)
        {
            return EInvalidArg;
        }

        DesignExtent = size;
        Presentation?.Resize(size);
        InvokeLifecycle("UserControl_Resize");
        return SOk;
    }

    int IVBOleObject.GetExtent(int drawAspect, ref VBOleSize size)
    {
        const int DvAspectContent = 1;
        if (drawAspect != DvAspectContent)
        {
            return EInvalidArg;
        }

        var extent = DesignExtent;
        if (extent.Width <= 0 || extent.Height <= 0)
        {
            return EFail;
        }

        size = extent;
        return SOk;
    }

    /// <summary>
    /// Registers a sink for the OLE advisory notifications. The list is kept even though this
    /// control raises none of them yet: a container that cannot register is a container that
    /// stops asking, and the connection number it gets back has to stay valid for its Unadvise.
    /// </summary>
    int IVBOleObject.Advise(IntPtr adviseSink, out int connection)
    {
        connection = 0;
        if (adviseSink == IntPtr.Zero)
        {
            return EInvalidArg;
        }

        lock (_sync)
        {
            connection = _nextAdviseConnection++;
            Marshal.AddRef(adviseSink);
            _adviseSinks[connection] = adviseSink;
        }

        return SOk;
    }

    int IVBOleObject.Unadvise(int connection)
    {
        lock (_sync)
        {
            if (!_adviseSinks.Remove(connection, out var sink))
            {
                return EInvalidArg;
            }

            Marshal.Release(sink);
        }

        return SOk;
    }

    int IVBOleObject.EnumAdvise(out IntPtr enumerator)
    {
        enumerator = IntPtr.Zero;
        return ENotImpl;
    }

    int IVBOleObject.GetMiscStatus(int aspect, out int status)
    {
        status = ControlMiscStatus;
        return SOk;
    }

    int IVBOleObject.SetColorScheme(IntPtr palette) => ENotImpl;

    // ---- IOleControl -------------------------------------------------------------------------

    /// <summary>
    /// The accelerator table this control wants merged. A generated UserControl declares no
    /// mnemonics of its own, so the answer is an empty table rather than a refusal -- a container
    /// that gets E_NOTIMPL here stops asking the control about keyboard handling entirely.
    /// </summary>
    int IVBOleControl.GetControlInfo(ref VBControlInfo controlInfo)
    {
        controlInfo.Size = Marshal.SizeOf<VBControlInfo>();
        controlInfo.Accelerators = IntPtr.Zero;
        controlInfo.AcceleratorCount = 0;
        controlInfo.Flags = 0;
        return SOk;
    }

    int IVBOleControl.OnMnemonic(IntPtr message) => ENotImpl;

    /// <summary>
    /// The container changed one of its ambient properties. VB6 turns this into
    /// <c>UserControl_AmbientChanged(PropertyName)</c>, by name -- so the DISPID has to be
    /// translated back into the name the VB6 programmer wrote.
    ///
    /// DISPID_UNKNOWN means "all of them", and VB6 raises the event once per property in that
    /// case. Raising it once with an empty name would be simpler and would break every handler
    /// that switches on the name.
    /// </summary>
    int IVBOleControl.OnAmbientPropertyChange(int dispId)
    {
        const int DispIdUnknown = -1;
        if (dispId == DispIdUnknown)
        {
            foreach (var name in AmbientNames.Values)
            {
                InvokeLifecycle("UserControl_AmbientChanged", name);
            }

            return SOk;
        }

        if (AmbientNames.TryGetValue(dispId, out var propertyName))
        {
            InvokeLifecycle("UserControl_AmbientChanged", propertyName);
        }

        return SOk;
    }

    /// <summary>
    /// A container freezes events while it is doing something a control's event handler must not
    /// interrupt -- loading a form, for one. The calls nest, so this counts rather than flags.
    /// </summary>
    int IVBOleControl.FreezeEvents(bool freeze)
    {
        lock (_sync)
        {
            _frozenEvents = freeze ? _frozenEvents + 1 : Math.Max(0, _frozenEvents - 1);
        }

        return SOk;
    }

    /// <summary>True while the container has asked for events to be held back.</summary>
    protected bool EventsAreFrozen => Volatile.Read(ref _frozenEvents) > 0;

    // ---- IOleWindow / in-place -----------------------------------------------------------------

    int IVBOleWindow.GetWindow(out IntPtr window) => GetWindowCore(out window);

    int IVBOleWindow.ContextSensitiveHelp(bool enterMode) => ENotImpl;

    int IVBOleInPlaceObject.GetWindow(out IntPtr window) => GetWindowCore(out window);

    int IVBOleInPlaceObject.ContextSensitiveHelp(bool enterMode) => ENotImpl;

    int IVBOleInPlaceObject.InPlaceDeactivate()
    {
        Presentation?.Deactivate();
        return SOk;
    }

    int IVBOleInPlaceObject.UIDeactivate()
    {
        Presentation?.DeactivateUi();
        return SOk;
    }

    int IVBOleInPlaceObject.SetObjectRects(IntPtr positionRectangle, IntPtr clipRectangle)
    {
        if (Presentation is not { } presentation)
        {
            return OleENotRunning;
        }

        presentation.SetObjectRects(positionRectangle, clipRectangle);
        InvokeLifecycle("UserControl_Resize");
        return SOk;
    }

    int IVBOleInPlaceObject.ReactivateAndUndo() => ENotImpl;

    int IVBOleInPlaceActiveObject.GetWindow(out IntPtr window) => GetWindowCore(out window);

    int IVBOleInPlaceActiveObject.ContextSensitiveHelp(bool enterMode) => ENotImpl;

    /// <summary>
    /// S_FALSE, not S_OK: the container asks whether the control consumed the key, and a control
    /// that claims every keystroke swallows the container's own accelerators.
    /// </summary>
    int IVBOleInPlaceActiveObject.TranslateAccelerator(IntPtr message) => SFalse;

    int IVBOleInPlaceActiveObject.OnFrameWindowActivate(bool activate) => SOk;

    int IVBOleInPlaceActiveObject.OnDocWindowActivate(bool activate) => SOk;

    int IVBOleInPlaceActiveObject.ResizeBorder(IntPtr border, IntPtr uiWindow, bool frameWindow) => SOk;

    int IVBOleInPlaceActiveObject.EnableModeless(bool enable) => SOk;

    // ---- IViewObject2 ------------------------------------------------------------------------

    /// <summary>
    /// Draws the control into a device context the container owns.
    ///
    /// This is the member that decides whether a control can be *placed* rather than only run: a
    /// design surface, a print preview and a thumbnail all draw a control they never activate.
    /// Without a presentation there is nothing to draw, and OLE_E_BLANK is the contract's word
    /// for exactly that.
    /// </summary>
    int IVBViewObject2.Draw(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        IntPtr drawDevice,
        IntPtr bounds,
        IntPtr windowBounds,
        IntPtr continueFunction,
        IntPtr continueParameter)
    {
        const int DvAspectContent = 1;
        if (drawAspect != DvAspectContent)
        {
            return EInvalidArg;
        }

        if (Presentation is not { } presentation)
        {
            return OleEBlank;
        }

        return presentation.Draw(drawDevice, bounds) ? SOk : EFail;
    }

    int IVBViewObject2.GetColorSet(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        out IntPtr colorSet)
    {
        colorSet = IntPtr.Zero;
        return SFalse;
    }

    int IVBViewObject2.Freeze(int drawAspect, int index, IntPtr aspect, out int freeze)
    {
        freeze = 0;
        return ENotImpl;
    }

    int IVBViewObject2.Unfreeze(int freeze) => ENotImpl;

    int IVBViewObject2.SetAdvise(int aspects, int advise, IntPtr adviseSink) => ENotImpl;

    int IVBViewObject2.GetAdvise(IntPtr aspects, IntPtr advise, out IntPtr adviseSink)
    {
        adviseSink = IntPtr.Zero;
        return ENotImpl;
    }

    int IVBViewObject2.GetExtent(int drawAspect, int index, IntPtr targetDevice, ref VBOleSize size) =>
        ((IVBOleObject)this).GetExtent(drawAspect, ref size);

    // ---- IViewObject -------------------------------------------------------------------------
    //
    // The same six members again, under the older interface id. A container that knows only
    // IViewObject asks for IViewObject and does not fall back to the newer one.

    int IVBViewObject.Draw(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        IntPtr drawDevice,
        IntPtr bounds,
        IntPtr windowBounds,
        IntPtr continueFunction,
        IntPtr continueParameter) =>
        ((IVBViewObject2)this).Draw(
            drawAspect, index, aspect, targetDevice, informationDevice,
            drawDevice, bounds, windowBounds, continueFunction, continueParameter);

    int IVBViewObject.GetColorSet(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        out IntPtr colorSet) =>
        ((IVBViewObject2)this).GetColorSet(
            drawAspect, index, aspect, targetDevice, informationDevice, out colorSet);

    int IVBViewObject.Freeze(int drawAspect, int index, IntPtr aspect, out int freeze) =>
        ((IVBViewObject2)this).Freeze(drawAspect, index, aspect, out freeze);

    int IVBViewObject.Unfreeze(int freeze) => ((IVBViewObject2)this).Unfreeze(freeze);

    int IVBViewObject.SetAdvise(int aspects, int advise, IntPtr adviseSink) =>
        ((IVBViewObject2)this).SetAdvise(aspects, advise, adviseSink);

    int IVBViewObject.GetAdvise(IntPtr aspects, IntPtr advise, out IntPtr adviseSink) =>
        ((IVBViewObject2)this).GetAdvise(aspects, advise, out adviseSink);

    // ---- IPersistPropertyBag -------------------------------------------------------------------

    void IVBPersistPropertyBag.GetClassID(out Guid classId) => classId = GetClassId();

    void IVBPersistPropertyBag.InitNew() => ((IVBPersistStreamInit)this).InitNew();

    /// <summary>
    /// The other way a container persists a control: named values instead of one opaque block.
    ///
    /// A control does not choose between the two -- the container does, and VB6's own designer
    /// chooses this one, which is why a <c>.frx</c> holds names and values rather than bytes. The
    /// control's own handlers are the same either way; only the bag differs, so this reads the
    /// container's bag into ours and runs the identical lifecycle.
    ///
    /// Which names to read is the control's business. There is no way to enumerate the container's
    /// bag -- <c>IPropertyBag</c> has no enumerator at all -- so the values arrive by being asked
    /// for, and a control that asks for nothing gets nothing.
    /// </summary>
    void IVBPersistPropertyBag.Load(IVBPropertyBag propertyBag, IntPtr errorLog)
    {
        ArgumentNullException.ThrowIfNull(propertyBag);
        _propertyBag = new VBPropertyBag(new VBContainerPropertyBagStore(propertyBag, errorLog));
        _initialised = true;
        _dirty = false;
        InvokeLifecycle("UserControl_ReadProperties", _propertyBag);
    }

    void IVBPersistPropertyBag.Save(IVBPropertyBag propertyBag, bool clearDirty, bool saveAllProperties)
    {
        ArgumentNullException.ThrowIfNull(propertyBag);
        var bag = new VBPropertyBag(new VBContainerPropertyBagStore(propertyBag, IntPtr.Zero));
        InvokeLifecycle("UserControl_WriteProperties", bag);
        if (clearDirty)
        {
            _dirty = false;
        }
    }

    // ---- helpers -----------------------------------------------------------------------------

    private int GetWindowCore(out IntPtr window)
    {
        window = Presentation?.WindowHandle ?? IntPtr.Zero;

        // A control with no window is not an error state a container can recover from by retrying,
        // and E_FAIL is what the contract reserves for it. OLE_E_NOTRUNNING would tell the
        // container that running the object first would produce one.
        return window == IntPtr.Zero ? EFail : SOk;
    }

    private void RequestSaveFromSite()
    {
        var site = _clientSite;
        if (site == IntPtr.Zero)
        {
            return;
        }

        var siteId = typeof(IVBOleClientSite).GUID;
        if (Marshal.QueryInterface(site, in siteId, out var clientSite) != 0)
        {
            return;
        }

        try
        {
            if (Marshal.GetObjectForIUnknown(clientSite) is IVBOleClientSite typed)
            {
                typed.SaveObject();
            }
        }
        finally
        {
            Marshal.Release(clientSite);
        }
    }

    /// <summary>
    /// The CLSID the compiler stamped on this class. It is the same GUID the manifest and the
    /// type library carry, and a container that gets a different one here treats the control as a
    /// different class than the one it created.
    /// </summary>
    private Guid GetClassId() => GetType().GUID;

    private string GetControlName()
    {
        // The emitted CLR type name carries the compiler's prefix; the VB6 name is what a container
        // has to show, and it is the one the type library and the ProgID use.
        var name = GetType().Name;
        const string prefix = "__vb6_class_";
        return name.StartsWith(prefix, StringComparison.Ordinal) ? name[prefix.Length..] : name;
    }

    /// <summary>
    /// Calls one of the control's own lifecycle handlers, if it wrote one.
    ///
    /// They are private subs in VB6 and private methods here, so this looks them up on the derived
    /// type by name -- the same contract the WinForms host uses for a control it hosts itself.
    /// Absence is normal: a control that writes no <c>UserControl_Resize</c> simply has none.
    /// </summary>
    private void InvokeLifecycle(string name, params object?[] arguments)
    {
        var method = GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method is null || method.GetParameters().Length != arguments.Length)
        {
            return;
        }

        try
        {
            method.Invoke(this, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(exception.InnerException)
                .Throw();
            throw;
        }
    }

    private static byte[] ReadAll(IStream stream)
    {
        var result = new MemoryStream();
        var buffer = new byte[1024];
        var read = Marshal.AllocCoTaskMem(sizeof(int));
        try
        {
            while (true)
            {
                Marshal.WriteInt32(read, 0);
                stream.Read(buffer, buffer.Length, read);
                var count = Marshal.ReadInt32(read);
                if (count <= 0)
                {
                    break;
                }

                result.Write(buffer, 0, count);
                if (count < buffer.Length)
                {
                    break;
                }
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(read);
        }

        return result.ToArray();
    }

    /// <summary>
    /// The ambient properties a container publishes, by DISPID. The numbers are the published
    /// ones; the names are what a VB6 <c>UserControl_AmbientChanged</c> handler compares against.
    /// </summary>
    private static readonly Dictionary<int, string> AmbientNames = new()
    {
        [-501] = "BackColor",
        [-513] = "ForeColor",
        [-701] = "Font",
        [-702] = "DisplayName",
        [-703] = "Font",
        [-704] = "LocaleID",
        [-705] = "LocaleID",
        [-706] = "MessageReflect",
        [-707] = "ScaleUnits",
        [-708] = "TextAlign",
        [-709] = "UserMode",
        [-710] = "UIDead",
        [-711] = "ShowGrabHandles",
        [-712] = "ShowHatching",
        [-713] = "DisplayAsDefault",
        [-714] = "SupportsMnemonics",
        [-715] = "AutoClip",
        [-716] = "RightToLeft"
    };

    /// <summary>True once the control has been given a state or told it has none.</summary>
    protected bool IsInitialised => _initialised;
}

/// <summary>
/// A property bag that lives in the container.
///
/// <c>IPropertyBag.Read</c> is where the asymmetry of this contract shows: a name the container
/// does not have is <em>not</em> an error the control should propagate. VB6 controls pass a default
/// to <c>ReadProperty</c> for exactly that case, and a container that has never saved this control
/// answers every read that way.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class VBContainerPropertyBagStore : IVBPropertyBagStore
{
    private readonly IVBPropertyBag _bag;
    private readonly IntPtr _errorLog;

    public VBContainerPropertyBagStore(IVBPropertyBag bag, IntPtr errorLog)
    {
        _bag = bag;
        _errorLog = errorLog;
    }

    public bool TryRead(string name, out object? value)
    {
        value = null;
        return _bag.Read(name, ref value, _errorLog) == 0;
    }

    public void Write(string name, object? value) => _bag.Write(name, ref value);
}

/// <summary>
/// The part of a control that needs a screen. A host implements it; without one the control is
/// still a complete Automation and persistence object, which is what a headless process needs and
/// what the test suite can measure.
/// </summary>
[SupportedOSPlatform("windows")]
public interface IVBControlPresentation
{
    /// <summary>The control's window, or <see cref="IntPtr.Zero"/> when it has none yet.</summary>
    IntPtr WindowHandle { get; }

    /// <summary>Activates the control in the container's window.</summary>
    bool Activate(IntPtr parentWindow, IntPtr positionRectangle, bool uiActive);

    void Hide();

    void Deactivate();

    void DeactivateUi();

    void SetObjectRects(IntPtr positionRectangle, IntPtr clipRectangle);

    void Resize(VBOleSize extent);

    /// <summary>Draws the control into a device context the container owns.</summary>
    bool Draw(IntPtr deviceContext, IntPtr bounds);
}

/// <summary>
/// The property bag as bytes.
///
/// The format is this compiler's own, and it may be: the container keeps the block and hands it
/// back unchanged without ever looking inside. What it must do is survive a round trip with the
/// value's *type* intact -- a bag that returns 42 as a string turns a control's
/// <c>ReadProperty</c> into a conversion, and VB6 code that switches on the type then takes the
/// wrong branch.
/// </summary>
internal static class VBPropertyBagSerializer
{
    private const int Magic = 0x42503656;   // 'V6PB'
    private const int Version = 1;

    private enum Tag : byte
    {
        Null = 0,
        Boolean = 1,
        Byte = 2,
        Short = 3,
        Integer = 4,
        Long = 5,
        Single = 6,
        Double = 7,
        Decimal = 8,
        Currency = 9,
        Date = 10,
        String = 11,
        Bytes = 12
    }

    public static byte[] Write(VBPropertyBag bag)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var stream = new MemoryStream();
        var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(Magic);
        writer.Write(Version);
        var entries = bag.Snapshot();
        writer.Write(entries.Count);
        foreach (var (name, value) in entries)
        {
            writer.Write(name);
            WriteValue(writer, value);
        }

        writer.Flush();
        return stream.ToArray();
    }

    public static VBPropertyBag Read(byte[] block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var bag = new VBPropertyBag();
        if (block.Length == 0)
        {
            return bag;
        }

        var reader = new BinaryReader(new MemoryStream(block), Encoding.UTF8);
        if (reader.BaseStream.Length < sizeof(int) * 3 ||
            reader.ReadInt32() != Magic ||
            reader.ReadInt32() != Version)
        {
            // A block this control did not write is not a state. Saying so beats restoring half a
            // control and letting the difference show up as a wrong value much later.
            throw new COMException(
                "The persisted block was not written by this control.",
                unchecked((int)0x800401F0));
        }

        var count = reader.ReadInt32();
        for (var index = 0; index < count; index++)
        {
            var name = reader.ReadString();
            bag.WriteProperty(name, ReadValue(reader));
        }

        return bag;
    }

    private static void WriteValue(BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.Write((byte)Tag.Null);
                return;
            case bool boolean:
                writer.Write((byte)Tag.Boolean);
                writer.Write(boolean);
                return;
            case byte number:
                writer.Write((byte)Tag.Byte);
                writer.Write(number);
                return;
            case short number:
                writer.Write((byte)Tag.Short);
                writer.Write(number);
                return;
            case int number:
                writer.Write((byte)Tag.Integer);
                writer.Write(number);
                return;
            case long number:
                writer.Write((byte)Tag.Long);
                writer.Write(number);
                return;
            case float number:
                writer.Write((byte)Tag.Single);
                writer.Write(number);
                return;
            case double number:
                writer.Write((byte)Tag.Double);
                writer.Write(number);
                return;
            case decimal number:
                writer.Write((byte)Tag.Decimal);
                writer.Write(number);
                return;
            case VBCurrency currency:
                writer.Write((byte)Tag.Currency);
                writer.Write(currency.ScaledValue);
                return;
            case DateTime moment:
                writer.Write((byte)Tag.Date);
                writer.Write(moment.ToBinary());
                return;
            case string text:
                writer.Write((byte)Tag.String);
                writer.Write(text);
                return;
            case byte[] bytes:
                writer.Write((byte)Tag.Bytes);
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return;
            default:
                // Converting an unknown value to its string form would round-trip a *different*
                // value, and the control would read back a String where it wrote an object.
                throw new COMException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "A property of type '{0}' cannot be persisted in a property bag.",
                        value.GetType().Name),
                    unchecked((int)0x80020005));
        }
    }

    private static object? ReadValue(BinaryReader reader) => (Tag)reader.ReadByte() switch
    {
        Tag.Null => null,
        Tag.Boolean => reader.ReadBoolean(),
        Tag.Byte => reader.ReadByte(),
        Tag.Short => reader.ReadInt16(),
        Tag.Integer => reader.ReadInt32(),
        Tag.Long => reader.ReadInt64(),
        Tag.Single => reader.ReadSingle(),
        Tag.Double => reader.ReadDouble(),
        Tag.Decimal => reader.ReadDecimal(),
        Tag.Currency => VBCurrency.FromScaled(reader.ReadInt64()),
        Tag.Date => DateTime.FromBinary(reader.ReadInt64()),
        Tag.String => reader.ReadString(),
        Tag.Bytes => reader.ReadBytes(reader.ReadInt32()),
        var tag => throw new COMException(
            string.Format(CultureInfo.InvariantCulture, "Unknown property tag {0}.", (byte)tag),
            unchecked((int)0x800401F0))
    };
}
