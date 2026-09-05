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

        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: VB6.ComActivationProbe <comhost.dll> <clsid> | " +
                "VB6.ComActivationProbe --local-server <clsid>");
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
