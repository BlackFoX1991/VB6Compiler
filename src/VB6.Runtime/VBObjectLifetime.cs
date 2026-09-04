using System.Reflection;
using System.Runtime.CompilerServices;

namespace VB6.Runtime;

/// <summary>
/// Makes the promise that <c>Class_Terminate</c> runs at all.
///
/// VB6 counts references and terminates the moment the last one goes. This runtime has a collector
/// instead, so the emitted class carries a finalizer — and a finalizer is not a promise: the CLR
/// does not run pending finalizers at process exit, so a program whose objects survive to the end
/// never saw its cleanup code run. Measured, not derived: a class whose <c>Class_Terminate</c>
/// prints stayed silent for every case — explicit <c>Set x = Nothing</c>, scope exit, reassignment
/// and program end alike.
///
/// So this type keeps a weak register of every instance that has a terminator and drains it when
/// the process ends, newest first. What it deliberately does <b>not</b> do is guess when a
/// reference was the last one. Firing Terminate early runs a program's cleanup on a live object,
/// and that is far worse than firing it late; the deterministic timing needs a real reference
/// count, which is an architecture decision rather than a gap here. The register only closes the
/// difference between "late" and "never".
/// </summary>
public static class VBObjectLifetime
{
    // The emitted method carries the mangled name; the plain one is kept as a fallback so a
    // change to the mangling shows up as a failing test rather than as silence.
    private static readonly string[] TerminatorNames = ["__vb6_Class_Terminate", "Class_Terminate"];

    private static readonly object Gate = new();
    private static readonly List<WeakReference<object>> Live = [];
    private static readonly ConditionalWeakTable<object, StrongBox<int>> States = [];
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
            States.GetValue(instance, static _ => new StrongBox<int>(0));
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
        var state = States.GetValue(instance, static _ => new StrongBox<int>(0));
        return Interlocked.Exchange(ref state.Value, 1) == 0;
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
}
