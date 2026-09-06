using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace VB6.Runtime;

/// <summary>
/// Coordinates the lifetime of generated classes that carry <c>Class_Terminate</c>.
///
/// VB6 counts references and terminates the moment the last one goes. This runtime has a collector
/// instead, so the emitted class carries a finalizer — and a finalizer is not a promise: the CLR
/// does not run pending finalizers at process exit, so a program whose objects survive to the end
/// never saw its cleanup code run. Measured, not derived: a class whose <c>Class_Terminate</c>
/// prints stayed silent for every case — explicit <c>Set x = Nothing</c>, scope exit, reassignment
/// and program end alike.
///
/// Generated stores now report their ownership changes here: a newly constructed or returned
/// object transfers its one reference into its destination, while an alias retains the source
/// before it releases the old destination. A COM activation is adopted by the same storage
/// protocol: its RCW keeps one runtime reference until the last VB6 storage owner leaves, while a
/// borrowed RCW is kept alive through an explicit IUnknown reference without invalidating a host
/// owned wrapper. The weak register remains the last line of defence for an object that escaped an
/// uninstrumented boundary, and drains such instances at process exit. It is deliberately not
/// used as evidence that an uninstrumented storage form has VB6 timing.
/// </summary>
public static class VBObjectLifetime
{
    // The emitted method carries the mangled name; the plain one is kept as a fallback so a
    // change to the mangling shows up as a failing test rather than as silence.
    private static readonly string[] TerminatorNames = ["__vb6_Class_Terminate", "Class_Terminate"];

    private static readonly object Gate = new();
    private static readonly List<WeakReference<object>> Live = [];
    private static readonly ConditionalWeakTable<object, LifetimeState> States = [];
    private static readonly Dictionary<Type, MethodInfo?> Terminators = [];
    private static readonly Dictionary<(Type Type, string Name), FieldInfo> Fields = [];
    private static readonly Dictionary<Type, FieldInfo[]> InstanceFields = [];

    private static bool _drainInstalled;
    private static int _suppressPendingTerminators;
    private static int _pruneThreshold = 64;

    /// <summary>
    /// Records an instance that carries a terminator. Called from the generated constructor of a
    /// class with <c>Class_Terminate</c>; a class without one is not registered and pays nothing.
    /// </summary>
    public static void Register(object? instance)
    {
        if (instance is null)
        {
            return;
        }

        lock (Gate)
        {
            // Construction creates one owned reference. The generated New/store sequence moves
            // it into the first destination; copying an existing reference calls Replace instead.
            States.GetValue(instance, static _ => new LifetimeState());
            Live.Add(new WeakReference<object>(instance));

            if (Live.Count >= _pruneThreshold)
            {
                // A weak reference whose target is gone is dead weight, and a long-running program
                // creates a lot of them. Pruning on a doubling threshold keeps the register
                // proportional to what is actually alive rather than to what was ever created.
                Live.RemoveAll(static entry => !entry.TryGetTarget(out _));
                _pruneThreshold = Math.Max(64, Live.Count * 2);
            }

            if (!_drainInstalled)
            {
                _drainInstalled = true;
                AppDomain.CurrentDomain.ProcessExit += static (_, _) => RunPendingTerminators();
            }
        }
    }

    /// <summary>
    /// Runs an instance's terminator unless it has already run. Both routes to Terminate come
    /// through here — the emitted finalizer and the shutdown drain — so an object that the
    /// collector reached first is not terminated a second time at exit, and the other way round.
    /// </summary>
    public static void RunTerminator(object? instance)
    {
        if (instance is null || !TryBeginTerminate(instance))
        {
            return;
        }

        InvokeTerminator(instance);
    }

    /// <summary>
    /// Records another generated storage owner. An array retains the objects in all of its
    /// elements for a copied descriptor; a generated class increments its own counter, including
    /// one emitted by a referenced project that shares this runtime. A COM object obtains an
    /// explicit IUnknown hold while VB6 storage refers to it.
    /// </summary>
    public static void Retain(object? instance)
    {
        if (instance is IVBObjectLifetimeContainer container)
        {
            container.RetainObjectReferences();
            return;
        }

        if (instance is null || !TryGetLifetimeState(instance, out var state))
        {
            return;
        }

        if (state.IsComObject)
        {
            if (OperatingSystem.IsWindows())
            {
                RetainComObject(instance, state);
            }
            return;
        }

        if (Volatile.Read(ref state.Terminating) != 0)
        {
            return;
        }

        Interlocked.Increment(ref state.References);
    }

    /// <summary>
    /// Drops one generated storage owner. Array storage releases its elements; for a generated
    /// class, reaching zero calls <c>Class_Terminate</c> synchronously. A tracked COM activation
    /// releases its RCW at the same boundary. This makes alias and <c>Set ... = Nothing</c> timing
    /// observable instead of leaving it to the GC.
    /// </summary>
    public static void Release(object? instance)
    {
        if (instance is IVBObjectLifetimeContainer container)
        {
            container.ReleaseObjectReferences();
            return;
        }

        if (instance is null || !States.TryGetValue(instance, out var state))
        {
            return;
        }

        if (state.IsComObject)
        {
            if (OperatingSystem.IsWindows())
            {
                ReleaseComObject(instance, state);
            }
            return;
        }

        if (Volatile.Read(ref state.Terminating) != 0)
        {
            return;
        }

        var remaining = Interlocked.Decrement(ref state.References);
        if (remaining == 0)
        {
            RunTerminator(instance);
        }
    }

    /// <summary>
    /// Replaces one borrowed source value in a storage slot. The incoming value is retained before
    /// the outgoing one is released, so <c>Set value = value</c> never terminates a live object.
    /// The result is the value that the emitter writes into its typed destination.
    /// </summary>
    public static object? Replace(object? current, object? replacement)
    {
        Retain(replacement);
        Release(current);
        return replacement;
    }

    /// <summary>
    /// Replaces a slot with an already-owned value, such as <c>New C</c> or a generated function
    /// return. Its reference moves into the destination instead of being retained a second time.
    /// </summary>
    public static object? Transfer(object? current, object? replacement)
    {
        Release(current);
        return replacement;
    }

    /// <summary>
    /// Replaces a slot with a freshly activated COM object. Unlike a generated function result,
    /// this raw RCW has not yet entered VB6 storage, so its activation reference becomes the
    /// destination owner.
    /// </summary>
    public static object? TransferComActivation(object? current, object? replacement)
    {
        AdoptComActivation(replacement);
        Release(current);
        return replacement;
    }

    /// <summary>
    /// Marks a freshly activated COM value as owned by its first VB6 storage destination. Generated
    /// objects and values returned from generated procedures already carry a storage reference,
    /// so callers must use this only for the direct <c>New</c>, <c>CreateObject</c> or
    /// <c>GetObject</c> runtime result.
    /// </summary>
    public static void AdoptComActivation(object? instance)
    {
        if (OperatingSystem.IsWindows())
        {
            AdoptComObject(instance);
        }
    }

    /// <summary>
    /// Replaces a Variant slot after its value-copy operation. Array values arrive as fresh
    /// independent storage whose elements were retained while copying; scalars and objects remain
    /// borrowed. Selecting the path from the runtime value keeps both cases correct.
    /// </summary>
    public static object? ReplaceCopiedVariant(object? current, object? replacement) =>
        replacement is IVBArray ? Transfer(current, replacement) : Replace(current, replacement);

    /// <summary>
    /// Replaces a generated class field with a borrowed source value. Reflection is used only at
    /// this boundary so the emitted field remains strongly typed; it lets the runtime retain the
    /// incoming value before it releases a self-referential outgoing value.
    /// </summary>
    public static void ReplaceField(object instance, string fieldName, object? replacement)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var field = GetField(instance.GetType(), fieldName);
        var current = field.GetValue(instance);
        Retain(replacement);
        field.SetValue(instance, replacement);
        Release(current);
    }

    /// <summary>Moves an already-owned value, such as New or a function result, into a class field.</summary>
    public static void TransferField(object instance, string fieldName, object? replacement)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var field = GetField(instance.GetType(), fieldName);
        var current = field.GetValue(instance);
        field.SetValue(instance, replacement);
        Release(current);
    }

    /// <summary>Moves a direct COM activation into a generated class field.</summary>
    public static void TransferComActivationField(object instance, string fieldName, object? replacement)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var field = GetField(instance.GetType(), fieldName);
        var current = field.GetValue(instance);
        AdoptComActivation(replacement);
        field.SetValue(instance, replacement);
        Release(current);
    }

    /// <summary>Variant-field form of <see cref="ReplaceCopiedVariant"/>.</summary>
    public static void ReplaceCopiedVariantField(object instance, string fieldName, object? replacement)
    {
        if (replacement is IVBArray)
        {
            TransferField(instance, fieldName, replacement);
            return;
        }

        ReplaceField(instance, fieldName, replacement);
    }

    /// <summary>
    /// Runs the terminators still outstanding, most recently created first. Nesting usually
    /// follows creation order, so the reverse order tears an object down before the objects it
    /// was built from.
    /// </summary>
    public static void RunPendingTerminators()
    {
        if (Volatile.Read(ref _suppressPendingTerminators) != 0)
        {
            return;
        }

        List<object> pending = [];
        lock (Gate)
        {
            for (var index = Live.Count - 1; index >= 0; index--)
            {
                if (Live[index].TryGetTarget(out var instance) && TryBeginTerminate(instance))
                {
                    pending.Add(instance);
                }
            }

            Live.Clear();
        }

        foreach (var instance in pending)
        {
            // The object is being terminated here and now, so the finalizer has nothing left to
            // do. Suppressing it also keeps the collector from racing this loop during shutdown.
            GC.SuppressFinalize(instance);
            InvokeTerminator(instance);
        }
    }

    private static bool TryBeginTerminate(object instance)
    {
        var state = States.GetValue(instance, static _ => new LifetimeState());
        return Interlocked.Exchange(ref state.Terminating, 1) == 0;
    }

    private static void InvokeTerminator(object instance)
    {
        MethodInfo? terminator;
        lock (Gate)
        {
            var type = instance.GetType();
            if (!Terminators.TryGetValue(type, out terminator))
            {
                terminator = null;
                foreach (var name in TerminatorNames)
                {
                    terminator = type.GetMethod(
                        name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        binder: null,
                        types: Type.EmptyTypes,
                        modifiers: null);
                    if (terminator is not null)
                    {
                        break;
                    }
                }

                Terminators[type] = terminator;
            }
        }

        if (terminator is null)
        {
            return;
        }

        try
        {
            terminator.Invoke(instance, null);
        }
        catch (TargetInvocationException)
        {
            // Teardown is the wrong moment to take the process down. VB6 runs terminators while
            // the program is already ending, and an error there cannot be handled by code that
            // has stopped running -- and on the finalizer thread an escaping exception would kill
            // the process outright, which no VB6 program does.
        }

        // An event connection owns its sink. Once either end terminates, detach every matching
        // subscription before fields are released so a stale handler cannot retain or invoke a
        // terminated generated class.
        try
        {
            VBEvents.UnsubscribeObject(instance);
        }
        catch (Exception)
        {
            // A host or an already-released COM wrapper may reject detaching during shutdown;
            // object teardown still has to complete.
        }

        // VB6 keeps member references alive while Class_Terminate executes and releases them
        // afterwards. This also handles a class that is itself the final owner of another class.
        ReleaseInstanceFields(instance);
    }

    private static FieldInfo GetField(Type type, string name)
    {
        lock (Gate)
        {
            if (Fields.TryGetValue((type, name), out var field))
            {
                return field;
            }

            field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(type.FullName, name);
            Fields.Add((type, name), field);
            return field;
        }
    }

    /// <summary>
    /// Prevents the process-exit fallback from running class terminators after VB6's abrupt
    /// <c>End</c> statement. Normal scope cleanup remains responsible for orderly termination.
    /// </summary>
    public static void SuppressPendingTerminatorsForEnd() =>
        Interlocked.Exchange(ref _suppressPendingTerminators, 1);

    private static bool TryGetLifetimeState(object instance, out LifetimeState state)
    {
        if (States.TryGetValue(instance, out state!))
        {
            return true;
        }

        if (!OperatingSystem.IsWindows() || !Marshal.IsComObject(instance))
        {
            state = null!;
            return false;
        }

        state = States.GetValue(instance, static _ => LifetimeState.CreateBorrowedComObject());
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void AdoptComObject(object? instance)
    {
        if (instance is null || !OperatingSystem.IsWindows() || !Marshal.IsComObject(instance))
        {
            return;
        }

        var state = States.GetValue(instance, static _ => LifetimeState.CreateBorrowedComObject());
        if (!state.IsComObject)
        {
            return;
        }

        lock (state)
        {
            if (state.References == 0)
            {
                state.ComIdentity = Marshal.GetIUnknownForObject(instance);
            }

            state.References++;
            state.OwnedRcwReferences++;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RetainComObject(object instance, LifetimeState state)
    {
        lock (state)
        {
            if (state.References == 0)
            {
                state.ComIdentity = Marshal.GetIUnknownForObject(instance);
            }

            state.References++;
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReleaseComObject(object instance, LifetimeState state)
    {
        IntPtr identity = IntPtr.Zero;
        var ownedRcwReferences = 0;
        lock (state)
        {
            if (state.References <= 0)
            {
                return;
            }

            state.References--;
            if (state.References != 0)
            {
                return;
            }

            identity = state.ComIdentity;
            state.ComIdentity = IntPtr.Zero;
            ownedRcwReferences = state.OwnedRcwReferences;
            state.OwnedRcwReferences = 0;
        }

        if (identity != IntPtr.Zero)
        {
            _ = Marshal.Release(identity);
        }

        // Only an activation adopted by Transfer owns an RCW reference. Borrowed objects retain
        // an IUnknown while VB6 storage refers to them, but their host-owned RCW stays valid.
        for (var index = 0; index < ownedRcwReferences; index++)
        {
            try
            {
                _ = Marshal.ReleaseComObject(instance);
            }
            catch (InvalidComObjectException)
            {
                break;
            }
        }
    }

    private static void ReleaseInstanceFields(object instance)
    {
        FieldInfo[] fields;
        lock (Gate)
        {
            var type = instance.GetType();
            if (!InstanceFields.TryGetValue(type, out fields!))
            {
                fields = type
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => !field.FieldType.IsValueType && field.FieldType != typeof(string))
                    .ToArray();
                InstanceFields.Add(type, fields);
            }
        }

        foreach (var field in fields)
        {
            var value = field.GetValue(instance);
            if (value is null ||
                (value is not IVBObjectLifetimeContainer && !States.TryGetValue(value, out _)))
            {
                continue;
            }

            // A terminated object has no observable fields. Clearing first breaks the managed
            // CLR edge before Release can synchronously run the nested object's terminator.
            field.SetValue(instance, null);
            Release(value);
        }
    }

    private sealed class LifetimeState
    {
        // The constructor's result has one owner until New transfers it into generated storage.
        public int References = 1;
        public int Terminating;
        public bool IsComObject;
        public IntPtr ComIdentity;
        public int OwnedRcwReferences;

        public static LifetimeState CreateBorrowedComObject() => new()
        {
            References = 0,
            IsComObject = true
        };
    }
}
