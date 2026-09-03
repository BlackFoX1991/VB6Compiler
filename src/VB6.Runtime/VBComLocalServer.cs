using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// The out-of-process side of an ActiveX EXE.
///
/// VB6 starts the same executable in two roles. Double-clicked, it runs its <c>Sub Main</c>. Started
/// by COM with <c>/Embedding</c> -- or <c>/Automation</c>, which older clients use -- it must not
/// run the program at all: it registers its class objects, pumps messages until the client is done
/// with them, and exits. Getting that wrong is loud in one direction and silent in the other: a
/// server that runs Main under /Embedding shows a window nobody asked for, and one that never
/// registers leaves the client waiting for a server that will never answer.
/// </summary>
[SupportedOSPlatform("windows")]
public static class VBComLocalServer
{
    private const uint ClsCtxLocalServer = 0x4;
    private const uint RegClsMultipleUse = 1;
    private const uint RegClsSuspended = 4;

    private static readonly object Sync = new();
    private static int _objectCount;
    private static int _lockCount;
    private static bool _served;

    /// <summary>
    /// How long the server waits for its first activation. A client that started the server always
    /// activates immediately; without this the process would outlive a client that died between
    /// starting it and connecting.
    /// </summary>
    private static readonly TimeSpan ActivationTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Runs as a COM local server when the command line says so. Returns <see langword="false"/>
    /// when the program was started normally, and the caller then runs its own entry point.
    /// </summary>
    public static bool TryRunAsLocalServer()
    {
        if (!OperatingSystem.IsWindows() || !IsEmbeddingRequested(Environment.GetCommandLineArgs()))
        {
            return false;
        }

        Run(Assembly.GetEntryAssembly());
        return true;
    }

    /// <summary>
    /// <c>/Embedding</c> and <c>/Automation</c>, in either slash or dash form. VB6 accepts both
    /// spellings, and so do the clients that start these servers.
    /// </summary>
    internal static bool IsEmbeddingRequested(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (argument.Length < 2 || argument[0] is not ('/' or '-'))
            {
                continue;
            }

            var name = argument[1..];
            if (string.Equals(name, "Embedding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Automation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void Run(Assembly? entryAssembly)
    {
        var factories = new List<(Guid ClassId, VBComClassFactory Factory, uint Cookie)>();
        foreach (var (classId, type) in ReadComClasses(entryAssembly))
        {
            var factory = new VBComClassFactory(type);
            var hresult = CoRegisterClassObject(
                ref System.Runtime.CompilerServices.Unsafe.AsRef(in classId),
                factory,
                ClsCtxLocalServer,
                RegClsMultipleUse | RegClsSuspended,
                out var cookie);
            Marshal.ThrowExceptionForHR(hresult);
            factories.Add((classId, factory, cookie));
        }

        if (factories.Count == 0)
        {
            // Silently exiting here is the failure that leaves a client waiting for a server that
            // will never answer. Say so instead.
            Console.Error.WriteLine(
                "VB6 local server: no COM classes found in " +
                (entryAssembly?.GetName().Name ?? "<no entry assembly>") + ".");
            return;
        }

        // Registering suspended and resuming afterwards closes the window in which a client could
        // reach one class object while another is not registered yet.
        Marshal.ThrowExceptionForHR(CoResumeClassObjects());

        try
        {
            PumpUntilIdle();
        }
        finally
        {
            foreach (var entry in factories)
            {
                _ = CoRevokeClassObject(entry.Cookie);
            }
        }
    }

    /// <summary>
    /// The message loop. A local server has to pump: COM delivers cross-apartment calls as window
    /// messages, so a server that merely sleeps never receives anything.
    /// </summary>
    private static void PumpUntilIdle()
    {
        var started = DateTime.UtcNow;
        while (true)
        {
            while (PeekMessage(out var message, IntPtr.Zero, 0, 0, PeekRemove))
            {
                if (message.message == WmQuit)
                {
                    return;
                }

                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }

            lock (Sync)
            {
                if (_served && _objectCount == 0 && _lockCount == 0)
                {
                    return;
                }

                if (!_served && DateTime.UtcNow - started > ActivationTimeout)
                {
                    return;
                }
            }

            // A released proxy only drops its managed object at the next collection, so the count
            // that decides shutdown needs the collector to run at all.
            if (Volatile.Read(ref _objectCount) > 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            Thread.Sleep(50);
        }
    }

    private static IEnumerable<(Guid ClassId, Type Type)> ReadComClasses(Assembly? assembly)
    {
        if (assembly is null)
        {
            yield break;
        }

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsClass ||
                type.IsAbstract ||
                type.GetCustomAttribute<ComVisibleAttribute>()?.Value != true ||
                type.GetCustomAttribute<GuidAttribute>() is not { } guid ||
                !Guid.TryParse(guid.Value, out var classId))
            {
                continue;
            }

            yield return (classId, type);
        }
    }

    /// <summary>
    /// Counts a served object and arranges to notice when it goes away. The client releases a
    /// proxy, not the object, so nothing else tells the server that the object is gone; the
    /// tracker lives exactly as long as the object it is attached to.
    /// </summary>
    internal static void TrackServedObject(object instance)
    {
        lock (Sync)
        {
            _objectCount++;
            _served = true;
        }

        ServedObjects.Add(instance, new ServedObjectTracker());
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, ServedObjectTracker>
        ServedObjects = new();

    private sealed class ServedObjectTracker
    {
        ~ServedObjectTracker() => ObjectReleased();
    }

    internal static void ObjectReleased()
    {
        lock (Sync)
        {
            _objectCount = Math.Max(0, _objectCount - 1);
        }
    }

    internal static void LockServer(bool locked)
    {
        lock (Sync)
        {
            _lockCount = locked ? _lockCount + 1 : Math.Max(0, _lockCount - 1);
            if (locked)
            {
                _served = true;
            }
        }
    }

    private const uint WmQuit = 0x0012;
    private const uint PeekRemove = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int x;
        public int y;
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        ref Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint dwRegister);

    [DllImport("ole32.dll")]
    private static extern int CoResumeClassObjects();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(
        out NativeMessage lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint wRemoveMsg);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr DispatchMessage(ref NativeMessage lpMsg);
}

/// <summary>
/// The class factory COM asks for when a client activates a class of this server. It counts the
/// objects it hands out so the server knows when it may exit.
///
/// Both this interface and its implementation are public on purpose: the CLR builds a COM callable
/// wrapper only for public types, and an internal one is simply invisible to COM. The symptom is
/// E_NOINTERFACE when COM asks the registered class object for IClassFactory -- the server starts,
/// registers, and still cannot serve anything.
/// </summary>
[ComVisible(true)]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[SupportedOSPlatform("windows")]
public interface IVBClassFactory
{
    [PreserveSig]
    int CreateInstance(
        [MarshalAs(UnmanagedType.IUnknown)] object? outer,
        ref Guid interfaceId,
        out IntPtr instance);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool locked);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
[SupportedOSPlatform("windows")]
public sealed class VBComClassFactory : IVBClassFactory
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int EFail = unchecked((int)0x80004005);

    private readonly Type _type;

    public VBComClassFactory(Type type) => _type = type;

    public int CreateInstance(object? outer, ref Guid interfaceId, out IntPtr instance)
    {
        instance = IntPtr.Zero;
        if (outer is not null)
        {
            // Aggregation is not part of the VB6 contract, and saying so is better than handing
            // back an object that ignores the outer unknown.
            return ClassENoAggregation;
        }

        object created;
        try
        {
            created = Activator.CreateInstance(_type)
                ?? throw new InvalidOperationException("The class could not be created.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return EFail;
        }

        VBComLocalServer.TrackServedObject(created);

        var unknown = Marshal.GetIUnknownForObject(created);
        try
        {
            return Marshal.QueryInterface(unknown, in interfaceId, out instance);
        }
        finally
        {
            Marshal.Release(unknown);
        }
    }

    public int LockServer(bool locked)
    {
        VBComLocalServer.LockServer(locked);
        return 0;
    }

}
