using System.Threading;

namespace VB6.Semantics;

/// <summary>
/// Supplies the module-specific VB6 UDT type space to the existing binder without making type
/// lookup global. AsyncLocal keeps concurrent compilations isolated and restores nested scopes
/// deterministically when disposed.
/// </summary>
public static class UserDefinedTypeLookupScope
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, UserDefinedTypeSymbol>?> CurrentTypes = new();

    public static IDisposable Push(IReadOnlyDictionary<string, UserDefinedTypeSymbol> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        var previous = CurrentTypes.Value;
        CurrentTypes.Value = types;
        return new Scope(previous);
    }

    internal static UserDefinedTypeSymbol? Lookup(string name)
    {
        var types = CurrentTypes.Value;
        return types is not null && types.TryGetValue(name, out var type)
            ? type
            : null;
    }

    private sealed class Scope : IDisposable
    {
        private readonly IReadOnlyDictionary<string, UserDefinedTypeSymbol>? _previous;
        private bool _disposed;

        public Scope(IReadOnlyDictionary<string, UserDefinedTypeSymbol>? previous)
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
