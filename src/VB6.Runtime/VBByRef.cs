namespace VB6.Runtime;

/// <summary>
/// Storage for VB6 ByRef arguments that are not variables.
///
/// VB6 accepts a literal, an expression, or a function result where a ByRef parameter is declared.
/// It materializes a temporary, passes that by reference, and discards whatever the callee wrote
/// back - there is nowhere to write it. A parameter declared ByRef therefore stays ByRef for the
/// callee even when the caller had no variable to offer.
/// </summary>
public static class VBByRef
{
    /// <summary>
    /// Returns a reference to a fresh temporary holding <paramref name="value"/>.
    ///
    /// The storage is a single-element array so the reference stays valid for the duration of the
    /// call and is collected afterwards, which is what makes the discarded write-back correct
    /// rather than merely unobserved. A per-call allocation is deliberate: a shared slot would
    /// break recursion and nested calls, where several temporaries are live at once.
    /// </summary>
    public static ref T Temp<T>(T value)
    {
        var storage = new T[1];
        storage[0] = value;
        return ref storage[0];
    }
}
