namespace VB6.Runtime;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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

    private static T ConvertElement(object? value) =>
        (T)VBArrayOperations.ConvertArrayElement(value, typeof(T))!;

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
    public static bool IsAllocated(object? value) => value is IVBArray or Array;

    public static object RequireAllocated(object? value) => value ??
        throw new InvalidOperationException("The array must be allocated before file data can be read into it.");

    /// <summary>
    /// Converts a CLR SAFEARRAY result into the compiler's bound-preserving array representation.
    /// COM dispatch returns <see cref="System.Array"/> even when the imported VB6 signature is a
    /// typed array, so the managed backend performs this conversion at the dynamic-call boundary.
    /// </summary>
    public static VBArray<T>? FromObject<T>(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is VBArray<T> typed)
        {
            return typed;
        }

        if (value is IVBArray vbArray)
        {
            return CopyArray<T>(vbArray, indices => vbArray.GetObjectValue(indices));
        }

        if (value is Array clrArray)
        {
            return CopyArray<T>(
                clrArray,
                indices => clrArray.GetValue(indices));
        }

        throw new InvalidCastException("The dynamic result does not contain a VB6 array.");
    }

    /// <summary>
    /// Copies a native SAFEARRAY result into an existing VB6 array when its shape is unchanged;
    /// otherwise returns the converted array as the new ByRef value.
    /// </summary>
    public static VBArray<T>? CopyBack<T>(VBArray<T>? target, object? value)
    {
        if (value is null)
        {
            return null;
        }

        var source = FromObject<T>(value)!;
        if (target is null || target.Rank != source.Rank)
        {
            return source;
        }

        for (var dimension = 1; dimension <= target.Rank; dimension++)
        {
            if (target.LBound(dimension) != source.LBound(dimension) ||
                target.UBound(dimension) != source.UBound(dimension))
            {
                return source;
            }
        }

        if (target.Length != 0)
        {
            var lowerBounds = Enumerable.Range(1, target.Rank).Select(target.LBound).ToArray();
            var upperBounds = Enumerable.Range(1, target.Rank).Select(target.UBound).ToArray();
            var indices = lowerBounds.ToArray();
            for (var offset = 0; offset < target.Length; offset++)
            {
                ((IVBArray)target).SetObjectValue(indices, ((IVBArray)source).GetObjectValue(indices));
                IncrementIndices(indices, lowerBounds, upperBounds);
            }
        }

        return target;
    }

    /// <summary>
    /// Creates a CLR array with the same bounds and physical element order as a VB6 array.
    /// COM event delegates use this representation at the SAFEARRAY boundary.
    /// </summary>
    public static Array? ToClrArray<T>(VBArray<T>? source)
    {
        if (source is null)
        {
            return null;
        }

        var lengths = new int[source.Rank];
        var lowerBounds = new int[source.Rank];
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            lowerBounds[dimension] = source.LBound(dimension + 1);
            lengths[dimension] = checked(source.UBound(dimension + 1) - lowerBounds[dimension] + 1);
        }

        var result = Array.CreateInstance(typeof(T), lengths, lowerBounds);
        if (result.Length == 0)
        {
            return result;
        }

        var upperBounds = new int[source.Rank];
        for (var dimension = 0; dimension < source.Rank; dimension++)
        {
            upperBounds[dimension] = source.UBound(dimension + 1);
        }

        var indices = lowerBounds.ToArray();
        for (var offset = 0; offset < source.Length; offset++)
        {
            result.SetValue(source.GetValueAtFlatIndex(offset), indices);
            IncrementIndices(indices, lowerBounds, upperBounds);
        }

        return result;
    }

    /// <summary>
    /// Creates the CLR array shape expected by a native SAFEARRAY descriptor. Native-width
    /// <c>LongPtr()</c> arrays use <c>VT_I4</c> on x86 and <c>VT_I8</c> on x64, while their
    /// compiler-facing storage remains <see cref="IntPtr"/>.
    /// </summary>
    public static Array? ToNativeSafeArray<T>(VBArray<T>? source, ushort safeArrayElementType)
    {
        if (source is null)
        {
            return null;
        }

        if (!VBComDispatch.TryCreateAutomationArray(
                source,
                (ushort)((ushort)VarEnum.VT_ARRAY | safeArrayElementType),
                out var result) ||
            result is null)
        {
            throw new InvalidOperationException(
                $"The VB6 array could not be converted to SAFEARRAY element type 0x{safeArrayElementType:X4}.");
        }

        return result;
    }

    private static VBArray<T> CopyArray<T>(IVBArray source, Func<int[], object?> getValue)
    {
        var bounds = new VBArrayBound[source.Rank];
        for (var dimension = 0; dimension < bounds.Length; dimension++)
        {
            bounds[dimension] = new VBArrayBound(
                source.LBound(dimension + 1),
                source.UBound(dimension + 1));
        }

        return CopyArray(new VBArray<T>(bounds), getValue);
    }

    private static VBArray<T> CopyArray<T>(Array source, Func<int[], object?> getValue)
    {
        var bounds = new VBArrayBound[source.Rank];
        for (var dimension = 0; dimension < bounds.Length; dimension++)
        {
            bounds[dimension] = new VBArrayBound(
                source.GetLowerBound(dimension),
                source.GetUpperBound(dimension));
        }

        return CopyArray(new VBArray<T>(bounds), getValue);
    }

    private static VBArray<T> CopyArray<T>(VBArray<T> target, Func<int[], object?> getValue)
    {
        if (target.Length == 0)
        {
            return target;
        }

        var lowerBounds = Enumerable.Range(1, target.Rank)
            .Select(target.LBound)
            .ToArray();
        var upperBounds = Enumerable.Range(1, target.Rank)
            .Select(target.UBound)
            .ToArray();
        var indices = lowerBounds.ToArray();
        for (var offset = 0; offset < target.Length; offset++)
        {
            ((IVBArray)target).SetObjectValue(indices, getValue(indices));
            IncrementIndices(indices, lowerBounds, upperBounds);
        }

        return target;
    }

    private static void IncrementIndices(int[] indices, int[] lowerBounds, int[] upperBounds)
    {
        for (var dimension = indices.Length - 1; dimension >= 0; dimension--)
        {
            if (indices[dimension] < upperBounds[dimension])
            {
                indices[dimension]++;
                return;
            }

            indices[dimension] = lowerBounds[dimension];
        }
    }

    public static int LBound(object? value, int dimension = 1) => value switch
    {
        IVBArray array => array.LBound(dimension),
        Array array => array.GetLowerBound(dimension - 1),
        _ => throw new InvalidOperationException("The Variant does not contain an array.")
    };

    public static int UBound(object? value, int dimension = 1) => value switch
    {
        IVBArray array => array.UBound(dimension),
        Array array => array.GetUpperBound(dimension - 1),
        _ => throw new InvalidOperationException("The Variant does not contain an array.")
    };

    public static object? GetElement(object? value, int[] indices) =>
        GetElement(value, indices.Cast<object?>().ToArray());

    public static object? GetElement(object? value, object?[] indices)
    {
        return value switch
        {
            IVBArray array => array.GetObjectValue(ToArrayIndices(indices)),
            Array array => array.GetValue(ToArrayIndices(indices)),
            _ => VBDynamicDispatch.GetDefaultMember(value, indices)
        };
    }

    /// <summary>
    /// Returns a writable reference for an element of a Variant() array. A Variant array created
    /// by Array(...) stores boxed values in VBArray&lt;object&gt;, which allows a managed ByRef call
    /// to update the original slot without a temporary write-back protocol.
    /// </summary>
    public static ref object? GetElementReference(object? value, int[] indices)
    {
        if (GetArray(value) is VBArray<object?> array)
        {
            return ref array[indices];
        }

        throw new InvalidOperationException(
            "A Variant array element is ByRef-addressable only when the runtime array stores Variant elements.");
    }

    public static void SetElement(object? value, int[] indices, object? element) =>
        SetElement(value, indices.Cast<object?>().ToArray(), element);

    public static void SetElement(object? value, object?[] indices, object? element)
    {
        if (value is IVBArray array)
        {
            array.SetObjectValue(ToArrayIndices(indices), element);
            return;
        }

        if (value is Array clrArray)
        {
            clrArray.SetValue(
                ConvertArrayElement(element, clrArray.GetType().GetElementType() ?? typeof(object)),
                ToArrayIndices(indices));
            return;
        }

        VBDynamicDispatch.SetDefaultMember(value, indices, element);
    }

    private static int[] ToArrayIndices(object?[] indices) =>
        indices.Select(index => VBConversions.ConvertCLng(index)).ToArray();

    private static object GetArray(object? value) => value switch
    {
        IVBArray array => array,
        Array array => array,
        _ => throw new InvalidOperationException("The Variant does not contain an array.")
    };

    internal static object? ConvertArrayElement(object? value, Type elementType)
    {
        if (value is null)
        {
            return elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
        }

        if (elementType == typeof(object) || elementType.IsInstanceOfType(value))
        {
            return value;
        }

        if (elementType == typeof(byte)) return VBConversions.CByte(value);
        if (elementType == typeof(short)) return VBConversions.CInt(value);
        if (elementType == typeof(int)) return VBConversions.CLng(value);
        if (elementType == typeof(long)) return VBConversions.CLngLng(value);
        if (elementType == typeof(ushort)) return VBConversions.CUShort(value);
        if (elementType == typeof(uint)) return VBConversions.CUInt(value);
        if (elementType == typeof(ulong)) return VBConversions.CULng(value);
        if (elementType == typeof(IntPtr)) return VBConversions.CLngPtr(value);
        if (elementType == typeof(float)) return VBConversions.CSng(value);
        if (elementType == typeof(double) && value is DateTime dateTime) return dateTime.ToOADate();
        if (elementType == typeof(double)) return VBConversions.CDbl(value);
        if (elementType == typeof(decimal)) return VBConversions.CDec(value);
        if (elementType == typeof(bool)) return VBConversions.CBool(value);
        if (elementType == typeof(string)) return VBConversions.CStr(value);
        if (elementType == typeof(VBCurrency)) return VBConversions.CCur(value);
        if (elementType == typeof(VBDateValue)) return new VBDateValue(VBConversions.CDbl(value));
        if (elementType == typeof(DateTime)) return DateTime.FromOADate(VBConversions.CDbl(value));
        if (elementType.IsEnum)
        {
            var underlying = ConvertArrayElement(value, Enum.GetUnderlyingType(elementType));
            return Enum.ToObject(elementType, underlying!);
        }

        return Convert.ChangeType(value, elementType, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Owns the native SAFEARRAY pointer used by a VB6 <c>Declare</c> array argument. VB6 passes an
/// array parameter as a pointer to the SAFEARRAY pointer, so the generated P/Invoke method takes
/// the address of the native pointer storage rather than the managed <see cref="VBArray{T}"/>.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class VBDeclareArrayBuffer : IDisposable
{
    private const int VariantSize = 16;
    private const int VariantDataOffset = 8;
    private readonly object? _target;
    private readonly ushort _expectedType;
    private readonly IntPtr _variant;
    private readonly IntPtr _pointerStorage;
    private bool _disposed;

    private VBDeclareArrayBuffer(object? value, ushort expectedType)
    {
        _target = value;
        _expectedType = expectedType;
        _variant = Marshal.AllocCoTaskMem(VariantSize);
        _pointerStorage = Marshal.AllocCoTaskMem(IntPtr.Size);
        ClearVariant();

        if (value is not null && !VBComDispatch.TryInitializeVariant(value, _variant, expectedType))
        {
            Dispose();
            throw new InvalidOperationException(
                $"The Declare array value could not be converted to SAFEARRAY type 0x{expectedType:X4}.");
        }

        if (value is null)
        {
            Marshal.WriteInt16(_variant, unchecked((short)expectedType));
            Marshal.WriteIntPtr(_variant, VariantDataOffset, IntPtr.Zero);
        }

        Marshal.WriteIntPtr(_pointerStorage, Marshal.ReadIntPtr(_variant, VariantDataOffset));
    }

    public static VBDeclareArrayBuffer Create<T>(VBArray<T>? value, ushort expectedType) =>
        new(value, expectedType);

    /// <summary>Returns the native SAFEARRAY** argument expected by a VB6 array Declare.</summary>
    public IntPtr GetNativeAddress()
    {
        ThrowIfDisposed();
        return _pointerStorage;
    }

    /// <summary>
    /// Converts the possibly replaced native SAFEARRAY back to a VB6 array and releases its
    /// native ownership. Existing arrays retain their identity when the shape is unchanged.
    /// </summary>
    public VBArray<T>? GetManagedArray<T>()
    {
        ThrowIfDisposed();
        try
        {
            var safeArray = Marshal.ReadIntPtr(_pointerStorage);
            if (safeArray == IntPtr.Zero)
            {
                return null;
            }

            Marshal.WriteInt16(_variant, unchecked((short)_expectedType));
            Marshal.WriteIntPtr(_variant, VariantDataOffset, safeArray);
            var value = Marshal.GetObjectForNativeVariant(_variant);
            return VBArrayOperations.CopyBack<T>(_target as VBArray<T>, value);
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (_variant != IntPtr.Zero)
            {
                var safeArray = _pointerStorage == IntPtr.Zero
                    ? IntPtr.Zero
                    : Marshal.ReadIntPtr(_pointerStorage);
                Marshal.WriteInt16(_variant, unchecked((short)_expectedType));
                Marshal.WriteIntPtr(_variant, VariantDataOffset, safeArray);
                VBComDispatch.ClearNativeVariant(_variant);
            }
        }
        finally
        {
            if (_pointerStorage != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_pointerStorage);
            }

            if (_variant != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(_variant);
            }
        }
    }

    ~VBDeclareArrayBuffer() => Dispose();

    private void ClearVariant()
    {
        Span<byte> empty = stackalloc byte[VariantSize];
        Marshal.Copy(empty.ToArray(), 0, _variant, VariantSize);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
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
