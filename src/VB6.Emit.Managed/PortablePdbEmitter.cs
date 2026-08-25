using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using VB6.IR;

namespace VB6.Emit.Managed;

/// <summary>
/// Emits the Portable PDB tables that can be derived from lowered IR without consulting VB6
/// syntax. Sequence points are added in the source-mapping slice; documents and user-visible local
/// scopes already live here so the PDB is a first-class deterministic backend artifact.
/// </summary>
public static class PortablePdbEmitter
{
    // Portable PDB well-known GUIDs.
    private static readonly Guid Sha256DocumentHashAlgorithm =
        new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid VisualBasicLanguage =
        new("3a12d0b8-c26c-11d0-b442-00a0244a1dd2");

    public static byte[] Emit(
        IrProgram program,
        byte[] peImage,
        ManagedEmitOptions options,
        IReadOnlyDictionary<IrProcedure, ImmutableArray<ManagedSequencePoint>>? sequencePoints = null)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(peImage);
        ArgumentNullException.ThrowIfNull(options);

        using var peStream = new MemoryStream(peImage, writable: false);
        using var peReader = new PEReader(peStream);
        var peMetadata = peReader.GetMetadataReader();
        var procedures = EnumerateProcedures(program).ToImmutableArray();
        if (peMetadata.MethodDefinitions.Count != procedures.Length)
        {
            throw new InvalidOperationException(
                $"Portable PDB method plan mismatch: PE has {peMetadata.MethodDefinitions.Count} methods, " +
                $"IR has {procedures.Length}.");
        }

        var pdbMetadata = new MetadataBuilder();
        var documents = EmitDocuments(pdbMetadata, options.SourceDocuments);
        var procedureDocuments = BuildProcedureDocumentMap(program, documents);

        // Portable PDB requires MethodDebugInformation to use the same row numbers as MethodDef.
        foreach (var procedure in procedures)
        {
            var document = procedureDocuments.TryGetValue(procedure, out var handle) ? handle : default;
            pdbMetadata.AddMethodDebugInformation(
                document,
                EncodeSequencePoints(pdbMetadata, procedure, document, sequencePoints));
        }

        EmitLocalScopes(pdbMetadata, peReader, peMetadata, procedures);

        var entryPoint = ResolveEntryPoint(program, procedures);
        var pdbBuilder = new PortablePdbBuilder(
            pdbMetadata,
            ReadTypeSystemRowCounts(peMetadata),
            entryPoint,
            DeterministicContentId);
        var pdbBlob = new BlobBuilder();
        pdbBuilder.Serialize(pdbBlob);
        return pdbBlob.ToArray();
    }

    private static Dictionary<string, DocumentHandle> EmitDocuments(
        MetadataBuilder metadata,
        ImmutableArray<ManagedSourceDocument> sourceDocuments)
    {
        var result = new Dictionary<string, DocumentHandle>(StringComparer.OrdinalIgnoreCase);
        foreach (var document in sourceDocuments
                     .OrderBy(document => NormalizePath(document.FilePath), StringComparer.OrdinalIgnoreCase))
        {
            var path = NormalizePath(document.FilePath);
            if (result.ContainsKey(path))
            {
                continue;
            }

            var handle = metadata.AddDocument(
                metadata.GetOrAddDocumentName(path),
                metadata.GetOrAddGuid(Sha256DocumentHashAlgorithm),
                metadata.GetOrAddBlob(document.Checksum),
                metadata.GetOrAddGuid(VisualBasicLanguage));
            result.Add(path, handle);
        }

        return result;
    }

    /// <summary>
    /// Writes one method's statement starts in the Portable PDB sequence-point encoding: an
    /// initial absolute offset and position, then deltas. Every statement of a VB6 program sits on
    /// one line, so the line delta within a point is always zero and only the column range varies.
    /// </summary>
    private static BlobHandle EncodeSequencePoints(
        MetadataBuilder metadata,
        IrProcedure procedure,
        DocumentHandle document,
        IReadOnlyDictionary<IrProcedure, ImmutableArray<ManagedSequencePoint>>? sequencePoints)
    {
        if (document.IsNil ||
            sequencePoints is null ||
            !sequencePoints.TryGetValue(procedure, out var points) ||
            points.IsDefaultOrEmpty)
        {
            return default;
        }

        var writer = new BlobBuilder();

        // Header: the local signature of the method. Locals are described by the method body
        // itself here, so the standalone signature row is absent.
        writer.WriteCompressedInteger(0);

        var previousOffset = 0;
        var previousLine = 0;
        var previousColumn = 0;
        var first = true;
        foreach (var point in points)
        {
            // Portable PDB counts lines and columns from one; LinePosition counts from zero.
            var startLine = point.Lines.Start.Line + 1;
            var startColumn = point.Lines.Start.Character + 1;
            var endLine = point.Lines.End.Line + 1;
            var endColumn = point.Lines.End.Character + 1;
            if (endLine < startLine || (endLine == startLine && endColumn <= startColumn))
            {
                // A zero-width or reversed range is not a valid sequence point. One column is the
                // smallest range that still points at the right place.
                endLine = startLine;
                endColumn = startColumn + 1;
            }

            writer.WriteCompressedInteger(first ? point.IlOffset : point.IlOffset - previousOffset);
            writer.WriteCompressedInteger(endLine - startLine);
            if (endLine == startLine)
            {
                writer.WriteCompressedInteger(endColumn - startColumn);
            }
            else
            {
                writer.WriteCompressedSignedInteger(endColumn - startColumn);
            }

            if (first)
            {
                writer.WriteCompressedInteger(startLine);
                writer.WriteCompressedInteger(startColumn);
                first = false;
            }
            else
            {
                writer.WriteCompressedSignedInteger(startLine - previousLine);
                writer.WriteCompressedSignedInteger(startColumn - previousColumn);
            }

            previousOffset = point.IlOffset;
            previousLine = startLine;
            previousColumn = startColumn;
        }

        return metadata.GetOrAddBlob(writer);
    }

    private static Dictionary<IrProcedure, DocumentHandle> BuildProcedureDocumentMap(
        IrProgram program,
        IReadOnlyDictionary<string, DocumentHandle> documents)
    {
        var result = new Dictionary<IrProcedure, DocumentHandle>(ReferenceEqualityComparer.Instance);
        foreach (var module in program.Modules)
        {
            if (module.SourcePath is null ||
                !documents.TryGetValue(NormalizePath(module.SourcePath), out var document))
            {
                continue;
            }

            foreach (var procedure in module.Procedures)
            {
                result[procedure] = document;
            }
        }

        foreach (var @class in program.ClassDefinitions)
        {
            var document = @class.Methods
                .Select(method => method.Blocks.SelectMany(block => block.Instructions)
                    .Select(instruction => instruction.SourceLocation?.FilePath)
                    .FirstOrDefault(path => path is not null))
                .FirstOrDefault(path => path is not null);
            if (document is null || !documents.TryGetValue(NormalizePath(document), out var handle))
            {
                continue;
            }

            foreach (var procedure in @class.Methods)
            {
                result[procedure] = handle;
            }
        }

        return result;
    }

    private static void EmitLocalScopes(
        MetadataBuilder metadata,
        PEReader peReader,
        MetadataReader peMetadata,
        ImmutableArray<IrProcedure> procedures)
    {
        var nextLocalVariableRow = 1;
        for (var index = 0; index < procedures.Length; index++)
        {
            var procedure = procedures[index];
            var methodHandle = MetadataTokens.MethodDefinitionHandle(index + 1);
            var method = peMetadata.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0)
            {
                continue;
            }

            var ilLength = peReader.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()?.Length ?? 0;
            var userLocals = procedure.Locals
                .Where(local => !local.IsCompilerGenerated)
                .OrderBy(local => local.Id)
                .ToImmutableArray();

            var firstVariable = default(LocalVariableHandle);
            if (!userLocals.IsDefaultOrEmpty)
            {
                firstVariable = MetadataTokens.LocalVariableHandle(nextLocalVariableRow);
                foreach (var local in userLocals)
                {
                    metadata.AddLocalVariable(
                        LocalVariableAttributes.None,
                        local.Id,
                        metadata.GetOrAddString(local.Name));
                    nextLocalVariableRow++;
                }
            }

            // Keep a scope even when the procedure has no user locals: debuggers need the method
            // start and length to recognize the complete VB6 procedure boundary.
            metadata.AddLocalScope(
                methodHandle,
                default,
                firstVariable,
                default,
                0,
                ilLength);
        }
    }

    private static ImmutableArray<int> ReadTypeSystemRowCounts(MetadataReader metadata)
    {
        var counts = ImmutableArray.CreateBuilder<int>(64);
        for (var index = 0; index < 64; index++)
        {
            try
            {
                counts.Add(metadata.GetTableRowCount((TableIndex)index));
            }
            catch (ArgumentOutOfRangeException)
            {
                counts.Add(0);
            }
        }

        return counts.MoveToImmutable();
    }

    private static MethodDefinitionHandle ResolveEntryPoint(
        IrProgram program,
        ImmutableArray<IrProcedure> procedures)
    {
        if (program.EntryPoint is null)
        {
            return default;
        }

        for (var index = 0; index < procedures.Length; index++)
        {
            if (ReferenceEquals(procedures[index], program.EntryPoint))
            {
                return MetadataTokens.MethodDefinitionHandle(index + 1);
            }
        }

        throw new InvalidOperationException("IR entry point is not part of the emitted method plan.");
    }

    private static IEnumerable<IrProcedure> EnumerateProcedures(IrProgram program)
    {
        foreach (var type in program.TypeDefinitions)
        {
            foreach (var method in type.Methods)
            {
                yield return method;
            }
        }

        foreach (var @class in program.ClassDefinitions)
        {
            foreach (var method in @class.Methods)
            {
                yield return method;
            }
        }

        foreach (var module in program.Modules)
        {
            foreach (var procedure in module.Procedures)
            {
                yield return procedure;
            }
        }
    }

    private static BlobContentId DeterministicContentId(IEnumerable<Blob> content)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var blob in content)
        {
            var bytes = blob.GetBytes();
            if (bytes.Array is not null)
            {
                hash.AppendData(bytes.Array, bytes.Offset, bytes.Count);
            }
        }

        return BlobContentId.FromHash(hash.GetHashAndReset());
    }

    internal static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return Path.IsPathRooted(normalized) ? Path.GetFileName(normalized) : normalized.TrimStart('/');
    }
}
