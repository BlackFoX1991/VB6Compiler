using System.Reflection;
using System.Runtime.CompilerServices;

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
/// before it releases the old destination. The weak register remains the last line of defence for
/// an object that escaped an uninstrumented boundary, and drains such instances at process exit.
/// It is deliberately not used as evidence that an uninstrumented storage form has VB6 timing.
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

    private static bool _drainInstalled;
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
    /// Records another owner for an instance that was registered by a generated constructor.
    /// Runtime and external COM objects are intentionally ignored: their ownership is governed by
    /// their own contracts, not by a managed Class_Terminate counter.
    /// </summary>
    public static void Retain(object? instance)
    {
        if (instance is null || !States.TryGetValue(instance, out var state) ||
            Volatile.Read(ref state.Terminating) != 0)
        {
            return;
        }

        Interlocked.Increment(ref state.References);
    }

    /// <summary>
    /// Drops one generated storage owner. Reaching zero calls Class_Terminate synchronously,
    /// which makes alias and Set ... = Nothing timing observable instead of leaving it to the GC.
    /// </summary>
    public static void Release(object? instance)
    {
        if (instance is null || !States.TryGetValue(instance, out var state))
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
    /// Runs the terminators still outstanding, most recently created first. Nesting usually
    /// follows creation order, so the reverse order tears an object down before the objects it
    /// was built from.
    /// </summary>
    public static void RunPendingTerminators()
    {
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
    }

    private sealed class LifetimeState
    {
        // The constructor's result has one owner until New transfers it into generated storage.
        public int References = 1;
        public int Terminating;
    }
}
