using System.Collections.Immutable;
using System.Globalization;
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
/// Emits the first native LLVM scalar slice. Primitive values, local and parameter storage,
/// arithmetic/comparison runtime operations, returns and basic-block branches are represented
/// directly in LLVM IR. Complex VB6 values and non-scalar ABIs remain explicit diagnostics until
/// their native representation is defined instead of being silently treated as integers.
/// </summary>
public sealed class LlvmEmitter
{
    public LlvmEmitResult Emit(IrProgram program, LlvmEmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(options);

        var diagnostics = ImmutableArray.CreateBuilder<LlvmEmitDiagnostic>();
        var builder = new StringBuilder();
        builder.AppendLine($"; VB6 native module: {options.ModuleName}");
        builder.AppendLine($"target triple = \"{GetTargetTriple(options.Architecture)}\"");
        builder.AppendLine();
        EmitCheckedIntegerHelpers(builder);
        EmitCheckedFloatingHelpers(builder);

        if (!program.TypeDefinitions.IsDefaultOrEmpty || !program.ClassDefinitions.IsDefaultOrEmpty)
        {
            diagnostics.Add(new LlvmEmitDiagnostic(
                "VB6L0006",
                "Native LLVM lowering for user-defined and class types is not implemented yet."));
        }

        var globalSlots = EmitGlobals(program.Modules, builder, diagnostics, options.Architecture);

        foreach (var procedure in program.Modules.SelectMany(module => module.Procedures))
        {
            var emitter = new NativeProcedureEmitter(builder, diagnostics, options.Architecture, globalSlots);
            if (procedure.IsExternal)
            {
                emitter.EmitExternalDeclaration(procedure);
            }
            else
            {
                emitter.Emit(procedure);
            }
            builder.AppendLine();
        }

        if (!program.Modules.SelectMany(module => module.Procedures).Any())
        {
            builder.AppendLine("; Module contains no procedures.");
        }

        return new LlvmEmitResult(
            diagnostics.Count == 0,
            builder.ToString(),
            diagnostics.ToImmutable());
    }

    private static void EmitCheckedIntegerHelpers(StringBuilder builder)
    {
        builder.AppendLine("declare void @llvm.trap()");
        builder.AppendLine("declare { i64, i1 } @llvm.sadd.with.overflow.i64(i64, i64)");
        builder.AppendLine("declare { i64, i1 } @llvm.ssub.with.overflow.i64(i64, i64)");
        builder.AppendLine("declare { i64, i1 } @llvm.smul.with.overflow.i64(i64, i64)");
        builder.AppendLine("declare { i64, i1 } @llvm.uadd.with.overflow.i64(i64, i64)");
        builder.AppendLine("declare { i64, i1 } @llvm.usub.with.overflow.i64(i64, i64)");
        builder.AppendLine("declare { i64, i1 } @llvm.umul.with.overflow.i64(i64, i64)");
        builder.AppendLine();
        EmitCheckedIntegerHelper(builder, "__vb6_sdiv_checked_i64", "sdiv", checkSignedOverflow: true);
        EmitCheckedIntegerHelper(builder, "__vb6_srem_checked_i64", "srem", checkSignedOverflow: false);
        EmitCheckedIntegerHelper(builder, "__vb6_udiv_checked_i64", "udiv", checkSignedOverflow: false);
        EmitCheckedIntegerHelper(builder, "__vb6_urem_checked_i64", "urem", checkSignedOverflow: false);
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_sadd_checked_i64", "sadd");
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_ssub_checked_i64", "ssub");
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_smul_checked_i64", "smul");
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_uadd_checked_i64", "uadd");
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_usub_checked_i64", "usub");
        EmitCheckedIntegerArithmeticHelper(builder, "__vb6_umul_checked_i64", "umul");
        EmitCheckedIntegerConversionHelper(builder, "__vb6_sconvert_checked_i64", signed: true);
        EmitCheckedIntegerConversionHelper(builder, "__vb6_uconvert_checked_i64", signed: false);
        EmitCheckedCurrencyMultiplicationHelper(builder);
        EmitCheckedIntegerToCurrencyHelpers(builder);
        EmitCheckedCurrencyToIntegerHelper(builder);
        EmitCheckedFloatingIntegerConversionHelpers(builder);
        EmitCheckedIntegerNegationHelper(builder, "__vb6_sneg_checked_i64", signed: true);
        EmitCheckedIntegerNegationHelper(builder, "__vb6_uneg_checked_i64", signed: false);
    }

    private static void EmitCheckedIntegerHelper(
        StringBuilder builder,
        string name,
        string operation,
        bool checkSignedOverflow)
    {
        if (checkSignedOverflow)
        {
            builder.Append("define i64 @").Append(name).AppendLine("(i64 %left, i64 %right, i64 %min_value) {");
        }
        else
        {
            builder.Append("define i64 @").Append(name).AppendLine("(i64 %left, i64 %right) {");
        }
        builder.AppendLine("entry:");
        builder.AppendLine("  %is_zero = icmp eq i64 %right, 0");
        if (checkSignedOverflow)
        {
            builder.AppendLine("  br i1 %is_zero, label %trap, label %check_overflow");
            builder.AppendLine("check_overflow:");
            builder.AppendLine("  %is_min = icmp eq i64 %left, %min_value");
            builder.AppendLine("  %is_negative_one = icmp eq i64 %right, -1");
            builder.AppendLine("  %is_overflow = and i1 %is_min, %is_negative_one");
            builder.AppendLine("  br i1 %is_overflow, label %trap, label %divide");
        }
        else
        {
            builder.AppendLine("  br i1 %is_zero, label %trap, label %divide");
        }

        builder.AppendLine("divide:");
        builder.Append("  %result = ").Append(operation).AppendLine(" i64 %left, %right");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedFloatingCurrencyConversionHelper(StringBuilder builder)
    {
        builder.AppendLine("define i64 @__vb6_fptocurrency_checked_i64(double %value) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %scaled = fmul double %value, 10000.0");
        builder.AppendLine("  %rounded = call double @llvm.roundeven.f64(double %scaled)");
        builder.AppendLine("  %is_nan = fcmp uno double %rounded, %rounded");
        builder.AppendLine("  %too_low = fcmp olt double %rounded, -9223372036854775808.0");
        builder.AppendLine("  %too_high = fcmp ogt double %rounded, 9223372036854774784.0");
        builder.AppendLine("  %range_overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  %overflow = or i1 %is_nan, %range_overflow");
        builder.AppendLine("  br i1 %overflow, label %trap, label %convert");
        builder.AppendLine("convert:");
        builder.AppendLine("  %result = fptosi double %rounded to i64");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedIntegerArithmeticHelper(
        StringBuilder builder,
        string name,
        string operation)
    {
        var signed = operation[0] == 's';
        var intrinsic = $"llvm.{operation}.with.overflow.i64";
        var comparisonPrefix = signed ? "s" : "u";
        builder.Append("define i64 @").Append(name)
            .AppendLine("(i64 %left, i64 %right, i64 %min_value, i64 %max_value) {");
        builder.AppendLine("entry:");
        builder.Append("  %pair = call { i64, i1 } @").Append(intrinsic)
            .AppendLine("(i64 %left, i64 %right)");
        builder.AppendLine("  %result = extractvalue { i64, i1 } %pair, 0");
        builder.AppendLine("  %intrinsic_overflow = extractvalue { i64, i1 } %pair, 1");
        builder.Append("  %too_low = icmp ").Append(comparisonPrefix).AppendLine("lt i64 %result, %min_value");
        builder.Append("  %too_high = icmp ").Append(comparisonPrefix).AppendLine("gt i64 %result, %max_value");
        builder.AppendLine("  %range_overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  %overflow = or i1 %intrinsic_overflow, %range_overflow");
        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedIntegerNegationHelper(
        StringBuilder builder,
        string name,
        bool signed)
    {
        var operation = signed ? "ssub" : "usub";
        var intrinsic = $"llvm.{operation}.with.overflow.i64";
        var comparisonPrefix = signed ? "s" : "u";
        builder.Append("define i64 @").Append(name)
            .AppendLine("(i64 %value, i64 %min_value, i64 %max_value) {");
        builder.AppendLine("entry:");
        builder.Append("  %pair = call { i64, i1 } @").Append(intrinsic)
            .AppendLine("(i64 0, i64 %value)");
        builder.AppendLine("  %result = extractvalue { i64, i1 } %pair, 0");
        builder.AppendLine("  %intrinsic_overflow = extractvalue { i64, i1 } %pair, 1");
        builder.Append("  %too_low = icmp ").Append(comparisonPrefix).AppendLine("lt i64 %result, %min_value");
        builder.Append("  %too_high = icmp ").Append(comparisonPrefix).AppendLine("gt i64 %result, %max_value");
        builder.AppendLine("  %range_overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  %overflow = or i1 %intrinsic_overflow, %range_overflow");
        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedIntegerConversionHelper(
        StringBuilder builder,
        string name,
        bool signed)
    {
        if (signed)
        {
            builder.Append("define i64 @").Append(name)
                .AppendLine("(i64 %value, i64 %min_value, i64 %max_value) {");
        }
        else
        {
            builder.Append("define i64 @").Append(name)
                .AppendLine("(i64 %value, i64 %max_value) {");
        }

        builder.AppendLine("entry:");
        if (signed)
        {
            builder.AppendLine("  %too_low = icmp slt i64 %value, %min_value");
            builder.AppendLine("  %too_high = icmp sgt i64 %value, %max_value");
            builder.AppendLine("  %overflow = or i1 %too_low, %too_high");
        }
        else
        {
            builder.AppendLine("  %overflow = icmp ugt i64 %value, %max_value");
        }

        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret i64 %value");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedCurrencyMultiplicationHelper(StringBuilder builder)
    {
        builder.AppendLine("define i64 @__vb6_mcurrency_checked_i64(i64 %left, i64 %right) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %left_wide = sext i64 %left to i128");
        builder.AppendLine("  %right_wide = sext i64 %right to i128");
        builder.AppendLine("  %product = mul i128 %left_wide, %right_wide");
        builder.AppendLine("  %quotient = sdiv i128 %product, 10000");
        builder.AppendLine("  %remainder = srem i128 %product, 10000");
        builder.AppendLine("  %remainder_is_negative = icmp slt i128 %remainder, 0");
        builder.AppendLine("  %absolute_remainder = sub i128 0, %remainder");
        builder.AppendLine("  %absolute_or_remainder = select i1 %remainder_is_negative, i128 %absolute_remainder, i128 %remainder");
        builder.AppendLine("  %twice_remainder = mul i128 %absolute_or_remainder, 2");
        builder.AppendLine("  %more_than_half = icmp sgt i128 %twice_remainder, 10000");
        builder.AppendLine("  %exactly_half = icmp eq i128 %twice_remainder, 10000");
        builder.AppendLine("  %quotient_remainder = srem i128 %quotient, 2");
        builder.AppendLine("  %quotient_is_odd = icmp ne i128 %quotient_remainder, 0");
        builder.AppendLine("  %tie_round = and i1 %exactly_half, %quotient_is_odd");
        builder.AppendLine("  %should_round = or i1 %more_than_half, %tie_round");
        builder.AppendLine("  %product_is_negative = icmp slt i128 %product, 0");
        builder.AppendLine("  %round_direction = select i1 %product_is_negative, i128 -1, i128 1");
        builder.AppendLine("  %rounded_quotient = add i128 %quotient, %round_direction");
        builder.AppendLine("  %result = select i1 %should_round, i128 %rounded_quotient, i128 %quotient");
        builder.AppendLine("  %too_low = icmp slt i128 %result, -9223372036854775808");
        builder.AppendLine("  %too_high = icmp sgt i128 %result, 9223372036854775807");
        builder.AppendLine("  %overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  %narrowed = trunc i128 %result to i64");
        builder.AppendLine("  ret i64 %narrowed");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedIntegerToCurrencyHelpers(StringBuilder builder)
    {
        EmitCheckedIntegerToCurrencyHelper(builder, "__vb6_sinteger_to_currency_checked_i64", signed: true);
        EmitCheckedIntegerToCurrencyHelper(builder, "__vb6_uinteger_to_currency_checked_i64", signed: false);
    }

    private static void EmitCheckedIntegerToCurrencyHelper(
        StringBuilder builder,
        string name,
        bool signed)
    {
        builder.Append("define i64 @").Append(name).AppendLine("(i64 %value) {");
        builder.AppendLine("entry:");
        builder.Append("  %wide = ").Append(signed ? "sext " : "zext ")
            .AppendLine("i64 %value to i128");
        builder.AppendLine("  %scaled = mul i128 %wide, 10000");
        builder.AppendLine("  %too_low = icmp slt i128 %scaled, -9223372036854775808");
        builder.AppendLine("  %too_high = icmp sgt i128 %scaled, 9223372036854775807");
        builder.AppendLine("  %overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  %result = trunc i128 %scaled to i64");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedFloatingIntegerConversionHelpers(StringBuilder builder)
    {
        builder.AppendLine("declare double @llvm.roundeven.f64(double)");
        builder.AppendLine();
        builder.AppendLine("define i64 @__vb6_fptosi_checked_i64(double %value, double %min_value, double %max_value) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %rounded = call double @llvm.roundeven.f64(double %value)");
        builder.AppendLine("  %is_nan = fcmp uno double %rounded, %rounded");
        builder.AppendLine("  %too_low = fcmp olt double %rounded, %min_value");
        builder.AppendLine("  %too_high = fcmp ogt double %rounded, %max_value");
        builder.AppendLine("  %range_overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  %overflow = or i1 %is_nan, %range_overflow");
        builder.AppendLine("  br i1 %overflow, label %trap, label %convert");
        builder.AppendLine("convert:");
        builder.AppendLine("  %result = fptosi double %rounded to i64");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("define i64 @__vb6_fptoui_checked_i64(double %value, double %max_value) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %rounded = call double @llvm.roundeven.f64(double %value)");
        builder.AppendLine("  %is_nan = fcmp uno double %rounded, %rounded");
        builder.AppendLine("  %too_low = fcmp olt double %rounded, 0.0");
        builder.AppendLine("  %too_high = fcmp ogt double %rounded, %max_value");
        builder.AppendLine("  %range_overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  %overflow = or i1 %is_nan, %range_overflow");
        builder.AppendLine("  br i1 %overflow, label %trap, label %convert");
        builder.AppendLine("convert:");
        builder.AppendLine("  %result = fptoui double %rounded to i64");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
        EmitCheckedFloatingCurrencyConversionHelper(builder);
    }

    private static void EmitCheckedCurrencyToIntegerHelper(StringBuilder builder)
    {
        builder.AppendLine("define i64 @__vb6_currency_to_integer_checked_i64(i64 %scaled, i64 %min_value, i64 %max_value) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %quotient = sdiv i64 %scaled, 10000");
        builder.AppendLine("  %remainder = srem i64 %scaled, 10000");
        builder.AppendLine("  %remainder_is_negative = icmp slt i64 %remainder, 0");
        builder.AppendLine("  %absolute_remainder = sub i64 0, %remainder");
        builder.AppendLine("  %absolute_or_remainder = select i1 %remainder_is_negative, i64 %absolute_remainder, i64 %remainder");
        builder.AppendLine("  %twice_remainder = mul i64 %absolute_or_remainder, 2");
        builder.AppendLine("  %more_than_half = icmp sgt i64 %twice_remainder, 10000");
        builder.AppendLine("  %exactly_half = icmp eq i64 %twice_remainder, 10000");
        builder.AppendLine("  %quotient_remainder = srem i64 %quotient, 2");
        builder.AppendLine("  %quotient_is_odd = icmp ne i64 %quotient_remainder, 0");
        builder.AppendLine("  %tie_round = and i1 %exactly_half, %quotient_is_odd");
        builder.AppendLine("  %should_round = or i1 %more_than_half, %tie_round");
        builder.AppendLine("  %scaled_is_negative = icmp slt i64 %scaled, 0");
        builder.AppendLine("  %round_direction = select i1 %scaled_is_negative, i64 -1, i64 1");
        builder.AppendLine("  %rounded_quotient = add i64 %quotient, %round_direction");
        builder.AppendLine("  %result = select i1 %should_round, i64 %rounded_quotient, i64 %quotient");
        builder.AppendLine("  %too_low = icmp slt i64 %result, %min_value");
        builder.AppendLine("  %too_high = icmp sgt i64 %result, %max_value");
        builder.AppendLine("  %overflow = or i1 %too_low, %too_high");
        builder.AppendLine("  br i1 %overflow, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret i64 %result");
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void EmitCheckedFloatingHelpers(StringBuilder builder)
    {
        EmitCheckedSingleBinaryHelper(builder, "__vb6_fadd_checked_f32", "fadd");
        EmitCheckedSingleBinaryHelper(builder, "__vb6_fsub_checked_f32", "fsub");
        EmitCheckedSingleBinaryHelper(builder, "__vb6_fmul_checked_f32", "fmul");
        EmitCheckedSingleUnaryHelper(builder, "__vb6_fneg_checked_f32");
        EmitCheckedFloatingDivisionHelper(builder, "__vb6_fdiv_checked_f32", "float", "0x7FF0000000000000", "0xFFF0000000000000", checkInfinity: true);
        EmitCheckedFloatingDivisionHelper(builder, "__vb6_fdiv_checked_f64", "double", "0x7FF0000000000000", "0xFFF0000000000000", checkInfinity: false);
    }

    private static void EmitCheckedSingleBinaryHelper(StringBuilder builder, string name, string operation)
    {
        builder.Append("define float @").Append(name).AppendLine("(float %left, float %right) {");
        builder.AppendLine("entry:");
        builder.Append("  %result = ").Append(operation).AppendLine(" float %left, %right");
        EmitSingleInfinityGuard(builder, "result");
        builder.AppendLine("  br i1 %is_infinite, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret float %result");
        EmitFloatingTrapBlock(builder);
    }

    private static void EmitCheckedSingleUnaryHelper(StringBuilder builder, string name)
    {
        builder.Append("define float @").Append(name).AppendLine("(float %value) {");
        builder.AppendLine("entry:");
        builder.AppendLine("  %result = fneg float %value");
        EmitSingleInfinityGuard(builder, "result");
        builder.AppendLine("  br i1 %is_infinite, label %trap, label %done");
        builder.AppendLine("done:");
        builder.AppendLine("  ret float %result");
        EmitFloatingTrapBlock(builder);
    }

    private static void EmitCheckedFloatingDivisionHelper(
        StringBuilder builder,
        string name,
        string llvmType,
        string positiveInfinity,
        string negativeInfinity,
        bool checkInfinity)
    {
        builder.Append("define ").Append(llvmType).Append(" @").Append(name)
            .Append('(').Append(llvmType).AppendLine(" %left, " + llvmType + " %right) {");
        builder.AppendLine("entry:");
        builder.Append("  %is_zero = fcmp oeq ").Append(llvmType).AppendLine(" %right, 0.0");
        builder.AppendLine("  br i1 %is_zero, label %trap, label %divide");
        builder.AppendLine("divide:");
        builder.Append("  %result = fdiv ").Append(llvmType).AppendLine(" %left, %right");
        if (checkInfinity)
        {
            builder.Append("  %is_pos_inf = fcmp oeq ").Append(llvmType).Append(' ')
                .AppendLine("%result, " + positiveInfinity);
            builder.Append("  %is_neg_inf = fcmp oeq ").Append(llvmType).Append(' ')
                .AppendLine("%result, " + negativeInfinity);
            builder.AppendLine("  %is_infinite = or i1 %is_pos_inf, %is_neg_inf");
            builder.AppendLine("  br i1 %is_infinite, label %trap, label %done");
        }
        else
        {
            builder.AppendLine("  br label %done");
        }

        builder.AppendLine("done:");
        builder.Append("  ret ").Append(llvmType).AppendLine(" %result");
        EmitFloatingTrapBlock(builder);
    }

    private static void EmitSingleInfinityGuard(StringBuilder builder, string resultName)
    {
        builder.AppendLine("  %is_pos_inf = fcmp oeq float %" + resultName + ", 0x7FF0000000000000");
        builder.AppendLine("  %is_neg_inf = fcmp oeq float %" + resultName + ", 0xFFF0000000000000");
        builder.AppendLine("  %is_infinite = or i1 %is_pos_inf, %is_neg_inf");
    }

    private static void EmitFloatingTrapBlock(StringBuilder builder)
    {
        builder.AppendLine("trap:");
        builder.AppendLine("  call void @llvm.trap()");
        builder.AppendLine("  unreachable");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static Dictionary<IrGlobal, string> EmitGlobals(
        ImmutableArray<IrModule> modules,
        StringBuilder builder,
        ImmutableArray<LlvmEmitDiagnostic>.Builder diagnostics,
        LlvmArchitecture architecture)
    {
        var slots = new Dictionary<IrGlobal, string>(ReferenceEqualityComparer.Instance);
        var globalIndex = 0;
        foreach (var module in modules)
        {
            foreach (var global in module.Globals)
            {
                if (slots.ContainsKey(global))
                {
                    continue;
                }

                if (!NativeProcedureEmitter.TryGetLlvmType(global.Type, architecture, out var llvmType))
                {
                    diagnostics.Add(new LlvmEmitDiagnostic(
                        "VB6L0007",
                        $"Native LLVM type for module global '{global.Symbol.Name}' ({global.Type.Name}) is not implemented yet."));
                    continue;
                }

                var name = $"__vb6_global_{MangleIdentifier(module.Name)}_{MangleIdentifier(global.Name)}_{globalIndex++}";
                slots.Add(global, name);

                var initializer = NativeProcedureEmitter.ZeroLiteral(llvmType);
                if (global.Initializer is IrConstantExpression constant)
                {
                    if (!NativeProcedureEmitter.TryFormatConstant(constant.Value, global.Type, out initializer))
                    {
                        diagnostics.Add(new LlvmEmitDiagnostic(
                            "VB6L0003",
                            $"Initializer for module global '{global.Symbol.Name}' cannot be emitted as LLVM type '{llvmType}'."));
                        initializer = NativeProcedureEmitter.ZeroLiteral(llvmType);
                    }
                }
                else if (global.Initializer is not null and not IrDefaultExpression)
                {
                    diagnostics.Add(new LlvmEmitDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for initializer of module global '{global.Symbol.Name}' is not implemented yet."));
                }

                builder.Append("@\"").Append(NativeProcedureEmitter.EscapeIdentifier(name)).Append("\" = internal ")
                    .Append(global.IsConstant ? "constant " : "global ")
                    .Append(llvmType).Append(' ').AppendLine(initializer);
            }
        }

        if (slots.Count > 0)
        {
            builder.AppendLine();
        }

        return slots;
    }

    private static string GetTargetTriple(LlvmArchitecture architecture) => architecture switch
    {
        LlvmArchitecture.X86 => "i686-pc-windows-msvc",
        LlvmArchitecture.X64 => "x86_64-pc-windows-msvc",
        _ => throw new ArgumentOutOfRangeException(nameof(architecture))
    };

    private static string MangleIdentifier(string identifier)
    {
        var characters = identifier.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        return characters.Length == 0 ? "unnamed" : new string(characters);
    }

    private sealed class NativeProcedureEmitter
    {
        private readonly StringBuilder _builder;
        private readonly ImmutableArray<LlvmEmitDiagnostic>.Builder _diagnostics;
        private readonly LlvmArchitecture _architecture;
        private readonly Dictionary<IrGlobal, string> _globalSlots;
        private readonly Dictionary<int, string> _parameterSlots = new();
        private readonly Dictionary<int, string> _localSlots = new();
        private readonly Dictionary<int, string> _blockLabels = new();
        private int _temporaryId;

        public NativeProcedureEmitter(
            StringBuilder builder,
            ImmutableArray<LlvmEmitDiagnostic>.Builder diagnostics,
            LlvmArchitecture architecture,
            Dictionary<IrGlobal, string> globalSlots)
        {
            _builder = builder;
            _diagnostics = diagnostics;
            _architecture = architecture;
            _globalSlots = globalSlots;
        }

        public void Emit(IrProcedure procedure)
        {
            var returnType = GetTypeOrFallback(procedure.ReturnType, "procedure return type");
            var parameterTypes = procedure.Parameters
                .Select(GetParameterLlvmType)
                .ToArray();

            _blockLabels.Clear();
            foreach (var block in procedure.Blocks)
            {
                _blockLabels[block.Id] = $"bb{block.Id}";
            }

            _builder.Append("define ").Append(returnType).Append(" @\"")
                .Append(EscapeIdentifier(procedure.Name)).Append("\"(");
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                if (index > 0)
                {
                    _builder.Append(", ");
                }

                _builder.Append(parameterTypes[index]).Append(" %arg").Append(index);
            }

            _builder.AppendLine(") {");

            if (procedure.Blocks.IsDefaultOrEmpty)
            {
                _builder.AppendLine("entry:");
                EmitFallbackReturn(returnType);
                _builder.AppendLine("}");
                return;
            }

            var firstBlock = true;
            foreach (var block in procedure.Blocks)
            {
                _builder.Append(_blockLabels[block.Id]).AppendLine(":");
                if (firstBlock)
                {
                    EmitStorage(procedure);
                    firstBlock = false;
                }

                foreach (var instruction in block.Instructions)
                {
                    EmitInstruction(instruction);
                }

                EmitTerminator(block.Terminator, returnType);
            }

            _builder.AppendLine("}");
        }

        public void EmitExternalDeclaration(IrProcedure procedure)
        {
            var returnType = procedure.ReturnType is null
                ? "void"
                : GetTypeOrFallback(procedure.ReturnType, $"external procedure '{procedure.Name}' return type");
            var parameterTypes = procedure.Parameters
                .Select(GetParameterLlvmType)
                .ToArray();

            if (!string.IsNullOrEmpty(procedure.ExternalLibrary))
            {
                _builder.Append("; external library: ").AppendLine(procedure.ExternalLibrary);
            }

            _builder.Append("declare ").Append(returnType).Append(" @\"")
                .Append(EscapeIdentifier(GetExternalName(procedure))).Append("\"(");
            for (var index = 0; index < parameterTypes.Length; index++)
            {
                if (index > 0)
                {
                    _builder.Append(", ");
                }

                _builder.Append(parameterTypes[index]);
            }

            _builder.AppendLine(")");
        }

        private void EmitFallbackReturn(string returnType)
        {
            if (returnType == "void")
            {
                _builder.AppendLine("  ret void");
            }
            else
            {
                _builder.Append("  ret ").Append(returnType).Append(' ')
                    .AppendLine(ZeroLiteral(returnType));
            }
        }

        private void EmitStorage(IrProcedure procedure)
        {
            foreach (var parameter in procedure.Parameters)
            {
                if (parameter.PassingMode == ParameterPassingMode.ByRef)
                {
                    _parameterSlots[parameter.Index] = $"%arg{parameter.Index}";
                    continue;
                }

                var llvmType = GetTypeOrFallback(parameter.Type, $"parameter '{parameter.Name}'");
                var slot = $"%param_{parameter.Index}";
                _parameterSlots[parameter.Index] = slot;
                _builder.Append("  ").Append(slot).Append(" = alloca ").AppendLine(llvmType);
                _builder.Append("  store ").Append(llvmType).Append(" %arg")
                    .Append(parameter.Index).AppendLine(", ptr " + slot);
            }

            foreach (var local in procedure.Locals)
            {
                var llvmType = GetTypeOrFallback(local.Type, $"local '{local.Name}'");
                var slot = $"%local_{local.Id}";
                _localSlots[local.Id] = slot;
                _builder.Append("  ").Append(slot).Append(" = alloca ").AppendLine(llvmType);
            }
        }

        private string GetParameterLlvmType(IrParameter parameter)
        {
            if (parameter.PassingMode == ParameterPassingMode.ByRef)
            {
                _ = GetTypeOrFallback(parameter.Type, $"ByRef parameter '{parameter.Name}'");
                return "ptr";
            }

            return GetTypeOrFallback(parameter.Type, $"parameter '{parameter.Name}'");
        }

        private void EmitInstruction(IrInstruction instruction)
        {
            switch (instruction)
            {
                case IrNopInstruction:
                    return;
                case IrStoreInstruction store:
                    EmitStore(store);
                    return;
                case IrVariantArraySetInstruction:
                    AddDiagnostic("VB6L0001", "Native LLVM lowering for Variant array writes is not implemented yet.");
                    return;
                case IrEvaluateInstruction evaluate:
                    _ = EmitExpression(evaluate.Expression);
                    return;
                case IrStoreAddressInstruction:
                    AddDiagnostic("VB6L0001", "Native LLVM lowering for address stores is not implemented yet.");
                    return;
                case IrBaseFinalizeInstruction:
                    AddDiagnostic("VB6L0001", "Native LLVM lowering for class finalization is not implemented yet.");
                    return;
                case IrRaiseEventInstruction:
                case IrSubscribeEventInstruction:
                    AddDiagnostic("VB6L0001", "Native LLVM lowering for events is not implemented yet.");
                    return;
                case IrErrorBoundaryStartInstruction:
                case IrErrorBoundaryEndInstruction:
                case IrResumeInstruction:
                    AddDiagnostic("VB6L0001", "Native LLVM lowering for VB6 error handling is not implemented yet.");
                    return;
                default:
                    AddDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for '{instruction.GetType().Name}' is not implemented yet.");
                    return;
            }
        }

        private void EmitStore(IrStoreInstruction store)
        {
            var pointer = EmitPlacePointer(store.Target, out var targetType);
            var value = EmitExpression(store.Value);
            if (pointer is null || targetType is null)
            {
                return;
            }

            if (!string.Equals(targetType, value.LlvmType, StringComparison.Ordinal))
            {
                AddDiagnostic(
                    "VB6L0004",
                    $"Native LLVM store type '{value.LlvmType}' does not match target type '{targetType}'.");
                return;
            }

            _builder.Append("  store ").Append(targetType).Append(' ').Append(value.Value)
                .AppendLine(", ptr " + pointer);
        }

        private string? EmitPlacePointer(IrPlace place, out string? llvmType)
        {
            llvmType = TryGetLlvmType(place.Type, _architecture, out var mappedType) ? mappedType : null;
            if (llvmType is null)
            {
                AddUnsupportedType(place.Type, "storage");
                return null;
            }

            switch (place)
            {
                case IrLocalPlace local when _localSlots.TryGetValue(local.Local.Id, out var localSlot):
                    return localSlot;
                case IrParameterPlace parameter when _parameterSlots.TryGetValue(parameter.Parameter.Index, out var parameterSlot):
                    return parameterSlot;
                case IrGlobalPlace global when _globalSlots.TryGetValue(global.Global, out var globalName):
                    return "@\"" + EscapeIdentifier(globalName) + "\"";
                default:
                    AddDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for place '{place.GetType().Name}' is not implemented yet.");
                    return null;
            }
        }

        private NativeValue EmitExpression(IrExpression expression)
        {
            switch (expression)
            {
                case IrConstantExpression constant:
                    return EmitConstant(constant.Value, constant.ConstantType);
                case IrDefaultExpression defaultValue:
                    return ZeroValue(defaultValue.DefaultType);
                case IrLoadExpression load:
                    return EmitLoad(load.Place);
                case IrRuntimeCallExpression runtime:
                    return EmitRuntimeCall(runtime);
                case IrProcedureCallExpression procedureCall:
                    return EmitProcedureCall(procedureCall);
                case IrAddressExpression:
                case IrLocalAddressExpression:
                case IrAddressOfExpression:
                    AddDiagnostic("VB6L0001", $"Native LLVM lowering for address expression '{expression.GetType().Name}' is not implemented yet.");
                    return ZeroValue(expression.Type);
                default:
                    AddDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for expression '{expression.GetType().Name}' is not implemented yet.");
                    return ZeroValue(expression.Type);
            }
        }

        private NativeValue EmitLoad(IrPlace place)
        {
            var pointer = EmitPlacePointer(place, out var llvmType);
            if (pointer is null || llvmType is null)
            {
                return ZeroValue(place.Type);
            }

            var temporary = NextTemporary();
            _builder.Append("  ").Append(temporary).Append(" = load ").Append(llvmType)
                .AppendLine(", ptr " + pointer);
            return new NativeValue(place.Type, llvmType, temporary);
        }

        private NativeValue EmitConstant(object? value, TypeSymbol type)
        {
            var llvmType = GetTypeOrFallback(type, "constant");
            if (!TryFormatConstant(value, type, out var literal))
            {
                AddDiagnostic("VB6L0003", $"Constant '{value}' cannot be emitted as LLVM type '{llvmType}'.");
                literal = ZeroLiteral(llvmType);
            }

            return new NativeValue(type, llvmType, literal);
        }

        private NativeValue EmitRuntimeCall(IrRuntimeCallExpression runtime)
        {
            var arguments = runtime.Arguments.Select(argument => EmitExpression(argument.Expression)).ToArray();
            var methodName = runtime.Method.ToString();

            if (methodName == "MultiplyCurrency")
            {
                return EmitCheckedCurrencyMultiplication(runtime.ResultType, arguments);
            }

            if (methodName is "AddSingle" or "SubtractSingle" or "MultiplySingle" or "NegateSingle")
            {
                return EmitCheckedSingleOperation(methodName, runtime.ResultType, arguments);
            }

            if (methodName is "DivideSingle" or "DivideDouble")
            {
                return EmitCheckedFloatingDivision(methodName, runtime.ResultType, arguments);
            }

            if (methodName.StartsWith("Add", StringComparison.Ordinal) ||
                methodName.StartsWith("Subtract", StringComparison.Ordinal) ||
                methodName.StartsWith("Multiply", StringComparison.Ordinal))
            {
                var operation = methodName.StartsWith("Add", StringComparison.Ordinal) ? "add" :
                    methodName.StartsWith("Subtract", StringComparison.Ordinal) ? "sub" : "mul";
                if (!IsFloating(arguments))
                {
                    if (TryGetCheckedIntegerShape(runtime.ResultType, _architecture, out _, out _))
                    {
                        return EmitCheckedIntegerArithmetic(methodName, runtime.ResultType, arguments);
                    }

                    var arithmetic = operation switch
                    {
                        "add" => "integer or Currency addition",
                        "sub" => "integer or Currency subtraction",
                        _ => "integer or Currency multiplication"
                    };
                    return RejectCheckedOperation(methodName, runtime.ResultType, arithmetic);
                }

                return EmitBinary(runtime.ResultType, arguments, operation);
            }

            if (methodName.StartsWith("IntegerDivide", StringComparison.Ordinal) ||
                methodName.StartsWith("Mod", StringComparison.Ordinal))
            {
                if (TryGetIntegerShape(runtime.ResultType, _architecture, out _, out _))
                {
                    return EmitIntegerDivision(
                        runtime.ResultType,
                        arguments,
                        methodName.StartsWith("Mod", StringComparison.Ordinal));
                }

                return RejectCheckedOperation(
                    methodName,
                    runtime.ResultType,
                    methodName.StartsWith("IntegerDivide", StringComparison.Ordinal)
                        ? "integer division"
                        : "integer remainder");
            }

            if (methodName.StartsWith("Negate", StringComparison.Ordinal))
            {
                if (!IsFloating(arguments))
                {
                    if (TryGetCheckedIntegerShape(runtime.ResultType, _architecture, out _, out _))
                    {
                        return EmitCheckedIntegerArithmetic(methodName, runtime.ResultType, arguments);
                    }

                    return RejectCheckedOperation(
                        methodName,
                        runtime.ResultType,
                        "integer or Currency negation");
                }

                return EmitUnary(runtime.ResultType, arguments, IsFloating(arguments) ? "fneg" : "neg");
            }

            if (methodName.StartsWith("Not", StringComparison.Ordinal))
            {
                return EmitUnary(runtime.ResultType, arguments, "not");
            }

            if (methodName.StartsWith("And", StringComparison.Ordinal) ||
                methodName.StartsWith("Or", StringComparison.Ordinal) ||
                methodName.StartsWith("Xor", StringComparison.Ordinal))
            {
                var operation = methodName.StartsWith("And", StringComparison.Ordinal) ? "and" :
                    methodName.StartsWith("Or", StringComparison.Ordinal) ? "or" : "xor";
                return EmitBinary(runtime.ResultType, arguments, operation);
            }

            if (methodName.StartsWith("Eqv", StringComparison.Ordinal))
            {
                return EmitEqv(runtime.ResultType, arguments);
            }

            if (methodName.StartsWith("Imp", StringComparison.Ordinal))
            {
                return EmitImp(runtime.ResultType, arguments);
            }

            if (methodName is "Equal" or "NotEqual" or "Less" or "LessOrEqual" or "Greater" or "GreaterOrEqual")
            {
                return EmitComparison(runtime.ResultType, arguments, methodName);
            }

            if (IsScalarConversion(methodName))
            {
                return EmitScalarConversion(methodName, runtime.ResultType, arguments);
            }

            AddDiagnostic(
                "VB6L0001",
                $"Native LLVM lowering for runtime method '{runtime.Method}' is not implemented yet.");
            return ZeroValue(runtime.ResultType);
        }

        private NativeValue RejectCheckedOperation(
            string methodName,
            TypeSymbol resultType,
            string operation)
        {
            AddDiagnostic(
                "VB6L0001",
                $"Native LLVM lowering for runtime method '{methodName}' requires checked {operation} runtime semantics and is not implemented yet.");
            return ZeroValue(resultType);
        }

        private NativeValue EmitCheckedSingleOperation(
            string methodName,
            TypeSymbol resultType,
            NativeValue[] arguments)
        {
            var unary = methodName == "NegateSingle";
            if ((unary && arguments.Length != 1) || (!unary && arguments.Length != 2) ||
                arguments.Any(argument => argument.LlvmType != "float"))
            {
                AddDiagnostic("VB6L0004", $"LLVM Single operation '{methodName}' has invalid operands.");
                return ZeroValue(resultType);
            }

            var helperName = methodName switch
            {
                "AddSingle" => "__vb6_fadd_checked_f32",
                "SubtractSingle" => "__vb6_fsub_checked_f32",
                "MultiplySingle" => "__vb6_fmul_checked_f32",
                "NegateSingle" => "__vb6_fneg_checked_f32",
                _ => throw new ArgumentOutOfRangeException(nameof(methodName))
            };
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call float @").Append(helperName).Append('(');
            if (unary)
            {
                _builder.Append("float ").Append(arguments[0].Value);
            }
            else
            {
                _builder.Append("float ").Append(arguments[0].Value)
                    .Append(", float ").Append(arguments[1].Value);
            }

            _builder.AppendLine(")");
            return new NativeValue(resultType, "float", call);
        }

        private NativeValue EmitCheckedFloatingDivision(
            string methodName,
            TypeSymbol resultType,
            NativeValue[] arguments)
        {
            if (arguments.Length != 2 || arguments.Any(argument => argument.LlvmType != (methodName == "DivideSingle" ? "float" : "double")))
            {
                AddDiagnostic("VB6L0004", $"LLVM floating division '{methodName}' has invalid operands.");
                return ZeroValue(resultType);
            }

            var llvmType = methodName == "DivideSingle" ? "float" : "double";
            var helperName = methodName == "DivideSingle"
                ? "__vb6_fdiv_checked_f32"
                : "__vb6_fdiv_checked_f64";
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call ").Append(llvmType).Append(" @")
                .Append(helperName).Append('(').Append(llvmType).Append(' ').Append(arguments[0].Value)
                .Append(", ").Append(llvmType).Append(' ').Append(arguments[1].Value).AppendLine(")");
            return new NativeValue(resultType, llvmType, call);
        }

        private NativeValue EmitCheckedCurrencyMultiplication(
            TypeSymbol resultType,
            NativeValue[] arguments)
        {
            if (resultType != TypeSymbol.Currency ||
                arguments.Length != 2 ||
                arguments.Any(argument => argument.SemanticType != TypeSymbol.Currency || argument.LlvmType != "i64"))
            {
                AddDiagnostic("VB6L0004", "LLVM Currency multiplication requires two Currency operands.");
                return ZeroValue(resultType);
            }

            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_mcurrency_checked_i64(i64 ")
                .Append(arguments[0].Value).Append(", i64 ").Append(arguments[1].Value).AppendLine(")");
            return new NativeValue(resultType, "i64", call);
        }

        private NativeValue EmitCheckedIntegerArithmetic(
            string methodName,
            TypeSymbol resultType,
            NativeValue[] arguments)
        {
            var unary = methodName.StartsWith("Negate", StringComparison.Ordinal);
            var expectedArgumentCount = unary ? 1 : 2;
            if (arguments.Length != expectedArgumentCount)
            {
                AddDiagnostic(
                    "VB6L0004",
                    $"LLVM checked integer operation '{methodName}' requires {expectedArgumentCount} operand(s).");
                return ZeroValue(resultType);
            }

            if (!TryGetCheckedIntegerShape(resultType, _architecture, out var bits, out var unsigned) ||
                !TryGetLlvmType(resultType, _architecture, out var resultLlvmType))
            {
                AddDiagnostic("VB6L0004", $"LLVM checked integer operation '{methodName}' has an unsupported result type.");
                return ZeroValue(resultType);
            }

            foreach (var argument in arguments)
            {
                if (!TryGetCheckedIntegerShape(argument.SemanticType, _architecture, out var argumentBits, out var argumentUnsigned) ||
                    argumentBits != bits ||
                    argumentUnsigned != unsigned ||
                    argument.LlvmType != resultLlvmType)
                {
                    AddDiagnostic(
                        "VB6L0004",
                        $"LLVM checked integer operation '{methodName}' requires matching integer or Currency operands and result types.");
                    return ZeroValue(resultType);
                }
            }

            var helperName = unary
                ? unsigned ? "__vb6_uneg_checked_i64" : "__vb6_sneg_checked_i64"
                : methodName.StartsWith("Add", StringComparison.Ordinal)
                    ? unsigned ? "__vb6_uadd_checked_i64" : "__vb6_sadd_checked_i64"
                    : methodName.StartsWith("Subtract", StringComparison.Ordinal)
                        ? unsigned ? "__vb6_usub_checked_i64" : "__vb6_ssub_checked_i64"
                        : unsigned ? "__vb6_umul_checked_i64" : "__vb6_smul_checked_i64";
            var left = ExtendIntegerToHelperWidth(arguments[0], unsigned);
            var right = unary ? string.Empty : ExtendIntegerToHelperWidth(arguments[1], unsigned);
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @").Append(helperName).Append('(');
            if (unary)
            {
                _builder.Append("i64 ").Append(left);
            }
            else
            {
                _builder.Append("i64 ").Append(left)
                    .Append(", i64 ").Append(right);
            }

            _builder.Append(", i64 ").Append(unsigned ? "0" : SignedMinimumLiteral(bits))
                .Append(", i64 ").Append(unsigned ? UnsignedMaximumLiteral(bits) : SignedMaximumLiteral(bits))
                .AppendLine(")");

            if (resultLlvmType == "i64")
            {
                return new NativeValue(resultType, resultLlvmType, call);
            }

            var narrowed = NextTemporary();
            _builder.Append("  ").Append(narrowed).Append(" = trunc i64 ").Append(call)
                .Append(" to ").AppendLine(resultLlvmType);
            return new NativeValue(resultType, resultLlvmType, narrowed);
        }

        private NativeValue EmitIntegerDivision(
            TypeSymbol resultType,
            NativeValue[] arguments,
            bool remainder)
        {
            if (arguments.Length != 2 || arguments[0].LlvmType != arguments[1].LlvmType)
            {
                AddDiagnostic("VB6L0004", "LLVM integer division requires two operands of the same type.");
                return ZeroValue(resultType);
            }

            if (!TryGetIntegerShape(arguments[0].SemanticType, _architecture, out var bits, out var unsigned) ||
                !TryGetIntegerShape(arguments[1].SemanticType, _architecture, out var rightBits, out var rightUnsigned) ||
                bits != rightBits ||
                unsigned != rightUnsigned ||
                !TryGetIntegerShape(resultType, _architecture, out var resultBits, out var resultUnsigned) ||
                bits != resultBits ||
                unsigned != resultUnsigned)
            {
                AddDiagnostic("VB6L0004", "LLVM integer division requires matching integer operand and result types.");
                return ZeroValue(resultType);
            }

            var left = ExtendIntegerToHelperWidth(arguments[0], unsigned);
            var right = ExtendIntegerToHelperWidth(arguments[1], unsigned);
            var helperName = unsigned
                ? remainder ? "__vb6_urem_checked_i64" : "__vb6_udiv_checked_i64"
                : remainder ? "__vb6_srem_checked_i64" : "__vb6_sdiv_checked_i64";
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @").Append(helperName)
                .Append("(i64 ").Append(left).Append(", i64 ").Append(right);
            if (!unsigned && !remainder)
            {
                _builder.Append(", i64 ").Append(SignedMinimumLiteral(bits));
            }
            _builder.AppendLine(")");

            var resultLlvmType = GetTypeOrFallback(resultType, "integer division result");
            if (resultLlvmType == "i64")
            {
                return new NativeValue(resultType, resultLlvmType, call);
            }

            var narrowed = NextTemporary();
            _builder.Append("  ").Append(narrowed).Append(" = trunc i64 ").Append(call)
                .Append(" to ").AppendLine(resultLlvmType);
            return new NativeValue(resultType, resultLlvmType, narrowed);
        }

        private static string SignedMinimumLiteral(int bits) => bits switch
        {
            8 => "-128",
            16 => "-32768",
            32 => "-2147483648",
            64 => "-9223372036854775808",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private static string SignedMaximumLiteral(int bits) => bits switch
        {
            8 => "127",
            16 => "32767",
            32 => "2147483647",
            64 => "9223372036854775807",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private static string UnsignedMaximumLiteral(int bits) => bits switch
        {
            8 => "255",
            16 => "65535",
            32 => "4294967295",
            64 => "-1",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private static string SignedFloatingMinimumLiteral(int bits) => bits switch
        {
            8 => "-128.0",
            16 => "-32768.0",
            32 => "-2147483648.0",
            64 => "-9223372036854775808.0",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private static string SignedFloatingMaximumLiteral(int bits) => bits switch
        {
            8 => "127.0",
            16 => "32767.0",
            32 => "2147483647.0",
            64 => "9223372036854774784.0",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private static string UnsignedFloatingMaximumLiteral(int bits) => bits switch
        {
            8 => "255.0",
            16 => "65535.0",
            32 => "4294967295.0",
            64 => "18446744073709549568.0",
            _ => throw new ArgumentOutOfRangeException(nameof(bits))
        };

        private string ExtendIntegerToHelperWidth(NativeValue value, bool unsigned)
        {
            if (value.LlvmType == "i64")
            {
                return value.Value;
            }

            var temporary = NextTemporary();
            _builder.Append("  ").Append(temporary).Append(" = ")
                .Append(unsigned ? "zext " : "sext ").Append(value.LlvmType).Append(' ')
                .Append(value.Value).AppendLine(" to i64");
            return temporary;
        }

        private NativeValue EmitScalarConversion(
            string methodName,
            TypeSymbol targetType,
            NativeValue[] arguments)
        {
            if (arguments.Length != 1)
            {
                AddDiagnostic(
                    "VB6L0004",
                    $"LLVM conversion '{methodName}' requires one operand.");
                return ZeroValue(targetType);
            }

            var source = arguments[0];
            var targetLlvmType = GetTypeOrFallback(targetType, $"conversion '{methodName}' target type");
            if (!TryGetLlvmType(targetType, _architecture, out _))
            {
                return new NativeValue(targetType, targetLlvmType, ZeroLiteral(targetLlvmType));
            }

            if (targetType == source.SemanticType && targetLlvmType == source.LlvmType)
            {
                return new NativeValue(targetType, targetLlvmType, source.Value);
            }

            if (targetType == TypeSymbol.Boolean)
            {
                return EmitBooleanConversion(methodName, targetType, source);
            }

            if (source.SemanticType == TypeSymbol.Boolean)
            {
                return EmitBooleanToScalar(methodName, targetType, targetLlvmType, source);
            }

            if (targetType == TypeSymbol.Currency &&
                TryGetIntegerShape(source.SemanticType, _architecture, out _, out var currencySourceUnsigned))
            {
                return EmitIntegerToCurrencyConversion(
                    targetType,
                    targetLlvmType,
                    source,
                    currencySourceUnsigned);
            }

            if (targetType == TypeSymbol.Currency &&
                (source.SemanticType == TypeSymbol.Single || source.SemanticType == TypeSymbol.Double))
            {
                return EmitFloatingToCurrencyConversion(targetType, targetLlvmType, source);
            }

            if (source.SemanticType == TypeSymbol.Currency &&
                TryGetIntegerShape(targetType, _architecture, out var currencyTargetBits, out var currencyTargetUnsigned))
            {
                return EmitCurrencyToIntegerConversion(
                    targetType,
                    targetLlvmType,
                    source,
                    currencyTargetBits,
                    currencyTargetUnsigned);
            }

            if (source.SemanticType == TypeSymbol.Currency || targetType == TypeSymbol.Currency)
            {
                return RejectScalarConversion(methodName, targetType, source);
            }

            if ((source.SemanticType == TypeSymbol.Single || source.SemanticType == TypeSymbol.Double) &&
                TryGetIntegerShape(targetType, _architecture, out var floatingTargetBits, out var floatingTargetUnsigned) &&
                floatingTargetBits <= 64)
            {
                return EmitFloatingToIntegerConversion(
                    targetType,
                    targetLlvmType,
                    source,
                    floatingTargetBits,
                    floatingTargetUnsigned);
            }

            if (TryGetIntegerShape(source.SemanticType, _architecture, out var sourceBits, out var sourceUnsigned) &&
                TryGetIntegerShape(targetType, _architecture, out var targetBits, out var targetUnsigned))
            {
                return EmitIntegerConversion(
                    methodName,
                    targetType,
                    targetLlvmType,
                    source,
                    sourceBits,
                    sourceUnsigned,
                    targetBits,
                    targetUnsigned);
            }

            if (TryGetIntegerShape(source.SemanticType, _architecture, out sourceBits, out sourceUnsigned) &&
                targetLlvmType is "float" or "double")
            {
                var operation = sourceUnsigned ? "uitofp" : "sitofp";
                var temporary = NextTemporary();
                _builder.Append("  ").Append(temporary).Append(" = ").Append(operation).Append(' ')
                    .Append(source.LlvmType).Append(' ').Append(source.Value).Append(" to ")
                    .AppendLine(targetLlvmType);
                return new NativeValue(targetType, targetLlvmType, temporary);
            }

            if (source.LlvmType is "float" or "double" && targetLlvmType is "float" or "double")
            {
                if (source.LlvmType == targetLlvmType)
                {
                    return new NativeValue(targetType, targetLlvmType, source.Value);
                }

                if (source.LlvmType == "float" && targetLlvmType == "double")
                {
                    var temporary = NextTemporary();
                    _builder.Append("  ").Append(temporary).Append(" = fpext float ")
                        .Append(source.Value).AppendLine(" to double");
                    return new NativeValue(targetType, targetLlvmType, temporary);
                }

                return RejectScalarConversion(methodName, targetType, source);
            }

            return RejectScalarConversion(methodName, targetType, source);
        }

        private NativeValue EmitFloatingToIntegerConversion(
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source,
            int targetBits,
            bool targetUnsigned)
        {
            var floatingValue = source.Value;
            if (source.LlvmType == "float")
            {
                var widened = NextTemporary();
                _builder.Append("  ").Append(widened).Append(" = fpext float ")
                    .Append(source.Value).AppendLine(" to double");
                floatingValue = widened;
            }

            var call = NextTemporary();
            if (targetUnsigned)
            {
                _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_fptoui_checked_i64(double ")
                    .Append(floatingValue).Append(", double ")
                    .Append(UnsignedFloatingMaximumLiteral(targetBits)).AppendLine(")");
            }
            else
            {
                _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_fptosi_checked_i64(double ")
                    .Append(floatingValue).Append(", double ").Append(SignedFloatingMinimumLiteral(targetBits))
                    .Append(", double ").Append(SignedFloatingMaximumLiteral(targetBits)).AppendLine(")");
            }

            if (targetLlvmType == "i64")
            {
                return new NativeValue(targetType, targetLlvmType, call);
            }

            var narrowed = NextTemporary();
            _builder.Append("  ").Append(narrowed).Append(" = trunc i64 ").Append(call)
                .Append(" to ").AppendLine(targetLlvmType);
            return new NativeValue(targetType, targetLlvmType, narrowed);
        }

        private NativeValue EmitIntegerToCurrencyConversion(
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source,
            bool sourceUnsigned)
        {
            var widened = ExtendIntegerToHelperWidth(source, sourceUnsigned);
            var helper = sourceUnsigned
                ? "__vb6_uinteger_to_currency_checked_i64"
                : "__vb6_sinteger_to_currency_checked_i64";
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @").Append(helper)
                .Append("(i64 ").Append(widened).AppendLine(")");
            return new NativeValue(targetType, targetLlvmType, call);
        }

        private NativeValue EmitFloatingToCurrencyConversion(
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source)
        {
            var floatingValue = source.Value;
            if (source.LlvmType == "float")
            {
                var widened = NextTemporary();
                _builder.Append("  ").Append(widened).Append(" = fpext float ")
                    .Append(source.Value).AppendLine(" to double");
                floatingValue = widened;
            }

            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_fptocurrency_checked_i64(double ")
                .Append(floatingValue).AppendLine(")");
            return new NativeValue(targetType, targetLlvmType, call);
        }

        private NativeValue EmitCurrencyToIntegerConversion(
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source,
            int targetBits,
            bool targetUnsigned)
        {
            var minimum = targetUnsigned ? "0" : SignedMinimumLiteral(targetBits);
            var maximum = targetUnsigned && targetBits == 64
                ? SignedMaximumLiteral(64)
                : targetUnsigned ? UnsignedMaximumLiteral(targetBits) : SignedMaximumLiteral(targetBits);
            var call = NextTemporary();
            _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_currency_to_integer_checked_i64(i64 ")
                .Append(source.Value).Append(", i64 ").Append(minimum)
                .Append(", i64 ").Append(maximum).AppendLine(")");

            if (targetLlvmType == "i64")
            {
                return new NativeValue(targetType, targetLlvmType, call);
            }

            var narrowed = NextTemporary();
            _builder.Append("  ").Append(narrowed).Append(" = trunc i64 ").Append(call)
                .Append(" to ").AppendLine(targetLlvmType);
            return new NativeValue(targetType, targetLlvmType, narrowed);
        }

        private NativeValue EmitBooleanConversion(
            string methodName,
            TypeSymbol targetType,
            NativeValue source)
        {
            if (source.SemanticType == TypeSymbol.Boolean)
            {
                return new NativeValue(targetType, "i1", source.Value);
            }

            string operation;
            string zero;
            if (TryGetIntegerShape(source.SemanticType, _architecture, out _, out _) ||
                source.SemanticType == TypeSymbol.Currency)
            {
                operation = "icmp ne";
                zero = "0";
            }
            else if (source.SemanticType == TypeSymbol.Single)
            {
                operation = "fcmp une";
                zero = "0.0";
            }
            else if (source.SemanticType == TypeSymbol.Date || source.SemanticType == TypeSymbol.Double)
            {
                operation = "fcmp une";
                zero = "0.0";
            }
            else
            {
                return RejectScalarConversion(methodName, targetType, source);
            }

            var temporary = NextTemporary();
            _builder.Append("  ").Append(temporary).Append(" = ").Append(operation).Append(' ')
                .Append(source.LlvmType).Append(' ').Append(source.Value).Append(", ").AppendLine(zero);
            return new NativeValue(targetType, "i1", temporary);
        }

        private NativeValue EmitBooleanToScalar(
            string methodName,
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source)
        {
            if (TryGetIntegerShape(targetType, _architecture, out _, out _))
            {
                var trueValue = AllOnes(targetLlvmType);
                var temporary = NextTemporary();
                _builder.Append("  ").Append(temporary).Append(" = select i1 ")
                    .Append(source.Value).Append(", ").Append(targetLlvmType).Append(' ').Append(trueValue)
                    .Append(", ").Append(targetLlvmType).Append(" 0").AppendLine();
                return new NativeValue(targetType, targetLlvmType, temporary);
            }

            if (targetType == TypeSymbol.Currency)
            {
                var temporary = NextTemporary();
                _builder.Append("  ").Append(temporary).Append(" = select i1 ")
                    .Append(source.Value).AppendLine(", i64 -10000, i64 0");
                return new NativeValue(targetType, targetLlvmType, temporary);
            }

            if (targetType == TypeSymbol.Single || targetType == TypeSymbol.Double)
            {
                return EmitBooleanSelect(
                    targetType,
                    targetLlvmType,
                    "-1.0",
                    source.Value);
            }

            if (targetType == TypeSymbol.Date)
            {
                return EmitBooleanSelect(
                    targetType,
                    targetLlvmType,
                    "1.0",
                    source.Value);
            }

            return RejectScalarConversion(methodName, targetType, source);
        }

        private NativeValue EmitBooleanSelect(
            TypeSymbol targetType,
            string targetLlvmType,
            string trueValue,
            string sourceValue)
        {
            var temporary = NextTemporary();
            _builder.Append("  ").Append(temporary).Append(" = select i1 ").Append(sourceValue).Append(", ")
                .Append(targetLlvmType).Append(' ').Append(trueValue)
                .Append(", ").Append(targetLlvmType).Append(" 0.0").AppendLine();
            return new NativeValue(targetType, targetLlvmType, temporary);
        }

        private NativeValue EmitIntegerConversion(
            string methodName,
            TypeSymbol targetType,
            string targetLlvmType,
            NativeValue source,
            int sourceBits,
            bool sourceUnsigned,
            int targetBits,
            bool targetUnsigned)
        {
            if (sourceBits == targetBits && sourceUnsigned == targetUnsigned)
            {
                return new NativeValue(targetType, targetLlvmType, source.Value);
            }

            var safeWidening = sourceBits < targetBits &&
                (sourceUnsigned == targetUnsigned || sourceUnsigned && !targetUnsigned);
            if (safeWidening)
            {
                var operation = sourceUnsigned ? "zext" : "sext";
                var temporary = NextTemporary();
                _builder.Append("  ").Append(temporary).Append(" = ").Append(operation).Append(' ')
                    .Append(source.LlvmType).Append(' ').Append(source.Value).Append(" to ")
                    .AppendLine(targetLlvmType);
                return new NativeValue(targetType, targetLlvmType, temporary);
            }

            var widened = ExtendIntegerToHelperWidth(source, sourceUnsigned);
            var call = NextTemporary();
            if (sourceUnsigned)
            {
                _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_uconvert_checked_i64(i64 ")
                    .Append(widened).Append(", i64 ")
                    .Append(targetUnsigned ? UnsignedMaximumLiteral(targetBits) : SignedMaximumLiteral(targetBits))
                    .AppendLine(")");
            }
            else
            {
                var minimum = targetUnsigned ? "0" : SignedMinimumLiteral(targetBits);
                var maximum = targetUnsigned
                    ? targetBits == 64 ? SignedMaximumLiteral(64) : UnsignedMaximumLiteral(targetBits)
                    : SignedMaximumLiteral(targetBits);
                _builder.Append("  ").Append(call).Append(" = call i64 @__vb6_sconvert_checked_i64(i64 ")
                    .Append(widened).Append(", i64 ").Append(minimum)
                    .Append(", i64 ").Append(maximum).AppendLine(")");
            }

            if (targetLlvmType == "i64")
            {
                return new NativeValue(targetType, targetLlvmType, call);
            }

            var narrowed = NextTemporary();
            _builder.Append("  ").Append(narrowed).Append(" = trunc i64 ").Append(call)
                .Append(" to ").AppendLine(targetLlvmType);
            return new NativeValue(targetType, targetLlvmType, narrowed);
        }

        private NativeValue RejectScalarConversion(
            string methodName,
            TypeSymbol targetType,
            NativeValue source)
        {
            AddDiagnostic(
                "VB6L0001",
                $"Native LLVM conversion '{methodName}' requires checked or rounded runtime semantics for '{source.SemanticType.Name}' to '{targetType.Name}'.");
            return ZeroValue(targetType);
        }

        private static bool IsScalarConversion(string methodName) => methodName is
            "CByte" or "CInt" or "CLng" or "CLngPtr" or "CUShort" or "CUInt" or "CULng" or
            "CDate" or "CVDate" or "CLngLng" or "CCur" or "CSng" or "CDbl" or "CBool" or
            "ConvertCByte" or "ConvertCInt" or "ConvertCLng" or "ConvertCLngPtr" or
            "ConvertCUShort" or "ConvertCUInt" or "ConvertCULng" or "ConvertCDate" or
            "ConvertCLngLng" or "ConvertCCur" or "ConvertCSng" or "ConvertCDbl" or
            "ConvertCBool" or "ConvertCStr";

        private static bool TryGetIntegerShape(
            TypeSymbol type,
            LlvmArchitecture architecture,
            out int bits,
            out bool unsigned)
        {
            if (type == TypeSymbol.Byte)
            {
                bits = 8;
                unsigned = true;
                return true;
            }

            if (type == TypeSymbol.Integer)
            {
                bits = 16;
                unsigned = false;
                return true;
            }

            if (type == TypeSymbol.Long)
            {
                bits = 32;
                unsigned = false;
                return true;
            }

            if (type == TypeSymbol.LongLong)
            {
                bits = 64;
                unsigned = false;
                return true;
            }

            if (type == TypeSymbol.LongPtr)
            {
                bits = architecture == LlvmArchitecture.X86 ? 32 : 64;
                unsigned = false;
                return true;
            }

            if (type == TypeSymbol.UShort)
            {
                bits = 16;
                unsigned = true;
                return true;
            }

            if (type == TypeSymbol.UInteger)
            {
                bits = 32;
                unsigned = true;
                return true;
            }

            if (type == TypeSymbol.ULong)
            {
                bits = 64;
                unsigned = true;
                return true;
            }

            bits = 0;
            unsigned = false;
            return false;
        }

        private static bool TryGetCheckedIntegerShape(
            TypeSymbol type,
            LlvmArchitecture architecture,
            out int bits,
            out bool unsigned)
        {
            if (type == TypeSymbol.Currency)
            {
                bits = 64;
                unsigned = false;
                return true;
            }

            return TryGetIntegerShape(type, architecture, out bits, out unsigned);
        }

        private NativeValue EmitProcedureCall(IrProcedureCallExpression call)
        {
            if (call.Receiver is not null)
            {
                AddDiagnostic("VB6L0001", "Native LLVM lowering for procedure receivers is not implemented yet.");
                return ZeroValue(call.ResultType);
            }

            if (call.Arguments.Length != call.Procedure.Parameters.Length)
            {
                AddDiagnostic(
                    "VB6L0004",
                    $"LLVM procedure call '{call.Procedure.Name}' has {call.Arguments.Length} argument(s), but the procedure declares {call.Procedure.Parameters.Length} parameter(s).");
            }

            var argumentCount = Math.Min(call.Arguments.Length, call.Procedure.Parameters.Length);
            var arguments = new List<string>(argumentCount);
            for (var index = 0; index < argumentCount; index++)
            {
                var argument = call.Arguments[index];
                var parameter = call.Procedure.Parameters[index];
                var irParameter = new IrParameter(
                    parameter,
                    index,
                    parameter.Name,
                    parameter.Type,
                    parameter.PassingMode);
                var parameterType = GetParameterLlvmType(irParameter);

                if (argument.Kind == IrCallArgumentKind.Address)
                {
                    if (parameter.PassingMode != ParameterPassingMode.ByRef ||
                        argument.Expression is not IrAddressExpression address)
                    {
                        AddDiagnostic(
                            "VB6L0004",
                            $"LLVM procedure call '{call.Procedure.Name}' has an invalid address argument for parameter '{parameter.Name}'.");
                        continue;
                    }

                    var pointer = EmitPlacePointer(address.Place, out var elementType);
                    if (pointer is null || elementType is null || parameterType != "ptr")
                    {
                        continue;
                    }

                    if (!string.Equals(elementType, GetTypeOrFallback(parameter.Type, $"parameter '{parameter.Name}'"), StringComparison.Ordinal))
                    {
                        AddDiagnostic(
                            "VB6L0004",
                            $"LLVM procedure call '{call.Procedure.Name}' address argument type does not match parameter '{parameter.Name}'.");
                        continue;
                    }

                    arguments.Add("ptr " + pointer);
                    continue;
                }

                if (parameter.PassingMode == ParameterPassingMode.ByRef)
                {
                    AddDiagnostic(
                        "VB6L0004",
                        $"LLVM procedure call '{call.Procedure.Name}' requires an address for ByRef parameter '{parameter.Name}'.");
                    continue;
                }

                var value = EmitExpression(argument.Expression);
                if (!string.Equals(value.LlvmType, parameterType, StringComparison.Ordinal))
                {
                    AddDiagnostic(
                        "VB6L0004",
                        $"LLVM procedure call '{call.Procedure.Name}' value argument type does not match parameter '{parameter.Name}'.");
                    continue;
                }

                arguments.Add(parameterType + " " + value.Value);
            }

            var returnType = call.Procedure.ReturnType is null
                ? "void"
                : GetTypeOrFallback(call.Procedure.ReturnType, $"procedure '{call.Procedure.Name}' return type");
            var result = returnType == "void" ? string.Empty : NextTemporary();
            if (result.Length != 0)
            {
                _builder.Append("  ").Append(result).Append(" = ");
            }
            else
            {
                _builder.Append("  ");
            }

            _builder.Append("call ").Append(returnType).Append(" @\"")
                .Append(EscapeIdentifier(GetExternalName(call.Procedure))).Append("\"(")
                .Append(string.Join(", ", arguments)).AppendLine(")");

            return new NativeValue(call.ResultType, returnType, result);
        }

        private static string GetExternalName(IrProcedure procedure) =>
            procedure.ExternalAlias ?? procedure.Symbol?.ExternalAlias ?? procedure.Name;

        private static string GetExternalName(ProcedureSymbol procedure) =>
            procedure.ExternalAlias ?? procedure.Name;

        private NativeValue EmitBinary(TypeSymbol resultType, NativeValue[] arguments, string operation)
        {
            if (arguments.Length != 2 || arguments[0].LlvmType != arguments[1].LlvmType)
            {
                AddDiagnostic("VB6L0004", $"LLVM binary operation '{operation}' requires two operands of the same type.");
                return ZeroValue(resultType);
            }

            var temporary = NextTemporary();
            _builder.Append("  ").Append(temporary).Append(" = ").Append(operation).Append(' ')
                .Append(arguments[0].LlvmType).Append(' ').Append(arguments[0].Value).Append(", ")
                .Append(arguments[1].Value).AppendLine();
            return new NativeValue(resultType, arguments[0].LlvmType, temporary);
        }

        private NativeValue EmitUnary(TypeSymbol resultType, NativeValue[] arguments, string operation)
        {
            if (arguments.Length != 1)
            {
                AddDiagnostic("VB6L0004", $"LLVM unary operation '{operation}' requires one operand.");
                return ZeroValue(resultType);
            }

            var temporary = NextTemporary();
            if (operation == "not")
            {
                _builder.Append("  ").Append(temporary).Append(" = xor ").Append(arguments[0].LlvmType)
                    .Append(' ').Append(arguments[0].Value).Append(", ").AppendLine(AllOnes(arguments[0].LlvmType));
            }
            else if (operation == "fneg")
            {
                _builder.Append("  ").Append(temporary).Append(" = fneg ").Append(arguments[0].LlvmType)
                    .Append(' ').AppendLine(arguments[0].Value);
            }
            else
            {
                _builder.Append("  ").Append(temporary).Append(" = sub ").Append(arguments[0].LlvmType)
                    .Append(" 0, ").AppendLine(arguments[0].Value);
            }

            return new NativeValue(resultType, arguments[0].LlvmType, temporary);
        }

        private NativeValue EmitEqv(TypeSymbol resultType, NativeValue[] arguments)
        {
            var xor = EmitBinary(resultType, arguments, "xor");
            return EmitUnary(resultType, new[] { xor }, "not");
        }

        private NativeValue EmitImp(TypeSymbol resultType, NativeValue[] arguments)
        {
            if (arguments.Length != 2 || arguments[0].LlvmType != arguments[1].LlvmType)
            {
                AddDiagnostic("VB6L0004", "LLVM Imp requires two operands of the same type.");
                return ZeroValue(resultType);
            }

            var notLeft = EmitUnary(arguments[0].SemanticType, new[] { arguments[0] }, "not");
            return EmitBinary(resultType, new[] { notLeft, arguments[1] }, "or");
        }

        private NativeValue EmitComparison(TypeSymbol resultType, NativeValue[] arguments, string operation)
        {
            if (arguments.Length != 2 || arguments[0].LlvmType != arguments[1].LlvmType)
            {
                AddDiagnostic("VB6L0004", $"LLVM comparison '{operation}' requires two operands of the same type.");
                return ZeroValue(resultType);
            }

            var temporary = NextTemporary();
            var floating = IsFloating(arguments);
            var predicate = operation switch
            {
                "Equal" => floating ? "oeq" : "eq",
                "NotEqual" => floating ? "one" : "ne",
                "Less" => floating ? "olt" : IsUnsigned(arguments[0].SemanticType) ? "ult" : "slt",
                "LessOrEqual" => floating ? "ole" : IsUnsigned(arguments[0].SemanticType) ? "ule" : "sle",
                "Greater" => floating ? "ogt" : IsUnsigned(arguments[0].SemanticType) ? "ugt" : "sgt",
                "GreaterOrEqual" => floating ? "oge" : IsUnsigned(arguments[0].SemanticType) ? "uge" : "sge",
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            var instruction = floating ? "fcmp" : "icmp";
            _builder.Append("  ").Append(temporary).Append(" = ").Append(instruction).Append(' ')
                .Append(predicate).Append(' ').Append(arguments[0].LlvmType).Append(' ')
                .Append(arguments[0].Value).Append(", ").AppendLine(arguments[1].Value);
            return new NativeValue(resultType, "i1", temporary);
        }

        private void EmitTerminator(IrTerminator terminator, string returnType)
        {
            switch (terminator)
            {
                case IrReturnTerminator ret:
                    if (ret.Value is null)
                    {
                        if (!string.Equals(returnType, "void", StringComparison.Ordinal))
                        {
                            AddDiagnostic("VB6L0004", "A non-void LLVM procedure requires a return value.");
                            _builder.Append("  ret ").Append(returnType).Append(' ')
                                .AppendLine(ZeroLiteral(returnType));
                        }
                        else
                        {
                            _builder.AppendLine("  ret void");
                        }
                    }
                    else
                    {
                        var value = EmitExpression(ret.Value);
                        if (!string.Equals(value.LlvmType, returnType, StringComparison.Ordinal))
                        {
                            AddDiagnostic("VB6L0004", "LLVM return value type does not match the procedure return type.");
                            _builder.Append("  ret ").Append(returnType).Append(' ')
                                .AppendLine(ZeroLiteral(returnType));
                        }
                        else
                        {
                            _builder.Append("  ret ").Append(returnType).Append(' ')
                                .AppendLine(value.Value);
                        }
                    }
                    return;
                case IrGotoTerminator go:
                    _builder.Append("  br label %").AppendLine(GetBlockLabel(go.TargetBlockId));
                    return;
                case IrConditionalTerminator conditional:
                    var condition = EmitExpression(conditional.Condition);
                    if (condition.LlvmType != "i1")
                    {
                        AddDiagnostic("VB6L0004", "LLVM conditional branches require an i1 condition.");
                    }
                    _builder.Append("  br i1 ").Append(condition.Value).Append(", label %")
                        .Append(GetBlockLabel(conditional.TrueBlockId)).Append(", label %")
                        .AppendLine(GetBlockLabel(conditional.FalseBlockId));
                    return;
                default:
                    AddDiagnostic(
                        "VB6L0001",
                        $"Native LLVM lowering for terminator '{terminator.GetType().Name}' is not implemented yet.");
                    _builder.Append("  ret ").Append(returnType).Append(' ')
                        .AppendLine(ZeroLiteral(returnType));
                    return;
            }
        }

        private string GetBlockLabel(int blockId)
        {
            if (_blockLabels.TryGetValue(blockId, out var label))
            {
                return label;
            }

            AddDiagnostic("VB6L0004", $"LLVM branch target block '{blockId}' does not exist.");
            return $"missing_{blockId}";
        }

        private NativeValue ZeroValue(TypeSymbol type)
        {
            var llvmType = GetTypeOrFallback(type, "zero value");
            return new NativeValue(type, llvmType, ZeroLiteral(llvmType));
        }

        private string GetTypeOrFallback(TypeSymbol? type, string context)
        {
            if (type is not null && TryGetLlvmType(type, _architecture, out var llvmType))
            {
                return llvmType;
            }

            if (type is not null)
            {
                AddUnsupportedType(type, context);
            }
            else if (!string.Equals(context, "procedure return type", StringComparison.Ordinal))
            {
                AddDiagnostic("VB6L0002", $"LLVM type for {context} is missing.");
            }

            return type is null ? "void" : "i32";
        }

        private void AddUnsupportedType(TypeSymbol type, string context) =>
            AddDiagnostic("VB6L0002", $"Native LLVM type for '{type.Name}' ({context}) is not implemented yet.");

        private void AddDiagnostic(string code, string message) =>
            _diagnostics.Add(new LlvmEmitDiagnostic(code, message));

        private string NextTemporary() => $"%t{_temporaryId++}";

        internal static bool TryGetLlvmType(TypeSymbol type, LlvmArchitecture architecture, out string llvmType)
        {
            llvmType = type switch
            {
                _ when type == TypeSymbol.Boolean => "i1",
                _ when type == TypeSymbol.Byte => "i8",
                _ when type == TypeSymbol.Integer => "i16",
                _ when type == TypeSymbol.Long => "i32",
                _ when type == TypeSymbol.LongLong => "i64",
                _ when type == TypeSymbol.LongPtr => architecture == LlvmArchitecture.X86 ? "i32" : "i64",
                _ when type == TypeSymbol.UShort => "i16",
                _ when type == TypeSymbol.UInteger => "i32",
                _ when type == TypeSymbol.ULong => "i64",
                _ when type == TypeSymbol.Single => "float",
                _ when type == TypeSymbol.Date || type == TypeSymbol.Double => "double",
                _ when type == TypeSymbol.Currency => "i64",
                _ => string.Empty
            };
            return llvmType.Length != 0;
        }

        internal static bool TryFormatConstant(object? value, TypeSymbol type, out string literal)
        {
            if (type == TypeSymbol.Boolean)
            {
                literal = Convert.ToBoolean(value, CultureInfo.InvariantCulture) ? "1" : "0";
                return true;
            }

            if (type == TypeSymbol.Single || type == TypeSymbol.Double || type == TypeSymbol.Date)
            {
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    literal = string.Empty;
                    return false;
                }

                literal = number.ToString("R", CultureInfo.InvariantCulture);
                if (!literal.Contains('.', StringComparison.Ordinal) &&
                    !literal.Contains('e', StringComparison.OrdinalIgnoreCase))
                {
                    literal += ".0";
                }

                return true;
            }

            if (type == TypeSymbol.Currency)
            {
                try
                {
                    var number = value is bool boolean
                        ? boolean ? -1m : 0m
                        : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    var scaled = decimal.Round(number, 4, MidpointRounding.ToEven) * 10_000m;
                    literal = decimal.ToInt64(scaled).ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            if (type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long ||
                type == TypeSymbol.LongLong || type == TypeSymbol.LongPtr || type == TypeSymbol.UShort ||
                type == TypeSymbol.UInteger || type == TypeSymbol.ULong)
            {
                try
                {
                    if (value is IntPtr pointer)
                    {
                        literal = pointer.ToInt64().ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (value is UIntPtr unsignedPointer)
                    {
                        literal = unsignedPointer.ToUInt64().ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    if (type == TypeSymbol.ULong || value is ulong)
                    {
                        literal = Convert.ToUInt64(value, CultureInfo.InvariantCulture)
                            .ToString(CultureInfo.InvariantCulture);
                        return true;
                    }

                    literal = Convert.ToInt64(value, CultureInfo.InvariantCulture)
                        .ToString(CultureInfo.InvariantCulture);
                    return true;
                }
                catch (FormatException)
                {
                }
                catch (InvalidCastException)
                {
                }
                catch (OverflowException)
                {
                }
            }

            literal = string.Empty;
            return false;
        }

        internal static string ZeroLiteral(string llvmType) => llvmType switch
        {
            "float" or "double" => "0.0",
            "void" => string.Empty,
            _ => "0"
        };

        private static string AllOnes(string llvmType) => llvmType switch
        {
            "i1" => "1",
            _ when llvmType.StartsWith("i", StringComparison.Ordinal) => "-1",
            _ => "0"
        };

        private static bool IsFloating(NativeValue[] arguments) =>
            arguments.Length > 0 && (arguments[0].LlvmType is "float" or "double");

        private static bool IsUnsigned(TypeSymbol type) =>
            type == TypeSymbol.Byte || type == TypeSymbol.UShort || type == TypeSymbol.UInteger || type == TypeSymbol.ULong;

        internal static string EscapeIdentifier(string identifier) =>
            identifier.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

        private sealed record NativeValue(TypeSymbol SemanticType, string LlvmType, string Value);
    }
}
