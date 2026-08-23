namespace VB6.Runtime;

/// <summary>
/// Memory intrinsics whose exact contract depends on the selected native ABI. The managed runtime
/// keeps explicit failure semantics until the x86/x64 backend supplies stable addresses and UDT
/// layout operations.
/// </summary>
public static class VBMemory
{
    public static int VarPtr(object? value) =>
        throw new PlatformNotSupportedException("VarPtr requires a native VB6 memory backend.");

    public static int ObjPtr(object? value) =>
        throw new PlatformNotSupportedException("ObjPtr requires a native COM/native object backend.");

    public static int StrPtr(string? value) =>
        throw new PlatformNotSupportedException("StrPtr requires a native string memory backend.");

    public static void LSet(object? target, object? source) =>
        throw new PlatformNotSupportedException("LSet requires native UDT layout semantics.");
}
