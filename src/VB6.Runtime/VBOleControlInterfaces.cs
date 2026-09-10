using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// The interfaces an OLE control container asks a control for.
///
/// These are declarations of a published contract, not a design of ours: the order of the methods
/// *is* the vtable, and a container reads it by position. A member out of order does not fail --
/// it calls the wrong function with the wrong arguments. Every method therefore keeps its
/// published place, including the ones this implementation answers with <c>E_NOTIMPL</c>, and a
/// derived interface **re-declares** everything its base has, because COM knows nothing about
/// interface inheritance in the CLR sense.
///
/// They are declared as ordinary managed interfaces on purpose. Measured: the CLR hands out a
/// managed interface carrying an OLE IID from its own CCW, and the slots line up -- unlike
/// <c>IID_IDispatch</c>, which it refuses and which needs the hand-built vtable in
/// <see cref="VBComDispatchSurface"/>. Doing it by hand here would be more code with a worse
/// failure mode.
///
/// Pointer arguments stay <see cref="IntPtr"/> wherever the contents belong to the caller. A
/// marshalled structure would be a copy, and for the ones a container reads back that copy is
/// silently discarded.
/// </summary>
[ComVisible(true)]
[Guid("00000112-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleObject
{
    [PreserveSig] int SetClientSite(IntPtr clientSite);

    [PreserveSig] int GetClientSite(out IntPtr clientSite);

    [PreserveSig] int SetHostNames(
        [MarshalAs(UnmanagedType.LPWStr)] string containerApplication,
        [MarshalAs(UnmanagedType.LPWStr)] string containerObject);

    [PreserveSig] int Close(int saveOption);

    [PreserveSig] int SetMoniker(int whichMoniker, IntPtr moniker);

    [PreserveSig] int GetMoniker(int assign, int whichMoniker, out IntPtr moniker);

    [PreserveSig] int InitFromData(IntPtr dataObject, [MarshalAs(UnmanagedType.Bool)] bool creation, int reserved);

    [PreserveSig] int GetClipboardData(int reserved, out IntPtr dataObject);

    [PreserveSig] int DoVerb(
        int verb,
        IntPtr message,
        IntPtr activeSite,
        int index,
        IntPtr parentWindow,
        IntPtr positionRectangle);

    [PreserveSig] int EnumVerbs(out IntPtr enumerator);

    [PreserveSig] int Update();

    [PreserveSig] int IsUpToDate();

    [PreserveSig] int GetUserClassID(out Guid classId);

    [PreserveSig] int GetUserType(int formOfType, out IntPtr userType);

    [PreserveSig] int SetExtent(int drawAspect, ref VBOleSize size);

    [PreserveSig] int GetExtent(int drawAspect, ref VBOleSize size);

    [PreserveSig] int Advise(IntPtr adviseSink, out int connection);

    [PreserveSig] int Unadvise(int connection);

    [PreserveSig] int EnumAdvise(out IntPtr enumerator);

    [PreserveSig] int GetMiscStatus(int aspect, out int status);

    [PreserveSig] int SetColorScheme(IntPtr palette);
}

/// <summary>
/// The control half of the contract: keyboard mnemonics, frozen events and the notification that
/// an ambient property of the container changed.
/// </summary>
[ComVisible(true)]
[Guid("B196B288-BAB4-101A-B69C-00AA00341D07")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleControl
{
    [PreserveSig] int GetControlInfo(ref VBControlInfo controlInfo);

    [PreserveSig] int OnMnemonic(IntPtr message);

    [PreserveSig] int OnAmbientPropertyChange(int dispId);

    [PreserveSig] int FreezeEvents([MarshalAs(UnmanagedType.Bool)] bool freeze);
}

/// <summary>The window an in-place object lives in.</summary>
[ComVisible(true)]
[Guid("00000114-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleWindow
{
    [PreserveSig] int GetWindow(out IntPtr window);

    [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);
}

/// <summary>
/// In-place activation. The first two members are <c>IOleWindow</c>'s and are re-declared here
/// because the vtable carries them; C# interface inheritance would not put them in this vtable.
/// </summary>
[ComVisible(true)]
[Guid("00000113-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleInPlaceObject
{
    [PreserveSig] int GetWindow(out IntPtr window);

    [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);

    [PreserveSig] int InPlaceDeactivate();

    [PreserveSig] int UIDeactivate();

    [PreserveSig] int SetObjectRects(IntPtr positionRectangle, IntPtr clipRectangle);

    [PreserveSig] int ReactivateAndUndo();
}

/// <summary>
/// The active in-place object, as the frame sees it. <c>IOleWindow</c> again holds the first two
/// slots.
/// </summary>
[ComVisible(true)]
[Guid("00000117-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleInPlaceActiveObject
{
    [PreserveSig] int GetWindow(out IntPtr window);

    [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enterMode);

    [PreserveSig] int TranslateAccelerator(IntPtr message);

    [PreserveSig] int OnFrameWindowActivate([MarshalAs(UnmanagedType.Bool)] bool activate);

    [PreserveSig] int OnDocWindowActivate([MarshalAs(UnmanagedType.Bool)] bool activate);

    [PreserveSig] int ResizeBorder(IntPtr border, IntPtr uiWindow, [MarshalAs(UnmanagedType.Bool)] bool frameWindow);

    [PreserveSig] int EnableModeless([MarshalAs(UnmanagedType.Bool)] bool enable);
}

/// <summary>
/// Drawing. <c>IViewObject2</c> adds a single member to <c>IViewObject</c>, so the first six slots
/// are re-declared in their published order.
///
/// A container that only needs a picture of the control -- a design surface, a printout, a
/// thumbnail -- uses this instead of activating it. That is the difference between a control that
/// can be placed in a foreign designer and one that can only run.
/// </summary>
[ComVisible(true)]
[Guid("00000127-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBViewObject2
{
    [PreserveSig] int Draw(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        IntPtr drawDevice,
        IntPtr bounds,
        IntPtr windowBounds,
        IntPtr continueFunction,
        IntPtr continueParameter);

    [PreserveSig] int GetColorSet(
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        out IntPtr colorSet);

    [PreserveSig] int Freeze(int drawAspect, int index, IntPtr aspect, out int freeze);

    [PreserveSig] int Unfreeze(int freeze);

    [PreserveSig] int SetAdvise(int aspects, int advise, IntPtr adviseSink);

    [PreserveSig] int GetAdvise(IntPtr aspects, IntPtr advise, out IntPtr adviseSink);

    [PreserveSig] int GetExtent(int drawAspect, int index, IntPtr targetDevice, ref VBOleSize size);
}

/// <summary>
/// The container side this control talks back to. It is declared here because a control that
/// cannot ask its site anything has no ambient properties, cannot tell the container it changed,
/// and cannot ask to be activated.
/// </summary>
[ComVisible(true)]
[Guid("00000118-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBOleClientSite
{
    [PreserveSig] int SaveObject();

    [PreserveSig] int GetMoniker(int assign, int whichMoniker, out IntPtr moniker);

    [PreserveSig] int GetContainer(out IntPtr container);

    [PreserveSig] int ShowObject();

    [PreserveSig] int OnShowWindow([MarshalAs(UnmanagedType.Bool)] bool show);

    [PreserveSig] int RequestNewObjectLayout();
}

/// <summary><c>SIZEL</c>: an extent in HIMETRIC units, which is what OLE measures a control in.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct VBOleSize
{
    public int Width;
    public int Height;
}

/// <summary><c>CONTROLINFO</c>: the accelerator table a container merges for this control.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct VBControlInfo
{
    public int Size;
    public IntPtr Accelerators;
    public short AcceleratorCount;
    public int Flags;
}
