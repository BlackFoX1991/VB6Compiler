using System.Collections.Immutable;
using System.Globalization;
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
        catch (NotSupportedException exception)
        {
            // The one expected failure: the IR contains something this backend cannot emit yet.
            // Every such site names the construct, so the message alone is a usable report.
            return Failed("VB6E0001", exception.Message);
        }
        catch (Exception exception)
        {
            // Anything else is a defect in the emitter rather than a gap in its coverage. This is
            // the only channel out of metadata emission, so it has to carry enough to find the
            // cause - the bare message of, say, a NullReferenceException identifies nothing.
            return Failed("VB6E0003", $"Managed emit failed unexpectedly: {exception}");
        }
    }

    private static ManagedEmitResult Failed(string code, string message) =>
        new(false, ImmutableArray.Create(new ManagedEmitDiagnostic(code, message)), null, null);

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
        private readonly Dictionary<IrClassDefinition, TypeDefinitionHandle> _classHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ClassTypeSymbol, TypeDefinitionHandle> _classSymbolHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ClassTypeSymbol, MethodDefinitionHandle> _classConstructorHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<IrModule, TypeDefinitionHandle> _moduleTypeHandles =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<TypeSymbol, TypeSpecificationHandle> _arrayTypeSpecs =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, MemberReferenceHandle> _memberReferences = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EntityHandle> _methodSpecifications = new(StringComparer.Ordinal);
        private readonly ImmutableDictionary<IrProcedure, ImmutableArray<ManagedSequencePoint>>.Builder _sequencePoints =
            ImmutableDictionary.CreateBuilder<IrProcedure, ImmutableArray<ManagedSequencePoint>>(
                ReferenceEqualityComparer.Instance);
        private readonly Dictionary<Type, TypeReferenceHandle> _reflectionTypeRefs = new();
        private readonly Dictionary<string, TypeReferenceHandle> _namedTypeRefs = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ModuleReferenceHandle> _moduleReferences =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TypePlan> _typePlans = new();
        private AssemblyReferenceHandle _coreLibReference;
        private AssemblyReferenceHandle _runtimeReference;
        private TypeReferenceHandle _systemObject;
        private TypeReferenceHandle _systemValueType;
        private TypeReferenceHandle _systemException;
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
                null)
            {
                SequencePoints = _sequencePoints.ToImmutable()
            };
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
            _systemException = GetReflectionTypeReference(typeof(Exception));
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
            foreach (var @class in _program.ClassDefinitions)
            {
                _typePlans.Add(TypePlan.ForClass(@class));
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
                else if (plan.Class is not null)
                {
                    _classHandles.Add(plan.Class, plan.TypeHandle);
                    _classSymbolHandles.Add(plan.Class.Symbol, plan.TypeHandle);
                    foreach (var field in plan.Class.Fields)
                    {
                        _fieldHandles.Add(field, MetadataTokens.FieldDefinitionHandle(nextField++));
                    }
                    foreach (var method in plan.Class.Methods)
                    {
                        AssignMethodHandle(method, ref nextMethod);
                        if (string.Equals(method.Name, ".ctor", StringComparison.Ordinal))
                        {
                            _classConstructorHandles.Add(plan.Class.Symbol, _methodHandles[method]);
                        }
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
                else if (plan.Class is not null)
                {
                    foreach (var field in plan.Class.Fields)
                    {
                        var actual = _metadata.AddFieldDefinition(
                            FieldAttributes.Private,
                            _metadata.GetOrAddString(field.Name),
                            EncodeFieldSignature(field.Type));
                        EnsureHandle(actual, _fieldHandles[field], "class field");
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
                if (procedure.IsExternal && procedure.ReturnType == TypeSymbol.String)
                {
                    var returnParameter = _metadata.AddParameter(
                        ParameterAttributes.None,
                        default,
                        sequenceNumber: 0);
                    AddAnsiStringMarshalling(returnParameter);
                }

                foreach (var parameter in procedure.Parameters.OrderBy(parameter => parameter.Index))
                {
                    var parameterHandle = _metadata.AddParameter(
                        ParameterAttributes.None,
                        _metadata.GetOrAddString(parameter.Name),
                        parameter.Index + 1);
                    if (procedure.IsExternal && parameter.Type == TypeSymbol.String)
                    {
                        AddAnsiStringMarshalling(parameterHandle);
                    }
                }
            }
            return result;
        }

        private void AddAnsiStringMarshalling(ParameterHandle parameter)
        {
            var blob = new BlobBuilder();
            blob.WriteByte(0x14); // NATIVE_TYPE_LPSTR
            _metadata.AddMarshallingDescriptor(parameter, _metadata.GetOrAddBlob(blob));
        }

        private Dictionary<IrProcedure, int> EmitMethodBodies()
        {
            var result = new Dictionary<IrProcedure, int>(ReferenceEqualityComparer.Instance);
            foreach (var procedure in AllProcedures())
            {
                if (procedure.IsExternal || IsInterfaceProcedure(procedure))
                {
                    continue;
                }

                var code = new BlobBuilder();
                var flow = new ControlFlowBuilder();
                var encoder = new InstructionEncoder(code, flow);
                var sequencePoints = ImmutableArray.CreateBuilder<ManagedSequencePoint>();
                var blockLabels = procedure.Blocks.ToDictionary(block => block.Id, _ => encoder.DefineLabel());
                var entry = procedure.Blocks.FirstOrDefault(block => block.Label.EndsWith("_entry", StringComparison.Ordinal))
                            ?? procedure.Blocks.FirstOrDefault();
                if (entry is null)
                {
                    encoder.OpCode(ILOpCode.Ret);
                }
                else
                {
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBGoSub),
                        nameof(VBGoSub.Enter))));
                    encoder.Branch(ILOpCode.Br, blockLabels[entry.Id]);
                    var errorBoundaries = procedure.Blocks
                        .SelectMany(block => block.Instructions)
                        .OfType<IrErrorBoundaryStartInstruction>()
                        .Select((boundary, index) => new ErrorBoundary(
                            index,
                            encoder.DefineLabel(),
                            encoder.DefineLabel(),
                            boundary.HandlerBlockId is int handlerBlockId
                                ? blockLabels[handlerBlockId]
                                : null))
                        .ToArray();
                    var boundaryIndex = 0;
                    ErrorBoundary? activeBoundary = null;
                    foreach (var block in procedure.Blocks)
                    {
                        encoder.MarkLabel(blockLabels[block.Id]);
                        foreach (var instruction in block.Instructions)
                        {
                            RecordSequencePoint(sequencePoints, encoder, instruction.SourceLocation);
                            switch (instruction)
                            {
                                case IrErrorBoundaryStartInstruction boundary when activeBoundary is null:
                                    activeBoundary = errorBoundaries[boundaryIndex++];
                                    encoder.MarkLabel(activeBoundary.TryStart);
                                    break;
                                case IrErrorBoundaryEndInstruction when activeBoundary is not null:
                                    EmitErrorBoundary(encoder, flow, activeBoundary);
                                    activeBoundary = null;
                                    break;
                                case IrErrorBoundaryStartInstruction:
                                    throw new InvalidOperationException("Nested error handling regions are not supported.");
                                case IrErrorBoundaryEndInstruction:
                                    throw new InvalidOperationException("Error handling region ended without a start.");
                                case IrResumeInstruction resume:
                                    EmitResumeInstruction(encoder, resume, errorBoundaries);
                                    break;
                                default:
                                    EmitInstruction(encoder, procedure, instruction);
                                    break;
                            }
                        }

                        RecordSequencePoint(sequencePoints, encoder, block.Terminator.SourceLocation);
                        EmitTerminator(encoder, procedure, block.Terminator, blockLabels);
                    }

                    if (activeBoundary is not null)
                    {
                        throw new InvalidOperationException("Error handling region crossed a basic-block boundary.");
                    }
                }

                var localSignature = EncodeLocalSignature(procedure);
                var offset = _methodBodyStream.AddMethodBody(
                    encoder,
                    maxStack: 64,
                    localVariablesSignature: localSignature,
                    attributes: MethodBodyAttributes.InitLocals);
                result.Add(procedure, offset);
                if (sequencePoints.Count > 0)
                {
                    _sequencePoints.Add(procedure, sequencePoints.ToImmutable());
                }
            }
            return result;
        }

        private void EmitErrorBoundary(
            InstructionEncoder encoder,
            ControlFlowBuilder flow,
            ErrorBoundary boundary)
        {
            encoder.Branch(ILOpCode.Leave, boundary.Continuation);
            var tryEnd = encoder.DefineLabel();
            encoder.MarkLabel(tryEnd);

            var handlerStart = encoder.DefineLabel();
            encoder.MarkLabel(handlerStart);
            encoder.LoadConstantI4(boundary.Index);
            encoder.Call(GetRuntimeMethodReference(Static(
                typeof(VBErrors),
                nameof(VBErrors.Set),
                typeof(Exception),
                typeof(int))));
            encoder.Branch(ILOpCode.Leave, boundary.HandlerTarget ?? boundary.Continuation);
            var handlerEnd = encoder.DefineLabel();
            encoder.MarkLabel(handlerEnd);
            encoder.MarkLabel(boundary.Continuation);

            // Exception region registration is validated by the method-body encoder below.
            flow.AddCatchRegion(boundary.TryStart, tryEnd, handlerStart, handlerEnd, _systemException);
        }

        private void EmitResumeInstruction(
            InstructionEncoder encoder,
            IrResumeInstruction resume,
            IReadOnlyList<ErrorBoundary> errorBoundaries)
        {
            encoder.Call(GetRuntimeMethodReference(Static(typeof(VBErrors), nameof(VBErrors.ResumeIndexValue))));
            encoder.Call(GetRuntimeMethodReference(Static(typeof(VBErrors), nameof(VBErrors.Clear))));
            var targets = errorBoundaries
                .Select(boundary => resume.Kind == IrResumeKind.Next
                    ? boundary.Continuation
                    : boundary.TryStart)
                .ToArray();
            if (targets.Length > 0)
            {
                var switchEncoder = encoder.Switch(targets.Length);
                foreach (var target in targets)
                {
                    switchEncoder.Branch(target);
                }
            }
            encoder.Call(GetRuntimeMethodReference(Static(typeof(VBErrors), nameof(VBErrors.InvalidResume))));
        }

        private sealed record ErrorBoundary(
            int Index,
            LabelHandle TryStart,
            LabelHandle Continuation,
            LabelHandle? HandlerTarget);

        /// <summary>
        /// Notes that the code about to be emitted starts a new statement. Consecutive
        /// instructions from one statement share its position, and only the first of them is a
        /// place a debugger should stop at.
        /// </summary>
        private static void RecordSequencePoint(
            ImmutableArray<ManagedSequencePoint>.Builder sequencePoints,
            InstructionEncoder encoder,
            IrSourceLocation? location)
        {
            if (location is null)
            {
                return;
            }

            var offset = encoder.Offset;
            if (sequencePoints.Count > 0)
            {
                var previous = sequencePoints[^1];

                // Two points may not share an offset, and repeating a position adds nothing: the
                // remaining instructions of a statement belong to the point already written.
                if (previous.IlOffset == offset ||
                    (previous.FilePath == location.FilePath && previous.Lines == location.Lines))
                {
                    return;
                }
            }

            sequencePoints.Add(new ManagedSequencePoint(offset, location.FilePath, location.Lines));
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
                case IrRaiseEventInstruction raiseEvent:
                    EmitRaiseEvent(encoder, procedure, raiseEvent);
                    break;
                case IrSubscribeEventInstruction subscribe:
                    EmitSubscribeEvent(encoder, procedure, subscribe);
                    break;
                case IrBaseFinalizeInstruction:
                    encoder.LoadArgument(0);
                    encoder.Call(GetRuntimeMethodReference(
                        typeof(object).GetMethod(
                            "Finalize",
                            BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingMethodException("System.Object.Finalize is required.")));
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
                case IrGoSubTerminator goSub:
                    encoder.LoadConstantI4(goSub.ReturnIndex);
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBGoSub),
                        nameof(VBGoSub.Push),
                        typeof(int))));
                    encoder.Branch(ILOpCode.Br, labels[goSub.TargetBlockId]);
                    break;
                case IrGoSubReturnTerminator goSubReturn:
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBGoSub),
                        nameof(VBGoSub.Pop))));
                    if (goSubReturn.ReturnTargetBlockIds.Length > 0)
                    {
                        var switchEncoder = encoder.Switch(goSubReturn.ReturnTargetBlockIds.Length);
                        foreach (var target in goSubReturn.ReturnTargetBlockIds)
                        {
                            switchEncoder.Branch(labels[target]);
                        }
                    }
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBGoSub),
                        nameof(VBGoSub.InvalidReturn))));
                    break;
                case IrOnGoToTerminator onGoTo:
                    EmitExpression(encoder, procedure, onGoTo.Index);
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBControlFlow),
                        nameof(VBControlFlow.OnGoToIndex),
                        typeof(int))));
                    if (onGoTo.TargetBlockIds.Length > 0)
                    {
                        var switchEncoder = encoder.Switch(onGoTo.TargetBlockIds.Length);
                        foreach (var target in onGoTo.TargetBlockIds)
                        {
                            switchEncoder.Branch(labels[target]);
                        }
                    }
                    encoder.Branch(ILOpCode.Br, labels[onGoTo.DefaultBlockId]);
                    break;
                case IrOnGoSubTerminator onGoSub:
                    EmitExpression(encoder, procedure, onGoSub.Index);
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBControlFlow),
                        nameof(VBControlFlow.OnGoToIndex),
                        typeof(int))));
                    if (onGoSub.TargetBlockIds.Length > 0)
                    {
                        var dispatchLabels = onGoSub.TargetBlockIds
                            .Select(_ => encoder.DefineLabel())
                            .ToArray();
                        var switchEncoder = encoder.Switch(dispatchLabels.Length);
                        foreach (var dispatchLabel in dispatchLabels)
                        {
                            switchEncoder.Branch(dispatchLabel);
                        }

                        encoder.Branch(ILOpCode.Br, labels[onGoSub.DefaultBlockId]);
                        for (var index = 0; index < dispatchLabels.Length; index++)
                        {
                            encoder.MarkLabel(dispatchLabels[index]);
                            encoder.LoadConstantI4(onGoSub.ReturnIndex);
                            encoder.Call(GetRuntimeMethodReference(Static(
                                typeof(VBGoSub),
                                nameof(VBGoSub.Push),
                                typeof(int))));
                            encoder.Branch(ILOpCode.Br, labels[onGoSub.TargetBlockIds[index]]);
                        }
                    }
                    else
                    {
                        encoder.Branch(ILOpCode.Br, labels[onGoSub.DefaultBlockId]);
                    }
                    break;
                case IrReturnTerminator ret:
                    if (ret.Value is not null)
                    {
                        EmitExpression(encoder, procedure, ret.Value);
                    }
                    encoder.Call(GetRuntimeMethodReference(Static(
                        typeof(VBGoSub),
                        nameof(VBGoSub.Leave))));
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
                case IrAddressOfExpression addressOf:
                    EmitAddressOf(encoder, addressOf);
                    break;
                case IrRuntimeCallExpression call:
                    EmitRuntimeCall(encoder, procedure, call);
                    break;
                case IrProcedureCallExpression call:
                    EmitProcedureCall(encoder, procedure, call);
                    break;
                case IrNewClassExpression @new:
                    EmitNewClass(encoder, @new);
                    break;
                case IrTypeOfExpression typeOf:
                    EmitTypeOf(encoder, procedure, typeOf);
                    break;
                case IrArrayCallExpression arrayCall:
                    EmitArrayCall(encoder, procedure, arrayCall);
                    break;
                case IrVariantArrayCallExpression variantArrayCall:
                    EmitVariantArrayCall(encoder, procedure, variantArrayCall);
                    break;
                case IrNewVBArrayExpression newArray:
                    EmitNewArray(encoder, procedure, newArray);
                    break;
                case IrEnsureArrayExpression ensureArray:
                    EmitEnsureArray(encoder, procedure, ensureArray);
                    break;
                case IrCopyArrayExpression copyArray:
                    EmitCopyArray(encoder, procedure, copyArray);
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
                encoder.LoadString(_metadata.GetOrAddUserString(Convert.ToString(constant.Value, CultureInfo.InvariantCulture) ?? string.Empty));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Boolean)
            {
                encoder.LoadConstantI4(Convert.ToBoolean(constant.Value, CultureInfo.InvariantCulture) ? 1 : 0);
                return;
            }
            if (constant.ConstantType == TypeSymbol.Byte || constant.ConstantType == TypeSymbol.Integer || constant.ConstantType == TypeSymbol.Long)
            {
                encoder.LoadConstantI4(Convert.ToInt32(constant.Value, CultureInfo.InvariantCulture));
                return;
            }
            if (constant.ConstantType == TypeSymbol.LongLong)
            {
                encoder.LoadConstantI8(Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture));
                return;
            }
            if (constant.ConstantType == TypeSymbol.LongPtr)
            {
                encoder.LoadConstantI8(Convert.ToInt64(constant.Value, CultureInfo.InvariantCulture));
                encoder.OpCode(ILOpCode.Box);
                encoder.Token(GetReflectionTypeReference(typeof(long)));
                encoder.Call(GetRuntimeMethodReference(Static(typeof(VBConversions), "CLngPtr", typeof(object))));
                return;
            }
            if (constant.ConstantType == TypeSymbol.UShort)
            {
                encoder.LoadConstantI4(Convert.ToUInt16(constant.Value, CultureInfo.InvariantCulture));
                return;
            }
            if (constant.ConstantType == TypeSymbol.UInteger)
            {
                encoder.LoadConstantI4(unchecked((int)Convert.ToUInt32(constant.Value, CultureInfo.InvariantCulture)));
                return;
            }
            if (constant.ConstantType == TypeSymbol.ULong)
            {
                encoder.LoadConstantI8(unchecked((long)Convert.ToUInt64(constant.Value, CultureInfo.InvariantCulture)));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Single)
            {
                encoder.LoadConstantR4(Convert.ToSingle(constant.Value, CultureInfo.InvariantCulture));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Date || constant.ConstantType == TypeSymbol.Double)
            {
                encoder.LoadConstantR8(Convert.ToDouble(constant.Value, CultureInfo.InvariantCulture));
                return;
            }
            if (constant.ConstantType == TypeSymbol.Currency)
            {
                var value = Convert.ToDecimal(constant.Value, CultureInfo.InvariantCulture);
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
                    encoder.LoadArgument(GetIlArgumentIndex(procedure, parameter.Parameter.Index));
                    if (parameter.Parameter.PassingMode == ParameterPassingMode.ByRef)
                    {
                        EmitLoadIndirect(encoder, parameter.Type);
                    }
                    break;
                case IrThisPlace:
                    encoder.LoadArgument(0);
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
                case IrArrayFlatElementPlace element:
                    EmitArrayFlatElementAddress(encoder, procedure, element);
                    EmitLoadIndirect(encoder, element.ElementType);
                    break;
                case IrIndirectPlace indirect:
                    EmitExpression(encoder, procedure, indirect.Address);
                    EmitLoadIndirect(encoder, indirect.ElementType);
                    break;
                case IrAccessorPlace accessor:
                    EmitAccessorGet(encoder, procedure, accessor);
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
                    encoder.StoreArgument(GetIlArgumentIndex(procedure, parameter.Parameter.Index));
                    break;
                case IrParameterPlace parameter:
                    encoder.LoadArgument(GetIlArgumentIndex(procedure, parameter.Parameter.Index));
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
                case IrArrayFlatElementPlace element:
                    EmitArrayFlatElementAddress(encoder, procedure, element);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, element.ElementType);
                    EmitStoreIndirect(encoder, element.ElementType);
                    break;
                case IrIndirectPlace indirect:
                    EmitExpression(encoder, procedure, indirect.Address);
                    EmitExpressionWithAssignmentConversion(encoder, procedure, value, indirect.ElementType);
                    EmitStoreIndirect(encoder, indirect.ElementType);
                    break;
                case IrAccessorPlace accessor:
                    EmitAccessorSet(encoder, procedure, accessor, value);
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
                        encoder.LoadArgument(GetIlArgumentIndex(procedure, parameter.Parameter.Index));
                    }
                    else
                    {
                        encoder.LoadArgumentAddress(GetIlArgumentIndex(procedure, parameter.Parameter.Index));
                    }
                    break;
                case IrThisPlace:
                    encoder.LoadArgument(0);
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
                case IrArrayFlatElementPlace element:
                    EmitArrayFlatElementAddress(encoder, procedure, element);
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

        private void EmitAddressOf(InstructionEncoder encoder, IrAddressOfExpression expression)
        {
            if (!_procedureSymbolHandles.TryGetValue(expression.Procedure, out var target))
            {
                throw new InvalidOperationException(
                    $"Procedure '{expression.Procedure.Name}' has no managed method definition.");
            }

            encoder.OpCode(ILOpCode.Ldftn);
            encoder.Token(target);
            if (expression.ResultType == TypeSymbol.Long)
            {
                encoder.OpCode(ILOpCode.Conv_i4);
                return;
            }

            if (expression.ResultType == TypeSymbol.LongPtr)
            {
                encoder.OpCode(ILOpCode.Newobj);
                encoder.Token(GetIntPtrConstructorReference());
                return;
            }

            throw new NotSupportedException(
                $"AddressOf result type '{expression.ResultType.Name}' is not supported.");
        }

        private MemberReferenceHandle GetIntPtrConstructorReference()
        {
            const string key = "System.IntPtr::.ctor(long)";
            if (_memberReferences.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var blob = new BlobBuilder();
            new BlobEncoder(blob).MethodSignature(isInstanceMethod: true).Parameters(
                1,
                returnType => returnType.Void(),
                parameters => parameters.AddParameter().Type().Int64());
            var handle = _metadata.AddMemberReference(
                GetReflectionTypeReference(typeof(IntPtr)),
                _metadata.GetOrAddString(".ctor"),
                _metadata.GetOrAddBlob(blob));
            _memberReferences.Add(key, handle);
            return handle;
        }

        private void EmitRuntimeCall(InstructionEncoder encoder, IrProcedure procedure, IrRuntimeCallExpression call)
        {
            if (call.Method is IrRuntimeMethod.FileGetDynamicArray or IrRuntimeMethod.FilePutDynamicArrayDescriptor)
            {
                foreach (var argument in call.Arguments)
                {
                    EmitExpression(encoder, procedure, argument.Expression);
                }

                var arrayType = call.Method == IrRuntimeMethod.FileGetDynamicArray
                    ? (ArrayTypeSymbol)call.ResultType
                    : (ArrayTypeSymbol)call.Arguments[1].Expression.Type;
                var methodName = call.Method == IrRuntimeMethod.FileGetDynamicArray
                    ? "GetDynamicArray"
                    : "PutDynamicArrayDescriptor";
                encoder.Call(GetFileDynamicArrayReference(methodName, arrayType.ElementType));
                return;
            }

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
            if (call.Receiver is not null)
            {
                EmitExpression(encoder, procedure, call.Receiver);
            }

            for (var index = 0; index < call.Arguments.Length; index++)
            {
                var argument = call.Arguments[index];
                var parameter = index < call.Procedure.Parameters.Length
                    ? call.Procedure.Parameters[index]
                    : null;

                if (call.Procedure.IsExternal && parameter?.IsAny == true)
                {
                    EmitExpression(encoder, procedure, argument.Expression);
                    encoder.OpCode(ILOpCode.Conv_i);
                    continue;
                }

                // A ByRef argument passes an address, which is already the parameter's type - only
                // a by-value argument can need the boxing that a Variant parameter asks for.
                if (argument.Kind == IrCallArgumentKind.Address ||
                    index >= call.Procedure.Parameters.Length)
                {
                    EmitExpression(encoder, procedure, argument.Expression);
                    continue;
                }

                EmitExpressionWithAssignmentConversion(
                    encoder,
                    procedure,
                    argument.Expression,
                    call.Procedure.Parameters[index].Type);
            }
            if (!_procedureSymbolHandles.TryGetValue(call.Procedure, out var target))
            {
                throw new InvalidOperationException($"Procedure '{call.Procedure.Name}' has no managed method definition.");
            }
            if (call.Receiver?.Type is ClassTypeSymbol { IsInterfaceContract: true })
            {
                encoder.OpCode(ILOpCode.Callvirt);
                encoder.Token(target);
            }
            else
            {
                encoder.Call(target);
            }
        }

        private void EmitAccessorGet(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrAccessorPlace accessor)
        {
            if (accessor.Getter is null)
            {
                throw new NotSupportedException("A property read has no Get accessor.");
            }

            if (accessor.Receiver is not null)
            {
                EmitExpression(encoder, procedure, accessor.Receiver);
            }

            foreach (var argument in accessor.Arguments)
            {
                EmitExpression(encoder, procedure, argument);
            }

            if (!_procedureSymbolHandles.TryGetValue(accessor.Getter, out var target))
            {
                throw new InvalidOperationException(
                    $"Property getter '{accessor.Getter.Name}' has no managed method definition.");
            }

            if (accessor.Receiver?.Type is ClassTypeSymbol { IsInterfaceContract: true })
            {
                encoder.OpCode(ILOpCode.Callvirt);
                encoder.Token(target);
            }
            else
            {
                encoder.Call(target);
            }
        }

        private void EmitAccessorSet(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrAccessorPlace accessor,
            IrExpression value)
        {
            if (accessor.Setter is null)
            {
                throw new NotSupportedException("A property assignment has no Let/Set accessor.");
            }

            if (accessor.Receiver is not null)
            {
                EmitExpression(encoder, procedure, accessor.Receiver);
            }

            foreach (var argument in accessor.Arguments)
            {
                EmitExpression(encoder, procedure, argument);
            }

            var valueType = accessor.Setter.Parameters.LastOrDefault()?.Type ?? accessor.ValueType;
            EmitExpressionWithAssignmentConversion(encoder, procedure, value, valueType);
            if (!_procedureSymbolHandles.TryGetValue(accessor.Setter, out var target))
            {
                throw new InvalidOperationException(
                    $"Property setter '{accessor.Setter.Name}' has no managed method definition.");
            }

            if (accessor.Receiver?.Type is ClassTypeSymbol { IsInterfaceContract: true })
            {
                encoder.OpCode(ILOpCode.Callvirt);
                encoder.Token(target);
            }
            else
            {
                encoder.Call(target);
            }
        }

        private void EmitNewClass(InstructionEncoder encoder, IrNewClassExpression expression)
        {
            if (expression.ClassType.IsInterfaceContract)
            {
                throw new NotSupportedException(
                    $"Interface contract '{expression.ClassType.Name}' cannot be instantiated.");
            }

            if (ReferenceEquals(expression.ClassType, VBStandardTypes.Collection))
            {
                encoder.Call(GetRuntimeMethodReference(
                    typeof(VBCollection).GetMethod(
                        nameof(VBCollection.Create),
                        BindingFlags.Public | BindingFlags.Static,
                        Type.EmptyTypes)
                    ?? throw new MissingMethodException("VBCollection.Create is required.")));
                return;
            }

            if (!_classConstructorHandles.TryGetValue(expression.ClassType, out var constructor))
            {
                throw new NotSupportedException(
                    $"Class '{expression.ClassType.Name}' has no managed constructor.");
            }

            encoder.OpCode(ILOpCode.Newobj);
            encoder.Token(constructor);
        }

        private void EmitRaiseEvent(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrRaiseEventInstruction raiseEvent)
        {
            if (raiseEvent.DeclaringClass is null)
            {
                throw new NotSupportedException("RaiseEvent requires a class instance receiver.");
            }

            encoder.LoadArgument(0);
            encoder.LoadString(_metadata.GetOrAddUserString(raiseEvent.Event.Name));
            encoder.LoadConstantI4(raiseEvent.Arguments.Length);
            encoder.OpCode(ILOpCode.Newarr);
            encoder.Token(GetReflectionTypeReference(typeof(object)));
            for (var index = 0; index < raiseEvent.Arguments.Length; index++)
            {
                encoder.OpCode(ILOpCode.Dup);
                encoder.LoadConstantI4(index);
                EmitExpression(encoder, procedure, raiseEvent.Arguments[index]);
                if (IsValueType(raiseEvent.Arguments[index].Type))
                {
                    encoder.OpCode(ILOpCode.Box);
                    encoder.Token(GetTypeEntityHandle(raiseEvent.Arguments[index].Type));
                }

                encoder.OpCode(ILOpCode.Stelem_ref);
            }

            encoder.Call(GetRuntimeMethodReference(
                typeof(VBEvents).GetMethod(
                    nameof(VBEvents.Raise),
                    BindingFlags.Public | BindingFlags.Static,
                    new[] { typeof(object), typeof(string), typeof(object[]) })
                ?? throw new MissingMethodException("VBEvents.Raise is required.")));
        }

        private void EmitSubscribeEvent(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrSubscribeEventInstruction subscribe)
        {
            EmitExpression(encoder, procedure, subscribe.Source);
            encoder.LoadString(_metadata.GetOrAddUserString(subscribe.Event.Name));
            EmitExpression(encoder, procedure, subscribe.Target);
            encoder.LoadString(_metadata.GetOrAddUserString(subscribe.Handler.Name));
            encoder.Call(GetRuntimeMethodReference(
                typeof(VBEvents).GetMethod(
                    nameof(VBEvents.SubscribeMethod),
                    BindingFlags.Public | BindingFlags.Static,
                    new[] { typeof(object), typeof(string), typeof(object), typeof(string) })
                ?? throw new MissingMethodException("VBEvents.SubscribeMethod is required.")));
        }

        private void EmitTypeOf(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrTypeOfExpression expression)
        {
            EmitExpression(encoder, procedure, expression.Expression);
            encoder.OpCode(ILOpCode.Ldtoken);
            encoder.Token(GetTypeEntityHandle(expression.TargetType));
            encoder.Call(GetRuntimeMethodReference(
                typeof(Type).GetMethod(
                    nameof(Type.GetTypeFromHandle),
                    BindingFlags.Public | BindingFlags.Static,
                    new[] { typeof(RuntimeTypeHandle) })
                ?? throw new MissingMethodException("System.Type.GetTypeFromHandle is required.")));
            encoder.Call(GetRuntimeMethodReference(
                typeof(VBObjectIdentity).GetMethod(
                    nameof(VBObjectIdentity.IsType),
                    BindingFlags.Public | BindingFlags.Static,
                    new[] { typeof(object), typeof(Type) })
                ?? throw new MissingMethodException("VBObjectIdentity.IsType is required.")));
        }

        private static int GetIlArgumentIndex(IrProcedure procedure, int parameterIndex) =>
            parameterIndex + (procedure.IsStatic ? 0 : 1);

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

        private void EmitArrayFlatElementAddress(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrArrayFlatElementPlace element)
        {
            EmitExpression(encoder, procedure, element.Array);
            EmitExpression(encoder, procedure, element.Index);
            encoder.Call(GetArrayMemberReference(
                ((ArrayTypeSymbol)element.Array.Type).ElementType,
                "GetReferenceAtFlatIndex",
                element.ElementType,
                returnByRef: true,
                returnUsesTypeParameter: true,
                typeof(int)));
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

        private void EmitVariantArrayCall(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrVariantArrayCallExpression call)
        {
            EmitExpression(encoder, procedure, call.Array);
            if (call.Operation == IrVariantArrayOperation.GetElement)
            {
                EmitInt32Array(encoder, procedure, call.Arguments);
                encoder.Call(GetRuntimeMethodReference(
                    typeof(VBArrayOperations).GetMethod(
                        nameof(VBArrayOperations.GetElement),
                        new[] { typeof(object), typeof(int[]) })
                    ?? throw new MissingMethodException("VBArrayOperations.GetElement(object,int[]) is required.")));
                return;
            }

            if (call.Arguments.Length != 1)
            {
                throw new InvalidOperationException("Variant array bounds require exactly one dimension argument.");
            }

            EmitExpression(encoder, procedure, call.Arguments[0]);
            var methodName = call.Operation == IrVariantArrayOperation.UBound
                ? nameof(VBArrayOperations.UBound)
                : nameof(VBArrayOperations.LBound);
            encoder.Call(GetRuntimeMethodReference(
                typeof(VBArrayOperations).GetMethod(
                    methodName,
                    new[] { typeof(object), typeof(int) })
                ?? throw new MissingMethodException($"VBArrayOperations.{methodName}(object,int) is required.")));
        }

        private void EmitEnsureArray(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrEnsureArrayExpression expression)
        {
            // The storage is passed by reference, so a created array is stored back into the
            // member rather than into a copy of the enclosing value.
            EmitAddress(encoder, procedure, expression.Storage);
            EmitVBArrayBounds(encoder, procedure, expression.Bounds);
            encoder.OpCode(ILOpCode.Call);
            encoder.Token(GetTypeStorageArrayReference("EnsureArray", expression.ArrayType.ElementType, storageByRef: true));
        }

        private void EmitCopyArray(
            InstructionEncoder encoder,
            IrProcedure procedure,
            IrCopyArrayExpression expression)
        {
            EmitExpression(encoder, procedure, expression.Source);
            EmitVBArrayBounds(encoder, procedure, expression.Bounds);
            encoder.OpCode(ILOpCode.Call);
            encoder.Token(GetTypeStorageArrayReference("CopyArray", expression.ArrayType.ElementType, storageByRef: false));
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
                        if (procedure.IsExternal && parameter.Symbol?.IsAny == true)
                        {
                            parameters.AddParameter().Type(isByRef: false).IntPtr();
                        }
                        else
                        {
                            EncodeType(parameters.AddParameter().Type(parameter.PassingMode == ParameterPassingMode.ByRef), parameter.Type);
                        }
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
            if (type == TypeSymbol.LongPtr) { encoder.Type(GetReflectionTypeReference(typeof(IntPtr)), isValueType: true); return; }
            if (type == TypeSymbol.UShort) { encoder.UInt16(); return; }
            if (type == TypeSymbol.UInteger) { encoder.UInt32(); return; }
            if (type == TypeSymbol.ULong) { encoder.UInt64(); return; }
            if (type == TypeSymbol.Single) { encoder.Single(); return; }
            if (type == TypeSymbol.Date) { encoder.Double(); return; }
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
            if (type is ClassTypeSymbol classType)
            {
                if (ReferenceEquals(classType, VBStandardTypes.Collection))
                {
                    encoder.Type(GetReflectionTypeReference(typeof(VBCollection)), isValueType: false);
                    return;
                }

                if (IsRuntimeObjectContract(classType))
                {
                    encoder.Object();
                    return;
                }

                encoder.Type(_classSymbolHandles[classType], isValueType: false);
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
            if (ReferenceEquals(type, VBStandardTypes.Collection)) return GetReflectionTypeReference(typeof(VBCollection));
            if (type is ClassTypeSymbol objectContract && IsRuntimeObjectContract(objectContract)) return _systemObject;
            if (type is ClassTypeSymbol classType) return _classSymbolHandles[classType];
            if (type is ArrayTypeSymbol array) return GetArrayTypeSpecification(array);
            if (type == TypeSymbol.Byte) return GetReflectionTypeReference(typeof(byte));
            if (type == TypeSymbol.Integer) return GetReflectionTypeReference(typeof(short));
            if (type == TypeSymbol.Long) return GetReflectionTypeReference(typeof(int));
            if (type == TypeSymbol.LongLong) return GetReflectionTypeReference(typeof(long));
            if (type == TypeSymbol.LongPtr) return GetReflectionTypeReference(typeof(IntPtr));
            if (type == TypeSymbol.UShort) return GetReflectionTypeReference(typeof(ushort));
            if (type == TypeSymbol.UInteger) return GetReflectionTypeReference(typeof(uint));
            if (type == TypeSymbol.ULong) return GetReflectionTypeReference(typeof(ulong));
            if (type == TypeSymbol.Single) return GetReflectionTypeReference(typeof(float));
            if (type == TypeSymbol.Date) return GetReflectionTypeReference(typeof(double));
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
                var isTypeInitializer = IsTypeInitializer(procedure);
                var isInterfaceMethod = IsInterfaceProcedure(procedure);
                var isInterfaceImplementation = IsInterfaceImplementationProcedure(procedure);
                var isInstanceConstructor = procedure.DeclaringClass is not null &&
                    procedure.IsCompilerGenerated &&
                    string.Equals(procedure.Name, ".ctor", StringComparison.Ordinal);
                var isFinalizer = procedure.DeclaringClass is not null &&
                    procedure.IsCompilerGenerated &&
                    string.Equals(procedure.Name, "Finalize", StringComparison.Ordinal);
                if (procedure.IsExternal)
                {
                    ValidateExternalSignature(procedure);
                }

                var visibility = isInterfaceMethod
                    ? MethodAttributes.Public
                    : isTypeInitializer
                    ? MethodAttributes.Private
                    : isFinalizer
                        ? MethodAttributes.Family
                    : isInstanceConstructor || procedure.DeclaringClass is not null
                        ? MethodAttributes.Public
                    : procedure == _program.EntryPoint
                        ? MethodAttributes.Public
                        : MethodAttributes.Assembly;
                var attributes = visibility | MethodAttributes.HideBySig;
                if (procedure.IsStatic) attributes |= MethodAttributes.Static;
                if (procedure.IsExternal) attributes |= MethodAttributes.PinvokeImpl;
                if (isInterfaceMethod)
                {
                    attributes |= MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot;
                }
                if (isInterfaceImplementation)
                {
                    attributes |= MethodAttributes.Virtual;
                }
                if (isTypeInitializer)
                {
                    attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                }
                if (isInstanceConstructor)
                {
                    attributes |= MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
                }
                if (isFinalizer)
                {
                    attributes |= MethodAttributes.Virtual;
                }

                var implementation = isInterfaceMethod
                    ? MethodImplAttributes.IL | MethodImplAttributes.Managed
                    : procedure.IsExternal
                    ? MethodImplAttributes.IL | MethodImplAttributes.Managed | MethodImplAttributes.PreserveSig
                    : MethodImplAttributes.IL | MethodImplAttributes.Managed;
                var actual = _metadata.AddMethodDefinition(
                    attributes,
                    implementation,
                    _metadata.GetOrAddString(procedure.Name),
                    EncodeMethodSignature(procedure),
                    // MetadataBuilder maps offset 0 to the first body in the stream; -1 is the
                    // no-RVA marker required by abstract interface methods.
                    procedure.IsExternal || isInterfaceMethod ? -1 : bodyOffsets[procedure],
                    parameterStarts[procedure]);
                EnsureHandle(actual, _methodHandles[procedure], "method");

                if (procedure.IsExternal)
                {
                    if (string.IsNullOrWhiteSpace(procedure.ExternalLibrary))
                    {
                        throw new NotSupportedException(
                            $"Declare procedure '{procedure.Name}' has no native library name.");
                    }

                    var library = procedure.ExternalLibrary!;
                    if (!_moduleReferences.TryGetValue(library, out var module))
                    {
                        module = _metadata.AddModuleReference(_metadata.GetOrAddString(library));
                        _moduleReferences.Add(library, module);
                    }

                    var importName = string.IsNullOrWhiteSpace(procedure.ExternalAlias)
                        ? procedure.Name
                        : procedure.ExternalAlias!;
                    _metadata.AddMethodImport(
                        _methodHandles[procedure],
                        MethodImportAttributes.CallingConventionWinApi |
                        MethodImportAttributes.CharSetAnsi |
                        MethodImportAttributes.ExactSpelling,
                        _metadata.GetOrAddString(importName),
                        module);
                }
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
                else if (plan.Class is not null)
                {
                    var attributes = plan.Class.IsInterface
                        ? TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Interface
                        : TypeAttributes.NotPublic;
                    actual = _metadata.AddTypeDefinition(
                        attributes,
                        _metadata.GetOrAddString("VB6.Generated"),
                        _metadata.GetOrAddString(
                            (plan.Class.IsInterface ? "__vb6_interface_" : "__vb6_class_") +
                            Sanitize(plan.Class.Name)),
                        plan.Class.IsInterface ? default : _systemObject,
                        plan.FirstField,
                        plan.FirstMethod);
                }
                else
                {
                    var attributes = TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed;
                    if (!plan.Module!.Procedures.Any(IsTypeInitializer))
                    {
                        attributes |= TypeAttributes.BeforeFieldInit;
                    }

                    actual = _metadata.AddTypeDefinition(
                        attributes,
                        _metadata.GetOrAddString("VB6.Generated"),
                        _metadata.GetOrAddString("__vb6_module_" + Sanitize(plan.Module.Name)),
                        _systemObject,
                        plan.FirstField,
                        plan.FirstMethod);
                }
                EnsureHandle(actual, plan.TypeHandle, "type");
            }

            foreach (var plan in _typePlans)
            {
                if (plan.Class is { IsInterface: false })
                {
                    EmitInterfaceImplementations(plan);
                }
            }
        }

        private void EmitInterfaceImplementations(TypePlan implementorPlan)
        {
            var implementor = implementorPlan.Class ??
                throw new InvalidOperationException("Interface implementation plan has no class definition.");

            foreach (var interfaceType in implementor.Symbol.ImplementedInterfaces)
            {
                var interfaceDefinition = _program.ClassDefinitions.SingleOrDefault(
                    definition => ReferenceEquals(definition.Symbol, interfaceType));
                if (interfaceDefinition is null)
                {
                    throw new InvalidOperationException(
                        $"Interface '{interfaceType.Name}' has no IR class definition.");
                }

                _metadata.AddInterfaceImplementation(
                    implementorPlan.TypeHandle,
                    _classHandles[interfaceDefinition]);

                foreach (var interfaceMethod in interfaceDefinition.Methods)
                {
                    if (interfaceMethod.Symbol is null)
                    {
                        continue;
                    }

                    var implementationName = interfaceType.Name + "_" + interfaceMethod.Symbol.Name;
                    var implementation = implementor.Methods.SingleOrDefault(candidate =>
                        candidate.Symbol is not null &&
                        string.Equals(
                            candidate.Symbol.Name,
                            implementationName,
                            StringComparison.OrdinalIgnoreCase) &&
                        candidate.Symbol.PropertyAccessor == interfaceMethod.Symbol.PropertyAccessor);
                    if (implementation is null)
                    {
                        throw new InvalidOperationException(
                            $"Class '{implementor.Symbol.Name}' has no managed implementation for " +
                            $"'{implementationName}'.");
                    }

                    _metadata.AddMethodImplementation(
                        implementorPlan.TypeHandle,
                        _methodHandles[implementation],
                        GetInterfaceMethodReference(interfaceDefinition, interfaceMethod));
                }
            }
        }

        private MemberReferenceHandle GetInterfaceMethodReference(
            IrClassDefinition interfaceDefinition,
            IrProcedure method)
        {
            var key = "interface::" +
                MetadataTokens.GetToken(_classHandles[interfaceDefinition]) + "::" +
                method.Name + "::" + method.Symbol?.PropertyAccessor;
            if (_memberReferences.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var handle = _metadata.AddMemberReference(
                _classHandles[interfaceDefinition],
                _metadata.GetOrAddString(method.Name),
                EncodeMethodSignature(method));
            _memberReferences.Add(key, handle);
            return handle;
        }

        private static bool IsInterfaceProcedure(IrProcedure procedure) =>
            procedure.DeclaringClass?.IsInterfaceContract == true;

        private static bool IsInterfaceImplementationProcedure(IrProcedure procedure)
        {
            if (procedure.DeclaringClass is not { } declaringClass || procedure.Symbol is not { } symbol)
            {
                return false;
            }

            foreach (var interfaceType in declaringClass.ImplementedInterfaces)
            {
                if (interfaceType.Procedures.Any(expected =>
                        string.Equals(
                            interfaceType.Name + "_" + expected.Name,
                            symbol.Name,
                            StringComparison.OrdinalIgnoreCase) &&
                        expected.Parameters.Length == symbol.Parameters.Length &&
                        expected.PropertyAccessor == symbol.PropertyAccessor))
                {
                    return true;
                }

                if (interfaceType.Properties.Any(expected =>
                        string.Equals(
                            interfaceType.Name + "_" + expected.Name,
                            symbol.Name,
                            StringComparison.OrdinalIgnoreCase) &&
                        expected.Accessor == symbol.PropertyAccessor))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTypeInitializer(IrProcedure procedure) =>
            procedure.IsCompilerGenerated &&
            procedure.IsStatic &&
            procedure.Parameters.IsDefaultOrEmpty &&
            procedure.ReturnType is null &&
            string.Equals(procedure.Name, ".cctor", StringComparison.Ordinal);

        private static void ValidateExternalSignature(IrProcedure procedure)
        {
            if (procedure.ReturnType is not null && !IsPInvokeScalar(procedure.ReturnType))
            {
                throw new NotSupportedException(
                    $"Declare function '{procedure.Name}' return type '{procedure.ReturnType.Name}' " +
                    "needs an explicit native marshalling contract.");
            }

            foreach (var parameter in procedure.Parameters)
            {
                if (parameter.Symbol?.IsAny != true && !IsPInvokeScalar(parameter.Type))
                {
                    throw new NotSupportedException(
                        $"Declare procedure '{procedure.Name}' parameter '{parameter.Name}' type " +
                        $"'{parameter.Type.Name}' needs an explicit native marshalling contract.");
                }
            }
        }

        private static bool IsPInvokeScalar(TypeSymbol type) =>
            type == TypeSymbol.Byte ||
            type == TypeSymbol.Integer ||
            type == TypeSymbol.Long ||
            type == TypeSymbol.LongLong ||
            type == TypeSymbol.LongPtr ||
            type == TypeSymbol.UShort ||
            type == TypeSymbol.UInteger ||
            type == TypeSymbol.ULong ||
            type == TypeSymbol.Single ||
            type == TypeSymbol.Date ||
            type == TypeSymbol.Double ||
            type == TypeSymbol.Boolean ||
            type == TypeSymbol.String;

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
                else if (plan.Class is not null)
                {
                    foreach (var method in plan.Class.Methods) yield return method;
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
            if (type == typeof(IntPtr)) { encoder.IntPtr(); return; }
            if (type == typeof(byte)) { encoder.Byte(); return; }
            if (type == typeof(short)) { encoder.Int16(); return; }
            if (type == typeof(int)) { encoder.Int32(); return; }
            if (type == typeof(ushort)) { encoder.UInt16(); return; }
            if (type == typeof(uint)) { encoder.UInt32(); return; }
            if (type == typeof(long)) { encoder.Int64(); return; }
            if (type == typeof(ulong)) { encoder.UInt64(); return; }
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

        /// <summary>
        /// References one of the <c>VBTypeStorage</c> array helpers for a single element type. Both
        /// take the member storage plus its declared bounds and return the array; only whether the
        /// storage is passed by reference differs. The helpers are generic, so the member reference
        /// carries the open signature - where the type parameter is <c>!!0</c> - and a method
        /// specification supplies the concrete instantiation.
        /// </summary>
        private EntityHandle GetTypeStorageArrayReference(string name, TypeSymbol elementType, bool storageByRef)
        {
            var specKey = "VBTypeStorage::" + name + "<" + elementType.Name + ">";
            if (_methodSpecifications.TryGetValue(specKey, out var cachedSpec))
            {
                return cachedSpec;
            }

            var definitionKey = "VBTypeStorage::" + name;
            if (!_memberReferences.TryGetValue(definitionKey, out var definition))
            {
                var blob = new BlobBuilder();
                new BlobEncoder(blob)
                    .MethodSignature(genericParameterCount: 1, isInstanceMethod: false)
                    .Parameters(
                        2,
                        returnType => EncodeOpenVBArray(returnType.Type()),
                        parameters =>
                        {
                            EncodeOpenVBArray(parameters.AddParameter().Type(isByRef: storageByRef));
                            parameters.AddParameter().Type().SZArray().Type(_vbArrayBound, isValueType: true);
                        });
                definition = _metadata.AddMemberReference(
                    GetReflectionTypeReference(typeof(VBTypeStorage)),
                    _metadata.GetOrAddString(name),
                    _metadata.GetOrAddBlob(blob));
                _memberReferences.Add(definitionKey, definition);
            }

            var specBlob = new BlobBuilder();
            var arguments = new BlobEncoder(specBlob).MethodSpecificationSignature(1);
            EncodeType(arguments.AddArgument(), elementType);
            var spec = (EntityHandle)_metadata.AddMethodSpecification(
                definition,
                _metadata.GetOrAddBlob(specBlob));
            _methodSpecifications.Add(specKey, spec);
            return spec;
        }

        private EntityHandle GetFileDynamicArrayReference(string name, TypeSymbol elementType)
        {
            var specKey = "VBFiles::" + name + "<" + elementType.Name + ">";
            if (_methodSpecifications.TryGetValue(specKey, out var cachedSpec))
            {
                return cachedSpec;
            }

            var definitionKey = "VBFiles::" + name;
            if (!_memberReferences.TryGetValue(definitionKey, out var definition))
            {
                var blob = new BlobBuilder();
                new BlobEncoder(blob)
                    .MethodSignature(genericParameterCount: 1, isInstanceMethod: false)
                    .Parameters(
                        name == "GetDynamicArray" ? 1 : 2,
                        returnType =>
                        {
                            if (name == "GetDynamicArray")
                            {
                                EncodeOpenVBArray(returnType.Type());
                            }
                            else
                            {
                                returnType.Void();
                            }
                        },
                        parameters =>
                        {
                            parameters.AddParameter().Type().Int32();
                            if (name != "GetDynamicArray")
                            {
                                EncodeOpenVBArray(parameters.AddParameter().Type());
                            }
                        });
                definition = _metadata.AddMemberReference(
                    GetReflectionTypeReference(typeof(VBFiles)),
                    _metadata.GetOrAddString(name),
                    _metadata.GetOrAddBlob(blob));
                _memberReferences.Add(definitionKey, definition);
            }

            var specBlob = new BlobBuilder();
            var arguments = new BlobEncoder(specBlob).MethodSpecificationSignature(1);
            EncodeType(arguments.AddArgument(), elementType);
            var spec = (EntityHandle)_metadata.AddMethodSpecification(
                definition,
                _metadata.GetOrAddBlob(specBlob));
            _methodSpecifications.Add(specKey, spec);
            return spec;
        }

        /// <summary>Encodes <c>VBArray&lt;!!0&gt;</c>, the generic method's own type parameter.</summary>
        private void EncodeOpenVBArray(SignatureTypeEncoder encoder)
        {
            var arguments = encoder.GenericInstantiation(_vbArray, 1, isValueType: false);
            arguments.AddArgument().GenericMethodTypeParameter(0);
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
            if (m == IrRuntimeMethod.GraphicsLine) return Static(
                typeof(VBInteraction),
                nameof(VBInteraction.GraphicsLine),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(object),
                typeof(bool),
                typeof(bool),
                typeof(bool));
            if (m == IrRuntimeMethod.EndProgram) return Static(
                typeof(VBControlFlow),
                nameof(VBControlFlow.EndProgram));
            if (m == IrRuntimeMethod.CByte) return Static(typeof(VBConversions), "CByte", typeof(object));
            if (m == IrRuntimeMethod.CInt) return Static(typeof(VBConversions), "CInt", typeof(object));
            if (m == IrRuntimeMethod.CLng) return Static(typeof(VBConversions), "CLng", typeof(object));
            if (m == IrRuntimeMethod.CLngPtr) return Static(typeof(VBConversions), "CLngPtr", typeof(object));
            if (m == IrRuntimeMethod.CUShort) return Static(typeof(VBConversions), "CUShort", typeof(object));
            if (m == IrRuntimeMethod.CUInt) return Static(typeof(VBConversions), "CUInt", typeof(object));
            if (m == IrRuntimeMethod.CULng) return Static(typeof(VBConversions), "CULng", typeof(object));
            if (m == IrRuntimeMethod.CDec) return Static(typeof(VBConversions), "CDec", typeof(object));
            if (m == IrRuntimeMethod.CDate) return Static(typeof(VBConversions), "CDate", typeof(object));
            if (m == IrRuntimeMethod.DateToVariant) return Static(typeof(VBConversions), "DateToVariant", typeof(double));
            if (m == IrRuntimeMethod.CLngLng) return Static(typeof(VBConversions), "CLngLng", typeof(object));
            if (m == IrRuntimeMethod.CCur) return Static(typeof(VBConversions), "CCur", typeof(object));
            if (m == IrRuntimeMethod.CSng) return Static(typeof(VBConversions), "CSng", typeof(object));
            if (m == IrRuntimeMethod.CDbl) return Static(typeof(VBConversions), "CDbl", typeof(object));
            if (m == IrRuntimeMethod.CBool) return Static(typeof(VBConversions), "CBool", typeof(object));
            if (m == IrRuntimeMethod.CStr) return Static(typeof(VBConversions), "CStr", typeof(object));
            if (m == IrRuntimeMethod.CVErr) return Static(typeof(VBConversions), "CVErr", typeof(object));
            if (m == IrRuntimeMethod.VariantToBoolean) return Static(typeof(VBVariants), "ToBoolean", typeof(object));
            if (m == IrRuntimeMethod.StringLike) return Static(typeof(VBStrings), nameof(VBStrings.Like), typeof(object), typeof(object), typeof(bool));
            if (m == IrRuntimeMethod.ObjectIs) return Static(typeof(VBObjectIdentity), nameof(VBObjectIdentity.IsSame), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.DynamicGetMember) return Static(typeof(VBDynamicDispatch), nameof(VBDynamicDispatch.GetMember), typeof(object), typeof(string));
            if (m == IrRuntimeMethod.DynamicGetIndexedMember) return Static(typeof(VBDynamicDispatch), nameof(VBDynamicDispatch.GetIndexedMember), typeof(object), typeof(string), typeof(VBArray<object>));
            if (m == IrRuntimeMethod.DynamicSetMember) return Static(typeof(VBDynamicDispatch), nameof(VBDynamicDispatch.SetMember), typeof(object), typeof(string), typeof(object));
            if (m == IrRuntimeMethod.DynamicSetIndexedMember) return Static(typeof(VBDynamicDispatch), nameof(VBDynamicDispatch.SetIndexedMember), typeof(object), typeof(string), typeof(VBArray<object>), typeof(object));
            if (m == IrRuntimeMethod.DynamicInvokeMember) return Static(typeof(VBDynamicDispatch), nameof(VBDynamicDispatch.InvokeMember), typeof(object), typeof(string), typeof(VBArray<object>));
            if (m == IrRuntimeMethod.InteractionDoEvents) return Static(typeof(VBInteraction), "DoEvents");
            if (m == IrRuntimeMethod.InteractionMsgBox) return Static(typeof(VBInteraction), "MsgBox", typeof(string), typeof(int), typeof(string));
            if (m == IrRuntimeMethod.InteractionInputBox) return Static(typeof(VBInteraction), "InputBox", typeof(string), typeof(string), typeof(string), typeof(float), typeof(float), typeof(string), typeof(int));
            if (m == IrRuntimeMethod.InteractionLoad) return Static(typeof(VBInteraction), "Load", typeof(object));
            if (m == IrRuntimeMethod.InteractionUnload) return Static(typeof(VBInteraction), "Unload", typeof(object));
            if (m == IrRuntimeMethod.InteractionCreateObject) return Static(typeof(VBInteraction), nameof(VBInteraction.CreateObject), typeof(string), typeof(string));
            if (m == IrRuntimeMethod.InteractionGetObject) return Static(typeof(VBInteraction), nameof(VBInteraction.GetObject), typeof(string), typeof(string));
            if (m == IrRuntimeMethod.InteractionShell) return Static(typeof(VBInteraction), nameof(VBInteraction.Shell), typeof(string), typeof(short));
            if (m == IrRuntimeMethod.InteractionCommand) return Static(typeof(VBInteraction), nameof(VBInteraction.Command));
            if (m == IrRuntimeMethod.InteractionGetSetting) return Static(typeof(VBInteraction), nameof(VBInteraction.GetSetting), typeof(string), typeof(string), typeof(string), typeof(string));
            if (m == IrRuntimeMethod.InteractionSaveSetting) return Static(typeof(VBInteraction), nameof(VBInteraction.SaveSetting), typeof(string), typeof(string), typeof(string), typeof(string));
            if (m == IrRuntimeMethod.InteractionSendKeys) return Static(typeof(VBInteraction), nameof(VBInteraction.SendKeys), typeof(string), typeof(bool));
            if (m == IrRuntimeMethod.InteractionPopupMenu) return Static(typeof(VBInteraction), nameof(VBInteraction.PopupMenu), typeof(object), typeof(int), typeof(float), typeof(float));
            if (m == IrRuntimeMethod.InteractionLoadPicture) return Static(typeof(VBInteraction), nameof(VBInteraction.LoadPicture), typeof(string));
            if (m == IrRuntimeMethod.InteractionPropertyChanged) return Static(typeof(VBInteraction), nameof(VBInteraction.PropertyChanged), typeof(string));
            if (m == IrRuntimeMethod.InteractionScaleX) return Static(typeof(VBInteraction), nameof(VBInteraction.ScaleX), typeof(float), typeof(int), typeof(int));
            if (m == IrRuntimeMethod.InteractionScaleY) return Static(typeof(VBInteraction), nameof(VBInteraction.ScaleY), typeof(float), typeof(int), typeof(int));
            if (m == IrRuntimeMethod.InteractionTextWidth) return Static(typeof(VBInteraction), nameof(VBInteraction.TextWidth), typeof(string));
            if (m == IrRuntimeMethod.InteractionTextHeight) return Static(typeof(VBInteraction), nameof(VBInteraction.TextHeight), typeof(string));
            if (m == IrRuntimeMethod.InteractionPrint) return Static(typeof(VBInteraction), nameof(VBInteraction.Print), typeof(object));
            if (m == IrRuntimeMethod.InteractionPaintPicture) return Static(typeof(VBInteraction), nameof(VBInteraction.PaintPicture), typeof(object), typeof(float), typeof(float), typeof(float), typeof(float));
            if (m == IrRuntimeMethod.MemoryVarPtr) return Static(typeof(VBMemory), nameof(VBMemory.VarPtr), typeof(object));
            if (m == IrRuntimeMethod.MemoryObjPtr) return Static(typeof(VBMemory), nameof(VBMemory.ObjPtr), typeof(object));
            if (m == IrRuntimeMethod.MemoryStrPtr) return Static(typeof(VBMemory), nameof(VBMemory.StrPtr), typeof(string));
            if (m == IrRuntimeMethod.MemoryLSet) return Static(typeof(VBMemory), nameof(VBMemory.LSet), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.CollectionCreate) return Static(typeof(VBCollection), nameof(VBCollection.Create));
            if (m == IrRuntimeMethod.CollectionEnumerateValues) return Static(typeof(VBCollection), nameof(VBCollection.EnumerateValues), typeof(VBCollection));
            if (m == IrRuntimeMethod.ControlEnumerateValues) return Static(typeof(VBInteraction), nameof(VBInteraction.EnumerateControls), typeof(object));
            if (m == IrRuntimeMethod.CollectionCount) return Static(typeof(VBCollection), nameof(VBCollection.CountValue), typeof(VBCollection));
            if (m == IrRuntimeMethod.CollectionItem) return Static(typeof(VBCollection), nameof(VBCollection.ItemValue), typeof(VBCollection), typeof(object));
            if (m == IrRuntimeMethod.CollectionAdd) return Static(typeof(VBCollection), nameof(VBCollection.AddValue), typeof(VBCollection), typeof(object), typeof(object), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.CollectionRemove) return Static(typeof(VBCollection), nameof(VBCollection.RemoveValue), typeof(VBCollection), typeof(object));
            if (m == IrRuntimeMethod.DateTimeNow) return Static(typeof(VBDateTime), "Now");
            if (m == IrRuntimeMethod.DateTimeValue) return Static(typeof(VBDateTime), "DateValue", typeof(object));
            if (m == IrRuntimeMethod.TimeDateValue) return Static(typeof(VBDateTime), "TimeValue", typeof(object));
            if (m is IrRuntimeMethod.DateTimeYear or IrRuntimeMethod.DateTimeMonth or
                IrRuntimeMethod.DateTimeDay or IrRuntimeMethod.DateTimeHour or
                IrRuntimeMethod.DateTimeMinute or IrRuntimeMethod.DateTimeSecond)
            {
                var name = m switch
                {
                    IrRuntimeMethod.DateTimeYear => "Year",
                    IrRuntimeMethod.DateTimeMonth => "Month",
                    IrRuntimeMethod.DateTimeDay => "Day",
                    IrRuntimeMethod.DateTimeHour => "Hour",
                    IrRuntimeMethod.DateTimeMinute => "Minute",
                    _ => "Second"
                };
                return Static(typeof(VBDateTime), name, typeof(double));
            }
            if (m == IrRuntimeMethod.DateTimeTimer) return Static(typeof(VBDateTime), "Timer");
            if (m == IrRuntimeMethod.DateTimeSerial) return Static(typeof(VBDateTime), "DateSerial", typeof(short), typeof(short), typeof(short));
            if (m == IrRuntimeMethod.TimeDateSerial) return Static(typeof(VBDateTime), "TimeSerial", typeof(short), typeof(short), typeof(short));
            if (m == IrRuntimeMethod.DateTimeAdd) return Static(typeof(VBDateTime), "DateAdd", typeof(string), typeof(double), typeof(double));
            if (m == IrRuntimeMethod.DateTimeDiff) return Static(typeof(VBDateTime), "DateDiff", typeof(string), typeof(double), typeof(double), typeof(int), typeof(int));
            if (m == IrRuntimeMethod.DateTimePart) return Static(typeof(VBDateTime), "DatePart", typeof(string), typeof(double), typeof(int), typeof(int));
            if (m == IrRuntimeMethod.DateTimeWeekday) return Static(typeof(VBDateTime), "Weekday", typeof(double), typeof(int));
            if (m == IrRuntimeMethod.DateTimeWeekdayName) return Static(typeof(VBDateTime), "WeekdayName", typeof(int), typeof(bool), typeof(int));
            if (m == IrRuntimeMethod.DateTimeMonthName) return Static(typeof(VBDateTime), "MonthName", typeof(int), typeof(bool));
            if (m == IrRuntimeMethod.ErrorNumber) return Static(typeof(VBErrors), nameof(VBErrors.NumberValue));
            if (m == IrRuntimeMethod.ErrorDescription) return Static(typeof(VBErrors), nameof(VBErrors.DescriptionValue));
            if (m == IrRuntimeMethod.ErrorSource) return Static(typeof(VBErrors), nameof(VBErrors.SourceValue));
            if (m == IrRuntimeMethod.ErrorLineNumber) return Static(typeof(VBErrors), nameof(VBErrors.LineNumberValue));
            if (m == IrRuntimeMethod.ErrorClear) return Static(typeof(VBErrors), nameof(VBErrors.Clear));
            if (m == IrRuntimeMethod.ErrorRaise) return Static(typeof(VBErrors), nameof(VBErrors.Raise), typeof(int), typeof(string), typeof(string), typeof(string), typeof(int));
            if (m == IrRuntimeMethod.FunctionTypeName) return Static(typeof(VBFunctions), nameof(VBFunctions.TypeName), typeof(object));
            if (m == IrRuntimeMethod.FunctionSwitch) return Static(typeof(VBFunctions), nameof(VBFunctions.Switch), typeof(VBArray<object>));
            if (m == IrRuntimeMethod.FunctionIIf) return Static(typeof(VBFunctions), nameof(VBFunctions.IIf), typeof(bool), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.FunctionRGB) return Static(typeof(VBFunctions), nameof(VBFunctions.RGB), typeof(int), typeof(int), typeof(int));
            if (m == IrRuntimeMethod.ArrayIsAllocated) return Static(typeof(VBArrayOperations), nameof(VBArrayOperations.IsAllocated), typeof(object));
            if (m == IrRuntimeMethod.ArrayRequireAllocated) return Static(typeof(VBArrayOperations), nameof(VBArrayOperations.RequireAllocated), typeof(object));

            if (m is IrRuntimeMethod.Equal or IrRuntimeMethod.NotEqual or IrRuntimeMethod.Less or IrRuntimeMethod.LessOrEqual or IrRuntimeMethod.Greater or IrRuntimeMethod.GreaterOrEqual or IrRuntimeMethod.Concat)
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.ConcatVariant)
                return Static(typeof(VBOperators), "ConcatVariant", typeof(object), typeof(object));
            if (m is IrRuntimeMethod.VariantEqual or IrRuntimeMethod.VariantNotEqual or
                IrRuntimeMethod.VariantLess or IrRuntimeMethod.VariantLessOrEqual or
                IrRuntimeMethod.VariantGreater or IrRuntimeMethod.VariantGreaterOrEqual)
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object), typeof(object));
            if (m is IrRuntimeMethod.StringVariantEqual or IrRuntimeMethod.StringVariantNotEqual or
                IrRuntimeMethod.StringVariantLess or IrRuntimeMethod.StringVariantLessOrEqual or
                IrRuntimeMethod.StringVariantGreater or IrRuntimeMethod.StringVariantGreaterOrEqual)
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object), typeof(object));
            if (m == IrRuntimeMethod.Power) return Static(typeof(VBOperators), "Power", typeof(double), typeof(double));
            if (m == IrRuntimeMethod.PowerVariant) return Static(typeof(VBOperators), "PowerVariant", typeof(object), typeof(object));
            if (m == IrRuntimeMethod.MultiplyVariant) return Static(typeof(VBOperators), "MultiplyInteger", typeof(object), typeof(object));

            if (m is IrRuntimeMethod.AddVariant or IrRuntimeMethod.SubtractVariant or
                IrRuntimeMethod.AddStringVariant or
                IrRuntimeMethod.DivideVariant or IrRuntimeMethod.IntegerDivideVariant or
                IrRuntimeMethod.ModVariant or IrRuntimeMethod.AndVariant or
                IrRuntimeMethod.OrVariant or IrRuntimeMethod.XorVariant or
                IrRuntimeMethod.EqvVariant or IrRuntimeMethod.ImpVariant)
            {
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object), typeof(object));
            }

            if (m is IrRuntimeMethod.NegateVariant or IrRuntimeMethod.NotVariant)
            {
                return Static(typeof(VBOperators), RuntimeName(m), typeof(object));
            }

            if (m == IrRuntimeMethod.FixedStringRead)
                return Static(typeof(VBTypeStorage), "ReadFixedString", typeof(string), typeof(int));
            if (m == IrRuntimeMethod.FixedStringWrite)
                return Static(typeof(VBTypeStorage), "WriteFixedString", typeof(string), typeof(int));

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
                if (name == "Val") return Static(typeof(VBStrings), name, typeof(string));
                if (name == "Hex") return Static(typeof(VBStrings), name, typeof(object));
                if (name == "Repeat") return Static(typeof(VBStrings), "String", typeof(int), typeof(object));
                if (name == "Format") return Static(typeof(VBStrings), nameof(VBStrings.FormatValue), typeof(object), typeof(string), typeof(int), typeof(int));
                if (name == "IsNumeric") return Static(typeof(VBStrings), name, typeof(object));
                if (name == "InStr") return Static(typeof(VBStrings), name, typeof(int), typeof(string), typeof(string), typeof(int));
                if (name == "InStrRev") return Static(typeof(VBStrings), name, typeof(string), typeof(string), typeof(int), typeof(int));
                if (name == "Replace") return Static(typeof(VBStrings), name, typeof(string), typeof(string), typeof(string), typeof(int), typeof(int), typeof(int));
                if (name == "Space") return Static(typeof(VBStrings), name, typeof(int));
                if (name == "Split") return Static(typeof(VBStrings), name, typeof(string), typeof(string), typeof(int), typeof(int));
                if (name == "StrConv") return Static(typeof(VBStrings), name, typeof(string), typeof(int), typeof(int));
            }

            if (m == IrRuntimeMethod.ConversionInt) return Static(typeof(VBConversions), "Int", typeof(object));
            if (m == IrRuntimeMethod.MathAbs) return Static(typeof(VBMath), "Abs", typeof(object));
            if (m == IrRuntimeMethod.MathSgn) return Static(typeof(VBMath), "Sgn", typeof(object));
            if (m == IrRuntimeMethod.MathFix) return Static(typeof(VBMath), "Fix", typeof(object));
            if (m == IrRuntimeMethod.MathRound) return Static(typeof(VBMath), "Round", typeof(object), typeof(short));
            if (m == IrRuntimeMethod.MathSqr) return Static(typeof(VBMath), "Sqr", typeof(double));
            if (m == IrRuntimeMethod.MathExp) return Static(typeof(VBMath), "Exp", typeof(double));
            if (m == IrRuntimeMethod.MathLog) return Static(typeof(VBMath), "Log", typeof(double));
            if (m == IrRuntimeMethod.MathSin) return Static(typeof(VBMath), "Sin", typeof(double));
            if (m == IrRuntimeMethod.MathCos) return Static(typeof(VBMath), "Cos", typeof(double));
            if (m == IrRuntimeMethod.MathTan) return Static(typeof(VBMath), "Tan", typeof(double));
            if (m == IrRuntimeMethod.MathAtn) return Static(typeof(VBMath), "Atn", typeof(double));

            if (m is IrRuntimeMethod.VariantEmpty or IrRuntimeMethod.VariantNull or
                IrRuntimeMethod.VariantNothing or IrRuntimeMethod.VariantMissing)
            {
                var name = m switch
                {
                    IrRuntimeMethod.VariantEmpty => "EmptyValue",
                    IrRuntimeMethod.VariantNull => "NullValue",
                    IrRuntimeMethod.VariantNothing => "NothingValue",
                    _ => "MissingValue"
                };
                return Static(typeof(VBVariants), name);
            }

            if (m is IrRuntimeMethod.VariantIsEmpty or IrRuntimeMethod.VariantIsNull or
                IrRuntimeMethod.VariantIsMissing or IrRuntimeMethod.VariantIsError or IrRuntimeMethod.VariantVarType)
            {
                var name = m switch
                {
                    IrRuntimeMethod.VariantIsEmpty => "IsEmpty",
                    IrRuntimeMethod.VariantIsNull => "IsNull",
                    IrRuntimeMethod.VariantIsMissing => "IsMissing",
                    IrRuntimeMethod.VariantIsError => "IsError",
                    _ => "VarType"
                };
                return Static(typeof(VBVariants), name, typeof(object));
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
                IrRuntimeMethod.FileOpenInput => Static(typeof(VBFiles), "OpenInput", typeof(int), typeof(string)),
                IrRuntimeMethod.FileOpenOutput => Static(typeof(VBFiles), "OpenOutput", typeof(int), typeof(string)),
                IrRuntimeMethod.FileOpenAppend => Static(typeof(VBFiles), "OpenAppend", typeof(int), typeof(string)),
                IrRuntimeMethod.FileOpenRandom => Static(typeof(VBFiles), "OpenRandom", typeof(int), typeof(string), typeof(int)),
                IrRuntimeMethod.FileRecordStart => ResolveFileRecordStart(call, out skippedArgument),
                IrRuntimeMethod.FileRecordEnd => Static(typeof(VBFiles), "EndRecord", typeof(int), typeof(bool)),
                IrRuntimeMethod.FilePrint => Static(typeof(VBFiles), "Print", typeof(int), typeof(object)),
                IrRuntimeMethod.FileClose => Static(typeof(VBFiles), "Close", typeof(int)),
                IrRuntimeMethod.FileCloseAll => Static(typeof(VBFiles), "CloseAll"),
                IrRuntimeMethod.FileSeek => Static(typeof(VBFiles), "Seek", typeof(int), typeof(long)),
                IrRuntimeMethod.FileFreeFile => Static(typeof(VBFiles), "FreeFile"),
                IrRuntimeMethod.FileLength => Static(typeof(VBFiles), "Length", typeof(int)),
                IrRuntimeMethod.FileEndOfFile => Static(typeof(VBFiles), "EndOfFile", typeof(int)),
                IrRuntimeMethod.FilePosition => Static(typeof(VBFiles), "Position", typeof(int)),
                IrRuntimeMethod.FileKill => Static(typeof(VBFiles), "Kill", typeof(string)),
                IrRuntimeMethod.FileDir => Static(typeof(VBFiles), "Dir", typeof(string), typeof(int)),
                IrRuntimeMethod.FileLengthByPath => Static(typeof(VBFiles), "Length", typeof(string)),
                IrRuntimeMethod.FilePut => ResolveFilePut(call, out skippedArgument),
                IrRuntimeMethod.FilePutRaw => ResolveFilePutRaw(call, out skippedArgument),
                IrRuntimeMethod.FilePutRawFixedString => ResolveFilePutRaw(call, out skippedArgument),
                IrRuntimeMethod.FileLineInput => Static(typeof(VBFiles), "LineInput", typeof(int)),
                IrRuntimeMethod.FileInputField => Static(typeof(VBFiles), "InputField", typeof(int)),
                IrRuntimeMethod.FileInput => Static(typeof(VBFiles), "Input", typeof(long), typeof(int)),
                _ => ResolveFileGet(call, out skippedArgument)
            };
        }

        private MethodInfo ResolveFileGet(IrRuntimeCallExpression call, out int skippedArgument)
        {
            var name = call.Method.ToString()["File".Length..];
            if (name.StartsWith("GetRaw", StringComparison.Ordinal))
            {
                skippedArgument = -1;
                return name == "GetRawFixedString"
                    ? Static(typeof(VBFiles), name, typeof(int), typeof(int))
                    : Static(typeof(VBFiles), name, typeof(int));
            }

            var omitted = call.Arguments.Length > 1 && call.Arguments[1].Expression is IrNullExpression;
            skippedArgument = omitted ? 1 : -1;
            return omitted
                ? Static(typeof(VBFiles), name, typeof(int))
                : Static(typeof(VBFiles), name, typeof(int), typeof(long));
        }

        private MethodInfo ResolveFileRecordStart(IrRuntimeCallExpression call, out int skippedArgument)
        {
            var omitted = call.Arguments[1].Expression is IrNullExpression;
            skippedArgument = omitted ? 1 : -1;
            return omitted
                ? Static(typeof(VBFiles), "BeginRecord", typeof(int))
                : Static(typeof(VBFiles), "BeginRecord", typeof(int), typeof(long));
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

        private MethodInfo ResolveFilePutRaw(IrRuntimeCallExpression call, out int skippedArgument)
        {
            skippedArgument = -1;
            if (call.Method == IrRuntimeMethod.FilePutRawFixedString)
            {
                return Static(typeof(VBFiles), "PutRawFixedString", typeof(int), typeof(string), typeof(int));
            }

            var valueType = RuntimeScalarType(call.Arguments[1].Expression.Type);
            return Static(typeof(VBFiles), "PutRaw", typeof(int), valueType);
        }

        private static string RuntimeName(IrRuntimeMethod method) => method switch
        {
            IrRuntimeMethod.IntegerDivideInteger => "IntegerDivide",
            IrRuntimeMethod.IntegerDivideVariant => "IntegerDivideVariant",
            _ => method.ToString()
        };

        private static Type RuntimeScalarType(TypeSymbol type) => type == TypeSymbol.Byte ? typeof(byte)
            : type == TypeSymbol.Integer ? typeof(short)
            : type == TypeSymbol.Long ? typeof(int)
            : type == TypeSymbol.LongLong ? typeof(long)
            : type == TypeSymbol.LongPtr ? typeof(IntPtr)
            : type == TypeSymbol.UShort ? typeof(ushort)
            : type == TypeSymbol.UInteger ? typeof(uint)
            : type == TypeSymbol.ULong ? typeof(ulong)
            : type == TypeSymbol.Single ? typeof(float)
            : type == TypeSymbol.Date ? typeof(double)
            : type == TypeSymbol.Double ? typeof(double)
            : type == TypeSymbol.Boolean ? typeof(bool)
            : type == TypeSymbol.String || type is FixedLengthStringTypeSymbol ? typeof(string)
            : type == TypeSymbol.Currency ? typeof(VBCurrency)
            : type == TypeSymbol.Variant ? typeof(object)
            : throw new NotSupportedException($"Runtime scalar type '{type.Name}' is not supported.");

        private static bool IsRuntimeObjectContract(ClassTypeSymbol type) =>
            ReferenceEquals(type, VBStandardTypes.Object) ||
            ReferenceEquals(type, VBStandardTypes.Control) ||
            ReferenceEquals(type, VBStandardTypes.Form) ||
            ReferenceEquals(type, VBStandardTypes.UserControl);

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
            type == TypeSymbol.String || type == TypeSymbol.Variant || type is ArrayTypeSymbol ||
            type is ClassTypeSymbol;

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
            public IrClassDefinition? Class { get; private init; }
            public IrModule? Module { get; private init; }
            public TypeDefinitionHandle TypeHandle { get; set; }
            public FieldDefinitionHandle FirstField { get; set; }
            public MethodDefinitionHandle FirstMethod { get; set; }

            public static TypePlan ModuleType() => new() { IsModulePseudoType = true };
            public static TypePlan ForUdt(IrTypeDefinition type) => new() { Udt = type };
            public static TypePlan ForClass(IrClassDefinition @class) => new() { Class = @class };
            public static TypePlan ForModule(IrModule module) => new() { Module = module };
        }
    }
}
