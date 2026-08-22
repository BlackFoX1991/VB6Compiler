namespace VB6.Runtime;

/// <summary>
/// One VB6 array dimension. Bounds are inclusive and may start at any signed 32-bit value. The
/// special 0..-1 bound is the zero-length shape used by Array() and ParamArray with no values.
/// </summary>
public readonly record struct VBArrayBound(int Lower, int Upper)
{
    public int Length
    {
        get
        {
            var length = checked((long)Upper - Lower + 1L);
            if (length == 0 && Lower == 0)
            {
                return 0;
            }

            if (length <= 0 || length > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Upper), "VB6 array bounds do not describe a valid dimension.");
            }

            return (int)length;
        }
    }
}

/// <summary>
/// Non-generic view used when an array is carried inside a VB6 Variant. The concrete element type
/// remains on <see cref="VBArray{T}"/>, while the Variant runtime only needs bounds and boxed
/// element access.
/// </summary>
public interface IVBArray
{
    int Rank { get; }
    int LBound(int dimension = 1);
    int UBound(int dimension = 1);
    object? GetObjectValue(int[] indices);
    void SetObjectValue(int[] indices, object? value);
}

/// <summary>
/// Runtime storage for a VB6 array. Unlike CLR arrays, each dimension preserves its explicit
/// lower bound so Option Base, LBound/UBound and ReDim can be implemented without losing VB6
/// semantics.
/// </summary>
public sealed class VBArray<T> : IVBArray
{
    private readonly VBArrayBound[] _bounds;
    private readonly T[] _items;

    public VBArray(params VBArrayBound[] bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (bounds.Length == 0)
        {
            throw new ArgumentException("A VB6 array must have at least one dimension.", nameof(bounds));
        }

        _bounds = bounds.ToArray();
        long totalLength = 1;
        foreach (var bound in _bounds)
        {
            totalLength = checked(totalLength * bound.Length);
            if (totalLength > int.MaxValue)
            {
                throw new OverflowException("VB6 array is too large for the current managed runtime representation.");
            }
        }

        _items = new T[(int)totalLength];
        InitializeElements();
    }

    public int Rank => _bounds.Length;
    public int Length => _items.Length;

    public int LBound(int dimension = 1) => GetBound(dimension).Lower;
    public int UBound(int dimension = 1) => GetBound(dimension).Upper;

    /// <summary>
    /// Returns the actual storage slot by reference. Besides ordinary reads/writes, this preserves
    /// VB6 ByRef semantics when an array element is passed to a procedure.
    /// </summary>
    public ref T this[params int[] indices] => ref _items[GetOffset(indices)];

    /// <summary>
    /// Returns one value by physical array order. The IR uses this for For Each so enumeration can
    /// be lowered to ordinary basic blocks without making IEnumerable an IR-level concept.
    /// </summary>
    public T GetValueAtFlatIndex(int index) => _items[index];

    /// <summary>
    /// Returns a writable reference by physical array order. Record I/O uses this when a dynamic
    /// array member has to be populated element by element after its descriptor is read.
    /// </summary>
    public ref T GetReferenceAtFlatIndex(int index) => ref _items[index];

    object? IVBArray.GetObjectValue(int[] indices) => this[indices];

    void IVBArray.SetObjectValue(int[] indices, object? value)
    {
        this[indices] = ConvertElement(value);
    }

    /// <summary>
    /// Reinitializes every element while preserving rank and bounds. This is the runtime operation
    /// used by VB6 <c>Erase</c> for fixed-size arrays. Dynamic-array Erase deallocates the variable
    /// itself and is emitted by the compiler instead.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_items);
        InitializeElements();
    }

    /// <summary>
    /// Creates independent array storage with the same VB6 rank and bounds. The optional element
    /// cloner lets generated UDT copy code recursively preserve value semantics when an element
    /// itself contains managed backing storage.
    /// </summary>
    public VBArray<T> Clone(Func<T, T>? elementCloner = null)
    {
        var clone = new VBArray<T>(_bounds);
        if (elementCloner is null)
        {
            Array.Copy(_items, clone._items, _items.Length);
            return clone;
        }

        for (var index = 0; index < _items.Length; index++)
        {
            clone._items[index] = elementCloner(_items[index]);
        }

        return clone;
    }

    /// <summary>
    /// Enumerates array values in VB array order. The rightmost dimension advances first, which is
    /// also the physical order used by the current storage mapping. Values are returned by value so
    /// a For Each control variable cannot accidentally alias an array slot by reference.
    /// </summary>
    public IEnumerable<T> EnumerateValues()
    {
        for (var index = 0; index < _items.Length; index++)
        {
            yield return _items[index];
        }
    }

    /// <summary>
    /// Implements the storage operation required by VB6 <c>ReDim Preserve</c>. VB6 permits
    /// Preserve to change only the upper bound of the final dimension. Earlier dimensions, the
    /// rank, and the final dimension's lower bound must remain unchanged.
    /// </summary>
    public VBArray<T> ReDimPreserve(params VBArrayBound[] bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        if (bounds.Length != Rank)
        {
            throw new ArgumentException(
                "ReDim Preserve cannot change the number of array dimensions.",
                nameof(bounds));
        }

        for (var dimension = 0; dimension < Rank - 1; dimension++)
        {
            if (bounds[dimension] != _bounds[dimension])
            {
                throw new ArgumentException(
                    "ReDim Preserve can change only the upper bound of the final dimension.",
                    nameof(bounds));
            }
        }

        var lastDimension = Rank - 1;
        if (bounds[lastDimension].Lower != _bounds[lastDimension].Lower)
        {
            throw new ArgumentException(
                "ReDim Preserve cannot change an array lower bound.",
                nameof(bounds));
        }

        var resized = new VBArray<T>(bounds);
        var oldLastLength = _bounds[lastDimension].Length;
        var newLastLength = bounds[lastDimension].Length;
        var preservedLastLength = Math.Min(oldLastLength, newLastLength);
        var rows = 1;
        for (var dimension = 0; dimension < Rank - 1; dimension++)
        {
            rows = checked(rows * _bounds[dimension].Length);
        }

        if (oldLastLength == 0 || newLastLength == 0)
        {
            return resized;
        }

        for (var row = 0; row < rows; row++)
        {
            Array.Copy(
                _items,
                row * oldLastLength,
                resized._items,
                row * newLastLength,
                preservedLastLength);
        }

        return resized;
    }

    private void InitializeElements()
    {
        // Variable-length VB6 Strings have "" as their initial value, unlike CLR string arrays,
        // whose default element value is null. Other currently supported VB6 element types map to
        // their CLR/default struct zero value.
        if (typeof(T) == typeof(string))
        {
            Array.Fill(_items, (T)(object)string.Empty);
        }
    }

    private static T ConvertElement(object? value)
    {
        if (value is null)
        {
            return default!;
        }

        if (value is T typed)
        {
            return typed;
        }

        if (typeof(T) == typeof(string))
        {
            return (T)(object)Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
        }

        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private VBArrayBound GetBound(int dimension)
    {
        if (dimension < 1 || dimension > Rank)
        {
            throw new IndexOutOfRangeException($"Array dimension {dimension} is outside the VB6 rank 1..{Rank}.");
        }

        return _bounds[dimension - 1];
    }

    private int GetOffset(int[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Length != Rank)
        {
            throw new IndexOutOfRangeException($"Expected {Rank} array subscript(s), got {indices.Length}.");
        }

        // The exact physical layout is intentionally encapsulated here. Language semantics only
        // depend on bounds and indexing; later native/COM interop can adapt layout in this one place.
        var offset = 0;
        var stride = 1;
        for (var dimension = Rank - 1; dimension >= 0; dimension--)
        {
            var bound = _bounds[dimension];
            var index = indices[dimension];
            if (index < bound.Lower || index > bound.Upper)
            {
                throw new IndexOutOfRangeException(
                    $"Subscript {index} is outside [{bound.Lower}..{bound.Upper}] for dimension {dimension + 1}.");
            }

            offset = checked(offset + checked((index - bound.Lower) * stride));
            stride = checked(stride * bound.Length);
        }

        return offset;
    }
}

/// <summary>Late-bound array operations for Variant values.</summary>
public static class VBArrayOperations
{
    public static bool IsAllocated(object? value) => value is IVBArray;

    public static int LBound(object? value, int dimension = 1) => GetArray(value).LBound(dimension);

    public static int UBound(object? value, int dimension = 1) => GetArray(value).UBound(dimension);

    public static object? GetElement(object? value, int[] indices) => GetArray(value).GetObjectValue(indices);

    public static void SetElement(object? value, int[] indices, object? element) =>
        GetArray(value).SetObjectValue(indices, element);

    private static IVBArray GetArray(object? value) => value switch
    {
        IVBArray array => array,
        _ => throw new InvalidOperationException("The Variant does not contain an array.")
    };
}

/// <summary>
/// Storage helpers for fixed-size arrays declared inside a user-defined type.
///
/// A VB6 <c>Type</c> member such as <c>Values(1 To 2) As Long</c> has its bounds fixed by the
/// declaration, but the storage cannot be created with the enclosing value: a UDT is a struct, so
/// every default instance - including each element of an array of that type - starts with a null
/// member. The array is therefore created on first access, against the declared bounds.
/// </summary>
public static class VBTypeStorage
{
    /// <summary>
    /// Returns the array held by a fixed UDT array member, creating it on first access. The
    /// storage is passed by reference so the created array is kept in the member itself rather
    /// than in a copy of the enclosing value.
    /// </summary>
    public static VBArray<T> EnsureArray<T>(ref VBArray<T>? storage, params VBArrayBound[] bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return storage ??= new VBArray<T>(bounds);
    }

    /// <summary>
    /// Copies a fixed UDT array member. Assigning a VB6 user-defined type copies it by value, and
    /// that includes its arrays - the CLR struct copy only duplicates the reference, which would
    /// leave both values sharing one array.
    /// </summary>
    public static VBArray<T> CopyArray<T>(VBArray<T>? source, params VBArrayBound[] bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        return source is null ? new VBArray<T>(bounds) : source.Clone();
    }

    /// <summary>
    /// Reads a <c>String * n</c> member. Such a member always has exactly n characters, so an
    /// untouched one reads as n spaces rather than as the CLR null a default struct starts with.
    /// </summary>
    public static string ReadFixedString(string? storage, int length) =>
        storage is null ? new string(' ', length) : WriteFixedString(storage, length);

    /// <summary>
    /// Stores into a <c>String * n</c> member. VB6 keeps the declared width: a longer value is
    /// truncated, a shorter one padded with spaces.
    /// </summary>
    public static string WriteFixedString(string? value, int length)
    {
        var text = value ?? string.Empty;
        return text.Length >= length ? text[..length] : text.PadRight(length);
    }
}
