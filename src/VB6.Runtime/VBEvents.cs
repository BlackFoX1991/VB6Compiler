using System.Reflection;

namespace VB6.Runtime;

/// <summary>
/// Managed event storage for emitted VB6 class instances. The compiler keeps event identity in IR;
/// this hub supplies a host-facing subscription contract without baking .NET delegate signatures
/// into generated class metadata.
/// </summary>
public static class VBEvents
{
    private static readonly object Sync = new();
    private static readonly Dictionary<object, Dictionary<string, List<Action<object?[]>>>> Sinks =
        new(ReferenceEqualityComparer.Instance);

    public static void Subscribe(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (Sync)
        {
            if (!Sinks.TryGetValue(source, out var events))
            {
                events = new Dictionary<string, List<Action<object?[]>>>(StringComparer.OrdinalIgnoreCase);
                Sinks.Add(source, events);
            }

            if (!events.TryGetValue(eventName, out var handlers))
            {
                handlers = new List<Action<object?[]>>();
                events.Add(eventName, handlers);
            }

            handlers.Add(handler);
        }
    }

    public static void Unsubscribe(
        object source,
        string eventName,
        Action<object?[]> handler)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(handler);

        lock (Sync)
        {
            if (!Sinks.TryGetValue(source, out var events) ||
                !events.TryGetValue(eventName, out var handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                events.Remove(eventName);
            }

            if (events.Count == 0)
            {
                Sinks.Remove(source);
            }
        }
    }

    public static void SubscribeMethod(
        object? source,
        string eventName,
        object target,
        string methodName)
    {
        if (source is null)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        var method = target.GetType().GetMethod(
                         methodName,
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     ?? target.GetType().GetMethod(
                         "__vb6_" + Mangle(methodName),
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     ?? throw new MissingMethodException(target.GetType().FullName, methodName);
        Subscribe(source, eventName, arguments => method.Invoke(target, arguments));
    }

    public static void Raise(object source, string eventName, object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(arguments);

        Action<object?[]>[] handlers;
        lock (Sync)
        {
            handlers = Sinks.TryGetValue(source, out var events) &&
                       events.TryGetValue(eventName, out var registered)
                ? registered.ToArray()
                : Array.Empty<Action<object?[]>>();
        }

        foreach (var handler in handlers)
        {
            handler(arguments);
        }
    }

    private static string Mangle(string name) =>
        new(name.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
}
