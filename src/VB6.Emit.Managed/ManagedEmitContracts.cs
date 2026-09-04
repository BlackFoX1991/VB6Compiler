using System.Collections.Immutable;
using VB6.IR;
using VB6.Syntax.Text;

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

    /// <summary>
    /// Resources linked into the emitted assembly. VB6 links a project resource file into the
    /// executable itself, so LoadResString reads from the running image rather than from a file
    /// that would have to be shipped and found beside it.
    /// </summary>
    public ImmutableArray<ManagedEmbeddedResource> EmbeddedResources { get; init; } =
        ImmutableArray<ManagedEmbeddedResource>.Empty;

    /// <summary>
    /// Emits COM-visible class identities and asks the artifact writer to produce the matching
    /// .NET COM host for library output. Application output cannot be exposed through comhost.
    /// </summary>
    public bool EnableComHosting { get; init; }

    /// <summary>
    /// Emits a side-by-side activation manifest next to the native COM host. This requires
    /// <see cref="EnableComHosting"/> and is available for Managed library output only.
    /// </summary>
    public bool EnableComManifest { get; init; }

    /// <summary>
    /// Includes the optional WinForms host contract in an executable Form build. The compiler
    /// core remains headless; this flag only applies to projects whose startup object is a Form.
    /// </summary>
    public bool EnableWinFormsHost { get; init; }
}

public sealed record ManagedEmitDiagnostic(string Code, string Message);

/// <summary>
/// One statement's start in a method body: the IL offset the statement's code begins at and the
/// source it was written as. This is what a debugger steps between, and only the emitter knows
/// the offset, so it has to travel out of the emit rather than be reconstructed from the image.
/// </summary>
public sealed record ManagedSequencePoint(
    int IlOffset,
    string FilePath,
    LinePositionSpan Lines);

public sealed record ManagedEmitResult(
    bool Success,
    ImmutableArray<ManagedEmitDiagnostic> Diagnostics,
    byte[]? PeImage,
    byte[]? PdbImage)
{
    /// <summary>
    /// Statement starts per emitted method, in IL order. Empty when the IR carried no source
    /// positions - a program lowered from synthesized input has nothing to point at.
    /// </summary>
    public ImmutableDictionary<IrProcedure, ImmutableArray<ManagedSequencePoint>> SequencePoints { get; init; } =
        ImmutableDictionary.Create<IrProcedure, ImmutableArray<ManagedSequencePoint>>(ReferenceEqualityComparer.Instance);
}

/// <summary>One resource linked into the emitted assembly.</summary>
public sealed record ManagedEmbeddedResource(string Name, ImmutableArray<byte> Content);
