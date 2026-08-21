using System.Collections.Immutable;

namespace VB6.Emit.Managed;

public enum ManagedOutputKind
{
    Application,
    Library
}

public enum ManagedPlatform
{
    AnyCpu,
    X86,
    X64
}

public sealed record ManagedEmitOptions(
    string AssemblyName,
    ManagedOutputKind OutputKind = ManagedOutputKind.Application,
    ManagedPlatform Platform = ManagedPlatform.AnyCpu,
    string? PdbPath = null,
    bool EmitPortablePdb = true);

public sealed record ManagedEmitDiagnostic(string Code, string Message);

public sealed record ManagedEmitResult(
    bool Success,
    ImmutableArray<ManagedEmitDiagnostic> Diagnostics,
    byte[]? PeImage,
    byte[]? PdbImage);
