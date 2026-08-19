using System.Threading;

namespace VB6.Semantics;

/// <summary>
/// Supplies module/project-specific VB6 named types to the existing binder without making type
/// lookup global. UDT identities and lightweight aliases such as Long-backed Enums share one
/// scoped lookup table. AsyncLocal keeps concurrent compilations isolated and nested scopes merge
/// with their parent so an Enum alias scope remains visible while a module-specific UDT scope is active.
/// </summary>
public static class UserDefinedTypeLookupScope
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, TypeSymbol>?> CurrentTypes = new();

    public static IDisposable Push(IReadOnlyDictionary<string, UserDefinedTypeSymbol> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return PushTypes(types.Select(entry =>
            new KeyValuePair<string, TypeSymbol>(entry.Key, entry.Value)));
    }

    public static IDisposable PushAliases(IReadOnlyDictionary<string, TypeSymbol> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        return PushTypes(types);
    }

    internal static TypeSymbol? Lookup(string name)
    {
        var types = CurrentTypes.Value;
        return types is not null && types.TryGetValue(name, out var type)
            ? type
            : null;
    }

    private static IDisposable PushTypes(IEnumerable<KeyValuePair<string, TypeSymbol>> types)
    {
        var previous = CurrentTypes.Value;
        var merged = new Dictionary<string, TypeSymbol>(StringComparer.OrdinalIgnoreCase);
        if (previous is not null)
        {
            foreach (var entry in previous)
            {
                merged[entry.Key] = entry.Value;
            }
        }

        foreach (var entry in types)
        {
            merged[entry.Key] = entry.Value;
        }

        CurrentTypes.Value = merged;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, TypeSymbol>? _previous;
        private bool _disposed;

        public Scope(IReadOnlyDictionary<string, TypeSymbol>? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentTypes.Value = _previous;
            _disposed = true;
        }
    }
}
