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
    public void Emit_LowersScalarStorageAndArithmetic()
    {
        var local = new IrLocal(0, "value", TypeSymbol.Long);
        var sum = new IrRuntimeCallExpression(
            IrRuntimeMethod.AddLong,
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
                new IrReturnTerminator(sum))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "%local_0 = alloca i32");
        StringAssert.Contains(result.ModuleText, "store i32 4, ptr %local_0");
        StringAssert.Contains(result.ModuleText, "load i32, ptr %local_0");
        StringAssert.Contains(result.ModuleText, "add i32");
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
    public void Emit_UsesScaledCurrencyStorageAndAddition()
    {
        var sum = new IrRuntimeCallExpression(
            IrRuntimeMethod.AddCurrency,
            ImmutableArray.Create<IrCallArgument>(
                new IrCallArgument(new IrConstantExpression(1.2345m, TypeSymbol.Currency)),
                new IrCallArgument(new IrConstantExpression(0.5m, TypeSymbol.Currency))),
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
                new IrReturnTerminator(sum))));

        var result = new LlvmEmitter().Emit(CreateProgram(procedure), new LlvmEmitOptions(LlvmArchitecture.X64));

        Assert.IsTrue(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        StringAssert.Contains(result.ModuleText, "define i64 @\"Main\"()");
        StringAssert.Contains(result.ModuleText, "add i64 12345, 5000");
        StringAssert.Contains(result.ModuleText, "ret i64 %t0");
    }

    [TestMethod]
    public void Emit_DiagnosesCurrencyMultiplicationUntilScaledArithmeticExists()
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

        Assert.IsFalse(result.Success);
        StringAssert.Contains(
            string.Join(Environment.NewLine, result.Diagnostics),
            "Currency multiplication requires checked scaled arithmetic");
        Assert.IsFalse(result.ModuleText.Contains("mul i64", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DiagnosesCheckedSingleArithmetic()
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

        Assert.IsFalse(result.Success);
        StringAssert.Contains(
            string.Join(Environment.NewLine, result.Diagnostics),
            "requires checked Single arithmetic runtime semantics");
        Assert.IsFalse(result.ModuleText.Contains("add float", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Emit_DiagnosesCheckedFloatingDivision()
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

        Assert.IsFalse(result.Success);
        StringAssert.Contains(
            string.Join(Environment.NewLine, result.Diagnostics),
            "requires checked floating-point division runtime semantics");
        Assert.IsFalse(result.ModuleText.Contains("fdiv double", StringComparison.Ordinal));
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
