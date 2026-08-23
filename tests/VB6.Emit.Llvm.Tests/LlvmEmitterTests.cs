using System.Collections.Immutable;
using VB6.Emit.Llvm;
using VB6.IR;
using VB6.Semantics;

namespace VB6.Emit.Llvm.Tests;

[TestClass]
public sealed class LlvmEmitterTests
{
    [TestMethod]
    public void Emit_MapsLongPtrToTheSelectedNativeWidth()
    {
        var program = CreateProgram(new IrProcedure(
            null,
            "Main",
            TypeSymbol.LongPtr,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(ReturnBlock(new IrConstantExpression(new IntPtr(1), TypeSymbol.LongPtr)))));

        var x86 = new LlvmEmitter().Emit(program, new LlvmEmitOptions(LlvmArchitecture.X86));
        var x64 = new LlvmEmitter().Emit(program, new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(x86.Success, string.Join(Environment.NewLine, x86.Diagnostics));
        Assert.IsTrue(x64.Success, string.Join(Environment.NewLine, x64.Diagnostics));
        StringAssert.Contains(x86.ModuleText, "target triple = \"i686-pc-windows-msvc\"");
        StringAssert.Contains(x86.ModuleText, "define i32 @\"Main\"()");
        StringAssert.Contains(x64.ModuleText, "target triple = \"x86_64-pc-windows-msvc\"");
        StringAssert.Contains(x64.ModuleText, "define i64 @\"Main\"()");
    }

    [TestMethod]
    public void Emit_LowersScalarStorageAndBitwiseOperation()
    {
        var local = new IrLocal(0, "value", TypeSymbol.Long);
        var resultExpression = new IrRuntimeCallExpression(
            IrRuntimeMethod.AndLong,
            ImmutableArray.Create<IrCallArgument>(
                new(new IrLoadExpression(new IrLocalPlace(local))),
                new(new IrConstantExpression(2L, TypeSymbol.Long))),
            TypeSymbol.Long);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray.Create(local),
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(new IrLocalPlace(local), new IrConstantExpression(4L, TypeSymbol.Long))),
                new IrReturnTerminator(resultExpression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "%local_0 = alloca i32");
        StringAssert.Contains(result.ModuleText, "store i32 4, ptr %local_0");
        StringAssert.Contains(result.ModuleText, "load i32, ptr %local_0");
        StringAssert.Contains(result.ModuleText, "and i32");
        StringAssert.Contains(result.ModuleText, "ret i32");
    }

    [TestMethod]
    public void Emit_LowersConditionalBasicBlockBranches()
    {
        var condition = new IrRuntimeCallExpression(
            IrRuntimeMethod.Equal,
            ImmutableArray.Create<IrCallArgument>(
                new(new IrConstantExpression(1L, TypeSymbol.Long)),
                new(new IrConstantExpression(1L, TypeSymbol.Long))),
            TypeSymbol.Boolean);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(
                new IrBasicBlock(0, "entry", ImmutableArray<IrInstruction>.Empty, new IrConditionalTerminator(condition, 1, 2)),
                ReturnBlock(new IrConstantExpression(7L, TypeSymbol.Long), 1),
                ReturnBlock(new IrConstantExpression(9L, TypeSymbol.Long), 2)));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "icmp eq i32 1, 1");
        StringAssert.Contains(result.ModuleText, "br i1 %t0, label %bb1, label %bb2");
        StringAssert.Contains(result.ModuleText, "bb1:");
        StringAssert.Contains(result.ModuleText, "bb2:");
    }

    [TestMethod]
    public void Emit_LowersScalarByRefParametersAsPointers()
    {
        var parameter = new IrParameter(
            new ParameterSymbol("value", TypeSymbol.Long, ParameterPassingMode.ByRef),
            0,
            "value",
            TypeSymbol.Long,
            ParameterPassingMode.ByRef);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray.Create(parameter),
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrParameterPlace(parameter),
                        new IrConstantExpression(3L, TypeSymbol.Long))),
                new IrReturnTerminator(new IrLoadExpression(new IrParameterPlace(parameter))))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "define i32 @\"Main\"(ptr %arg0)");
        StringAssert.Contains(result.ModuleText, "store i32 3, ptr %arg0");
        StringAssert.Contains(result.ModuleText, "load i32, ptr %arg0");
    }

    [TestMethod]
    public void Emit_LowersScalarProcedureCallsWithValueAndByRefArguments()
    {
        var inputParameter = new ParameterSymbol("input", TypeSymbol.Long, ParameterPassingMode.ByVal);
        var outputParameter = new ParameterSymbol("output", TypeSymbol.Long, ParameterPassingMode.ByRef);
        var helperSymbol = new ProcedureSymbol(
            "AddWithOutput",
            ImmutableArray.Create(inputParameter, outputParameter),
            TypeSymbol.Long);
        var inputParameterIr = new IrParameter(
            inputParameter,
            0,
            inputParameter.Name,
            inputParameter.Type,
            inputParameter.PassingMode);
        var outputParameterIr = new IrParameter(
            outputParameter,
            1,
            outputParameter.Name,
            outputParameter.Type,
            outputParameter.PassingMode);
        var helper = new IrProcedure(
            helperSymbol,
            helperSymbol.Name,
            helperSymbol.ReturnType,
            ImmutableArray.Create(inputParameterIr, outputParameterIr),
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrParameterPlace(outputParameterIr),
                        new IrConstantExpression(8L, TypeSymbol.Long))),
                new IrReturnTerminator(new IrLoadExpression(new IrParameterPlace(inputParameterIr))))));

        var local = new IrLocal(0, "value", TypeSymbol.Long);
        var main = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray.Create(local),
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrLocalPlace(local),
                        new IrConstantExpression(2L, TypeSymbol.Long))),
                new IrReturnTerminator(new IrProcedureCallExpression(
                    helperSymbol,
                    ImmutableArray.Create(
                        new IrCallArgument(new IrConstantExpression(5L, TypeSymbol.Long)),
                        new IrCallArgument(
                            new IrAddressExpression(new IrLocalPlace(local)),
                            IrCallArgumentKind.Address)),
                    TypeSymbol.Long)))));

        var program = new IrProgram(
            ImmutableArray.Create(new IrModule(
                "Module1",
                null,
                ImmutableArray<IrGlobal>.Empty,
                ImmutableArray.Create(main, helper))),
            ImmutableArray<IrTypeDefinition>.Empty,
            main);

        var result = new LlvmEmitter().Emit(program, new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i32 @\"AddWithOutput\"(i32 5, ptr %local_0)");
        StringAssert.Contains(result.ModuleText, "define i32 @\"AddWithOutput\"(i32 %arg0, ptr %arg1)");
    }

    [TestMethod]
    public void Emit_LowersScalarModuleGlobalsAsNativeSlots()
    {
        var symbol = new ModuleVariableSymbol("counter", TypeSymbol.Long);
        var global = new IrGlobal(
            symbol,
            "counter",
            TypeSymbol.Long,
            new IrConstantExpression(4L, TypeSymbol.Long),
            false);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrGlobalPlace(global),
                        new IrConstantExpression(7L, TypeSymbol.Long))),
                new IrReturnTerminator(new IrLoadExpression(new IrGlobalPlace(global))))));
        var program = new IrProgram(
            ImmutableArray.Create(new IrModule(
                "Module1",
                null,
                ImmutableArray.Create(global),
                ImmutableArray.Create(procedure))),
            ImmutableArray<IrTypeDefinition>.Empty,
            procedure);

        var result = new LlvmEmitter().Emit(program, new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "@\"__vb6_global_Module1_counter_0\" = internal global i32 4");
        StringAssert.Contains(result.ModuleText, "store i32 7, ptr @\"__vb6_global_Module1_counter_0\"");
        StringAssert.Contains(result.ModuleText, "load i32, ptr @\"__vb6_global_Module1_counter_0\"");
    }

    [TestMethod]
    public void Emit_UsesScaledCurrencyLiterals()
    {
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Currency,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(new IrConstantExpression(1.2345m, TypeSymbol.Currency)))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "define i64 @\"Main\"()");
        StringAssert.Contains(result.ModuleText, "ret i64 12345");
    }

    [TestMethod]
    public void Emit_LowersCheckedIntegerArithmetic()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.AddLong,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1, TypeSymbol.Long)),
                new IrCallArgument(new IrConstantExpression(2, TypeSymbol.Long))),
            TypeSymbol.Long);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.SubtractLong,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(4, TypeSymbol.Long)),
                            new IrCallArgument(new IrConstantExpression(2, TypeSymbol.Long))),
                        TypeSymbol.Long)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.MultiplyLong,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(3, TypeSymbol.Long)),
                            new IrCallArgument(new IrConstantExpression(2, TypeSymbol.Long))),
                        TypeSymbol.Long)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.AddUShort,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(3, TypeSymbol.UShort)),
                            new IrCallArgument(new IrConstantExpression(2, TypeSymbol.UShort))),
                        TypeSymbol.UShort)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.NegateUShort,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(0, TypeSymbol.UShort))),
                        TypeSymbol.UShort))),
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sadd_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_ssub_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_smul_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_uadd_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_uneg_checked_i64");
        StringAssert.Contains(result.ModuleText, "@llvm.sadd.with.overflow.i64");
        StringAssert.Contains(result.ModuleText, "@llvm.umul.with.overflow.i64");
        StringAssert.Contains(result.ModuleText, "i64 -2147483648, i64 2147483647");
        StringAssert.Contains(result.ModuleText, "i64 0, i64 65535");
        StringAssert.Contains(result.ModuleText, "trunc i64 %t");
    }

    [TestMethod]
    public void Emit_LowersCheckedCurrencyArithmetic()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.NegateCurrency,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1m, TypeSymbol.Currency))),
            TypeSymbol.Currency);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Currency,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.AddCurrency,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(1m, TypeSymbol.Currency)),
                            new IrCallArgument(new IrConstantExpression(2m, TypeSymbol.Currency))),
                        TypeSymbol.Currency)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.SubtractCurrency,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(3m, TypeSymbol.Currency)),
                            new IrCallArgument(new IrConstantExpression(1m, TypeSymbol.Currency))),
                        TypeSymbol.Currency))),
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sadd_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_ssub_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sneg_checked_i64");
        StringAssert.Contains(result.ModuleText, "i64 -9223372036854775808, i64 9223372036854775807");
    }

    [TestMethod]
    public void Emit_LowersCheckedCurrencyMultiplication()
    {
        var product = new IrRuntimeCallExpression(
            IrRuntimeMethod.MultiplyCurrency,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1m, TypeSymbol.Currency)),
                new IrCallArgument(new IrConstantExpression(2m, TypeSymbol.Currency))),
            TypeSymbol.Currency);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Currency,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(product))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_mcurrency_checked_i64(i64 10000, i64 20000)");
        StringAssert.Contains(result.ModuleText, "%product = mul i128 %left_wide, %right_wide");
        StringAssert.Contains(result.ModuleText, "%quotient = sdiv i128 %product, 10000");
        StringAssert.Contains(result.ModuleText, "%remainder = srem i128 %product, 10000");
        StringAssert.Contains(result.ModuleText, "%tie_round = and i1 %exactly_half, %quotient_is_odd");
        StringAssert.Contains(result.ModuleText, "icmp slt i128 %result, -9223372036854775808");
        StringAssert.Contains(result.ModuleText, "%narrowed = trunc i128 %result to i64");
    }

    [TestMethod]
    public void Emit_LowersCheckedSingleArithmetic()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.AddSingle,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1f, TypeSymbol.Single)),
                new IrCallArgument(new IrConstantExpression(2f, TypeSymbol.Single))),
            TypeSymbol.Single);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Single,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call float @__vb6_fadd_checked_f32");
        StringAssert.Contains(result.ModuleText, "%result = fadd float %left, %right");
        StringAssert.Contains(result.ModuleText, "%is_infinite = or i1 %is_pos_inf, %is_neg_inf");
    }

    [TestMethod]
    public void Emit_LowersCheckedSingleNegation()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.NegateSingle,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1f, TypeSymbol.Single))),
            TypeSymbol.Single);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Single,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call float @__vb6_fneg_checked_f32");
        StringAssert.Contains(result.ModuleText, "%result = fneg float %value");
    }

    [TestMethod]
    public void Emit_LowersCheckedFloatingDivision()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.DivideDouble,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1d, TypeSymbol.Double)),
                new IrCallArgument(new IrConstantExpression(0d, TypeSymbol.Double))),
            TypeSymbol.Double);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Double,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call double @__vb6_fdiv_checked_f64");
        StringAssert.Contains(result.ModuleText, "%is_zero = fcmp oeq double %right, 0.0");
        StringAssert.Contains(result.ModuleText, "%result = fdiv double %left, %right");
    }

    [TestMethod]
    public void Emit_LowersCheckedSingleDivision()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.DivideSingle,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1f, TypeSymbol.Single)),
                new IrCallArgument(new IrConstantExpression(0f, TypeSymbol.Single))),
            TypeSymbol.Single);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Single,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call float @__vb6_fdiv_checked_f32");
        StringAssert.Contains(result.ModuleText, "%is_zero = fcmp oeq float %right, 0.0");
        StringAssert.Contains(result.ModuleText, "%is_pos_inf = fcmp oeq float %result, 0x7FF0000000000000");
        StringAssert.Contains(result.ModuleText, "%result = fdiv float %left, %right");
    }

    [TestMethod]
    public void Emit_LowersCheckedSignedIntegerDivision()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.IntegerDivideLong,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(7, TypeSymbol.Long)),
                new IrCallArgument(new IrConstantExpression(2, TypeSymbol.Long))),
            TypeSymbol.Long);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sdiv_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sdiv_checked_i64(i64 %t0, i64 %t1, i64 -2147483648)");
        StringAssert.Contains(result.ModuleText, "%is_overflow = and i1 %is_min, %is_negative_one");
        StringAssert.Contains(result.ModuleText, "ret i32");
    }

    [TestMethod]
    public void Emit_LowersCheckedSignedIntegerRemainder()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.ModInteger,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression((short)7, TypeSymbol.Integer)),
                new IrCallArgument(new IrConstantExpression((short)2, TypeSymbol.Integer))),
            TypeSymbol.Integer);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Integer,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_srem_checked_i64");
        StringAssert.Contains(result.ModuleText, "sext i16 7 to i64");
        StringAssert.Contains(result.ModuleText, "trunc i64");
    }

    [TestMethod]
    public void Emit_LowersCheckedUnsignedIntegerRemainder()
    {
        var expression = new IrRuntimeCallExpression(
            IrRuntimeMethod.ModUInteger,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(7u, TypeSymbol.UInteger)),
                new IrCallArgument(new IrConstantExpression(2u, TypeSymbol.UInteger))),
            TypeSymbol.UInteger);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.UInteger,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray<IrInstruction>.Empty,
                new IrReturnTerminator(expression))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_urem_checked_i64");
        StringAssert.Contains(result.ModuleText, "zext i32 7 to i64");
        StringAssert.Contains(result.ModuleText, "zext i32 2 to i64");
    }

    [TestMethod]
    public void Emit_LowersScalarExternalDeclarationsAndCalls()
    {
        var parameterSymbol = new ParameterSymbol("value", TypeSymbol.Long, ParameterPassingMode.ByVal);
        var outputParameterSymbol = new ParameterSymbol("output", TypeSymbol.Long, ParameterPassingMode.ByRef);
        var externalSymbol = new ProcedureSymbol(
            "GetValue",
            ImmutableArray.Create(parameterSymbol, outputParameterSymbol),
            TypeSymbol.Long)
        {
            IsExternal = true,
            ExternalLibrary = "kernel32",
            ExternalAlias = "NativeGetValue"
        };
        var parameter = new IrParameter(
            parameterSymbol,
            0,
            parameterSymbol.Name,
            parameterSymbol.Type,
            parameterSymbol.PassingMode);
        var outputParameter = new IrParameter(
            outputParameterSymbol,
            1,
            outputParameterSymbol.Name,
            outputParameterSymbol.Type,
            outputParameterSymbol.PassingMode);
        var external = new IrProcedure(
            externalSymbol,
            externalSymbol.Name,
            externalSymbol.ReturnType,
            ImmutableArray.Create(parameter, outputParameter),
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray<IrBasicBlock>.Empty,
            IsExternal: true,
            ExternalLibrary: externalSymbol.ExternalLibrary,
            ExternalAlias: externalSymbol.ExternalAlias);
        var local = new IrLocal(0, "output", TypeSymbol.Long);
        var main = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray.Create(local),
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrLocalPlace(local),
                        new IrConstantExpression(0L, TypeSymbol.Long))),
                new IrReturnTerminator(new IrProcedureCallExpression(
                    externalSymbol,
                    ImmutableArray.Create(
                        new IrCallArgument(new IrConstantExpression(3L, TypeSymbol.Long)),
                        new IrCallArgument(
                            new IrAddressExpression(new IrLocalPlace(local)),
                            IrCallArgumentKind.Address)),
                    TypeSymbol.Long)))));
        var program = new IrProgram(
            ImmutableArray.Create(new IrModule(
                "Module1",
                null,
                ImmutableArray<IrGlobal>.Empty,
                ImmutableArray.Create(external, main))),
            ImmutableArray<IrTypeDefinition>.Empty,
            main);

        var result = new LlvmEmitter().Emit(program, new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "; external library: kernel32");
        StringAssert.Contains(result.ModuleText, "declare i32 @\"NativeGetValue\"(i32, ptr)");
        StringAssert.Contains(result.ModuleText, "call i32 @\"NativeGetValue\"(i32 3, ptr %local_0)");
    }

    [TestMethod]
    public void Emit_LowersSafeScalarConversions()
    {
        var flag = new IrLocal(0, "flag", TypeSymbol.Boolean);
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray.Create(flag),
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrStoreInstruction(
                        new IrLocalPlace(flag),
                        new IrConstantExpression(true, TypeSymbol.Boolean)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CLngPtr,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(7L, TypeSymbol.Long))),
                        TypeSymbol.LongPtr)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CDbl,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(7L, TypeSymbol.Long))),
                        TypeSymbol.Double)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CBool,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(7L, TypeSymbol.Long))),
                        TypeSymbol.Boolean)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CLng,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrLoadExpression(new IrLocalPlace(flag)))),
                        TypeSymbol.Long))),
                new IrReturnTerminator(new IrConstantExpression(0L, TypeSymbol.Long)))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "sext i32 7 to i64");
        StringAssert.Contains(result.ModuleText, "sitofp i32 7 to double");
        StringAssert.Contains(result.ModuleText, "icmp ne i32 7, 0");
        StringAssert.Contains(result.ModuleText, "select i1 %t3, i32 -1, i32 0");
    }

    [TestMethod]
    public void Emit_LowersCheckedIntegerConversions()
    {
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CByte,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(255, TypeSymbol.Long))),
                        TypeSymbol.Byte)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CInt,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(-7, TypeSymbol.Long))),
                        TypeSymbol.Integer)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CLng,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(7u, TypeSymbol.UInteger))),
                        TypeSymbol.Long)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CUShort,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(65535UL, TypeSymbol.ULong))),
                        TypeSymbol.UShort)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CInt,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(2.5m, TypeSymbol.Currency))),
                        TypeSymbol.Integer)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CUShort,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(65534.5m, TypeSymbol.Currency))),
                        TypeSymbol.UShort))),
                new IrReturnTerminator(new IrConstantExpression(0L, TypeSymbol.Long)))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_sconvert_checked_i64");
        StringAssert.Contains(result.ModuleText, "i64 0, i64 255");
        StringAssert.Contains(result.ModuleText, "i64 -32768, i64 32767");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_uconvert_checked_i64");
        StringAssert.Contains(result.ModuleText, "i64 2147483647");
        StringAssert.Contains(result.ModuleText, "i64 65535");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_currency_to_integer_checked_i64");
        StringAssert.Contains(result.ModuleText, "%quotient = sdiv i64 %scaled, 10000");
        StringAssert.Contains(result.ModuleText, "i64 25000, i64 -32768, i64 32767");
        StringAssert.Contains(result.ModuleText, "trunc i64");
    }

    [TestMethod]
    public void Emit_LowersRoundedFloatingIntegerConversions()
    {
        var procedure = new IrProcedure(
            null,
            "Main",
            TypeSymbol.Long,
            ImmutableArray<IrParameter>.Empty,
            ImmutableArray<IrLocal>.Empty,
            ImmutableArray.Create(new IrBasicBlock(
                0,
                "entry",
                ImmutableArray.Create<IrInstruction>(
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CInt,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(2.5d, TypeSymbol.Double))),
                        TypeSymbol.Integer)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CByte,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(254.5f, TypeSymbol.Single))),
                        TypeSymbol.Byte)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CUShort,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(65534.5d, TypeSymbol.Double))),
                        TypeSymbol.UShort)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CLngLng,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(2.5d, TypeSymbol.Double))),
                        TypeSymbol.LongLong)),
                    new IrEvaluateInstruction(new IrRuntimeCallExpression(
                        IrRuntimeMethod.CULng,
                        ImmutableArray.Create<IrCallArgument>(
                            new IrCallArgument(new IrConstantExpression(2.5d, TypeSymbol.Double))),
                        TypeSymbol.ULong))),
                new IrReturnTerminator(new IrConstantExpression(0L, TypeSymbol.Long)))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "declare double @llvm.roundeven.f64(double)");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_fptosi_checked_i64");
        StringAssert.Contains(result.ModuleText, "call i64 @__vb6_fptoui_checked_i64");
        StringAssert.Contains(result.ModuleText, "double -32768.0, double 32767.0");
        StringAssert.Contains(result.ModuleText, "double 255.0");
        StringAssert.Contains(result.ModuleText, "double 65535.0");
        StringAssert.Contains(result.ModuleText, "double -9223372036854775808.0, double 9223372036854774784.0");
        StringAssert.Contains(result.ModuleText, "double 18446744073709549568.0");
        StringAssert.Contains(result.ModuleText, "fpext float 254.5 to double");
        StringAssert.Contains(result.ModuleText, "fptosi double %rounded to i64");
        StringAssert.Contains(result.ModuleText, "fptoui double %rounded to i64");
    }

    private static IrProgram CreateProgram(IrProcedure procedure) => new(
        ImmutableArray.Create(new IrModule(
            "Module1",
            null,
            ImmutableArray<IrGlobal>.Empty,
            ImmutableArray.Create(procedure))),
        ImmutableArray<IrTypeDefinition>.Empty,
        procedure);

    private static IrBasicBlock ReturnBlock(IrExpression value, int id = 0) =>
        new(id, $"block{id}", ImmutableArray<IrInstruction>.Empty, new IrReturnTerminator(value));
}
