using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.ComActivationProbe;

[SupportedOSPlatform("windows")]
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: VB6.ComActivationProbe <comhost.dll> <clsid>");
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
                    Console.WriteLine((int)dispatch.Add(2, 5));
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
}
