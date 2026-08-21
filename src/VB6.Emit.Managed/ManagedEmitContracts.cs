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

/// <summary>
/// Stable source-document input used by Portable PDB emission. The path is the logical path stored
/// in the PDB, not an absolute build-machine path. Checksum is SHA-256 over the exact source bytes.
/// </summary>
public sealed record ManagedSourceDocument(
    string FilePath,
    ImmutableArray<byte> Checksum);

public sealed record ManagedEmitOptions(
    string AssemblyName,
    ManagedOutputKind OutputKind = ManagedOutputKind.Application,
    ManagedPlatform Platform = ManagedPlatform.AnyCpu,
    string? PdbPath = null,
    bool EmitPortablePdb = true)
{
    public ImmutableArray<ManagedSourceDocument> SourceDocuments { get; init; } =
        ImmutableArray<ManagedSourceDocument>.Empty;
}

public sealed record ManagedEmitDiagnostic(string Code, string Message);

public sealed record ManagedEmitResult(
    bool Success,
    ImmutableArray<ManagedEmitDiagnostic> Diagnostics,
    byte[]? PeImage,
    byte[]? PdbImage);
