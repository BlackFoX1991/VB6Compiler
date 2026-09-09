using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using VB6.Runtime;

namespace VB6.Runtime.Tests;

/// <summary>
/// The <c>IDispatch</c> a generated class answers on. It is this project's own, not the CLR's:
/// the DISPIDs are the ones the emitter stamped and the type library published, the names are the
/// VB6 names, and the answer is written into the caller's VARIANT here rather than by a marshaller
/// that refuses half of what VB6 can return.
/// </summary>
[TestClass]
public sealed class ComDispatchSurfaceTests
{
    [ComVisible(true)]
    public sealed class Rechner : VBComEventSource
    {
        [DispId(1)]
        public int Verdopple(int wert) => wert * 2;

        [DispId(2)]
        public string Name { get; set; } = "leer";

        [DispId(3)]
        public int Fehler() => throw new VB6RaisedError(11, "Division durch Null");

        // Ohne DispId gehört ein Mitglied nicht zur veröffentlichten Fläche.
        public int Intern() => 99;
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void TheSurfaceAnswersOnTheStampedDispIds()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM is a Windows contract.");
            return;
        }

        using var dispatch = ComDispatchHandle.For(new Rechner());

        // Ein Aufruf über die Nummer, die die Typbibliothek nennt.
        Assert.AreEqual(0, dispatch.Invoke(1, out var result, 21));
        Assert.AreEqual(42, result);

        // Und dieselbe Nummer findet ein Client auch über den VB6-Namen.
        Assert.AreEqual(1, dispatch.GetDispId("Verdopple"));
        Assert.AreEqual(2, dispatch.GetDispId("Name"));

        // Ein Mitglied ohne DispId ist für COM nicht da.
        Assert.AreEqual(unchecked((int)0x80020006), dispatch.TryGetDispId("Intern", out _));
        Assert.AreEqual(unchecked((int)0x80020003), dispatch.Invoke(4711, out _));
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void PropertiesReadAndWriteThroughTheirDispId()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM is a Windows contract.");
            return;
        }

        var instance = new Rechner();
        using var dispatch = ComDispatchHandle.For(instance);

        Assert.AreEqual(0, dispatch.Invoke(2, out var read));
        Assert.AreEqual("leer", read);

        Assert.AreEqual(0, dispatch.InvokePut(2, "voll"));
        Assert.AreEqual("voll", instance.Name);
    }

    [TestMethod]
    [SupportedOSPlatform("windows")]
    public void AServerErrorArrivesAsAnExceptionWithItsNumber()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("COM is a Windows contract.");
            return;
        }

        using var dispatch = ComDispatchHandle.For(new Rechner());

        // DISP_E_EXCEPTION allein ist für einen Client eine Nummer ohne Grund; die EXCEPINFO
        // trägt die VB6-Fehlernummer als FACILITY_CONTROL-Code.
        Assert.AreEqual(unchecked((int)0x80020009), dispatch.Invoke(3, out _, exceptionInfo: true));
        Assert.AreEqual(unchecked((int)(0x800A0000 + 11)), dispatch.LastExceptionCode);
        StringAssert.Contains(dispatch.LastExceptionDescription ?? string.Empty, "Division durch Null");
    }

    /// <summary>
    /// A raw IDispatch pointer plus the few calls these tests make on it. Everything goes through
    /// the vtable, so the tests exercise the same path a foreign client does.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private sealed class ComDispatchHandle : IDisposable
    {
        private static readonly Guid DispatchId = new("00020400-0000-0000-C000-000000000046");

        private readonly IntPtr _unknown;
        private readonly IntPtr _dispatch;

        private ComDispatchHandle(IntPtr unknown, IntPtr dispatch)
        {
            _unknown = unknown;
            _dispatch = dispatch;
        }

        public int LastExceptionCode { get; private set; }

        public string? LastExceptionDescription { get; private set; }

        public static ComDispatchHandle For(object instance)
        {
            var unknown = Marshal.GetIUnknownForObject(instance);
            var identity = DispatchId;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in identity, out var dispatch));
            return new ComDispatchHandle(unknown, dispatch);
        }

        public int GetDispId(string name)
        {
            var hresult = TryGetDispId(name, out var dispId);
            Assert.AreEqual(0, hresult, name);
            return dispId;
        }

        public int TryGetDispId(string name, out int dispId)
        {
            var names = Marshal.AllocCoTaskMem(IntPtr.Size);
            var text = Marshal.StringToCoTaskMemUni(name);
            var ids = Marshal.AllocCoTaskMem(sizeof(int));
            try
            {
                Marshal.WriteIntPtr(names, text);
                var call = Marshal.GetDelegateForFunctionPointer<GetIDsOfNamesDelegate>(Slot(5));
                var iid = Guid.Empty;
                var hresult = call(_dispatch, ref iid, names, 1, 1033, ids);
                dispId = Marshal.ReadInt32(ids);
                return hresult;
            }
            finally
            {
                Marshal.FreeCoTaskMem(ids);
                Marshal.FreeCoTaskMem(text);
                Marshal.FreeCoTaskMem(names);
            }
        }

        public int Invoke(int dispId, out object? result, int? argument = null, bool exceptionInfo = false) =>
            InvokeCore(dispId, DispatchMethodOrGet, argument, out result, exceptionInfo);

        public int InvokePut(int dispId, object value)
        {
            var arguments = Marshal.AllocCoTaskMem(VariantSize);
            var parameters = Marshal.AllocCoTaskMem(IntPtr.Size * 2 + sizeof(int) * 2);
            try
            {
                Clear(arguments, VariantSize);
                Marshal.GetNativeVariantForObject(value, arguments);
                Marshal.WriteIntPtr(parameters, arguments);
                Marshal.WriteIntPtr(parameters, IntPtr.Size, IntPtr.Zero);
                Marshal.WriteInt32(parameters, IntPtr.Size * 2, 1);
                Marshal.WriteInt32(parameters, IntPtr.Size * 2 + sizeof(int), 0);

                var call = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(Slot(6));
                var iid = Guid.Empty;
                return call(_dispatch, dispId, ref iid, 1033, 4, parameters, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeCoTaskMem(parameters);
                Marshal.FreeCoTaskMem(arguments);
            }
        }

        private int InvokeCore(int dispId, ushort flags, int? argument, out object? result, bool exceptionInfo)
        {
            var arguments = argument is null ? IntPtr.Zero : Marshal.AllocCoTaskMem(VariantSize);
            var parameters = Marshal.AllocCoTaskMem(IntPtr.Size * 2 + sizeof(int) * 2);
            var resultVariant = Marshal.AllocCoTaskMem(VariantSize);
            var exception = exceptionInfo ? Marshal.AllocCoTaskMem(IntPtr.Size * 8) : IntPtr.Zero;
            try
            {
                Clear(resultVariant, VariantSize);
                if (exception != IntPtr.Zero)
                {
                    Clear(exception, IntPtr.Size * 8);
                }

                if (arguments != IntPtr.Zero)
                {
                    Clear(arguments, VariantSize);
                    Marshal.GetNativeVariantForObject(argument!.Value, arguments);
                }

                Marshal.WriteIntPtr(parameters, arguments);
                Marshal.WriteIntPtr(parameters, IntPtr.Size, IntPtr.Zero);
                Marshal.WriteInt32(parameters, IntPtr.Size * 2, argument is null ? 0 : 1);
                Marshal.WriteInt32(parameters, IntPtr.Size * 2 + sizeof(int), 0);

                var call = Marshal.GetDelegateForFunctionPointer<InvokeDelegate>(Slot(6));
                var iid = Guid.Empty;
                var hresult = call(_dispatch, dispId, ref iid, 1033, flags, parameters, resultVariant, exception, IntPtr.Zero);
                result = hresult == 0 && Marshal.ReadInt16(resultVariant) != 0
                    ? Marshal.GetObjectForNativeVariant(resultVariant)
                    : null;

                if (exception != IntPtr.Zero)
                {
                    LastExceptionDescription = Marshal.PtrToStringBSTR(Marshal.ReadIntPtr(exception, IntPtr.Size * 2));
                    LastExceptionCode = Marshal.ReadInt32(exception, IntPtr.Size * 7);
                }

                return hresult;
            }
            finally
            {
                if (exception != IntPtr.Zero) { Marshal.FreeCoTaskMem(exception); }
                Marshal.FreeCoTaskMem(resultVariant);
                Marshal.FreeCoTaskMem(parameters);
                if (arguments != IntPtr.Zero) { Marshal.FreeCoTaskMem(arguments); }
            }
        }

        private IntPtr Slot(int index) => Marshal.ReadIntPtr(Marshal.ReadIntPtr(_dispatch), IntPtr.Size * index);

        private static void Clear(IntPtr block, int size)
        {
            for (var offset = 0; offset < size; offset++)
            {
                Marshal.WriteByte(block, offset, 0);
            }
        }

        public void Dispose()
        {
            Marshal.Release(_dispatch);
            Marshal.Release(_unknown);
        }

        private const ushort DispatchMethodOrGet = 1 | 2;
        private static readonly int VariantSize = IntPtr.Size == 8 ? 24 : 16;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetIDsOfNamesDelegate(
            IntPtr self,
            ref Guid iid,
            IntPtr names,
            uint count,
            uint lcid,
            IntPtr dispIds);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int InvokeDelegate(
            IntPtr self,
            int dispId,
            ref Guid iid,
            uint lcid,
            ushort flags,
            IntPtr parameters,
            IntPtr result,
            IntPtr exception,
            IntPtr argumentError);
    }
}
