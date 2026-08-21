using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using VB6.Compiler;
using VB6.IR;

namespace VB6.Emit.Managed.Tests;

[TestClass]
public sealed class ManagedEmitterTests
{
    [TestMethod]
    public void Emit_CreatesIlOnlyAnyCpuApplicationWithManagedEntryPoint()
    {
        var program = Lower("""
            Sub Main()
                Debug.Print 10
            End Sub
            """);

        var result = new ManagedEmitter().Emit(program, new ManagedEmitOptions(
            "Smoke",
            EmitPortablePdb: false));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.IsNotNull(result.PeImage);
        using var stream = new MemoryStream(result.PeImage);
        using var pe = new PEReader(stream);
        Assert.IsTrue((pe.PEHeaders.CorHeader!.Flags & CorFlags.ILOnly) != 0);
        Assert.IsTrue((pe.PEHeaders.CorHeader.Flags & CorFlags.Requires32Bit) == 0);
        Assert.IsTrue((pe.PEHeaders.CorHeader.Flags & CorFlags.Prefers32Bit) == 0);
        Assert.AreNotEqual(0, pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress);
    }

    [TestMethod]
    public void Emit_IsDeterministicForSameInput()
    {
        var program = Lower("""
            Sub Main()
                Dim x As Long
                x = 40 + 2
                Debug.Print x
            End Sub
            """);
        var emitter = new ManagedEmitter();
        var options = new ManagedEmitOptions("Deterministic", EmitPortablePdb: false);

        var first = emitter.Emit(program, options);
        var second = emitter.Emit(program, options);

        Assert.IsTrue(first.Success, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.IsTrue(second.Success, string.Join(Environment.NewLine, second.Diagnostics));
        CollectionAssert.AreEqual(first.PeImage!, second.PeImage!);
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
