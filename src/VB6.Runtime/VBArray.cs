namespace VB6.Runtime;

/// <summary>
/// One VB6 array dimension. Bounds are inclusive and may start at any signed 32-bit value.
/// </summary>
public readonly record struct VBArrayBound(int Lower, int Upper)
{
    public int Length
    {
        get
        {
            var length = checked((long)Upper - Lower + 1L);
            if (length <= 0 || length > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Upper), "VB6 array bounds do not describe a valid dimension.");
            }

            return (int)length;
        }
    }
}

/// <summary>
/// Runtime storage for a VB6 array. Unlike CLR arrays, each dimension preserves its explicit
/// lower bound so Option Base, LBound/UBound and ReDim can be implemented without losing VB6
/// semantics.
/// </summary>
public sealed class VBArray<T>
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
    }

    private VBArray(VBArrayBound[] bounds, T[] items)
    {
        _bounds = bounds;
        _items = items;
    }

    public static VBArray<T> FromValues(params T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new VBArray<T>(
            new[] { new VBArrayBound(0, values.Length - 1) },
            values.ToArray());
    }

    public int Rank => _bounds.Length;
    public int Length => _items.Length;

    public int LBound(int dimension = 1) => GetBound(dimension).Lower;
    public int UBound(int dimension = 1) => GetBound(dimension).Upper;

    public void Clear() => Array.Clear(_items);

    public VBArray<T> Clone(Func<T, T>? cloneElement = null)
    {
        var copy = new VBArray<T>(_bounds.ToArray(), new T[_items.Length]);
        for (var index = 0; index < _items.Length; index++)
        {
            var item = _items[index];
            copy._items[index] = cloneElement is null ? item : cloneElement(item);
        }

        return copy;
    }

    public IEnumerable<T> Values()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }

    public VBArray<T> ResizePreserve(params VBArrayBound[] newBounds)
    {
        ArgumentNullException.ThrowIfNull(newBounds);
        if (newBounds.Length != Rank)
        {
            throw new InvalidOperationException($"ReDim Preserve expected {Rank} dimension(s), got {newBounds.Length}.");
        }

        for (var dimension = 0; dimension < Rank - 1; dimension++)
        {
            var oldBound = _bounds[dimension];
            var newBound = newBounds[dimension];
            if (oldBound.Lower != newBound.Lower || oldBound.Upper != newBound.Upper)
            {
                throw new InvalidOperationException("ReDim Preserve may only change the last array dimension.");
            }
        }

        var resized = new VBArray<T>(newBounds);
        CopyOverlap(resized, new int[Rank], dimension: 0);
        return resized;
    }

    public T this[params int[] indices]
    {
        get => _items[GetOffset(indices)];
        set => _items[GetOffset(indices)] = value;
    }

    public ref T Element(params int[] indices) => ref _items[GetOffset(indices)];

    private VBArrayBound GetBound(int dimension)
    {
        if (dimension < 1 || dimension > Rank)
        {
            throw new IndexOutOfRangeException($"Array dimension {dimension} is outside the VB6 rank 1..{Rank}.");
        }

        return _bounds[dimension - 1];
    }

    private void CopyOverlap(VBArray<T> destination, int[] indices, int dimension)
    {
        if (dimension == Rank)
        {
            destination[indices] = this[indices];
            return;
        }

        var sourceBound = _bounds[dimension];
        var destinationBound = destination._bounds[dimension];
        var lower = Math.Max(sourceBound.Lower, destinationBound.Lower);
        var upper = Math.Min(sourceBound.Upper, destinationBound.Upper);
        for (var index = lower; index <= upper; index++)
        {
            indices[dimension] = index;
            CopyOverlap(destination, indices, dimension + 1);
        }
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
