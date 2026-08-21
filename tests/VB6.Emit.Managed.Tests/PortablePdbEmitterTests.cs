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

    private static IrProgram Lower(string source)
    {
        var analysis = VBCompilation.Create(source, "Module1.bas").Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));
        return IrLowerer.Lower(new[]
        {
            new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!)
        });
    }
}
