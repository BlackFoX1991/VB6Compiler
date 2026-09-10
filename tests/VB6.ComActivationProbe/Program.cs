using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

namespace VB6.ComActivationProbe;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && string.Equals(args[0], "--local-server", StringComparison.Ordinal))
        {
            return ActivateLocalServer(args[1]);
        }

        if (args.Length == 2 && string.Equals(args[0], "--local-server-hold", StringComparison.Ordinal))
        {
            return HoldLocalServer(args[1]);
        }

        if (args.Length == 5 && string.Equals(args[0], "--dispid", StringComparison.Ordinal))
        {
            return InvokeByDispId(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]));
        }

        if (args.Length == 4 && string.Equals(args[0], "--variant", StringComparison.Ordinal))
        {
            return InvokeForVariant(args[1], args[2], int.Parse(args[3]), viaUnknown: true);
        }

        if (args.Length == 4 && string.Equals(args[0], "--variant-direct", StringComparison.Ordinal))
        {
            return InvokeForVariant(args[1], args[2], int.Parse(args[3]), viaUnknown: false);
        }

        if (args.Length == 5 && string.Equals(args[0], "--record-in", StringComparison.Ordinal))
        {
            return SendRecord(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]));
        }

        if (args.Length == 4 && string.Equals(args[0], "--actctx", StringComparison.Ordinal))
        {
            return InvokeThroughActivationContext(args[1], args[2], int.Parse(args[3]));
        }

        if (args.Length == 6 && string.Equals(args[0], "--events", StringComparison.Ordinal))
        {
            return ReceiveEvents(args[1], args[2], args[3], int.Parse(args[4]), args[5]);
        }

        if (args.Length == 3 && string.Equals(args[0], "--olecontrol", StringComparison.Ordinal))
        {
            return DescribeControlSurface(args[1], args[2]);
        }

        if (args.Length == 4 && string.Equals(args[0], "--olecontrol-run", StringComparison.Ordinal))
        {
            return RunControlLifecycle(args[1], args[2], args[3]);
        }

        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: VB6.ComActivationProbe <comhost.dll> <clsid> | " +
                "VB6.ComActivationProbe --local-server <clsid> | " +
                "VB6.ComActivationProbe --local-server-hold <clsid> | " +
                "VB6.ComActivationProbe --dispid <comhost.dll> <clsid> <dispid> <argument>");
            return 2;
        }

        var module = NativeLibrary.Load(args[0]);
        try
        {
            var getClassObject = Marshal.GetDelegateForFunctionPointer<
                DllGetClassObjectDelegate>(NativeLibrary.GetExport(module, "DllGetClassObject"));
            var clsid = Guid.Parse(args[1]);
            var classFactoryIid = new Guid("00000001-0000-0000-C000-000000000046");
            var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
            var factoryPointer = IntPtr.Zero;
            var factoryHResult = getClassObject(
                ref clsid,
                ref classFactoryIid,
                out factoryPointer);
            if (factoryHResult != 0)
            {
                Console.Error.WriteLine($"DllGetClassObject failed: 0x{factoryHResult:X8}");
                return factoryHResult;
            }

            try
            {
                var createInstance = GetClassFactoryCreateInstance(factoryPointer);
                var objectPointer = IntPtr.Zero;
                var createHResult = createInstance(
                    factoryPointer,
                    IntPtr.Zero,
                    ref dispatchIid,
                    out objectPointer);
                if (createHResult != 0)
                {
                    Console.Error.WriteLine($"IClassFactory.CreateInstance failed: 0x{createHResult:X8}");
                    return createHResult;
                }

                try
                {
                    var comObject = Marshal.GetObjectForIUnknown(objectPointer);
                    dynamic dispatch = comObject;
                    var sum = (int)dispatch.Add(2, 5);
                    var incremented = InvokeByRefLong(objectPointer, "Increment", 41);
                    var values = Array.CreateInstance(
                        typeof(object),
                        new[] { 2, 2 },
                        new[] { 1, 3 });
                    values.SetValue(10, 1, 3);
                    values.SetValue(20, 1, 4);
                    values.SetValue(30, 2, 3);
                    values.SetValue(40, 2, 4);
                    var arrayResult = InvokeByRefVariantArray(
                        objectPointer,
                        "MutateVariantArray",
                        values);
                    Console.WriteLine($"{sum}|{incremented}|{arrayResult}");
                }
                finally
                {
                    if (objectPointer != IntPtr.Zero)
                    {
                        Marshal.Release(objectPointer);
                    }
                }
            }
            finally
            {
                if (factoryPointer != IntPtr.Zero)
                {
                    Marshal.Release(factoryPointer);
                }
            }

            return 0;
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    /// <summary>
    /// Activates a class by CLSID and calls one member by DISPID -- no name is used anywhere.
    ///
    /// That is what an already-built client does: it was compiled against an earlier version of
    /// the component and carries the numbers, not the names. If a rebuild renumbers or re-identifies
    /// anything, this call is the one that breaks.
    /// </summary>
    private static int InvokeByDispId(string comHostPath, string classIdText, int dispId, int argument)
    {
        var module = NativeLibrary.Load(comHostPath);
        try
        {
            var getClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(
                NativeLibrary.GetExport(module, "DllGetClassObject"));
            var clsid = Guid.Parse(classIdText);
            var classFactoryIid = new Guid("00000001-0000-0000-C000-000000000046");
            var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
            var factoryHResult = getClassObject(ref clsid, ref classFactoryIid, out var factoryPointer);
            if (factoryHResult != 0)
            {
                Console.Error.WriteLine($"DllGetClassObject failed: 0x{factoryHResult:X8}");
                return factoryHResult;
            }

            try
            {
                var createInstance = GetClassFactoryCreateInstance(factoryPointer);
                var createHResult = createInstance(
                    factoryPointer,
                    IntPtr.Zero,
                    ref dispatchIid,
                    out var objectPointer);
                if (createHResult != 0)
                {
                    Console.Error.WriteLine($"IClassFactory.CreateInstance failed: 0x{createHResult:X8}");
                    return createHResult;
                }

                try
                {
                    Console.WriteLine(InvokeOneInt32ByDispId(objectPointer, dispId, argument));
                    return 0;
                }
                finally
                {
                    Marshal.Release(objectPointer);
                }
            }
            finally
            {
                Marshal.Release(factoryPointer);
            }
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    /// <summary>
    /// Calls a member without arguments and prints the VARTYPE that comes back, or the HRESULT the
    /// server answered with. Used to see what a foreign client actually receives for a member whose
    /// type the library describes -- a record among them.
    /// </summary>
    /// <summary>
    /// Reg-free COM the way a real client does it: the manifest's activation context is activated,
    /// the class is created by CLSID through CoCreateInstance, and a member is called by DISPID.
    /// Everything the runtime has to resolve -- the class, its type library, the record info behind
    /// a record -- has to come out of that context, because nothing here is registered.
    /// </summary>
    private static int InvokeThroughActivationContext(string manifestPath, string classIdText, int dispId)
    {
        // RPC_E_CHANGED_MODE heisst nur, dass der Thread schon initialisiert ist -- .NET tut das
        // selbst. Fuer diesen Aufruf ist das kein Fehler.
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            Console.WriteLine($"coinit=0x{initialization:X8}");
            return 0;
        }

        var context = new ActCtx
        {
            cbSize = Marshal.SizeOf<ActCtx>(),
            lpSource = manifestPath
        };
        var handle = CreateActCtx(ref context);
        if (handle == new IntPtr(-1))
        {
            Console.WriteLine($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        if (!ActivateActCtx(handle, out var cookie))
        {
            Console.WriteLine($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            var activation = CoCreateInstance(ref classId, IntPtr.Zero, ClsCtxInprocServer, ref dispatchId, out var dispatch);
            if (activation != 0)
            {
                Console.WriteLine($"cocreate=0x{activation:X8}");
                return 0;
            }

            try
            {
                var vtable = Marshal.ReadIntPtr(dispatch);
                var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                    Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
                var result = Marshal.AllocCoTaskMem(VariantSize);
                try
                {
                    ClearNativeMemory(result);
                    var parameters = new NativeDispParams();
                    var iid = Guid.Empty;
                    var hresult = invoke(
                        dispatch,
                        dispId,
                        ref iid,
                        1033,
                        DispatchMethod,
                        ref parameters,
                        result,
                        IntPtr.Zero,
                        out _);
                    Console.WriteLine(hresult == 0
                        ? $"vt={Marshal.ReadInt16(result)}"
                        : $"invoke=0x{hresult:X8}");
                }
                finally
                {
                    Marshal.FreeCoTaskMem(result);
                }
            }
            finally
            {
                Marshal.Release(dispatch);
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(handle);
        }

        return 0;
    }

    /// <summary>
    /// What a container asks a control for, asked in the container's own order.
    ///
    /// An ActiveX control is not one interface but a set, and a container decides what it can do
    /// with a control by asking for them one at a time. This mode activates the class reg-free out
    /// of its manifest and reports, for every interface in that set, whether the object answers.
    /// The answer is the gap list -- nothing here is derived from what the compiler happens to
    /// emit today.
    /// </summary>
    private static int DescribeControlSurface(string manifestPath, string classIdText)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            Console.WriteLine($"coinit=0x{initialization:X8}");
            return 0;
        }

        var context = new ActCtx
        {
            cbSize = Marshal.SizeOf<ActCtx>(),
            lpSource = manifestPath
        };
        var handle = CreateActCtx(ref context);
        if (handle == new IntPtr(-1))
        {
            Console.WriteLine($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        if (!ActivateActCtx(handle, out var cookie))
        {
            Console.WriteLine($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var unknownId = new Guid("00000000-0000-0000-C000-000000000046");
            var activation = CoCreateInstance(
                ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownId, out var unknown);
            if (activation != 0)
            {
                Console.WriteLine($"cocreate=0x{activation:X8}");
                return 0;
            }

            try
            {
                foreach (var (name, iid) in ControlSurface)
                {
                    var id = Guid.Parse(iid);
                    var hresult = Marshal.QueryInterface(unknown, in id, out var candidate);
                    if (hresult == 0)
                    {
                        Marshal.Release(candidate);
                    }

                    Console.WriteLine($"{name}={(hresult == 0 ? "ja" : $"0x{hresult:X8}")}");
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(handle);
        }

        return 0;
    }

    /// <summary>
    /// The whole life of a control in a foreign container: create it, ask it what it is, give it a
    /// state, take the state back, hand it to a *second* instance and read the value out again.
    ///
    /// Every call goes through the raw vtable at its published slot rather than through a managed
    /// interface declaration. That is deliberate: this probe links the same runtime the server
    /// does, so a wrongly ordered declaration would be wrong identically on both sides and the
    /// round trip would pass. A slot number is an independent statement.
    /// </summary>
    private static int RunControlLifecycle(string manifestPath, string classIdText, string caption)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            Console.WriteLine($"coinit=0x{initialization:X8}");
            return 0;
        }

        var context = new ActCtx
        {
            cbSize = Marshal.SizeOf<ActCtx>(),
            lpSource = manifestPath
        };
        var handle = CreateActCtx(ref context);
        if (handle == new IntPtr(-1))
        {
            Console.WriteLine($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        if (!ActivateActCtx(handle, out var cookie))
        {
            Console.WriteLine($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var source = Create(classId);
            if (source == IntPtr.Zero)
            {
                return 0;
            }

            try
            {
                Describe(source);

                // Der Container gibt dem Control zuerst seine Site und dann seinen Zustand --
                // genau die Reihenfolge, die OLEMISC_SETCLIENTSITEFIRST verlangt.
                Call(source, OleObjectIid, SlotSetClientSite, (IntPtr self, IntPtr site) =>
                {
                    var call = Slot<SetClientSiteDelegate>(self, SlotSetClientSite);
                    return call(self, site);
                }, IntPtr.Zero, "setclientsite");

                WithInterface(source, PersistStreamInitIid, persist =>
                {
                    var initNew = Slot<NoArgumentDelegate>(persist, SlotInitNew);
                    Console.WriteLine($"initnew=0x{initNew(persist):X8}");
                });

                var dispatch = Marshal.GetObjectForIUnknown(source);
                dispatch.GetType().InvokeMember(
                    "Caption",
                    BindingFlags.SetProperty,
                    binder: null,
                    dispatch,
                    [caption],
                    CultureInfo.InvariantCulture);

                if (CreateStreamOnHGlobal(IntPtr.Zero, deleteOnRelease: true, out var stream) != 0)
                {
                    Console.WriteLine("createstream=fehlgeschlagen");
                    return 0;
                }

                try
                {
                    WithInterface(source, PersistStreamInitIid, persist =>
                    {
                        var save = Slot<SaveDelegate>(persist, SlotSave);
                        Console.WriteLine($"save=0x{save(persist, stream, 1):X8}");
                    });

                    // Zuruecksetzen ueber die Rahmen-eigene IStream-Deklaration: Der Strom gehoert
                    // hier dem Container, nicht dem Server.
                    ((System.Runtime.InteropServices.ComTypes.IStream)Marshal.GetObjectForIUnknown(stream))
                        .Seek(0, 0, IntPtr.Zero);

                    var target = Create(classId);
                    if (target == IntPtr.Zero)
                    {
                        return 0;
                    }

                    try
                    {
                        WithInterface(target, PersistStreamInitIid, persist =>
                        {
                            var load = Slot<LoadDelegate>(persist, SlotLoad);
                            Console.WriteLine($"load=0x{load(persist, stream):X8}");
                        });

                        var restored = Marshal.GetObjectForIUnknown(target);
                        var value = restored.GetType().InvokeMember(
                            "Caption",
                            BindingFlags.GetProperty,
                            binder: null,
                            restored,
                            null,
                            CultureInfo.InvariantCulture);
                        Console.WriteLine($"caption={value}");

                        WithInterface(target, OleObjectIid, oleObject =>
                        {
                            var close = Slot<CloseDelegate>(oleObject, SlotClose);
                            Console.WriteLine($"close=0x{close(oleObject, 1):X8}");
                        });
                    }
                    finally
                    {
                        Marshal.Release(target);
                    }
                }
                finally
                {
                    Marshal.Release(stream);
                }
            }
            finally
            {
                Console.WriteLine($"release={Marshal.Release(source)}");
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(handle);
        }

        return 0;

        static IntPtr Create(Guid classId)
        {
            var unknownId = new Guid("00000000-0000-0000-C000-000000000046");
            var hresult = CoCreateInstance(
                ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownId, out var unknown);
            if (hresult == 0)
            {
                return unknown;
            }

            Console.WriteLine($"cocreate=0x{hresult:X8}");
            return IntPtr.Zero;
        }

        static void Describe(IntPtr unknown) => WithInterface(unknown, OleObjectIid, oleObject =>
        {
            var miscStatus = Slot<GetMiscStatusDelegate>(oleObject, SlotGetMiscStatus);
            Console.WriteLine(miscStatus(oleObject, DvAspectContent, out var status) == 0
                ? $"miscstatus=0x{status:X}"
                : "miscstatus=fehlgeschlagen");

            var extent = Slot<GetExtentDelegate>(oleObject, SlotGetExtent);
            var size = default(ProbeSize);
            Console.WriteLine(extent(oleObject, DvAspectContent, ref size) == 0
                ? $"extent={size.Width}x{size.Height}"
                : "extent=fehlgeschlagen");

            var userType = Slot<GetUserTypeDelegate>(oleObject, SlotGetUserType);
            if (userType(oleObject, 1, out var text) == 0)
            {
                Console.WriteLine($"usertype={Marshal.PtrToStringUni(text)}");
                Marshal.FreeCoTaskMem(text);
            }

            var classId = Slot<GetUserClassIdDelegate>(oleObject, SlotGetUserClassId);
            if (classId(oleObject, out var identifier) == 0)
            {
                Console.WriteLine($"userclsid={identifier.ToString("B").ToUpperInvariant()}");
            }
        });

        static void WithInterface(IntPtr unknown, string iid, Action<IntPtr> action)
        {
            var id = Guid.Parse(iid);
            var hresult = Marshal.QueryInterface(unknown, in id, out var pointer);
            if (hresult != 0)
            {
                Console.WriteLine($"qi {iid}=0x{hresult:X8}");
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

        static void Call(IntPtr unknown, string iid, int slot, Func<IntPtr, IntPtr, int> call, IntPtr argument, string label)
        {
            WithInterface(unknown, iid, pointer =>
                Console.WriteLine($"{label}=0x{call(pointer, argument):X8}"));
        }

        static TDelegate Slot<TDelegate>(IntPtr self, int slot) where TDelegate : Delegate =>
            Marshal.GetDelegateForFunctionPointer<TDelegate>(
                Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), IntPtr.Size * slot));
    }

    // Die veroeffentlichten Slotnummern, gezaehlt ab IUnknown. Sie sind der eigentliche Vertrag:
    // Eine Methode an falscher Stelle ruft die falsche Funktion, statt zu scheitern.
    private const string OleObjectIid = "00000112-0000-0000-C000-000000000046";
    private const string PersistStreamInitIid = "7FD52380-4E07-101B-AE2D-08002B2EC713";
    private const int DvAspectContent = 1;
    private const int SlotSetClientSite = 3;
    private const int SlotClose = 6;
    private const int SlotGetUserClassId = 15;
    private const int SlotGetUserType = 16;
    private const int SlotGetExtent = 18;
    private const int SlotGetMiscStatus = 22;
    private const int SlotLoad = 5;
    private const int SlotSave = 6;
    private const int SlotInitNew = 8;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProbeSize
    {
        public int Width;
        public int Height;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NoArgumentDelegate(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetClientSiteDelegate(IntPtr self, IntPtr site);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CloseDelegate(IntPtr self, int saveOption);

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

    /// <summary>
    /// The interfaces an OLE control container asks for. The list is taken from the published
    /// OLE Controls contract, not from what this compiler emits -- an inventory built from the
    /// existing output would only ever confirm itself.
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
    /// The sink a client hands to a connection point. It is a plain COM-visible object with the
    /// event as a method -- exactly what a VB6 client's <c>WithEvents</c> variable amounts to.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public sealed class EventSink
    {
        public string Received { get; private set; } = string.Empty;

        public void Fertig(int stand) => Received = "Fertig:" + stand;
    }

    /// <summary>
    /// Binds the event source the way a client with the type library does: it asks the connection
    /// point container for the source interface *by its IID from the library*, advises a sink,
    /// and then calls a member that raises the event. Nothing here knows the server's internals.
    /// </summary>
    private static int ReceiveEvents(
        string manifestPath,
        string classIdText,
        string sourceIidText,
        int dispId,
        string expected)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitApartmentThreaded);
        if (initialization < 0 && initialization != unchecked((int)0x80010106))
        {
            Console.WriteLine($"coinit=0x{initialization:X8}");
            return 0;
        }

        var context = new ActCtx
        {
            cbSize = Marshal.SizeOf<ActCtx>(),
            lpSource = manifestPath
        };
        var handle = CreateActCtx(ref context);
        if (handle == new IntPtr(-1))
        {
            Console.WriteLine($"createactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        if (!ActivateActCtx(handle, out var cookie))
        {
            Console.WriteLine($"activateactctx=0x{Marshal.GetLastWin32Error():X8}");
            return 0;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
            var activation = CoCreateInstance(ref classId, IntPtr.Zero, ClsCtxInprocServer, ref unknownIid, out var unknown);
            if (activation != 0)
            {
                Console.WriteLine($"cocreate=0x{activation:X8}");
                return 0;
            }

            var server = Marshal.GetObjectForIUnknown(unknown);
            var container = server as IConnectionPointContainer;
            if (container is null)
            {
                Console.WriteLine("noconnectionpointcontainer");
                return 0;
            }

            var sourceIid = Guid.Parse(sourceIidText);
            container.FindConnectionPoint(ref sourceIid, out var point);
            if (point is null)
            {
                Console.WriteLine("noconnectionpoint");
                return 0;
            }

            point.GetConnectionInterface(out var reported);
            var sink = new EventSink();
            point.Advise(sink, out var connection);
            try
            {
                // Der Aufruf geht ueber IDispatch am rohen Zeiger, nicht ueber das verwaltete
                // Objekt: In-Proc gibt Marshal.GetObjectForIUnknown die Instanz selbst heraus, und
                // ein Aufruf darauf waere gar kein COM-Aufruf mehr.
                var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
                var queryResult = Marshal.QueryInterface(unknown, in dispatchIid, out var dispatch);
                if (queryResult != 0)
                {
                    Console.WriteLine($"queryinterface=0x{queryResult:X8}");
                    return 0;
                }

                try
                {
                    var vtable = Marshal.ReadIntPtr(dispatch);
                    var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                        Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
                    var parameters = new NativeDispParams();
                    var iid = Guid.Empty;
                    var hresult = invoke(
                        dispatch,
                        dispId,
                        ref iid,
                        1033,
                        DispatchMethod,
                        ref parameters,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        out _);
                    Console.WriteLine(
                        $"iid={reported:D} received={sink.Received} expected={expected} " +
                        $"match={string.Equals(sink.Received, expected, StringComparison.Ordinal)} " +
                        $"invoke=0x{hresult:X8}");
                }
                finally
                {
                    Marshal.Release(dispatch);
                }
            }
            finally
            {
                point.Unadvise(connection);
                Marshal.Release(unknown);
            }
        }
        finally
        {
            DeactivateActCtx(0, cookie);
            ReleaseActCtx(handle);
        }

        return 0;
    }

    private const int ClsCtxInprocServer = 1;
    private const int CoInitApartmentThreaded = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    private static int InvokeForVariant(string comHostPath, string classIdText, int dispId, bool viaUnknown)
    {
        var module = NativeLibrary.Load(comHostPath);
        try
        {
            var getClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(
                NativeLibrary.GetExport(module, "DllGetClassObject"));
            var clsid = Guid.Parse(classIdText);
            var classFactoryIid = new Guid("00000001-0000-0000-C000-000000000046");
            var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
            var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
            var factoryHResult = getClassObject(ref clsid, ref classFactoryIid, out var factoryPointer);
            if (factoryHResult != 0)
            {
                Console.WriteLine($"factory=0x{factoryHResult:X8}");
                return 0;
            }

            try
            {
                var createInstance = GetClassFactoryCreateInstance(factoryPointer);
                // Wie ein echter Client: erzeugen ueber IUnknown, dann nach IDispatch fragen. Wer
                // die Fabrik gleich nach IDispatch fragt, misst die Fabrik und nicht das Objekt.
                var requested = viaUnknown ? unknownIid : dispatchIid;
                var createHResult = createInstance(
                    factoryPointer,
                    IntPtr.Zero,
                    ref requested,
                    out var unknownPointer);
                if (createHResult != 0)
                {
                    Console.WriteLine($"create=0x{createHResult:X8}");
                    return 0;
                }

                IntPtr objectPointer;
                var queryHResult = 0;
                if (viaUnknown)
                {
                    queryHResult = Marshal.QueryInterface(unknownPointer, in dispatchIid, out objectPointer);
                    Marshal.Release(unknownPointer);
                }
                else
                {
                    objectPointer = unknownPointer;
                }

                if (queryHResult != 0)
                {
                    Console.WriteLine($"qi=0x{queryHResult:X8}");
                    return 0;
                }

                try
                {
                    var vtable = Marshal.ReadIntPtr(objectPointer);
                    var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                        Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
                    var result = Marshal.AllocCoTaskMem(VariantSize);
                    try
                    {
                        ClearNativeMemory(result);
                        var parameters = new NativeDispParams();
                        var iid = Guid.Empty;
                        var hresult = invoke(
                            objectPointer,
                            dispId,
                            ref iid,
                            1033,
                            DispatchMethod,
                            ref parameters,
                            result,
                            IntPtr.Zero,
                            out _);
                        Console.WriteLine(hresult == 0
                            ? DescribeResult(result)
                            : $"invoke=0x{hresult:X8}");
                    }
                    finally
                    {
                        // Der Client gibt frei, was er bekommen hat. Bei einem VT_RECORD geht
                        // VariantClear durch IRecordInfo::RecordDestroy -- also durch den Server.
                        _ = VariantClear(result);
                        Marshal.FreeCoTaskMem(result);
                    }

                    return 0;
                }
                finally
                {
                    Marshal.Release(objectPointer);
                }
            }
            finally
            {
                Marshal.Release(factoryPointer);
            }
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    /// <summary>
    /// Sends a record *into* the server: the client builds a VT_RECORD from the description the
    /// server itself hands out for that member's type, fills the fields through IRecordInfo and
    /// passes it as an argument. The other direction of the same contract -- and the one where a
    /// wrong layout shows up as garbage rather than as an error code.
    /// </summary>
    private static int SendRecord(string comHostPath, string classIdText, int producingDispId, int consumingDispId)
    {
        var module = NativeLibrary.Load(comHostPath);
        try
        {
            var getClassObject = Marshal.GetDelegateForFunctionPointer<DllGetClassObjectDelegate>(
                NativeLibrary.GetExport(module, "DllGetClassObject"));
            var clsid = Guid.Parse(classIdText);
            var classFactoryIid = new Guid("00000001-0000-0000-C000-000000000046");
            var dispatchIid = new Guid("00020400-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(getClassObject(ref clsid, ref classFactoryIid, out var factory));
            try
            {
                var createInstance = GetClassFactoryCreateInstance(factory);
                Marshal.ThrowExceptionForHR(createInstance(factory, IntPtr.Zero, ref dispatchIid, out var dispatch));
                try
                {
                    // Erst einen Record vom Server holen -- daran hängt die IRecordInfo, mit der
                    // der Client einen eigenen bauen kann. Genau so kommt ein VB6-Client an die
                    // Beschreibung eines UDT, den er nicht selbst deklariert hat.
                    var template = Marshal.AllocCoTaskMem(VariantSize);
                    try
                    {
                        ClearNativeMemory(template);
                        var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
                            Marshal.ReadIntPtr(Marshal.ReadIntPtr(dispatch), IntPtr.Size * 6));
                        var iid = Guid.Empty;
                        var empty = new NativeDispParams();
                        Marshal.ThrowExceptionForHR(invoke(
                            dispatch, producingDispId, ref iid, 1033, DispatchMethod, ref empty, template, IntPtr.Zero, out _));

                        var info = (IRecordInfo)Marshal.GetObjectForIUnknown(
                            Marshal.ReadIntPtr(template, VariantDataOffset + IntPtr.Size));
                        info.GetSize(out var size);
                        var record = Marshal.AllocCoTaskMem((int)size);
                        try
                        {
                            info.RecordInit(record);
                            WriteField(info, record, "X", 11);
                            WriteField(info, record, "Y", 22);

                            var argument = Marshal.AllocCoTaskMem(VariantSize);
                            var result = Marshal.AllocCoTaskMem(VariantSize);
                            try
                            {
                                ClearNativeMemory(argument);
                                ClearNativeMemory(result);
                                Marshal.WriteInt16(argument, 36);   // VT_RECORD
                                Marshal.WriteIntPtr(argument, VariantDataOffset, record);
                                Marshal.WriteIntPtr(
                                    argument,
                                    VariantDataOffset + IntPtr.Size,
                                    Marshal.ReadIntPtr(template, VariantDataOffset + IntPtr.Size));

                                var parameters = new NativeDispParams { Arguments = argument, ArgumentCount = 1 };
                                var hresult = invoke(
                                    dispatch, consumingDispId, ref iid, 1033, DispatchMethod, ref parameters, result, IntPtr.Zero, out _);
                                Console.WriteLine(hresult == 0
                                    ? $"sum={Marshal.GetObjectForNativeVariant(result)}"
                                    : $"invoke=0x{hresult:X8}");
                            }
                            finally
                            {
                                Marshal.FreeCoTaskMem(result);
                                Marshal.FreeCoTaskMem(argument);
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(record);
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(template);
                    }

                    return 0;
                }
                finally
                {
                    Marshal.Release(dispatch);
                }
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    private static void WriteField(IRecordInfo info, IntPtr record, string name, int value)
    {
        var variant = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            ClearNativeMemory(variant);
            Marshal.GetNativeVariantForObject(value, variant);
            info.PutField(0, record, name, variant);
        }
        finally
        {
            Marshal.FreeCoTaskMem(variant);
        }
    }

    /// <summary>
    /// What came back, in the client's own words. A record is read through the IRecordInfo the
    /// server put beside the data -- exactly the way a VB6 or C++ client reads a UDT, and the only
    /// way to tell a real VT_RECORD from a variant that merely claims the type.
    /// </summary>
    private static string DescribeResult(IntPtr result)
    {
        const short VtRecord = 36;
        var type = Marshal.ReadInt16(result);
        if (type != VtRecord)
        {
            return $"vt={type}";
        }

        var data = Marshal.ReadIntPtr(result, VariantDataOffset);
        var info = (IRecordInfo)Marshal.GetObjectForIUnknown(Marshal.ReadIntPtr(result, VariantDataOffset + IntPtr.Size));
        info.GetName(out var name);
        info.GetSize(out var size);

        var fields = new List<string>();
        foreach (var field in new[] { "X", "Y" })
        {
            var value = Marshal.AllocCoTaskMem(VariantSize);
            try
            {
                ClearNativeMemory(value);
                info.GetField(data, field, value);
                fields.Add(field + "=" + Marshal.GetObjectForNativeVariant(value));
            }
            catch (COMException)
            {
                fields.Add(field + "=?");
            }
            finally
            {
                Marshal.FreeCoTaskMem(value);
            }
        }

        return $"vt={type} name={name} size={size} {string.Join(",", fields)}";
    }

    [ComImport]
    [Guid("0000002F-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IRecordInfo
    {
        void RecordInit(IntPtr record);

        void RecordClear(IntPtr record);

        void RecordCopy(IntPtr source, IntPtr destination);

        void GetGuid(out Guid guid);

        void GetName([MarshalAs(UnmanagedType.BStr)] out string name);

        void GetSize(out uint size);

        void GetTypeInfo(out IntPtr typeInfo);

        void GetField(IntPtr record, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr value);

        void GetFieldNoCopy(IntPtr record, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr value, out IntPtr raw);

        void PutField(uint flags, IntPtr record, [MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr value);
    }

    private static int InvokeOneInt32ByDispId(IntPtr dispatch, int dispId, int argument)
    {
        var vtable = Marshal.ReadIntPtr(dispatch);
        var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
        var arguments = Marshal.AllocCoTaskMem(VariantSize);
        var resultVariant = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            ClearNativeMemory(arguments);
            ClearNativeMemory(resultVariant);
            Marshal.WriteInt16(arguments, VariantI4);
            Marshal.WriteInt32(arguments, VariantDataOffset, argument);
            var parameters = new NativeDispParams
            {
                Arguments = arguments,
                ArgumentCount = 1
            };
            var iid = Guid.Empty;
            var invokeHResult = invoke(
                dispatch,
                dispId,
                ref iid,
                1033,
                DispatchMethod,
                ref parameters,
                resultVariant,
                IntPtr.Zero,
                out _);
            if (invokeHResult != 0)
            {
                throw new InvalidOperationException($"IDispatch.Invoke failed: 0x{invokeHResult:X8}");
            }

            return Marshal.ReadInt32(resultVariant, VariantDataOffset);
        }
        finally
        {
            Marshal.FreeCoTaskMem(resultVariant);
            Marshal.FreeCoTaskMem(arguments);
        }
    }

    private static int ActivateLocalServer(string classIdText)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitMultiThreaded);
        if (initialization < 0)
        {
            Console.Error.WriteLine($"CoInitializeEx failed: 0x{initialization:X8}");
            return initialization;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            var dispatch = IntPtr.Zero;
            var activation = CoCreateInstance(
                ref classId,
                IntPtr.Zero,
                ClsCtxLocalServer,
                ref dispatchId,
                out dispatch);
            if (activation != 0)
            {
                Console.Error.WriteLine($"CoCreateInstance failed: 0x{activation:X8}");
                return activation;
            }

            try
            {
                Console.WriteLine(InvokeTwoInt32(dispatch, "Summe", 20, 22));
                return 0;
            }
            finally
            {
                if (dispatch != IntPtr.Zero)
                {
                    Marshal.Release(dispatch);
                }
            }
        }
        finally
        {
            CoUninitialize();
        }
    }

    /// <summary>
    /// Activates the server, reads its own reference count off IUnknown, then keeps the reference
    /// until a line arrives on standard input.
    ///
    /// This is the only vantage point from which the runtime's release can be checked against a
    /// holder it does not share anything with. In one process a second holder and the runtime end
    /// up on the same wrapper, so "the other holder survived" partly tests the CLR. Across the
    /// boundary the two references are genuinely independent, and the server's own lifetime says
    /// whether releasing one touched the other.
    ///
    /// The counts printed here belong to this process's proxy, not to the object in the server --
    /// a proxy is what a client can see, and claiming otherwise would overstate the measurement.
    /// </summary>
    private static int HoldLocalServer(string classIdText)
    {
        var initialization = CoInitializeEx(IntPtr.Zero, CoInitMultiThreaded);
        if (initialization < 0)
        {
            Console.Error.WriteLine($"CoInitializeEx failed: 0x{initialization:X8}");
            return initialization;
        }

        try
        {
            var classId = Guid.Parse(classIdText);
            var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
            var dispatch = IntPtr.Zero;
            var activation = CoCreateInstance(
                ref classId,
                IntPtr.Zero,
                ClsCtxLocalServer,
                ref dispatchId,
                out dispatch);
            if (activation != 0)
            {
                Console.Error.WriteLine($"CoCreateInstance failed: 0x{activation:X8}");
                return activation;
            }

            var released = false;
            try
            {
                Console.WriteLine("SUM=" + InvokeTwoInt32(dispatch, "Summe", 20, 22));

                // AddRef then Release again: the pair has to move the count by exactly one in each
                // direction, and it leaves the activation reference untouched.
                Console.WriteLine("ADDREF=" + CallAddRef(dispatch));
                Console.WriteLine("RELEASE=" + CallRelease(dispatch));

                Console.WriteLine("HOLDING");
                Console.Out.Flush();

                _ = Console.In.ReadLine();

                Console.WriteLine("FINAL=" + CallRelease(dispatch));
                released = true;
                Console.WriteLine("RELEASED");
                Console.Out.Flush();
                return 0;
            }
            finally
            {
                if (!released && dispatch != IntPtr.Zero)
                {
                    Marshal.Release(dispatch);
                }
            }
        }
        finally
        {
            CoUninitialize();
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint AddRefDelegate(IntPtr @this);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseDelegate(IntPtr @this);

    /// <summary>IUnknown::AddRef, vtable slot 1, called for its returned count.</summary>
    private static uint CallAddRef(IntPtr unknown) =>
        Marshal.GetDelegateForFunctionPointer<AddRefDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(unknown), IntPtr.Size * 1))(unknown);

    /// <summary>IUnknown::Release, vtable slot 2, called for its returned count.</summary>
    private static uint CallRelease(IntPtr unknown) =>
        Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(unknown), IntPtr.Size * 2))(unknown);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DllGetClassObjectDelegate(
        ref Guid classId,
        ref Guid interfaceId,
        out IntPtr result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateInstanceDelegate(
        IntPtr @this,
        IntPtr outer,
        ref Guid interfaceId,
        out IntPtr result);

    private static CreateInstanceDelegate GetClassFactoryCreateInstance(IntPtr factory)
    {
        var vtable = Marshal.ReadIntPtr(factory);
        var method = Marshal.ReadIntPtr(vtable, IntPtr.Size * 3);
        return Marshal.GetDelegateForFunctionPointer<CreateInstanceDelegate>(method);
    }

    private static int InvokeByRefLong(IntPtr dispatch, string memberName, int value)
    {
        var vtable = Marshal.ReadIntPtr(dispatch);
        var getIdsOfNames = Marshal.GetDelegateForFunctionPointer<GetIdsOfNamesDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 5));
        var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        var argumentValue = Marshal.AllocCoTaskMem(sizeof(int));
        var argumentVariant = Marshal.AllocCoTaskMem(VariantSize);
        var resultVariant = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            var getIdHResult = getIdsOfNames(
                dispatch,
                ref iid,
                names,
                1,
                1033,
                out var dispId);
            if (getIdHResult != 0)
            {
                throw new InvalidOperationException($"GetIDsOfNames failed: 0x{getIdHResult:X8}");
            }

            ClearNativeMemory(argumentVariant);
            ClearNativeMemory(resultVariant);
            Marshal.WriteInt32(argumentValue, value);
            Marshal.WriteInt16(argumentVariant, (short)(VariantByRef | VariantI4));
            Marshal.WriteIntPtr(argumentVariant, VariantDataOffset, argumentValue);
            var parameters = new NativeDispParams
            {
                Arguments = argumentVariant,
                ArgumentCount = 1
            };
            var invokeHResult = invoke(
                dispatch,
                dispId,
                ref iid,
                1033,
                DispatchMethod,
                ref parameters,
                resultVariant,
                IntPtr.Zero,
                out _);
            if (invokeHResult != 0)
            {
                throw new InvalidOperationException($"IDispatch.Invoke failed: 0x{invokeHResult:X8}");
            }

            return Marshal.ReadInt32(argumentValue);
        }
        finally
        {
            Marshal.FreeCoTaskMem(resultVariant);
            Marshal.FreeCoTaskMem(argumentVariant);
            Marshal.FreeCoTaskMem(argumentValue);
            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static int InvokeTwoInt32(IntPtr dispatch, string memberName, int left, int right)
    {
        var vtable = Marshal.ReadIntPtr(dispatch);
        var getIdsOfNames = Marshal.GetDelegateForFunctionPointer<GetIdsOfNamesDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 5));
        var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        var arguments = Marshal.AllocCoTaskMem(VariantSize * 2);
        var resultVariant = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            var getIdHResult = getIdsOfNames(
                dispatch,
                ref iid,
                names,
                1,
                1033,
                out var dispId);
            if (getIdHResult != 0)
            {
                throw new InvalidOperationException($"GetIDsOfNames failed: 0x{getIdHResult:X8}");
            }

            ClearNativeMemory(arguments);
            ClearNativeMemory(IntPtr.Add(arguments, VariantSize));
            ClearNativeMemory(resultVariant);
            // IDispatch receives positional arguments in reverse order.
            WriteInt32Variant(arguments, right);
            WriteInt32Variant(IntPtr.Add(arguments, VariantSize), left);
            var parameters = new NativeDispParams
            {
                Arguments = arguments,
                ArgumentCount = 2
            };
            var invokeHResult = invoke(
                dispatch,
                dispId,
                ref iid,
                1033,
                DispatchMethod,
                ref parameters,
                resultVariant,
                IntPtr.Zero,
                out _);
            if (invokeHResult != 0)
            {
                throw new InvalidOperationException($"IDispatch.Invoke failed: 0x{invokeHResult:X8}");
            }

            if (Marshal.ReadInt16(resultVariant) != VariantI4)
            {
                throw new InvalidOperationException("Summe did not return a VT_I4 VARIANT.");
            }

            return Marshal.ReadInt32(resultVariant, VariantDataOffset);
        }
        finally
        {
            Marshal.FreeCoTaskMem(resultVariant);
            Marshal.FreeCoTaskMem(arguments);
            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static void WriteInt32Variant(IntPtr address, int value)
    {
        Marshal.WriteInt16(address, VariantI4);
        Marshal.WriteInt32(address, VariantDataOffset, value);
    }

    private static string InvokeByRefVariantArray(
        IntPtr dispatch,
        string memberName,
        Array value)
    {
        var vtable = Marshal.ReadIntPtr(dispatch);
        var getIdsOfNames = Marshal.GetDelegateForFunctionPointer<GetIdsOfNamesDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 5));
        var invoke = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(
            Marshal.ReadIntPtr(vtable, IntPtr.Size * 6));
        var name = Marshal.StringToCoTaskMemUni(memberName);
        var names = Marshal.AllocCoTaskMem(IntPtr.Size);
        var argumentVariant = Marshal.AllocCoTaskMem(VariantSize);
        var innerVariant = Marshal.AllocCoTaskMem(VariantSize);
        var resultVariant = Marshal.AllocCoTaskMem(VariantSize);
        try
        {
            Marshal.WriteIntPtr(names, name);
            var iid = Guid.Empty;
            var getIdHResult = getIdsOfNames(
                dispatch,
                ref iid,
                names,
                1,
                1033,
                out var dispId);
            if (getIdHResult != 0)
            {
                throw new InvalidOperationException($"GetIDsOfNames failed: 0x{getIdHResult:X8}");
            }

            ClearNativeMemory(argumentVariant);
            ClearNativeMemory(innerVariant);
            ClearNativeMemory(resultVariant);
            Marshal.GetNativeVariantForObject(value, innerVariant);
            Marshal.WriteInt16(argumentVariant, (short)(VariantByRef | VariantVariant));
            Marshal.WriteIntPtr(argumentVariant, VariantDataOffset, innerVariant);
            var parameters = new NativeDispParams
            {
                Arguments = argumentVariant,
                ArgumentCount = 1
            };
            var invokeHResult = invoke(
                dispatch,
                dispId,
                ref iid,
                1033,
                DispatchMethod,
                ref parameters,
                resultVariant,
                IntPtr.Zero,
                out _);
            if (invokeHResult != 0)
            {
                throw new InvalidOperationException($"IDispatch.Invoke failed: 0x{invokeHResult:X8}");
            }

            var updated = (Array?)Marshal.GetObjectForNativeVariant(innerVariant)
                ?? throw new InvalidOperationException("The COM server returned no SAFEARRAY.");
            return $"{updated.GetValue(1, 4)}|{updated.GetValue(2, 3)}";
        }
        finally
        {
            _ = VariantClear(innerVariant);
            Marshal.FreeCoTaskMem(resultVariant);
            Marshal.FreeCoTaskMem(innerVariant);
            Marshal.FreeCoTaskMem(argumentVariant);
            Marshal.FreeCoTaskMem(names);
            Marshal.FreeCoTaskMem(name);
        }
    }

    private static void ClearNativeMemory(IntPtr address)
    {
        Marshal.Copy(new byte[VariantSize], 0, address, VariantSize);
    }

    private const short VariantByRef = 0x4000;
    private const short VariantI4 = 0x0003;
    private const short VariantVariant = 0x000C;
    // On x64 the BRECORD arm makes VARIANT 24 bytes. The proxy serializes the whole struct, so
    // advancing by the x86 size makes a second argument overwrite the first one's record arm.
    private static readonly int VariantSize = IntPtr.Size == 8 ? 24 : 16;
    private const int VariantDataOffset = 8;

    [DllImport("oleaut32.dll")]
    private static extern int VariantClear(IntPtr variant);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid classId,
        IntPtr outer,
        uint context,
        ref Guid interfaceId,
        out IntPtr instance);

    private const uint CoInitMultiThreaded = 0;
    private const uint ClsCtxLocalServer = 0x4;
    private const ushort DispatchMethod = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDispParams
    {
        public IntPtr Arguments;
        public IntPtr NamedArguments;
        public uint ArgumentCount;
        public uint NamedArgumentCount;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetIdsOfNamesDelegate(
        IntPtr @this,
        ref Guid interfaceId,
        IntPtr names,
        uint nameCount,
        uint lcid,
        out int dispId);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InvokeDelegate(
        IntPtr @this,
        int dispId,
        ref Guid interfaceId,
        uint lcid,
        ushort flags,
        ref NativeDispParams parameters,
        IntPtr result,
        IntPtr exceptionInfo,
        out uint argumentError);
}
