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

            if (methodName.StartsWith("Add", StringComparison.Ordinal) ||
                methodName.StartsWith("Subtract", StringComparison.Ordinal) ||
                methodName.StartsWith("Multiply", StringComparison.Ordinal))
            {
                var operation = methodName.StartsWith("Add", StringComparison.Ordinal) ? "add" :
                    methodName.StartsWith("Subtract", StringComparison.Ordinal) ? "sub" : "mul";
                return EmitBinary(runtime.ResultType, arguments, operation);
            }

            if (methodName.StartsWith("IntegerDivide", StringComparison.Ordinal) ||
                methodName.StartsWith("Mod", StringComparison.Ordinal))
            {
                var unsigned = arguments.Length > 0 && IsUnsigned(arguments[0].SemanticType);
                var operation = methodName.StartsWith("IntegerDivide", StringComparison.Ordinal)
                    ? unsigned ? "udiv" : "sdiv"
                    : unsigned ? "urem" : "srem";
                return EmitBinary(runtime.ResultType, arguments, operation);
            }

            if (methodName is "DivideSingle" or "DivideDouble")
            {
                return EmitBinary(runtime.ResultType, arguments, "fdiv");
            }

            if (methodName.StartsWith("Negate", StringComparison.Ordinal))
            {
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

            AddDiagnostic(
                "VB6L0001",
                $"Native LLVM lowering for runtime method '{runtime.Method}' is not implemented yet.");
            return ZeroValue(runtime.ResultType);
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

            if (type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long ||
                type == TypeSymbol.LongLong || type == TypeSymbol.LongPtr || type == TypeSymbol.UShort ||
                type == TypeSymbol.UInteger || type == TypeSymbol.ULong || type == TypeSymbol.Currency)
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
