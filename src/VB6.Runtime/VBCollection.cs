using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>Managed storage for the VB6 standard Collection object.</summary>
public sealed class VBCollection
{
    private readonly List<Entry> _items = new();
    private readonly Dictionary<string, int> _keys = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _items.Count;

    public static VBCollection Create() => new();

    /// <summary>Returns the current Collection order as a Variant-compatible array.</summary>
    public static VBArray<object> EnumerateValues(VBCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var values = new VBArray<object>(new VBArrayBound(0, collection._items.Count - 1));
        for (var index = 0; index < collection._items.Count; index++)
        {
            values[index] = collection._items[index].Value!;
        }

        return values;
    }

    public object? Item(object? index)
    {
        var position = ResolveIndex(index);
        return _items[position].Value;
    }

    /// <summary>
    /// VB6 deklariert Key, Before und After als optional. Ohne [Optional] lehnt der
    /// spaet gebundene Pfad einen Aufruf mit weniger als vier Argumenten ab, waehrend der
    /// typisierte Pfad ueber AddValue weiterhin alle vier uebergibt.
    /// </summary>
    public void Add(
        object? item,
        [Optional] object? key,
        [Optional] object? before,
        [Optional] object? after)
    {
        if (!VBVariants.IsMissing(before) && !VBVariants.IsMissing(after))
        {
            throw InvalidCollectionArgument("Only one of Before and After may be specified.");
        }

        var insertAt = _items.Count;
        if (!VBVariants.IsMissing(before))
        {
            insertAt = ResolveIndex(before, outOfRangeNumber: 5);
        }
        else if (!VBVariants.IsMissing(after))
        {
            insertAt = checked(ResolveIndex(after, outOfRangeNumber: 5) + 1);
        }

        string? normalizedKey = null;
        if (key is object nonNullKey && !VBVariants.IsMissing(nonNullKey))
        {
            normalizedKey = NormalizeKey(nonNullKey);
            if (_keys.ContainsKey(normalizedKey))
            {
                throw new VB6RuntimeErrorException(
                    457,
                    $"This key is already associated with an element of this collection: '{normalizedKey}'.");
            }
        }

        _items.Insert(insertAt, new Entry(item, normalizedKey));
        RebuildKeys();
    }

    public void Remove(object? index)
    {
        var position = ResolveIndex(index);
        _items.RemoveAt(position);
        RebuildKeys();
    }

    public static int CountValue(VBCollection collection) => collection.Count;

    public static object? ItemValue(VBCollection collection, object? index) => collection.Item(index);

    public static void AddValue(
        VBCollection collection,
        object? item,
        object? key,
        object? before,
        object? after) => collection.Add(item, key, before, after);

    public static void RemoveValue(VBCollection collection, object? index) => collection.Remove(index);

    /// <summary>
    /// Resolves a key or a one-based position to a storage index. VB6 separates the two failures:
    /// a numeric position outside the collection is 9 ("Subscript out of range"), while an unknown
    /// key is 5 ("Invalid procedure call or argument"). Only <c>Item</c> and <c>Remove</c> address
    /// an element, so only they report 9; <c>Add</c> passes <paramref name="outOfRangeNumber"/> 5
    /// for its Before/After position, which VB6 treats as an invalid argument to Add rather than a
    /// subscript.
    /// </summary>
    private int ResolveIndex(object? index, int outOfRangeNumber = 9)
    {
        if (index is string key)
        {
            if (!_keys.TryGetValue(key, out var keyedIndex))
            {
                throw InvalidCollectionArgument($"Collection key '{key}' was not found.");
            }

            return keyedIndex;
        }

        var oneBased = VBConversions.CLng(index);
        if (oneBased < 1 || oneBased > _items.Count)
        {
            throw new VB6RuntimeErrorException(
                outOfRangeNumber,
                $"Collection index {oneBased} is out of range.");
        }

        return oneBased - 1;
    }

    private static string NormalizeKey(object key) => VBConversions.CStr(key);

    private static VB6RuntimeErrorException InvalidCollectionArgument(string description) =>
        new(5, description);

    private void RebuildKeys()
    {
        _keys.Clear();
        for (var index = 0; index < _items.Count; index++)
        {
            var key = _items[index].Key;
            if (key is not null)
            {
                _keys.Add(key, index);
            }
        }
    }

    private sealed record Entry(object? Value, string? Key);
}
