using System.Collections.Immutable;
using System.Text;
using VB6.IR;
using VB6.Semantics;

namespace VB6.Emit.Llvm;

public enum LlvmArchitecture
{
    X86,
    X64
}

public sealed record LlvmEmitOptions(
    LlvmArchitecture Architecture,
    string ModuleName = "VB6Program");

public sealed record LlvmEmitDiagnostic(string Code, string Message);

public sealed record LlvmEmitResult(
    bool Success,
    string ModuleText,
    ImmutableArray<LlvmEmitDiagnostic> Diagnostics);

/// <summary>
/// Textual LLVM backend boundary for the native Windows targets. The first slice emits a valid
/// target module and rejects IR operations until their native ABI lowering is implemented; this
/// keeps x86/x64 selection and diagnostics testable without pretending that a managed operation
/// has native calling convention semantics.
/// </summary>
public sealed class LlvmEmitter
{
    public LlvmEmitResult Emit(IrProgram program, LlvmEmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = ImmutableArray.CreateBuilder<LlvmEmitDiagnostic>();
        foreach (var procedure in program.Modules.SelectMany(module => module.Procedures))
        {
            foreach (var instruction in procedure.Blocks.SelectMany(block => block.Instructions))
            {
                if (instruction is not IrNopInstruction)
                {
                    diagnostics.Add(new LlvmEmitDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for '{instruction.GetType().Name}' is not implemented yet."));
                }
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"; VB6 native module: {options.ModuleName}");
        builder.AppendLine($"target triple = \"{GetTargetTriple(options.Architecture)}\"");
        builder.AppendLine();
        builder.AppendLine("; Runtime ABI declarations are added as native operations graduate from the managed contract.");

        return new LlvmEmitResult(
            diagnostics.Count == 0,
            builder.ToString(),
            diagnostics.ToImmutable());
    }

    private static string GetTargetTriple(LlvmArchitecture architecture) => architecture switch
    {
        LlvmArchitecture.X86 => "i686-pc-windows-msvc",
        LlvmArchitecture.X64 => "x86_64-pc-windows-msvc",
        _ => throw new ArgumentOutOfRangeException(nameof(architecture))
    };
}
