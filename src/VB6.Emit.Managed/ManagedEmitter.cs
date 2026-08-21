using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using VB6.IR;
using VB6.Runtime;
using VB6.Semantics;

namespace VB6.Emit.Managed;

public sealed class ManagedEmitter
{
    public ManagedEmitResult Emit(IrProgram program, ManagedEmitOptions options)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AssemblyName);

        try
        {
            var context = new EmitContext(program, options);
            return context.Emit();
        }
        catch (Exception exception)
        {
            return new ManagedEmitResult(
                false,
                ImmutableArray.Create(new ManagedEmitDiagnostic("VB6E0001", exception.Message)),
                null,
                null);
        }
    }

    private sealed class EmitContext
    {
        private readonly IrProgram _program;
        private readonly ManagedEmitOptions _options;
        private readonly MetadataBuilder _metadata = new();
        private readonly BlobBuilder _ilStream = new();
        private readonly MethodBodyStreamEncoder _methodBodyStream;
        private readonly Dictionary<IrProcedure, MethodDefinitionHandle> _methodHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ProcedureSymbol, MethodDefinitionHandle> _procedureSymbolHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrGlobal, FieldDefinitionHandle> _globalHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrField, FieldDefinitionHandle> _fieldHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrTypeDefinition, TypeDefinitionHandle> _udtHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<UserDefinedTypeSymbol, TypeDefinitionHandle> _udtSymbolHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrModule, TypeDefinitionHandle> _moduleTypeHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TypeSymbol, TypeSpecificationHandle> _arrayTypeSpecs =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, MemberReferenceHandle> _memberReferences = new(StringComparer.Ordinal);
        private readonly Dictionary<Type, TypeReferenceHandle> _reflectionTypeRefs = new();
        private readonly Dictionary<string, TypeReferenceHandle> _namedTypeRefs = new(StringComparer.Ordinal);
        private readonly List<TypePlan> _typePlans = new();
        private AssemblyReferenceHandle _coreLibReference;
        private AssemblyReferenceHandle _runtimeReference;
        private TypeReferenceHandle _systemObject;
        private TypeReferenceHandle _systemValueType;
        private TypeReferenceHandle _vbCurrency;
        private TypeReferenceHandle _vbArray;
        private TypeReferenceHandle _vbArrayBound;

        public EmitContext(IrProgram program, ManagedEmitOptions options)
        {
            _program = program;
            _options = options;
            _methodBodyStream = new MethodBodyStreamEncoder(_ilStream);
        }

        public ManagedEmitResult Emit()
        {
            AddAssemblyAndModuleMetadata();
            AddAssemblyReferences();
            BuildPlansAndAssignHandles();
            EmitFieldDefinitions();
            var parameterStarts = EmitParameters();
            var bodyOffsets = EmitMethodBodies();
            EmitMethodDefinitions(parameterStarts, bodyOffsets);
            EmitTypeDefinitions();

            var entryPoint = _options.OutputKind == ManagedOutputKind.Application
                ? ResolveEntryPoint()
                : default;

            var characteristics = _options.OutputKind == ManagedOutputKind.Library
                ? Characteristics.ExecutableImage | Characteristics.Dll
                : Characteristics.ExecutableImage;
            var header = new PEHeaderBuilder(
                machine: _options.Platform == ManagedPlatform.X64 ? Machine.Amd64 : Machine.I386,
                imageCharacteristics: characteristics,
                subsystem: Subsystem.WindowsCui);
            var flags = CorFlags.ILOnly;
            if (_options.Platform == ManagedPlatform.X86)
            {
                flags |= CorFlags.Requires32Bit;
            }

            var peBuilder = new ManagedPEBuilder(
                header,
                new MetadataRootBuilder(_metadata),
                _ilStream,
                entryPoint: entryPoint,
                flags: flags,
                deterministicIdProvider: DeterministicContentId);
            var peBlob = new BlobBuilder();
            peBuilder.Serialize(peBlob);

            return new ManagedEmitResult(
                true,
                ImmutableArray<ManagedEmitDiagnostic>.Empty,
                peBlob.ToArray(),
                null);
        }

        private void AddAssemblyAndModuleMetadata()
        {
            var logicalIdentity = _options.AssemblyName + "\n" + _options.OutputKind + "\n" +
                                  _options.Platform + "\n" + IrDumper.Dump(_program);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(logicalIdentity));
            var guidBytes = hash.AsSpan(0, 16).ToArray();
            var mvid = new Guid(guidBytes);

            _metadata.AddModule(
                0,
                _metadata.GetOrAddString(_options.AssemblyName +
                    (_options.OutputKind == ManagedOutputKind.Library ? ".dll" : ".exe")),
                _metadata.GetOrAddGuid(mvid),
                default,
                default);
            _metadata.AddAssembly(
                _metadata.GetOrAddString(_options.AssemblyName),
                new Version(1, 0, 0, 0),
                default,
                default,
                AssemblyFlags.None,
                AssemblyHashAlgorithm.Sha256);
        }

        private void AddAssemblyReferences()
        {
            _coreLibReference = AddAssemblyReference(typeof(object).Assembly.GetName());
            _runtimeReference = AddAssemblyReference(typeof(VBConversions).Assembly.GetName());

            _systemObject = AddTypeReference(_coreLibReference, "System", "Object");
            _systemValueType = AddTypeReference(_coreLibReference, "System", "ValueType");
            AddPrimitiveTypeRefs();
            _vbCurrency = AddTypeReference(_runtimeReference, "VB6.Runtime", "VBCurrency");
            _vbArray = AddTypeReference(_runtimeReference, "VB6.Runtime", "VBArray`1");
            _vbArrayBound = AddTypeReference(_runtimeReference, "VB6.Runtime", "VBArrayBound");
        }

        private void AddPrimitiveTypeRefs()
        {
            foreach (var type in new[]
                     {
                         typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double),
                         typeof(bool), typeof(string), typeof(object), typeof(decimal), typeof(ValueType)
                     })
            {
                GetReflectionTypeReference(type);
            }
        }

        private AssemblyReferenceHandle AddAssemblyReference(AssemblyName assemblyName)
        {
            var token = assemblyName.GetPublicKeyToken();
            return _metadata.AddAssemblyReference(
                _metadata.GetOrAddString(assemblyName.Name ?? throw new InvalidOperationException("Assembly has no name.")),
                assemblyName.Version ?? new Version(0, 0, 0, 0),
                string.IsNullOrEmpty(assemblyName.CultureName) ? default : _metadata.GetOrAddString(assemblyName.CultureName),
                token is { Length: > 0 } ? _metadata.GetOrAddBlob(token) : default,
                AssemblyFlags.None,
                default);
        }

        private TypeReferenceHandle AddTypeReference(EntityHandle scope, string @namespace, string name)
        {
            var key = MetadataTokens.GetToken(scope) + ":" + @namespace + "." + name;
            if (_namedTypeRefs.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var handle = _metadata.AddTypeReference(
                scope,
                _metadata.GetOrAddString(@namespace),
                _metadata.GetOrAddString(name));
            _namedTypeRefs.Add(key, handle);
            return handle;
        }

        private TypeReferenceHandle GetReflectionTypeReference(Type type)
        {
            if (_reflectionTypeRefs.TryGetValue(type, out var handle))
            {
                return handle;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                type = type.GetGenericTypeDefinition();
            }

            var assembly = type.Assembly == typeof(VBConversions).Assembly
                ? _runtimeReference
                : _coreLibReference;
            handle = AddTypeReference(assembly, type.Namespace ?? string.Empty, type.Name);
            _reflectionTypeRefs[type] = handle;
            return handle;
        }

        private void BuildPlansAndAssignHandles()
        {
            _typePlans.Add(TypePlan.ModuleType());
            foreach (var type in _program.TypeDefinitions)
            {
                _typePlans.Add(TypePlan.ForUdt(type));
            }
            foreach (var module in _program.Modules)
            {
                _typePlans.Add(TypePlan.ForModule(module));
            }

            var nextType = 1;
            var nextField = 1;
            var nextMethod = 1;
            foreach (var plan in _typePlans)
            {
                plan.TypeHandle = MetadataTokens.TypeDefinitionHandle(nextType++);
                plan.FirstField = MetadataTokens.FieldDefinitionHandle(nextField);
                plan.FirstMethod = MetadataTokens.MethodDefinitionHandle(nextMethod);

                if (plan.Udt is not null)
                {
                    _udtHandles.Add(plan.Udt, plan.TypeHandle);
                    _udtSymbolHandles.Add(plan.Udt.Symbol, plan.TypeHandle);
                    foreach (var field in plan.Udt.Fields)
                    {
                        _fieldHandles.Add(field, MetadataTokens.FieldDefinitionHandle(nextField++));
                    }
                    foreach (var method in plan.Udt.Methods)
                    {
                        AssignMethodHandle(method, ref nextMethod);
                    }
                }
                else if (plan.Module is not null)
                {
                    _moduleTypeHandles.Add(plan.Module, plan.TypeHandle);
                    foreach (var global in plan.Module.Globals)
                    {
                        _globalHandles.Add(global, MetadataTokens.FieldDefinitionHandle(nextField++));
                    }
                    foreach (var method in plan.Module.Procedures)
                    {
                        AssignMethodHandle(method, ref nextMethod);
                    }
                }
            }
        }

        private void AssignMethodHandle(IrProcedure procedure, ref int nextMethod)
        {
            var handle = MetadataTokens.MethodDefinitionHandle(nextMethod++);
            _methodHandles.Add(procedure, handle);
            if (procedure.Symbol is not null)
            {
                _procedureSymbolHandles[procedure.Symbol] = handle;
            }
        }

        private void EmitFieldDefinitions()
        {
            foreach (var plan in _typePlans)
            {
                if (plan.Udt is not null)
                {
                    foreach (var field in plan.Udt.Fields)
                    {
                        var actual = _metadata.AddFieldDefinition(
                            FieldAttributes.Assembly,
                            _metadata.GetOrAddString(field.Name),
                            EncodeFieldSignature(field.Type));
                        EnsureHandle(actual, _fieldHandles[field], "field");
                    }
                }
                else if (plan.Module is not null)
                {
                    foreach (var global in plan.Module.Globals)
                    {
                        var actual = _metadata.AddFieldDefinition(
                            FieldAttributes.Assembly | FieldAttributes.Static,
                            _metadata.GetOrAddString(global.Name),
                            EncodeFieldSignature(global.Type));
                        EnsureHandle(actual, _globalHandles[global], "global field");
                    }
                }
            }
        }

        private Dictionary<IrProcedure, ParameterHandle> EmitParameters()
        {
            var result = new Dictionary<IrProcedure, ParameterHandle>(ReferenceEqualityComparer.Instance);
            foreach (var procedure in AllProcedures())
            {
                var first = MetadataTokens.ParameterHandle(_metadata.GetRowCount(TableIndex.Param) + 1);
                result.Add(procedure, first);
                foreach (var parameter in procedure.Parameters.OrderBy(parameter => parameter.Index))
                {
                    _metadata.AddParameter(
                        ParameterAttributes.None,
                        _metadata.GetOrAddString(parameter.Name),
                        parameter.Index + 1);
                }
            }
            return result;
        }

        private Dictionary<IrProcedure, int> EmitMethodBodies()
        {
            var result = new Dictionary<IrProcedure, int>(ReferenceEqualityComparer.Instance);
            foreach (var procedure in AllProcedures())
            {
                var code = new BlobBuilder();
                var flow = new ControlFlowBuilder();
                var encoder = new InstructionEncoder(code, flow);
                var blockLabels = procedure.Blocks.ToDictionary(block => block.Id, _ => encoder.DefineLabel());
                var entry = procedure.Blocks.FirstOrDefault(block => block.Label.EndsWith("_entry", StringComparison.Ordinal))
                            ?? procedure.Blocks.FirstOrDefault();
                if (entry is null)
                {
                    encoder.OpCode(ILOpCode.Ret);
                }
                else
                {
                    encoder.Branch(ILOpCode.Br, blockLabels[entry.Id]);
                    foreach (var block in procedure.Blocks)
                    {
                        encoder.MarkLabel(blockLabels[block.Id]);
                        foreach (var instruction in block.Instructions)
                        {
                            EmitInstruction(encoder, procedure, instruction);
                        }
                        EmitTerminator(encoder, procedure, block.Terminator, blockLabels);
                    }
                }

                var localSignature = EncodeLocalSignature(procedure);
                var offset = _methodBodyStream.AddMethodBody(
                    encoder,
                    maxStack: 64,
                    localVariablesSignature: localSignature,
                    attributes: MethodBodyAttributes.InitLocals);
                result.Add(procedure, offset);
            }
            return result;
        }

        private void EmitInstruction(InstructionEncoder encoder, IrProcedure procedure, IrInstruction instruction)
        {
            switch (instruction)
            {
                case IrStoreInstruction store:
                    EmitStore(encoder, procedure, store.Target, store.Value);
                    break;
                case IrStoreAddressInstruction address:
                    EmitExpression(encoder, procedure, address.Address);
                    encoder.StoreLocal(address.AddressLocal.Id);
                    break;
                case IrEvaluateInstruction evaluate:
                    EmitExpression(encoder, procedure, evaluate.Expression);
                    if (evaluate.Expression.Type != TypeSymbol.Error)
                    {
                        encoder.OpCode(ILOpCode.Pop);
                    }
                    break;
                case IrNopInstruction:
                    encoder.OpCode(ILOpCode.Nop);
                    break;
                default:
                    throw new NotSupportedException($"Managed emit does not support IR instruction '{instruction.GetType().Name}'.");
            }
        }

        private void EmitTerminator(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrTerminator terminator,
            IReadOnlyDictionary<int, LabelHandle> labels)
        {
            switch (terminator)
            {
                case IrGotoTerminator go:
                    encoder.Branch(ILOpCode.Br, labels[go.TargetBlockId]);
                    break;
                case IrConditionalTerminator conditional:
                    EmitExpression(encoder, procedure, conditional.Condition);
                    encoder.Branch(ILOpCode.Brtrue, labels[conditional.TrueBlockId]);
                    encoder.Branch(ILOpCode.Br, labels[conditional.FalseBlockId]);
                    break;
                case IrReturnTerminator ret:
                    if (ret.Value is not null)
                    {
                        EmitExpression(encoder, procedure, ret.Value);
                    }
                    encoder.OpCode(ILOpCode.Ret);
                    break;
                default:
                    throw new NotSupportedException($"Managed emit does not support IR terminator '{terminator.GetType().Name}'.");
            }
        }

        private void EmitExpression(InstructionEncoder encoder, IrProcedure procedure, IrExpression expression)
        {
            switch (expression)
            {
                case IrConstantExpression constant:
                    EmitConstant(encoder, constant);
                    break;
                case IrDefaultExpression:
                    EmitDefault(encoder, procedure, expression.Type);
                    break;
                case IrNullExpression:
                    encoder.OpCode(ILOpCode.Ldnull);
                    break;
                case IrLoadExpression load:
                    EmitLoad(encoder, procedure, load.Place);
                    break;
                case IrAddressExpression address:
                    EmitAddress(encoder, procedure, address.Place);
                    break;
                case IrLocalAddressExpression localAddress:
                    encoder.LoadLocal(localAddress.Local.Id);
                    break;
                case IrRuntimeCallExpression call:
                    EmitRuntimeCall(encoder, procedure, call);
                    break;
                case IrProcedureCallExpression call:
                    EmitProcedureCall(encoder, procedure, call);
                    break;
                case IrArrayCallExpression arrayCall:
                    EmitArrayCall(encoder, procedure, arrayCall);
                    break;
                case IrNewVBArrayExpression newArray:
                    EmitNewArray(encoder, procedure, newArray);
                    break;
                case IrReDimPreserveExpression preserve:
                    EmitReDimPreserve(encoder, procedure, preserve);
                    break;
                default:
                    throw new NotSupportedException($"Managed emit does not support IR expression '{expression.GetType().Name}'.");
            }
        }

        private void EmitConstant(InstructionEncoder encoder, IrConstantExpression constant)
        {
            if (constant.ConstantType == TypeSymbol.String)
            {
                encoder.LoadString(_metadata.GetOrAddUserString(Convert.ToString(constant.Value) ?? string.Empty));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Boolean)
            {
                encoder.LoadConstantI4(Convert.ToBoolean(constant.Value) ? 1 : 0);
                return;
            }
            if (constant.ConstantType == TypeSymbol.Byte || constant.ConstantType == TypeSymbol.Integer || constant.ConstantType == TypeSymbol.Long)
            {
                encoder.LoadConstantI4(Convert.ToInt32(constant.Value));
                return;
            }
            if (constant.ConstantType == TypeSymbol.LongLong)
            {
                encoder.LoadConstantI8(Convert.ToInt64(constant.Value));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Single)
            {
                encoder.LoadConstantR4(Convert.ToSingle(constant.Value));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Double)
            {
                encoder.LoadConstantR8(Convert.ToDouble(constant.Value));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Currency)
            {
                var value = Convert.ToDecimal(constant.Value);
                var scaled = checked((long)(decimal.Round(value, 4, MidpointRounding.ToEven) * 10_000m));
                encoder.LoadConstantI8(scaled);
                encoder.Call(GetRuntimeMethodReference(
                    typeof(VBCurrency).GetMethod("FromScaled", BindingFlags.Public | BindingFlags.Static, new[] { typeof(long) })
                    ?? throw new MissingMethodException("VBCurrency.FromScaled(long) is required by the managed backend.")));
                return;
            }

            if (constant.Value is null)
            {
                encoder.OpCode(ILOpCode.Ldnull);
                return;
            }
            throw new NotSupportedException($"Constant type '{constant.ConstantType.Name}' is not supported by managed emit.");
        }

        private void EmitDefault(InstructionEncoder encoder, IrProcedure procedure, TypeSymbol type)
        {
            if (IsReferenceType(type))
            {
                if (type == TypeSymbol.String)
                {
                    encoder.LoadString(_metadata.GetOrAddUserString(string.Empty));
                }
                else
                {
                    encoder.OpCode(ILOpCode.Ldnull);
                }
                return;
            }

            var scratch = GetScratchLocal(procedure, type);
            encoder.LoadLocalAddress(scratch);
            encoder.OpCode(ILOpCode.Initobj);
            encoder.Token(GetTypeEntityHandle(type));
            encoder.LoadLocal(scratch);
        }

        private void EmitLoad(InstructionEncoder encoder, IrProcedure procedure, IrPlace place)
        {
            switch (place)
            {
                case IrLocalPlace local:
                    encoder.LoadLocal(local.Local.Id);
                    break;
                case IrParameterPlace parameter:
                    encoder.LoadArgument(parameter.Parameter.Index);
                    if (parameter.Parameter.PassingMode == ParameterPassingMode.ByRef)
                    {
                        EmitLoadIndirect(encoder, parameter.Type);
                    }
                    break;
                case IrGlobalPlace global:
                    encoder.OpCode(ILOpCode.Ldsfld);
                    encoder.Token(_globalHandles[global.Global]);
                    break;
                case IrFieldPlace field:
                    EmitAddress(encoder, procedure, field.Receiver);
                    encoder.OpCode(ILOpCode.Ldfld);
                    encoder.Token(_fieldHandles[field.Field]);
                    break;
                case IrArrayElementPlace element:
                    EmitArrayElementAddress(encoder, procedure, element);
                    EmitLoadIndirect(encoder, element.ElementType);
                    break;
                case IrIndirectPlace indirect:
                    EmitExpression(encoder, procedure, indirect.Address);
                    EmitLoadIndirect(encoder, indirect.ElementType);
                    break;
                default:
                    throw new NotSupportedException($"Managed load does not support place '{place.GetType().Name}'.");
            }
        }

        private void EmitStore(InstructionEncoder encoder, IrProcedure procedure, IrPlace place, IrExpression value)
        {
            switch (place)
            {
                case IrLocalPlace local:
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, local.Type);
                    encoder.StoreLocal(local.Local.Id);
                    break;
                case IrParameterPlace parameter when parameter.Parameter.PassingMode == ParameterPassingMode.ByVal:
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, parameter.Type);
                    encoder.StoreArgument(parameter.Parameter.Index);
                    break;
                case IrParameterPlace parameter:
                    encoder.LoadArgument(parameter.Parameter.Index);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, parameter.Type);
                    EmitStoreIndirect(encoder, parameter.Type);
                    break;
                case IrGlobalPlace global:
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, global.Type);
                    encoder.OpCode(ILOpCode.Stsfld);
                    encoder.Token(_globalHandles[global.Global]);
                    break;
                case IrFieldPlace field:
                    EmitAddress(encoder, procedure, field.Receiver);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, field.Type);
                    encoder.OpCode(ILOpCode.Stfld);
                    encoder.Token(_fieldHandles[field.Field]);
                    break;
                case IrArrayElementPlace element:
                    EmitArrayElementAddress(encoder, procedure, element);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, element.ElementType);
                    EmitStoreIndirect(encoder, element.ElementType);
                    break;
                case IrIndirectPlace indirect:
                    EmitExpression(encoder, procedure, indirect.Address);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, indirect.ElementType);
                    EmitStoreIndirect(encoder, indirect.ElementType);
                    break;
                default:
                    throw new NotSupportedException($"Managed store does not support place '{place.GetType().Name}'.");
            }
        }

        private void EmitAddress(InstructionEncoder encoder, IrProcedure procedure, IrPlace place)
        {
            switch (place)
            {
                case IrLocalPlace local:
                    encoder.LoadLocalAddress(local.Local.Id);
                    break;
                case IrParameterPlace parameter:
                    if (parameter.Parameter.PassingMode == ParameterPassingMode.ByRef)
                    {
                        encoder.LoadArgument(parameter.Parameter.Index);
                    }
                    else
                    {
                        encoder.LoadArgumentAddress(parameter.Parameter.Index);
                    }
                    break;
                case IrGlobalPlace global:
                    encoder.OpCode(ILOpCode.Ldsflda);
                    encoder.Token(_globalHandles[global.Global]);
                    break;
                case IrFieldPlace field:
                    EmitAddress(encoder, procedure, field.Receiver);
                    encoder.OpCode(ILOpCode.Ldflda);
                    encoder.Token(_fieldHandles[field.Field]);
                    break;
                case IrArrayElementPlace element:
                    EmitArrayElementAddress(encoder, procedure, element);
                    break;
                case IrIndirectPlace indirect:
                    EmitExpression(encoder, procedure, indirect.Address);
                    break;
                default:
                    throw new NotSupportedException($"Managed address does not support place '{place.GetType().Name}'.");
            }
        }

        private void EmitExpressionWithAssignmentConversion(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrExpression value,
            TypeSymbol targetType)
        {
            EmitExpression(encoder, procedure, value);
            if (targetType == TypeSymbol.Variant && IsValueType(value.Type))
            {
                encoder.OpCode(ILOpCode.Box);
                encoder.Token(GetTypeEntityHandle(value.Type));
            }
        }

        private void EmitRuntimeCall(InstructionEncoder encoder, IrProcedure procedure, IrRuntimeCallExpression call)
        {
            var info = ResolveRuntimeMethod(call, out var skippedArgument);
            var parameters = info.GetParameters();
            var emittedIndex = 0;
            for (var index = 0; index < call.Arguments.Length; index++)
            {
                if (index == skippedArgument)
                {
                    continue;
                }

                var argument = call.Arguments[index];
                EmitExpression(encoder, procedure, argument.Expression);
                var target = parameters[emittedIndex++].ParameterType;
                if (target == typeof(object) && IsValueType(argument.Expression.Type))
                {
                    encoder.OpCode(ILOpCode.Box);
                    encoder.Token(GetTypeEntityHandle(argument.Expression.Type));
                }
            }
            encoder.Call(GetRuntimeMethodReference(info));
        }

        private void EmitProcedureCall(InstructionEncoder encoder, IrProcedure procedure, IrProcedureCallExpression call)
        {
            foreach (var argument in call.Arguments)
            {
                EmitExpression(encoder, procedure, argument.Expression);
            }
            if (!_procedureSymbolHandles.TryGetValue(call.Procedure, out var target))
            {
                throw new InvalidOperationException($"Procedure '{call.Procedure.Name}' has no managed method definition.");
            }
            encoder.Call(target);
        }

        private void EmitArrayElementAddress(InstructionEncoder encoder, IrProcedure procedure, IrArrayElementPlace element)
        {
            EmitExpression(encoder, procedure, element.Array);
            EmitInt32Array(encoder, procedure, element.Indices);
            encoder.Call(GetArrayMemberReference(
                ((ArrayTypeSymbol)element.Array.Type).ElementType,
                "get_Item",
                element.ElementType,
                returnByRef: true,
                returnUsesTypeParameter: true,
                typeof(int[])));
        }

        private void EmitArrayCall(InstructionEncoder encoder, IrProcedure procedure, IrArrayCallExpression call)
        {
            if (call.Array.Type is not ArrayTypeSymbol arrayType)
            {
                throw new InvalidOperationException("Array call receiver is not a VB6 array type.");
            }
            EmitExpression(encoder, procedure, call.Array);
            foreach (var argument in call.Arguments)
            {
                EmitExpression(encoder, procedure, argument);
            }

            var member = call.Operation switch
            {
                IrArrayOperation.Clear => GetArrayMemberReference(arrayType.ElementType, "Clear", null),
                IrArrayOperation.LBound => GetArrayMemberReference(
                    arrayType.ElementType, "LBound", TypeSymbol.Long, false, false, typeof(int)),
                IrArrayOperation.UBound => GetArrayMemberReference(
                    arrayType.ElementType, "UBound", TypeSymbol.Long, false, false, typeof(int)),
                IrArrayOperation.Length => GetArrayMemberReference(
                    arrayType.ElementType, "get_Length", TypeSymbol.Long),
                IrArrayOperation.GetFlatValue => GetArrayMemberReference(
                    arrayType.ElementType,
                    "GetValueAtFlatIndex",
                    arrayType.ElementType,
                    returnByRef: false,
                    returnUsesTypeParameter: true,
                    typeof(int)),
                _ => throw new NotSupportedException($"Array operation '{call.Operation}' is not supported yet.")
            };
            encoder.Call(member);
        }

        private void EmitNewArray(InstructionEncoder encoder, IrProcedure procedure, IrNewVBArrayExpression expression)
        {
            EmitVBArrayBounds(encoder, procedure, expression.Bounds);
            var ctor = GetArrayConstructorReference(expression.ArrayType.ElementType);
            encoder.OpCode(ILOpCode.Newobj);
            encoder.Token(ctor);
        }

        private void EmitReDimPreserve(InstructionEncoder encoder, IrProcedure procedure, IrReDimPreserveExpression expression)
        {
            EmitExpression(encoder, procedure, expression.Array);
            EmitVBArrayBounds(encoder, procedure, expression.Bounds);
            encoder.Call(GetArrayMemberReference(
                expression.ArrayType.ElementType,
                "ReDimPreserve",
                expression.ArrayType,
                returnByRef: false,
                returnUsesTypeParameter: true,
                typeof(VBArrayBound[])));
        }

        private void EmitVBArrayBounds(InstructionEncoder encoder, IrProcedure procedure, ImmutableArray<IrArrayBound> bounds)
        {
            encoder.LoadConstantI4(bounds.Length);
            encoder.OpCode(ILOpCode.Newarr);
            encoder.Token(_vbArrayBound);
            var ctor = GetVBArrayBoundConstructor();
            for (var index = 0; index < bounds.Length; index++)
            {
                encoder.OpCode(ILOpCode.Dup);
                encoder.LoadConstantI4(index);
                EmitExpression(encoder, procedure, bounds[index].Lower);
                EmitExpression(encoder, procedure, bounds[index].Upper);
                encoder.OpCode(ILOpCode.Newobj);
                encoder.Token(ctor);
                encoder.OpCode(ILOpCode.Stelem);
                encoder.Token(_vbArrayBound);
            }
        }

        private void EmitInt32Array(InstructionEncoder encoder, IrProcedure procedure, ImmutableArray<IrExpression> indices)
        {
            encoder.LoadConstantI4(indices.Length);
            encoder.OpCode(ILOpCode.Newarr);
            encoder.Token(GetReflectionTypeReference(typeof(int)));
            for (var index = 0; index < indices.Length; index++)
            {
                encoder.OpCode(ILOpCode.Dup);
                encoder.LoadConstantI4(index);
                EmitExpression(encoder, procedure, indices[index]);
                encoder.OpCode(ILOpCode.Stelem_i4);
            }
        }

        private void EmitLoadIndirect(InstructionEncoder encoder, TypeSymbol type)
        {
            encoder.OpCode(ILOpCode.Ldobj);
            encoder.Token(GetTypeEntityHandle(type));
        }

        private void EmitStoreIndirect(InstructionEncoder encoder, TypeSymbol type)
        {
            encoder.OpCode(ILOpCode.Stobj);
            encoder.Token(GetTypeEntityHandle(type));
        }

        private StandaloneSignatureHandle EncodeLocalSignature(IrProcedure procedure)
        {
            if (procedure.Locals.IsDefaultOrEmpty)
            {
                return default;
            }

            var blob = new BlobBuilder();
            var variables = new BlobEncoder(blob).LocalVariableSignature(procedure.Locals.Length);
            foreach (var local in procedure.Locals.OrderBy(local => local.Id))
            {
                EncodeType(variables.AddVariable().Type(isByRef: local.IsManagedAddress), local.Type);
            }
            return _metadata.AddStandaloneSignature(_metadata.GetOrAddBlob(blob));
        }

        private BlobHandle EncodeFieldSignature(TypeSymbol type)
        {
            var blob = new BlobBuilder();
            EncodeType(new BlobEncoder(blob).FieldSignature(), type);
            return _metadata.GetOrAddBlob(blob);
        }

        private BlobHandle EncodeMethodSignature(IrProcedure procedure)
        {
            var blob = new BlobBuilder();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: !procedure.IsStatic).Parameters(
                procedure.Parameters.Length,
                returnType =>
                {
                    if (procedure.ReturnType is null)
                    {
                        returnType.Void();
                    }
                    else
                    {
                        EncodeType(returnType.Type(), procedure.ReturnType);
                    }
                },
                parameters =>
                {
                    foreach (var parameter in procedure.Parameters.OrderBy(parameter => parameter.Index))
                    {
                        EncodeType(parameters.AddParameter().Type(parameter.PassingMode == ParameterPassingMode.ByRef), parameter.Type);
                    }
                });
            return _metadata.GetOrAddBlob(blob);
        }

        private void EncodeType(SignatureTypeEncoder encoder, TypeSymbol type)
        {
            if (type == TypeSymbol.Byte) { encoder.Byte(); return; }
            if (type == TypeSymbol.Integer) { encoder.Int16(); return; }
            if (type == TypeSymbol.Long) { encoder.Int32(); return; }
            if (type == TypeSymbol.LongLong) { encoder.Int64(); return; }
            if (type == TypeSymbol.Single) { encoder.Single(); return; }
            if (type == TypeSymbol.Double) { encoder.Double(); return; }
            if (type == TypeSymbol.Boolean) { encoder.Boolean(); return; }
            if (type == TypeSymbol.String || type is FixedLengthStringTypeSymbol) { encoder.String(); return; }
            if (type == TypeSymbol.Variant || type == TypeSymbol.Error) { encoder.Object(); return; }
            if (type == TypeSymbol.Currency) { encoder.Type(_vbCurrency, isValueType: true); return; }
            if (type is UserDefinedTypeSymbol udt)
            {
                encoder.Type(_udtSymbolHandles[udt], isValueType: true);
                return;
            }
            if (type is ArrayTypeSymbol array)
            {
                var arguments = encoder.GenericInstantiation(_vbArray, 1, isValueType: false);
                EncodeType(arguments.AddArgument(), array.ElementType);
                return;
            }
            throw new NotSupportedException($"Managed type mapping does not support '{type.Name}'.");
        }

        /// <summary>
        /// Encodes the return type inside the signature of a <c>VBArray&lt;T&gt;</c> member
        /// reference. Such a reference names a constructed type through its TypeSpec, but ECMA-335
        /// requires the signature of the generic *definition* - so wherever the member returns T,
        /// the signature has to say <c>!0</c> rather than the substituted element type. Encoding
        /// the concrete type makes the runtime look for a member that does not exist.
        ///
        /// Which members those are is passed in rather than inferred: <c>LBound</c> on a
        /// <c>VBArray&lt;Long&gt;</c> returns a plain Int32 that happens to equal the element type,
        /// so comparing types would rewrite it to <c>!0</c> and break it.
        /// </summary>
        private void EncodeArrayMemberReturnType(
            SignatureTypeEncoder encoder,
            TypeSymbol type,
            bool usesTypeParameter)
        {
            if (!usesTypeParameter)
            {
                EncodeType(encoder, type);
                return;
            }

            // VBArray<T> itself, as returned by ReDimPreserve and Clone.
            if (type is ArrayTypeSymbol)
            {
                var arguments = encoder.GenericInstantiation(_vbArray, 1, isValueType: false);
                arguments.AddArgument().GenericTypeParameter(0);
                return;
            }

            encoder.GenericTypeParameter(0);
        }

        private EntityHandle GetTypeEntityHandle(TypeSymbol type)
        {
            if (type == TypeSymbol.Currency) return _vbCurrency;
            if (type is UserDefinedTypeSymbol udt) return _udtSymbolHandles[udt];
            if (type is ArrayTypeSymbol array) return GetArrayTypeSpecification(array);
            if (type == TypeSymbol.Byte) return GetReflectionTypeReference(typeof(byte));
            if (type == TypeSymbol.Integer) return GetReflectionTypeReference(typeof(short));
            if (type == TypeSymbol.Long) return GetReflectionTypeReference(typeof(int));
            if (type == TypeSymbol.LongLong) return GetReflectionTypeReference(typeof(long));
            if (type == TypeSymbol.Single) return GetReflectionTypeReference(typeof(float));
            if (type == TypeSymbol.Double) return GetReflectionTypeReference(typeof(double));
            if (type == TypeSymbol.Boolean) return GetReflectionTypeReference(typeof(bool));
            if (type == TypeSymbol.String || type is FixedLengthStringTypeSymbol) return GetReflectionTypeReference(typeof(string));
            return _systemObject;
        }

        private TypeSpecificationHandle GetArrayTypeSpecification(ArrayTypeSymbol array)
        {
            if (_arrayTypeSpecs.TryGetValue(array, out var cached))
            {
                return cached;
            }
            var blob = new BlobBuilder();
            var encoder = new BlobEncoder(blob).TypeSpecificationSignature();
            var args = encoder.GenericInstantiation(_vbArray, 1, isValueType: false);
            EncodeType(args.AddArgument(), array.ElementType);
            var handle = _metadata.AddTypeSpecification(_metadata.GetOrAddBlob(blob));
            _arrayTypeSpecs.Add(array, handle);
            return handle;
        }

        private void EmitMethodDefinitions(
            IReadOnlyDictionary<IrProcedure, ParameterHandle> parameterStarts,
            IReadOnlyDictionary<IrProcedure, int> bodyOffsets)
        {
            foreach (var procedure in AllProcedures())
            {
                var visibility = procedure == _program.EntryPoint
                    ? MethodAttributes.Public
                    : MethodAttributes.Assembly;
                var attributes = visibility | MethodAttributes.HideBySig;
                if (procedure.IsStatic) attributes |= MethodAttributes.Static;
                var actual = _metadata.AddMethodDefinition(
                    attributes,
                    MethodImplAttributes.IL | MethodImplAttributes.Managed,
                    _metadata.GetOrAddString(procedure.Name),
                    EncodeMethodSignature(procedure),
                    bodyOffsets[procedure],
                    parameterStarts[procedure]);
                EnsureHandle(actual, _methodHandles[procedure], "method");
            }
        }

        private void EmitTypeDefinitions()
        {
            foreach (var plan in _typePlans)
            {
                TypeDefinitionHandle actual;
                if (plan.IsModulePseudoType)
                {
                    actual = _metadata.AddTypeDefinition(
                        TypeAttributes.NotPublic,
                        default,
                        _metadata.GetOrAddString("<Module>"),
                        default,
                        plan.FirstField,
                        plan.FirstMethod);
                }
                else if (plan.Udt is not null)
                {
                    actual = _metadata.AddTypeDefinition(
                        TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit,
                        _metadata.GetOrAddString("VB6.Generated"),
                        _metadata.GetOrAddString(plan.Udt.Name),
                        _systemValueType,
                        plan.FirstField,
                        plan.FirstMethod);
                }
                else
                {
                    actual = _metadata.AddTypeDefinition(
                        TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
                        _metadata.GetOrAddString("VB6.Generated"),
                        _metadata.GetOrAddString("__vb6_module_" + Sanitize(plan.Module!.Name)),
                        _systemObject,
                        plan.FirstField,
                        plan.FirstMethod);
                }
                EnsureHandle(actual, plan.TypeHandle, "type");
            }
        }

        private MethodDefinitionHandle ResolveEntryPoint()
        {
            if (_program.EntryPoint is null)
            {
                throw new InvalidOperationException("Managed application output requires an IR entry point.");
            }
            return _methodHandles[_program.EntryPoint];
        }

        private IEnumerable<IrProcedure> AllProcedures()
        {
            foreach (var plan in _typePlans)
            {
                if (plan.Udt is not null)
                {
                    foreach (var method in plan.Udt.Methods) yield return method;
                }
                else if (plan.Module is not null)
                {
                    foreach (var method in plan.Module.Procedures) yield return method;
                }
            }
        }

        private MemberReferenceHandle GetRuntimeMethodReference(MethodInfo method)
        {
            var key = method.DeclaringType!.FullName + "::" + method;
            if (_memberReferences.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var parent = (EntityHandle)GetReflectionTypeReference(method.DeclaringType);
            var signature = EncodeReflectionMethodSignature(method);
            var handle = _metadata.AddMemberReference(
                parent,
                _metadata.GetOrAddString(method.Name),
                signature);
            _memberReferences.Add(key, handle);
            return handle;
        }

        private BlobHandle EncodeReflectionMethodSignature(MethodInfo method)
        {
            var blob = new BlobBuilder();
            var parametersInfo = method.GetParameters();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: !method.IsStatic).Parameters(
                parametersInfo.Length,
                returnType =>
                {
                    if (method.ReturnType == typeof(void)) returnType.Void();
                    else EncodeReflectionType(returnType.Type(method.ReturnType.IsByRef), UnwrapByRef(method.ReturnType));
                },
                parameters =>
                {
                    foreach (var parameter in parametersInfo)
                    {
                        EncodeReflectionType(
                            parameters.AddParameter().Type(parameter.ParameterType.IsByRef),
                            UnwrapByRef(parameter.ParameterType));
                    }
                });
            return _metadata.GetOrAddBlob(blob);
        }

        private void EncodeReflectionType(SignatureTypeEncoder encoder, Type type)
        {
            if (type == typeof(void)) { encoder.VoidPointer(); return; }
            if (type == typeof(byte)) { encoder.Byte(); return; }
            if (type == typeof(short)) { encoder.Int16(); return; }
            if (type == typeof(int)) { encoder.Int32(); return; }
            if (type == typeof(long)) { encoder.Int64(); return; }
            if (type == typeof(float)) { encoder.Single(); return; }
            if (type == typeof(double)) { encoder.Double(); return; }
            if (type == typeof(bool)) { encoder.Boolean(); return; }
            if (type == typeof(string)) { encoder.String(); return; }
            if (type == typeof(object)) { encoder.Object(); return; }
            if (type.IsArray)
            {
                var element = type.GetElementType()!;
                EncodeReflectionType(encoder.SZArray(), element);
                return;
            }
            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var arguments = encoder.GenericInstantiation(
                    GetReflectionTypeReference(definition),
                    type.GetGenericArguments().Length,
                    definition.IsValueType);
                foreach (var argument in type.GetGenericArguments())
                {
                    EncodeReflectionType(arguments.AddArgument(), argument);
                }
                return;
            }
            encoder.Type(GetReflectionTypeReference(type), type.IsValueType);
        }

        private MemberReferenceHandle GetArrayConstructorReference(TypeSymbol elementType)
        {
            var key = "VBArray<" + elementType.Name + ">::.ctor";
            if (_memberReferences.TryGetValue(key, out var cached)) return cached;
            var blob = new BlobBuilder();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: true).Parameters(
                1,
                returnType => returnType.Void(),
                parameters => EncodeReflectionType(parameters.AddParameter().Type(), typeof(VBArrayBound[])));
            var handle = _metadata.AddMemberReference(
                GetArrayTypeSpecification(new ArrayTypeSymbol(elementType)),
                _metadata.GetOrAddString(".ctor"),
                _metadata.GetOrAddBlob(blob));
            _memberReferences.Add(key, handle);
            return handle;
        }

        private MemberReferenceHandle GetArrayMemberReference(
            TypeSymbol elementType,
            string name,
            TypeSymbol? returnType,
            bool returnByRef = false,
            bool returnUsesTypeParameter = false,
            params Type[] parameterTypes)
        {
            var key = "VBArray<" + elementType.Name + ">::" + name + "(" +
                      string.Join(",", parameterTypes.Select(type => type.FullName)) + ")->" +
                      returnType?.Name + ":" + returnByRef + ":" + returnUsesTypeParameter;
            if (_memberReferences.TryGetValue(key, out var cached)) return cached;

            var blob = new BlobBuilder();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: true).Parameters(
                parameterTypes.Length,
                returnEncoder =>
                {
                    if (returnType is null) returnEncoder.Void();
                    else EncodeArrayMemberReturnType(
                        returnEncoder.Type(returnByRef),
                        returnType,
                        returnUsesTypeParameter);
                },
                parameters =>
                {
                    foreach (var parameterType in parameterTypes)
                    {
                        EncodeReflectionType(parameters.AddParameter().Type(), parameterType);
                    }
                });
            var handle = _metadata.AddMemberReference(
                GetArrayTypeSpecification(new ArrayTypeSymbol(elementType)),
                _metadata.GetOrAddString(name),
                _metadata.GetOrAddBlob(blob));
            _memberReferences.Add(key, handle);
            return handle;
        }

        private MemberReferenceHandle GetVBArrayBoundConstructor()
        {
            const string key = "VBArrayBound::.ctor(int,int)";
            if (_memberReferences.TryGetValue(key, out var cached)) return cached;
            var blob = new BlobBuilder();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: true).Parameters(
                2,
                returnType => returnType.Void(),
                parameters =>
                {
                    parameters.AddParameter().Type().Int32();
                    parameters.AddParameter().Type().Int32();
                });
            var handle = _metadata.AddMemberReference(
                _vbArrayBound,
                _metadata.GetOrAddString(".ctor"),
                _metadata.GetOrAddBlob(blob));
            _memberReferences.Add(key, handle);
            return handle;
        }

        private MethodInfo ResolveRuntimeMethod(IrRuntimeCallExpression call, out int skippedArgument)
        {
            skippedArgument = -1;
            var m = call.Method;
            if (m == IrRuntimeMethod.DebugPrint) return Static(typeof(VBDebug), "Print", typeof(object));
            if (m == IrRuntimeMethod.CByte) return Static(typeof(VBConversions), "CByte", typeof(object));
            if (m == IrRuntimeMethod.CInt) return Static(typeof(VBConversions), "CInt", typeof(object));
            if (m == IrRuntimeMethod.CLng) return Static(typeof(VBConversions), "CLng", typeof(object));
            if (m == IrRuntimeMethod.CLngLng) return Static(typeof(VBConversions), "CLngLng", typeof(object));
            if (m == IrRuntimeMethod.CCur) return Static(typeof(VBConversions), "CCur", typeof(object));
            if (m == IrRuntimeMethod.CSng) return Static(typeof(VBConversions), "CSng", typeof(object));
            if (m == IrRuntimeMethod.CDbl) return Static(typeof(VBConversions), "CDbl", typeof(object));
            if (m == IrRuntimeMethod.CBool) return Static(typeof(VBConversions), "CBool", typeof(object));
            if (m == IrRuntimeMethod.CStr) return Static(typeof(VBConversions), "CStr", typeof(object));

            if (m is IrRuntimeMethod.Equal or IrRuntimeMethod.NotEqual or IrRuntimeMethod.Less or IrRuntimeMethod.LessOrEqual or IrRuntimeMethod.Greater or IrRuntimeMethod.GreaterOrEqual or IrRuntimeMethod.Concat)
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.Power) return Static(typeof(VBOperators), "Power", typeof(double), typeof(double));
            if (m == IrRuntimeMethod.MultiplyVariant) return Static(typeof(VBOperators), "MultiplyInteger", typeof(object), typeof(object));

            if (m.ToString().StartsWith("String", StringComparison.Ordinal))
            {
                var name = m.ToString()["String".Length..];
                if (name == "Len") return Static(typeof(VBStrings), "Len", typeof(object));
                if (name == "Mid") return call.Arguments.Length == 2
                    ? Static(typeof(VBStrings), "Mid", typeof(string), typeof(int))
                    : Static(typeof(VBStrings), "Mid", typeof(string), typeof(int), typeof(int));
                if (name == "Chr") return Static(typeof(VBStrings), "Chr", typeof(int));
                if (name is "Left" or "Right") return Static(typeof(VBStrings), name, typeof(string), typeof(int));
                if (name is "UCase" or "LCase" or "Trim" or "LTrim" or "RTrim" or "Asc") return Static(typeof(VBStrings), name, typeof(string));
                if (name == "IsNumeric") return Static(typeof(VBStrings), name, typeof(object));
            }

            if (m.ToString().StartsWith("File", StringComparison.Ordinal))
            {
                return ResolveFileMethod(call, out skippedArgument);
            }

            var operatorName = RuntimeName(m);
            var scalar = RuntimeScalarType(call.Arguments.FirstOrDefault()?.Expression.Type ?? call.ResultType);
            // Not and Negate are the unary operators; every other one takes both operands.
            var isUnary = m.ToString().StartsWith("Not", StringComparison.Ordinal) ||
                m.ToString().StartsWith("Negate", StringComparison.Ordinal);
            return isUnary
                ? Static(typeof(VBOperators), operatorName, scalar)
                : Static(typeof(VBOperators), operatorName, scalar, scalar);
        }

        private MethodInfo ResolveFileMethod(IrRuntimeCallExpression call, out int skippedArgument)
        {
            skippedArgument = -1;
            return call.Method switch
            {
                IrRuntimeMethod.FileOpenBinary => Static(typeof(VBFiles), "OpenBinary", typeof(int), typeof(string)),
                IrRuntimeMethod.FileClose => Static(typeof(VBFiles), "Close", typeof(int)),
                IrRuntimeMethod.FileCloseAll => Static(typeof(VBFiles), "CloseAll"),
                IrRuntimeMethod.FileSeek => Static(typeof(VBFiles), "Seek", typeof(int), typeof(long)),
                IrRuntimeMethod.FileFreeFile => Static(typeof(VBFiles), "FreeFile"),
                IrRuntimeMethod.FileLength => Static(typeof(VBFiles), "Length", typeof(int)),
                IrRuntimeMethod.FileEndOfFile => Static(typeof(VBFiles), "EndOfFile", typeof(int)),
                IrRuntimeMethod.FilePosition => Static(typeof(VBFiles), "Position", typeof(int)),
                IrRuntimeMethod.FilePut => ResolveFilePut(call, out skippedArgument),
                _ => ResolveFileGet(call, out skippedArgument)
            };
        }

        private MethodInfo ResolveFileGet(IrRuntimeCallExpression call, out int skippedArgument)
        {
            var name = call.Method.ToString()["File".Length..];
            var omitted = call.Arguments.Length > 1 && call.Arguments[1].Expression is IrNullExpression;
            skippedArgument = omitted ? 1 : -1;
            return omitted
                ? Static(typeof(VBFiles), name, typeof(int))
                : Static(typeof(VBFiles), name, typeof(int), typeof(long));
        }

        private MethodInfo ResolveFilePut(IrRuntimeCallExpression call, out int skippedArgument)
        {
            var valueType = RuntimeScalarType(call.Arguments[2].Expression.Type);
            var omitted = call.Arguments[1].Expression is IrNullExpression;
            skippedArgument = omitted ? 1 : -1;
            return omitted
                ? Static(typeof(VBFiles), "Put", typeof(int), valueType)
                : Static(typeof(VBFiles), "Put", typeof(int), typeof(long), valueType);
        }

        private static string RuntimeName(IrRuntimeMethod method) => method switch
        {
            IrRuntimeMethod.IntegerDivideInteger => "IntegerDivide",
            _ => method.ToString()
        };

        private static Type RuntimeScalarType(TypeSymbol type) => type == TypeSymbol.Byte ? typeof(byte)
            : type == TypeSymbol.Integer ? typeof(short)
            : type == TypeSymbol.Long ? typeof(int)
            : type == TypeSymbol.LongLong ? typeof(long)
            : type == TypeSymbol.Single ? typeof(float)
            : type == TypeSymbol.Double ? typeof(double)
            : type == TypeSymbol.Boolean ? typeof(bool)
            : type == TypeSymbol.String ? typeof(string)
            : type == TypeSymbol.Currency ? typeof(VBCurrency)
            : type == TypeSymbol.Variant ? typeof(object)
            : throw new NotSupportedException($"Runtime scalar type '{type.Name}' is not supported.");

        private static MethodInfo Static(Type type, string name, params Type[] parameters) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, parameters)
            ?? throw new MissingMethodException(type.FullName, name + "(" + string.Join(",", parameters.Select(t => t.Name)) + ")");

        private static Type UnwrapByRef(Type type) => type.IsByRef ? type.GetElementType()! : type;

        private int GetScratchLocal(IrProcedure procedure, TypeSymbol type)
        {
            // Defaults are emitted only for IR-defined locals today. The lowering phase materializes
            // all user-visible storage, so no emitter-owned semantic temporary is needed.
            var local = procedure.Locals.FirstOrDefault(candidate => candidate.Type == type && candidate.IsCompilerGenerated);
            if (local is not null) return local.Id;
            throw new InvalidOperationException($"IR must materialize a compiler local before default-initializing '{type.Name}'.");
        }

        private static bool IsReferenceType(TypeSymbol type) =>
            type == TypeSymbol.String || type == TypeSymbol.Variant || type is ArrayTypeSymbol;

        private static bool IsValueType(TypeSymbol type) => !IsReferenceType(type) && type != TypeSymbol.Error;

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

        private static void EnsureHandle<T>(T actual, T expected, string kind) where T : struct
        {
            if (!actual.Equals(expected))
            {
                throw new InvalidOperationException($"Deterministic metadata planning mismatch for {kind}: expected {expected}, got {actual}.");
            }
        }

        private static string Sanitize(string name)
        {
            var result = new string(name.Select(character =>
                char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray());
            return result.Length == 0 ? "unnamed" : result;
        }

        private sealed class TypePlan
        {
            private TypePlan() { }
            public bool IsModulePseudoType { get; private init; }
            public IrTypeDefinition? Udt { get; private init; }
            public IrModule? Module { get; private init; }
            public TypeDefinitionHandle TypeHandle { get; set; }
            public FieldDefinitionHandle FirstField { get; set; }
            public MethodDefinitionHandle FirstMethod { get; set; }

            public static TypePlan ModuleType() => new() { IsModulePseudoType = true };
            public static TypePlan ForUdt(IrTypeDefinition type) => new() { Udt = type };
            public static TypePlan ForModule(IrModule module) => new() { Module = module };
        }
    }
}
