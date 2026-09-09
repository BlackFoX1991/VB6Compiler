using System.Runtime.InteropServices;
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
            return InvokeForVariant(args[1], args[2], int.Parse(args[3]));
        }

        if (args.Length == 4 && string.Equals(args[0], "--actctx", StringComparison.Ordinal))
        {
            return InvokeThroughActivationContext(args[1], args[2], int.Parse(args[3]));
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

    private static int InvokeForVariant(string comHostPath, string classIdText, int dispId)
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
                Console.WriteLine($"factory=0x{factoryHResult:X8}");
                return 0;
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
                    Console.WriteLine($"create=0x{createHResult:X8}");
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
                            ? $"vt={Marshal.ReadInt16(result)}"
                            : $"invoke=0x{hresult:X8}");
                    }
                    finally
                    {
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
