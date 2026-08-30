using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using VB6.Compiler;
using VB6.IR;
using VB6.Runtime;
using VB6.Semantics;

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
        using var stream = new MemoryStream(result.PeImage!);
        using var pe = new PEReader(stream);
        var corHeader = pe.PEHeaders.CorHeader!;
        Assert.IsTrue((corHeader.Flags & CorFlags.ILOnly) != 0);
        Assert.IsTrue((corHeader.Flags & CorFlags.Requires32Bit) == 0);
        Assert.IsTrue((corHeader.Flags & CorFlags.Prefers32Bit) == 0);
        Assert.AreNotEqual(0, corHeader.EntryPointTokenOrRelativeVirtualAddress);
    }

    [TestMethod]
    [DataRow(ManagedPlatform.X86, Machine.I386, true)]
    [DataRow(ManagedPlatform.X64, Machine.Amd64, false)]
    public void Emit_UsesTheSelectedPeArchitecture(
        ManagedPlatform platform,
        Machine machine,
        bool requires32Bit)
    {
        var program = Lower("""
            Sub Main()
                Debug.Print 1
            End Sub
            """);

        var result = new ManagedEmitter().Emit(program, new ManagedEmitOptions(
            "SelectedPlatform",
            Platform: platform,
            EmitPortablePdb: false));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        using var stream = new MemoryStream(result.PeImage!);
        using var pe = new PEReader(stream);
        Assert.AreEqual(machine, pe.PEHeaders.CoffHeader.Machine);
        Assert.AreEqual(requires32Bit, pe.PEHeaders.CorHeader!.Flags.HasFlag(CorFlags.Requires32Bit));
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

    [TestMethod]
    public void Emit_UsesTypedComparisonHelperForScalarOperands()
    {
        var program = Lower("""
            Sub Main()
                Dim left As Long
                Dim right As Long
                left = 40
                right = 42
                If left < right Then
                    Debug.Print 1
                End If
            End Sub
            """);

        var result = new ManagedEmitter().Emit(program, new ManagedEmitOptions(
            "TypedComparison",
            EmitPortablePdb: false));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        using var stream = new MemoryStream(result.PeImage!);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var runtimeMethods = metadata.MemberReferences
            .Select(handle =>
            {
                var reference = metadata.GetMemberReference(handle);
                var parent = reference.Parent.Kind == HandleKind.TypeReference
                    ? metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)reference.Parent).Name)
                    : string.Empty;
                return (Parent: parent, Name: metadata.GetString(reference.Name));
            })
            .ToArray();

        Assert.IsTrue(
            runtimeMethods.Any(method => method.Parent == nameof(VBOperators) && method.Name == nameof(VBOperators.CompareInt32)),
            "Scalar comparisons must use the typed CompareInt32 helper.");
        Assert.IsFalse(
            runtimeMethods.Any(method => method.Parent == nameof(VBOperators) && method.Name == nameof(VBOperators.Equal)),
            "Scalar comparisons must not fall back to Equal(object, object).");
    }

    [TestMethod]
    public void Emit_AnnotatesAssemblyWithCompatibilityProfile()
    {
        var analysis = VBCompilation.Create(
            "Sub Main()\n    Debug.Print 1\nEnd Sub\n",
            "Module1.bas",
            new VBCompilationOptions
            {
                CompatibilityProfile = VBCompatibilityProfile.VB6Sp6
            }).Analyze();
        Assert.IsTrue(analysis.Success, string.Join(Environment.NewLine, analysis.Diagnostics));

        var program = IrLowerer.Lower(
            new[] { new IrModuleInput("Module1", "Module1.bas", analysis.SemanticModel!) },
            compatibilityProfile: VBCompatibilityProfile.VB6Sp6);
        var result = new ManagedEmitter().Emit(program, new ManagedEmitOptions(
            "ProfileMetadata",
            EmitPortablePdb: false,
            Platform: ManagedPlatform.AnyCpu));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var assembly = Assembly.Load(result.PeImage!);
        var profile = assembly.GetCustomAttribute<VBCompatibilityProfileAttribute>();
        Assert.IsNotNull(profile);
        Assert.AreEqual("VB6Sp6", profile!.Profile);
    }

    [TestMethod]
    public void Emit_AnnotatesDeclareArraysWithTheirAutomationElementTypes()
    {
        var program = Lower("""
            Private Declare Function Native Lib "native" () As Date()
            Private Declare Sub NativeArrays Lib "native" (ByRef dates() As Date, ByRef amounts() As Currency)

            Sub Main()
            End Sub
            """);

        var result = new ManagedEmitter().Emit(program, new ManagedEmitOptions(
            "SafeArrayMetadata",
            EmitPortablePdb: false));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var assembly = Assembly.Load(result.PeImage!);
        var method = assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Single(candidate => candidate.Name == "NativeArrays");
        var parameters = method.GetParameters();

        Assert.AreEqual(UnmanagedType.SafeArray, parameters[0].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(VarEnum.VT_DATE, parameters[0].GetCustomAttribute<MarshalAsAttribute>()?.SafeArraySubType);
        Assert.AreEqual(UnmanagedType.SafeArray, parameters[1].GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(VarEnum.VT_CY, parameters[1].GetCustomAttribute<MarshalAsAttribute>()?.SafeArraySubType);

        var returnMethod = assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Single(candidate => candidate.Name == "Native");
        Assert.AreEqual(UnmanagedType.SafeArray, returnMethod.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
        Assert.AreEqual(VarEnum.VT_DATE, returnMethod.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.SafeArraySubType);
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
    /// An IR node the backend does not handle is a known gap: it is reported as VB6E0001 with the
    /// construct named, and nothing escapes the emitter.
    /// </summary>
    [TestMethod]
    public void Emit_ReportsAnUnsupportedIrNodeAsAnEmitGap()
    {
        var program = Lower("""
            Sub Main()
                Debug.Print 10
            End Sub
            """);
        var broken = ReplaceEntryPointBody(program, new IrEvaluateInstruction(new UnsupportedExpression()));

        var result = new ManagedEmitter().Emit(broken, new ManagedEmitOptions("Gap", EmitPortablePdb: false));

        Assert.IsFalse(result.Success);
        var diagnostic = result.Diagnostics.Single();
        Assert.AreEqual("VB6E0001", diagnostic.Code);
        StringAssert.Contains(diagnostic.Message, nameof(UnsupportedExpression));
    }

    /// <summary>
    /// A defect inside the emitter is not a gap, and its bare message names nothing - so the
    /// report has to carry the exception type and where it came from.
    /// </summary>
    [TestMethod]
    public void Emit_ReportsAnEmitterDefectWithItsOrigin()
    {
        var program = Lower("""
            Sub Main()
                Debug.Print 10
            End Sub
            """);
        // A call to a procedure the program never defines: the emitter's own bookkeeping is
        // inconsistent, which is a defect rather than a construct it declines to emit.
        var missing = new ProcedureSymbol("Missing", ImmutableArray<ParameterSymbol>.Empty, null);
        var broken = ReplaceEntryPointBody(program, new IrEvaluateInstruction(
            new IrProcedureCallExpression(
                missing,
                ImmutableArray<IrCallArgument>.Empty,
                TypeSymbol.Long)));

        var result = new ManagedEmitter().Emit(broken, new ManagedEmitOptions("Defect", EmitPortablePdb: false));

        Assert.IsFalse(result.Success);
        var diagnostic = result.Diagnostics.Single();
        Assert.AreEqual("VB6E0003", diagnostic.Code);
        StringAssert.Contains(diagnostic.Message, nameof(InvalidOperationException));
        StringAssert.Contains(diagnostic.Message, "Missing");
    }

    private static IrProgram ReplaceEntryPointBody(IrProgram program, IrInstruction instruction)
    {
        var entryPoint = program.EntryPoint!;
        var block = entryPoint.Blocks[0] with
        {
            Instructions = ImmutableArray.Create(instruction)
        };
        var replaced = entryPoint with
        {
            Blocks = entryPoint.Blocks.SetItem(0, block)
        };
        var module = program.Modules[0];
        return program with
        {
            Modules = ImmutableArray.Create(module with
            {
                Procedures = module.Procedures.Replace(entryPoint, replaced)
            }),
            EntryPoint = replaced
        };
    }

    /// <summary>An IR expression no backend knows, standing in for a construct not yet emitted.</summary>
    private sealed record UnsupportedExpression() : IrExpression(TypeSymbol.Long);

}
