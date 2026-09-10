using System.Globalization;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;

namespace VB6.OleContainerProbe;

/// <summary>
/// An OLE control container. Not a client -- a container.
///
/// The difference is the point of this process. Every other probe in this repo activates a class
/// and calls it; a container owns a window, hands the control a client site, activates it in place
/// inside that window, draws it into a device context of its own and receives its events. Those
/// are the four things a control can only be measured for from here.
///
/// It has no reference to the compiler's runtime on purpose: a shared declaration would be wrong
/// identically on both sides, and every call below therefore goes through the raw vtable at its
/// published slot.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var log = new List<string>();
        try
        {
            switch (args)
            {
                case ["--presentation", var manifest, var clsid, var sourceIid, var dispId]:
                    RunPresentation(manifest, Guid.Parse(clsid), Guid.Parse(sourceIid), int.Parse(dispId), log);
                    break;
                case ["--surface", var manifest, var clsid]:
                    DescribeSurface(manifest, Guid.Parse(clsid), log);
                    break;
                case ["--roundtrip", var manifest, var clsid, var caption]:
                    RunStateRoundTrip(manifest, Guid.Parse(clsid), caption, log);
                    break;
                default:
                    Console.Error.WriteLine(
                        "Usage: VB6.OleContainerProbe --presentation <manifest> <clsid> <source-iid> <bump-dispid> | " +
                        "--surface <manifest> <clsid> | --roundtrip <manifest> <clsid> <caption>");
                    return 2;
            }
        }
        catch (Exception exception)
        {
            log.Add($"exception={exception.GetType().Name}: {exception.Message}");
        }

        foreach (var line in log)
        {
            Console.Out.WriteLine(line);
        }

        Console.Out.Flush();
        return 0;
    }

    /// <summary>
    /// What a container asks a control for, asked in the container's own order.
    ///
    /// An ActiveX control is not one interface but a set, and a container decides what it may do
    /// with a control by asking for them one at a time. The list comes from the published OLE
    /// Controls contract, not from what this compiler emits -- an inventory built from the existing
    /// output would only ever confirm itself.
    /// </summary>
    private static void DescribeSurface(string manifestPath, Guid classId, List<string> log) =>
        WithActivation(manifestPath, classId, log, control =>
        {
            foreach (var (name, iid) in ControlSurface)
            {
                var id = Guid.Parse(iid);
                var hresult = Marshal.QueryInterface(control, in id, out var candidate);
                if (hresult == 0)
                {
                    Marshal.Release(candidate);
                }

                log.Add($"{name}={(hresult == 0 ? "ja" : $"0x{hresult:X8}")}");
            }
        });

    /// <summary>
    /// Create, describe, give a state, take it back, hand it to a second instance and read the
    /// value out again -- through the raw vtable at each published slot.
    /// </summary>
    private static void RunStateRoundTrip(string manifestPath, Guid classId, string caption, List<string> log) =>
        WithActivation(manifestPath, classId, log, source =>
        {
            WithInterface(source, OleObjectIid, log, oleObject =>
            {
                var miscStatus = Slot<GetMiscStatusDelegate>(oleObject, SlotGetMiscStatus);
                log.Add(miscStatus(oleObject, DvAspectContent, out var status) == 0
                    ? $"miscstatus=0x{status:X}"
                    : "miscstatus=fehlgeschlagen");

                var extent = Slot<GetExtentDelegate>(oleObject, SlotGetExtent);
                var size = default(ProbeSize);
                log.Add(extent(oleObject, DvAspectContent, ref size) == 0
                    ? $"extent={size.Width}x{size.Height}"
                    : "extent=fehlgeschlagen");

                var userType = Slot<GetUserTypeDelegate>(oleObject, SlotGetUserType);
                if (userType(oleObject, 1, out var text) == 0)
                {
                    log.Add($"usertype={Marshal.PtrToStringUni(text)}");
                    Marshal.FreeCoTaskMem(text);
                }

                var identity = Slot<GetUserClassIdDelegate>(oleObject, SlotGetUserClassId);
                if (identity(oleObject, out var identifier) == 0)
                {
                    log.Add($"userclsid={identifier.ToString("B").ToUpperInvariant()}");
                }

                var setSite = Slot<SetClientSiteDelegate>(oleObject, SlotSetClientSite);
                log.Add($"setclientsite=0x{setSite(oleObject, IntPtr.Zero):X8}");
            });

            WithInterface(source, PersistStreamInitIid, log, persist =>
            {
                var initNew = Slot<NoArgumentDelegate>(persist, SlotInitNew);
                log.Add($"initnew=0x{initNew(persist):X8}");
            });

            var dispatch = Marshal.GetObjectForIUnknown(source);
            dispatch.GetType().InvokeMember(
                "Caption", BindingFlags.SetProperty, null, dispatch, [caption],
                CultureInfo.InvariantCulture);

            if (CreateStreamOnHGlobal(IntPtr.Zero, deleteOnRelease: true, out var stream) != 0)
            {
                log.Add("createstream=fehlgeschlagen");
                return;
            }

            try
            {
                WithInterface(source, PersistStreamInitIid, log, persist =>
                {
                    var save = Slot<SaveDelegate>(persist, SlotSave);
                    log.Add($"save=0x{save(persist, stream, 1):X8}");
                });

                ((IStream)Marshal.GetObjectForIUnknown(stream)).Seek(0, 0, IntPtr.Zero);

                var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
                var activation = CoCreateInstance(
                    ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownIid, out var target);
                if (activation != 0)
                {
                    log.Add($"cocreate2=0x{activation:X8}");
                    return;
                }

                try
                {
                    WithInterface(target, PersistStreamInitIid, log, persist =>
                    {
                        var load = Slot<LoadDelegate>(persist, SlotLoad);
                        log.Add($"load=0x{load(persist, stream):X8}");
                    });

                    var restored = Marshal.GetObjectForIUnknown(target);
                    var value = restored.GetType().InvokeMember(
                        "Caption", BindingFlags.GetProperty, null, restored, null,
                        CultureInfo.InvariantCulture);
                    log.Add($"caption={value}");

                    WithInterface(target, OleObjectIid, log, oleObject =>
                    {
                        var close = Slot<CloseDelegate>(oleObject, SlotClose);
                        log.Add($"close=0x{close(oleObject, 1):X8}");
                    });
                }
                finally
                {
                    log.Add($"release={Marshal.Release(target)}");
                }
            }
            finally
            {
                Marshal.Release(stream);
            }
        });

    /// <summary>
    /// The interfaces an OLE control container asks for, taken from the published contract.
    /// </summary>
    private static readonly (string Name, string Iid)[] ControlSurface =
    [
        ("IDispatch", "00020400-0000-0000-C000-000000000046"),
        ("IProvideClassInfo", "B196B283-BAB4-101A-B69C-00AA00341D07"),
        ("IProvideClassInfo2", "A6BC3AC0-DBAA-11CE-9DE3-00AA004BB851"),
        ("IConnectionPointContainer", "B196B284-BAB4-101A-B69C-00AA00341D07"),
        ("IPersistStreamInit", "7FD52380-4E07-101B-AE2D-08002B2EC713"),
        ("IPersistStream", "00000109-0000-0000-C000-000000000046"),
        ("IPersistPropertyBag", "37D84F60-42CB-11CE-8135-00AA004BB851"),
        ("IOleObject", "00000112-0000-0000-C000-000000000046"),
        ("IOleControl", "B196B288-BAB4-101A-B69C-00AA00341D07"),
        ("IOleWindow", "00000114-0000-0000-C000-000000000046"),
        ("IOleInPlaceObject", "00000113-0000-0000-C000-000000000046"),
        ("IOleInPlaceActiveObject", "00000117-0000-0000-C000-000000000046"),
        ("IViewObject", "0000010D-0000-0000-C000-000000000046"),
        ("IViewObject2", "00000127-0000-0000-C000-000000000046"),
        ("IDataObject", "0000010E-0000-0000-C000-000000000046"),
        ("ISpecifyPropertyPages", "B196B28B-BAB4-101A-B69C-00AA00341D07"),
        ("IQuickActivate", "B196B28A-BAB4-101A-B69C-00AA00341D07"),
        ("IPerPropertyBrowsing", "376BD3AA-3845-101B-84ED-08002B2EC713"),
        ("IObjectSafety", "CB5BDC81-93C1-11CF-8F20-00805F2CD064")
    ];

    /// <summary>
    /// Reg-free activation out of the manifest, with the class released again afterwards. The
    /// modes that need no window share it.
    /// </summary>
    private static void WithActivation(string manifestPath, Guid classId, List<string> log, Action<IntPtr> action)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            log.Add($"coinit=0x{initialization:X8}");
            return;
        }

        var context = new ActCtx { cbSize = Marshal.SizeOf<ActCtx>(), lpSource = manifestPath };
        var contextHandle = CreateActCtx(ref context);
        if (contextHandle == new IntPtr(-1))
        {
            log.Add($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return;
        }

        if (!ActivateActCtx(contextHandle, out var cookie))
        {
            log.Add($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return;
        }

        try
        {
            var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
            var activation = CoCreateInstance(
                ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownIid, out var control);
            log.Add($"cocreate=0x{activation:X8}");
            if (activation != 0)
            {
                return;
            }

            try
            {
                action(control);
            }
            finally
            {
                Marshal.Release(control);
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(contextHandle);
        }
    }

    private static void RunPresentation(string manifestPath, Guid classId, Guid sourceIid, int bumpDispId, List<string> log)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            log.Add($"coinit=0x{initialization:X8}");
            return;
        }

        var context = new ActCtx { cbSize = Marshal.SizeOf<ActCtx>(), lpSource = manifestPath };
        var contextHandle = CreateActCtx(ref context);
        if (contextHandle == new IntPtr(-1))
        {
            log.Add($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return;
        }

        if (!ActivateActCtx(contextHandle, out var cookie))
        {
            log.Add($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return;
        }

        // Ein echtes Fenster mit einer echten Pumpe. Ohne beides ist die Aktivierung an einem Platz
        // nicht messbar: Das Control haengt sich an ein Elternfenster und bekommt seine Nachrichten
        // aus dessen Schlange.
        using var window = new Form
        {
            Text = "OLE container probe",
            Width = 400,
            Height = 300,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            ShowInTaskbar = false
        };
        window.Show();
        Pump();

        try
        {
            var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
            var activation = CoCreateInstance(
                ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownIid, out var control);
            log.Add($"cocreate=0x{activation:X8}");
            if (activation != 0)
            {
                return;
            }

            try
            {
                HostInPlace(control, window, log);
                DrawIntoOurOwnContext(control, log);
                ReceiveOneEvent(control, sourceIid, bumpDispId, log);

                WithInterface(control, OleObjectIid, log, oleObject =>
                {
                    var close = Slot<CloseDelegate>(oleObject, SlotClose);
                    log.Add($"close=0x{close(oleObject, 1):X8}");
                });
            }
            finally
            {
                log.Add($"release={Marshal.Release(control)}");
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(contextHandle);
        }
    }

    /// <summary>
    /// The in-place sequence a container runs: site first, then state, then the verb. The order is
    /// the one <c>OLEMISC_SETCLIENTSITEFIRST</c> asks for, and the control reports that flag.
    /// </summary>
    private static void HostInPlace(IntPtr control, Form window, List<string> log)
    {
        WithInterface(control, OleObjectIid, log, oleObject =>
        {
            var site = new ContainerSite();
            var sitePointer = Marshal.GetIUnknownForObject(site);
            try
            {
                var setSite = Slot<SetClientSiteDelegate>(oleObject, SlotSetClientSite);
                log.Add($"setclientsite=0x{setSite(oleObject, sitePointer):X8}");
            }
            finally
            {
                Marshal.Release(sitePointer);
            }
        });

        WithInterface(control, PersistStreamInitIid, log, persist =>
        {
            var initNew = Slot<NoArgumentDelegate>(persist, SlotInitNew);
            log.Add($"initnew=0x{initNew(persist):X8}");
        });

        var placement = new ProbeRect { Left = 10, Top = 20, Right = 210, Bottom = 140 };
        WithInterface(control, OleObjectIid, log, oleObject =>
        {
            var doVerb = Slot<DoVerbDelegate>(oleObject, SlotDoVerb);
            log.Add($"doverb=0x{doVerb(oleObject, VerbUiActivate, IntPtr.Zero, IntPtr.Zero, 0, window.Handle, ref placement):X8}");
        });

        Pump();

        WithInterface(control, OleWindowIid, log, oleWindow =>
        {
            var getWindow = Slot<GetWindowDelegate>(oleWindow, SlotGetWindow);
            var hresult = getWindow(oleWindow, out var child);
            log.Add($"window=0x{hresult:X8}");
            if (hresult != 0 || child == IntPtr.Zero)
            {
                return;
            }

            // Das ist die Aussage, um die es geht: Das Fenster des Controls haengt im Fenster des
            // Containers. Ein Fenster, das nur existiert, hätte auch ein eigenes Popup sein können.
            var parent = GetParent(child);
            log.Add($"parent={(parent == window.Handle ? "container" : $"fremd:{parent:X}")}");

            if (GetWindowRect(child, out var bounds))
            {
                log.Add($"childsize={bounds.Right - bounds.Left}x{bounds.Bottom - bounds.Top}");
            }
        });

        // Und der Container verschiebt das Control an seinem Platz. Die beiden hinteren Werte eines
        // RECT sind Kanten, keine Groesse -- wer sie als Breite liest, bekommt ein Fenster, das mit
        // seiner Position wächst.
        var moved = new ProbeRect { Left = 5, Top = 5, Right = 105, Bottom = 55 };
        WithInterface(control, OleInPlaceObjectIid, log, inPlace =>
        {
            var setRects = Slot<SetObjectRectsDelegate>(inPlace, SlotSetObjectRects);
            log.Add($"setobjectrects=0x{setRects(inPlace, ref moved, ref moved):X8}");
        });

        Pump();

        WithInterface(control, OleWindowIid, log, oleWindow =>
        {
            var getWindow = Slot<GetWindowDelegate>(oleWindow, SlotGetWindow);
            if (getWindow(oleWindow, out var child) == 0 && child != IntPtr.Zero &&
                GetWindowRect(child, out var bounds))
            {
                log.Add($"movedsize={bounds.Right - bounds.Left}x{bounds.Bottom - bounds.Top}");
            }
        });
    }

    /// <summary>
    /// Draws the control into a device context the container owns and nothing else touches.
    ///
    /// The bitmap starts as one flat colour, so any pixel that differs afterwards came from the
    /// control. Checking the HRESULT alone would pass for a control that returns S_OK and paints
    /// nothing, which is the failure this is here to catch.
    /// </summary>
    private static void DrawIntoOurOwnContext(IntPtr control, List<string> log)
    {
        using var canvas = new Bitmap(200, 120, PixelFormat.Format32bppArgb);
        var untouched = Color.FromArgb(255, 255, 0, 255);
        using (var graphics = Graphics.FromImage(canvas))
        {
            graphics.Clear(untouched);
        }

        var bounds = new ProbeRect { Left = 0, Top = 0, Right = 200, Bottom = 120 };
        using (var graphics = Graphics.FromImage(canvas))
        {
            var deviceContext = graphics.GetHdc();
            try
            {
                WithInterface(control, ViewObjectIid, log, viewObject =>
                {
                    var draw = Slot<DrawDelegate>(viewObject, SlotDraw);
                    var hresult = draw(
                        viewObject, DvAspectContent, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                        deviceContext, ref bounds, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    log.Add($"draw=0x{hresult:X8}");
                });
            }
            finally
            {
                graphics.ReleaseHdc(deviceContext);
            }
        }

        var changed = 0;
        for (var y = 0; y < canvas.Height; y += 4)
        {
            for (var x = 0; x < canvas.Width; x += 4)
            {
                if (canvas.GetPixel(x, y).ToArgb() != untouched.ToArgb())
                {
                    changed++;
                }
            }
        }

        log.Add($"drawnpixels={changed}");
    }

    /// <summary>
    /// A sink on the control's connection point, and one call that should make it fire. This is the
    /// half of the contract a client cannot measure: a container is the thing events are for.
    /// </summary>
    private static void ReceiveOneEvent(IntPtr control, Guid sourceIid, int bumpDispId, List<string> log)
    {
        if (Marshal.GetObjectForIUnknown(control) is not IConnectionPointContainer container)
        {
            log.Add("connectionpointcontainer=nein");
            return;
        }

        var iid = sourceIid;
        container.FindConnectionPoint(ref iid, out var point);
        if (point is null)
        {
            log.Add("connectionpoint=nein");
            return;
        }

        var sink = new EventSink();
        point.Advise(sink, out var connection);
        try
        {
            var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
            if (Marshal.QueryInterface(control, in dispatchIid, out var dispatch) != 0)
            {
                log.Add("dispatch=nein");
                return;
            }

            try
            {
                // Ueber den rohen Zeiger, nicht ueber das verwaltete Objekt: In-Proc gibt
                // GetObjectForIUnknown die Instanz selbst heraus, und ein Aufruf darauf waere gar
                // kein COM-Aufruf mehr.
                var invoke = Slot<InvokeDelegate>(dispatch, SlotInvoke);
                var parameters = default(NativeDispParams);
                var empty = Guid.Empty;
                var hresult = invoke(
                    dispatch, bumpDispId, ref empty, 1033, DispatchMethod,
                    ref parameters, IntPtr.Zero, IntPtr.Zero, out _);
                log.Add($"invoke=0x{hresult:X8}");
                log.Add($"eventcalls={sink.Calls}");
            }
            finally
            {
                Marshal.Release(dispatch);
            }
        }
        finally
        {
            point.Unadvise(connection);
        }
    }

    private static void Pump()
    {
        for (var index = 0; index < 5; index++)
        {
            Application.DoEvents();
        }
    }

    private static void WithInterface(IntPtr unknown, Guid iid, List<string> log, Action<IntPtr> action)
    {
        var id = iid;
        var hresult = Marshal.QueryInterface(unknown, in id, out var pointer);
        if (hresult != 0)
        {
            log.Add($"qi {iid:D}=0x{hresult:X8}");
            return;
        }

        try
        {
            action(pointer);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    private static TDelegate Slot<TDelegate>(IntPtr self, int slot) where TDelegate : Delegate =>
        Marshal.GetDelegateForFunctionPointer<TDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), IntPtr.Size * slot));

    /// <summary>
    /// The container's side of the conversation. A control that cannot ask its site anything has
    /// no ambient properties and cannot ask to be shown, so a container that hands over nothing is
    /// not a container.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ContainerSite : IOleClientSite
    {
        public int SaveObject() => 0;

        public int GetMoniker(int assign, int whichMoniker, out IntPtr moniker)
        {
            moniker = IntPtr.Zero;
            return unchecked((int)0x80004001);
        }

        public int GetContainer(out IntPtr container)
        {
            container = IntPtr.Zero;
            return unchecked((int)0x80004001);
        }

        public int ShowObject() => 0;

        public int OnShowWindow(bool show) => 0;

        public int RequestNewObjectLayout() => unchecked((int)0x80004001);
    }

    [ComVisible(true)]
    [Guid("00000118-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleClientSite
    {
        [PreserveSig] int SaveObject();

        [PreserveSig] int GetMoniker(int assign, int whichMoniker, out IntPtr moniker);

        [PreserveSig] int GetContainer(out IntPtr container);

        [PreserveSig] int ShowObject();

        [PreserveSig] int OnShowWindow([MarshalAs(UnmanagedType.Bool)] bool show);

        [PreserveSig] int RequestNewObjectLayout();
    }

    /// <summary>
    /// What a VB6 container's <c>WithEvents</c> variable amounts to: a COM-visible object whose
    /// method is the event.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public sealed class EventSink
    {
        public int Calls { get; private set; }

        public void Clicked(int times) => Calls++;
    }

    private static readonly Guid OleObjectIid = new("00000112-0000-0000-C000-000000000046");
    private static readonly Guid OleWindowIid = new("00000114-0000-0000-C000-000000000046");
    private static readonly Guid OleInPlaceObjectIid = new("00000113-0000-0000-C000-000000000046");
    private static readonly Guid ViewObjectIid = new("0000010D-0000-0000-C000-000000000046");
    private static readonly Guid PersistStreamInitIid = new("7FD52380-4E07-101B-AE2D-08002B2EC713");

    private const int ClsCtxInprocServer = 1;
    private const uint CoInitApartmentThreaded = 2;
    private const int DvAspectContent = 1;
    private const int DispatchMethod = 1;
    private const int VerbUiActivate = -4;
    private const int SlotSetClientSite = 3;
    private const int SlotClose = 6;
    private const int SlotDoVerb = 11;
    private const int SlotGetWindow = 3;
    private const int SlotSetObjectRects = 7;
    private const int SlotDraw = 3;
    private const int SlotInitNew = 8;
    private const int SlotInvoke = 6;
    private const int SlotGetUserClassId = 15;
    private const int SlotGetUserType = 16;
    private const int SlotGetExtent = 18;
    private const int SlotGetMiscStatus = 22;
    private const int SlotLoad = 5;
    private const int SlotSave = 6;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProbeSize
    {
        public int Width;
        public int Height;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetMiscStatusDelegate(IntPtr self, int aspect, out int status);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetExtentDelegate(IntPtr self, int aspect, ref ProbeSize size);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetUserTypeDelegate(IntPtr self, int formOfType, out IntPtr userType);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetUserClassIdDelegate(IntPtr self, out Guid classId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LoadDelegate(IntPtr self, IntPtr stream);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SaveDelegate(IntPtr self, IntPtr stream, int clearDirty);

    [DllImport("ole32.dll")]
    private static extern int CreateStreamOnHGlobal(
        IntPtr hGlobal,
        [MarshalAs(UnmanagedType.Bool)] bool deleteOnRelease,
        out IntPtr stream);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProbeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDispParams
    {
        public IntPtr Arguments;
        public IntPtr NamedArguments;
        public int ArgumentCount;
        public int NamedArgumentCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ActCtx
    {
        public int cbSize;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpSource;
        public ushort wProcessorArchitecture;
        public ushort wLangId;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpAssemblyDirectory;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpResourceName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpApplicationName;
        public IntPtr hModule;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NoArgumentDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetClientSiteDelegate(IntPtr self, IntPtr site);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CloseDelegate(IntPtr self, int saveOption);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DoVerbDelegate(
        IntPtr self,
        int verb,
        IntPtr message,
        IntPtr activeSite,
        int index,
        IntPtr parentWindow,
        ref ProbeRect positionRectangle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetWindowDelegate(IntPtr self, out IntPtr window);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetObjectRectsDelegate(IntPtr self, ref ProbeRect position, ref ProbeRect clip);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DrawDelegate(
        IntPtr self,
        int drawAspect,
        int index,
        IntPtr aspect,
        IntPtr targetDevice,
        IntPtr informationDevice,
        IntPtr drawDevice,
        ref ProbeRect bounds,
        IntPtr windowBounds,
        IntPtr continueFunction,
        IntPtr continueParameter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InvokeDelegate(
        IntPtr self,
        int dispId,
        ref Guid iid,
        int lcid,
        ushort flags,
        ref NativeDispParams parameters,
        IntPtr result,
        IntPtr exception,
        out uint argumentError);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid classId,
        IntPtr outer,
        int context,
        ref Guid interfaceId,
        out IntPtr instance);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateActCtx(ref ActCtx actCtx);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ActivateActCtx(IntPtr actCtx, out IntPtr cookie);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeactivateActCtx(uint flags, IntPtr cookie);

    [DllImport("kernel32.dll")]
    private static extern void ReleaseActCtx(IntPtr actCtx);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out ProbeRect bounds);
}
