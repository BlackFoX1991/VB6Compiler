using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using VB6.Compiler;
using VB6.IR;

namespace VB6.Emit.Managed.Tests;

[TestClass]
public sealed class PortablePdbEmitterTests
{
    [TestMethod]
    public void Emit_ContainsSourceDocumentChecksumAndUserLocals()
    {
        const string source = """
            Sub Main()
                Dim total As Long
                total = 42
                Debug.Print total
            End Sub
            """;
        var program = Lower(source);
        var checksum = ImmutableArray.CreateRange(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var options = new ManagedEmitOptions("PortablePdb", EmitPortablePdb: true)
        {
            SourceDocuments = ImmutableArray.Create(new ManagedSourceDocument("Module1.bas", checksum))
        };
        var peResult = new ManagedEmitter().Emit(program, options);
        Assert.IsTrue(peResult.Success, string.Join(Environment.NewLine, peResult.Diagnostics));

        var pdb = PortablePdbEmitter.Emit(program, peResult.PeImage!, options);
        using var stream = new MemoryStream(pdb, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();

        Assert.AreEqual(1, reader.Documents.Count);
        var document = reader.GetDocument(reader.Documents.Single());
        Assert.AreEqual("Module1.bas", reader.GetString(document.Name));
        CollectionAssert.AreEqual(checksum.ToArray(), reader.GetBlobBytes(document.Hash));
        Assert.AreEqual(1, reader.MethodDebugInformation.Count);
        Assert.AreEqual(1, reader.LocalScopes.Count);
        Assert.AreEqual(1, reader.LocalVariables.Count);
        var local = reader.GetLocalVariable(reader.LocalVariables.Single());
        Assert.AreEqual("total", reader.GetString(local.Name));
    }

    [TestMethod]
    public void Emit_IsDeterministicForSamePeAndSourceDocuments()
    {
        const string source = """
            Sub Main()
                Dim value As Long
                value = 1
            End Sub
            """;
        var program = Lower(source);
        var options = new ManagedEmitOptions("PortablePdbDeterministic", EmitPortablePdb: true)
        {
            SourceDocuments = ImmutableArray.Create(new ManagedSourceDocument(
                "Module1.bas",
                ImmutableArray.CreateRange(SHA256.HashData(Encoding.UTF8.GetBytes(source)))))
        };
        var pe = new ManagedEmitter().Emit(program, options);
        Assert.IsTrue(pe.Success, string.Join(Environment.NewLine, pe.Diagnostics));

        var first = PortablePdbEmitter.Emit(program, pe.PeImage!, options);
        var second = PortablePdbEmitter.Emit(program, pe.PeImage!, options);

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void Emit_ContainsProcedureWideScopeWithoutUserLocals()
    {
        const string source = """
            Sub Main()
                Debug.Print 1
            End Sub
            """;
        var program = Lower(source);
        var options = new ManagedEmitOptions("PortablePdbProcedureScope", EmitPortablePdb: true)
        {
            SourceDocuments = ImmutableArray.Create(new ManagedSourceDocument(
                "Module1.bas",
                ImmutableArray.CreateRange(SHA256.HashData(Encoding.UTF8.GetBytes(source)))))
        };
        var peResult = new ManagedEmitter().Emit(program, options);
        Assert.IsTrue(peResult.Success, string.Join(Environment.NewLine, peResult.Diagnostics));

        var pdb = PortablePdbEmitter.Emit(program, peResult.PeImage!, options);
        using var stream = new MemoryStream(pdb, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();

        var scope = reader.GetLocalScope(reader.LocalScopes.Single());
        Assert.AreEqual(0, scope.StartOffset);
        Assert.IsTrue(scope.Length > 0);
        Assert.AreEqual(0, scope.GetLocalVariables().Count);
    }

    private static IrProgram Lower(string source)
    {
        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        return IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
    }

    /// <summary>
    /// The point of debug information: a debugger can map IL back to the statement that produced
    /// it. Reading the sequence points back proves the encoding, not just that a blob was written.
    /// </summary>
    [TestMethod]
    public void Emit_MapsEveryStatementToItsSourceLine()
    {
        const string source = """
            Sub Main()
                Dim total As Long
                total = 42
                Debug.Print total
            End Sub
            """;
        var program = Lower(source);
        var checksum = ImmutableArray.CreateRange(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var options = new ManagedEmitOptions("SequencePoints", EmitPortablePdb: true)
        {
            SourceDocuments = ImmutableArray.Create(new ManagedSourceDocument("Module1.bas", checksum))
        };
        var peResult = new ManagedEmitter().Emit(program, options);
        Assert.IsTrue(peResult.Success, string.Join(Environment.NewLine, peResult.Diagnostics));

        var pdb = PortablePdbEmitter.Emit(program, peResult.PeImage!, options, peResult.SequencePoints);
        using var stream = new MemoryStream(pdb, writable: false);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
        var reader = provider.GetMetadataReader();

        var debugInformation = reader.GetMethodDebugInformation(reader.MethodDebugInformation.Single());
        var points = debugInformation.GetSequencePoints().ToArray();

        // A Dim without an initializer produces no code, so the points are the two statements
        // that do: the assignment and the Debug.Print, in source order and each on its own line.
        Assert.AreEqual(2, points.Length);
        CollectionAssert.AreEqual(new[] { 3, 4 }, points.Select(point => point.StartLine).ToArray());
        Assert.IsFalse(points.Any(point => point.IsHidden));
        Assert.IsTrue(points.All(point => point.EndLine == point.StartLine));
        Assert.IsTrue(points.All(point => point.EndColumn > point.StartColumn));
        CollectionAssert.AreEqual(
            points.Select(point => point.Offset).OrderBy(offset => offset).ToArray(),
            points.Select(point => point.Offset).ToArray());
    }
}
