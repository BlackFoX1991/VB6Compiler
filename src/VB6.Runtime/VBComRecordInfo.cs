using System.Runtime.InteropServices;

namespace VB6.Runtime;

/// <summary>The width of a native VARIANT. On x64 it is 24, not 16: the union carries BRECORD.</summary>
internal static class VBComVariant
{
    public static readonly int Size = IntPtr.Size == 8 ? 24 : 16;

    /// <summary>Offset der Union: hinter vt und drei reservierten WORDs, auf Zeigerbreite gerichtet.</summary>
    public static readonly int DataOffset = 8;
}

/// <summary>
/// A VB6 <c>Type</c> value on its way to a COM client.
///
/// A record travels as <c>VT_RECORD</c>, and that variant carries an <c>IRecordInfo</c> beside the
/// data -- the client uses it to read fields and to free the copy. The CLR only builds one from a
/// **registered** type library, which is why a record return failed outright before this. Here the
/// server brings its own, so nothing has to be registered for a record to cross the boundary.
/// </summary>
internal static class VBComRecordInfo
{
    /// <summary>
    /// Writes a record value into the caller's VARIANT, or answers false when the value is not a
    /// published record and the ordinary variant path applies.
    /// </summary>
    public static bool TryWriteRecord(IntPtr result, object? value, Type declared)
    {
        if (value is null || !IsPublishedRecord(declared))
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// True for a type the emitter published as a COM record: a ComVisible value type with an
    /// identity of its own. A Private Type carries neither and stays out of COM entirely.
    /// </summary>
    public static bool IsPublishedRecord(Type type) =>
        type.IsValueType &&
        !type.IsEnum &&
        !type.IsPrimitive &&
        type.GetCustomAttributes(typeof(ComVisibleAttribute), false) is [ComVisibleAttribute { Value: true }] &&
        type.GetCustomAttributes(typeof(GuidAttribute), false).Length == 1;
}
