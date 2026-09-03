using System.Collections.Immutable;
using VB6.Runtime;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.IR;

public sealed record IrModuleInput(string Name, string? SourcePath, SemanticModel SemanticModel);

public static class IrLowerer
{
    public static IrProgram Lower(
        IEnumerable<IrModuleInput> modules,
        IEnumerable<BoundModuleVariable>? additionalGlobals = null,
        VBCompatibilityProfile compatibilityProfile = VBCompatibilityProfile.Deterministic)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var inputs = modules.ToImmutableArray();
        if (inputs.IsDefaultOrEmpty)
        {
            return new IrProgram(
                ImmutableArray<IrModule>.Empty,
                ImmutableArray<IrTypeDefinition>.Empty,
                null,
                CompatibilityProfile: compatibilityProfile);
        }

        var state = new ProgramLoweringState(
            inputs,
            additionalGlobals ?? Array.Empty<BoundModuleVariable>(),
            compatibilityProfile);
        return state.Lower();
    }

    private sealed class ProgramLoweringState
    {
        private readonly ImmutableArray<IrModuleInput> _inputs;
        private readonly ImmutableArray<BoundModuleVariable> _additionalGlobals;
        private readonly VBCompatibilityProfile _compatibilityProfile;
        private readonly Dictionary<ModuleVariableSymbol, IrGlobal> _globals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ModuleVariableSymbol, BoundExpression> _constantValues =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<string, BoundExpression> _constantValuesByName =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<UserDefinedTypeSymbol, IrTypeDefinition> _types =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<UserDefinedTypeMemberSymbol, IrField> _fields =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ModuleVariableSymbol, IrField> _classFields =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ClassTypeSymbol, ImmutableArray<BoundModuleVariable>> _classVariables =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ClassTypeSymbol, ImmutableArray<DesignerPropertyInitializer>>
            _classDesignerInitializers = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ClassTypeSymbol, Dictionary<string, ProcedureSymbol>> _classProcedures =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ModuleVariableSymbol, ImmutableArray<(EventSymbol Event, ProcedureSymbol Handler)>>
            _withEventsHandlers = new(ReferenceEqualityComparer.Instance);

        public ProgramLoweringState(
            ImmutableArray<IrModuleInput> inputs,
            IEnumerable<BoundModuleVariable> additionalGlobals,
            VBCompatibilityProfile compatibilityProfile)
        {
            _inputs = inputs;
            _additionalGlobals = additionalGlobals.ToImmutableArray();
            _compatibilityProfile = compatibilityProfile;
        }

        public IrProgram Lower()
        {
            PredeclareTypes();
            PredeclareClasses();
            PredeclareGlobals();

            var modules = ImmutableArray.CreateBuilder<IrModule>(_inputs.Length + 1);
            var classes = ImmutableArray.CreateBuilder<IrClassDefinition>();
            IrProcedure? entryPoint = null;
            foreach (var input in _inputs)
            {
                if (input.SemanticModel.ContainingClass is { } containingClass)
                {
                    var classProcedures = ImmutableArray.CreateBuilder<IrProcedure>();
                    foreach (var procedure in input.SemanticModel.Procedures)
                    {
                        classProcedures.Add(containingClass.IsInterfaceContract
                            ? LowerInterfaceProcedure(procedure, containingClass)
                            : new ProcedureLowerer(
                                this,
                                procedure,
                                containingClass: containingClass).Lower());
                    }

                    foreach (var external in input.SemanticModel.ExternalProcedures)
                    {
                        classProcedures.Add(new IrProcedure(
                            external,
                            external.Name,
                            external.ReturnType,
                            external.Parameters
                                .Select((parameter, index) => new IrParameter(
                                    parameter,
                                    index,
                                    Mangle(parameter.Name),
                                    parameter.Type,
                                    parameter.PassingMode))
                                .ToImmutableArray(),
                            ImmutableArray<IrLocal>.Empty,
                            ImmutableArray<IrBasicBlock>.Empty,
                            IsExternal: true,
                            ExternalLibrary: external.ExternalLibrary,
                            ExternalAlias: external.ExternalAlias,
                            DeclaringClass: containingClass));
                    }

                    if (!containingClass.IsInterfaceContract)
                    {
                        classProcedures.Insert(0, LowerClassConstructor(containingClass));
                        if (TryGetClassProcedure(
                                containingClass,
                                "Class_Terminate",
                                null,
                                out var terminator))
                        {
                            classProcedures.Add(LowerClassFinalizer(containingClass, terminator));
                        }
                    }
                    var fields = !containingClass.IsInterfaceContract &&
                        _classVariables.TryGetValue(containingClass, out var variables)
                        ? variables
                            .Where(variable => _classFields.ContainsKey(variable.Symbol))
                            .Select(variable => _classFields[variable.Symbol])
                            .ToImmutableArray()
                        : ImmutableArray<IrField>.Empty;
                    classes.Add(new IrClassDefinition(
                        containingClass,
                        Mangle(containingClass.Name),
                        fields,
                        classProcedures.ToImmutable(),
                        containingClass.IsInterfaceContract));
                    continue;
                }

                var globals = input.SemanticModel.ModuleVariables
                    .Where(variable => _globals.ContainsKey(variable.Symbol))
                    .Select(variable => _globals[variable.Symbol])
                    .Distinct()
                    .ToImmutableArray();

                var procedures = ImmutableArray.CreateBuilder<IrProcedure>();
                var moduleInitializers = input.SemanticModel.ModuleVariables
                    .Where(NeedsModuleInitialization)
                    .ToImmutableArray();
                if (!moduleInitializers.IsDefaultOrEmpty)
                {
                    procedures.Add(LowerModuleInitializer(moduleInitializers));
                }

                foreach (var procedure in input.SemanticModel.Procedures)
                {
                    var lowered = new ProcedureLowerer(this, procedure).Lower();
                    procedures.Add(lowered);
                    if (!procedure.Symbol.IsFunction &&
                        string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase))
                    {
                        entryPoint ??= lowered;
                    }
                }

                foreach (var external in input.SemanticModel.ExternalProcedures)
                {
                    procedures.Add(new IrProcedure(
                        external,
                        external.Name,
                        external.ReturnType,
                        external.Parameters
                            .Select((parameter, index) => new IrParameter(
                                parameter,
                                index,
                                Mangle(parameter.Name),
                                parameter.Type,
                                parameter.PassingMode))
                            .ToImmutableArray(),
                        ImmutableArray<IrLocal>.Empty,
                        ImmutableArray<IrBasicBlock>.Empty,
                        IsExternal: true,
                        ExternalLibrary: external.ExternalLibrary,
                        ExternalAlias: external.ExternalAlias));
                }

                modules.Add(new IrModule(input.Name, input.SourcePath, globals, procedures.ToImmutable()));
            }

            var extraGlobals = _additionalGlobals
                .Where(variable => _globals.ContainsKey(variable.Symbol))
                .Select(variable => _globals[variable.Symbol])
                .Where(global => modules.All(module => !module.Globals.Contains(global)))
                .Distinct()
                .ToImmutableArray();
            if (!extraGlobals.IsDefaultOrEmpty)
            {
                var extraInitializers = _additionalGlobals
                    .Where(NeedsModuleInitialization)
                    .Where(variable =>
                        _globals.TryGetValue(variable.Symbol, out var global) && extraGlobals.Contains(global))
                    .ToImmutableArray();
                var extraProcedures = extraInitializers.IsDefaultOrEmpty
                    ? ImmutableArray<IrProcedure>.Empty
                    : ImmutableArray.Create(LowerModuleInitializer(extraInitializers));
                modules.Add(new IrModule("__CompilerGlobals", null, extraGlobals, extraProcedures));
            }

            return new IrProgram(
                modules.ToImmutable(),
                _types.Values.ToImmutableArray(),
                entryPoint,
                classes.ToImmutable(),
                _compatibilityProfile);
        }

        public IrGlobal GetGlobal(ModuleVariableSymbol symbol) =>
            _globals.TryGetValue(symbol, out var global)
                ? global
                : throw new InvalidOperationException($"Global '{symbol.Name}' was not declared before lowering.");

        public IrField GetField(UserDefinedTypeMemberSymbol symbol) =>
            _fields.TryGetValue(symbol, out var field)
                ? field
                : throw new InvalidOperationException($"UDT field '{symbol.Name}' was not declared before lowering.");

        public bool TryGetClassField(ModuleVariableSymbol symbol, out IrField field) =>
            _classFields.TryGetValue(symbol, out field!);

        public bool TryGetClassField(
            ClassTypeSymbol classType,
            string name,
            out IrField field)
        {
            if (_classVariables.TryGetValue(classType, out var variables))
            {
                foreach (var variable in variables)
                {
                    if (string.Equals(variable.Symbol.Name, name, StringComparison.OrdinalIgnoreCase) &&
                        _classFields.TryGetValue(variable.Symbol, out field!))
                    {
                        return true;
                    }
                }
            }

            field = null!;
            return false;
        }

        public bool TryGetWithEventsHandlers(
            ModuleVariableSymbol symbol,
            out ImmutableArray<(EventSymbol Event, ProcedureSymbol Handler)> handlers) =>
            _withEventsHandlers.TryGetValue(symbol, out handlers);

        public ProcedureSymbol ResolveClassProcedure(ClassTypeSymbol classType, ProcedureSymbol requested)
        {
            return _classProcedures.TryGetValue(classType, out var procedures) &&
                   procedures.TryGetValue(ProcedureKey(requested.Name, requested.PropertyAccessor), out var procedure)
                ? procedure
                : requested;
        }

        public bool TryGetClassProcedure(
            ClassTypeSymbol classType,
            string name,
            PropertyAccessorKind? accessor,
            out ProcedureSymbol procedure)
        {
            if (_classProcedures.TryGetValue(classType, out var procedures) &&
                procedures.TryGetValue(ProcedureKey(name, accessor), out var found))
            {
                procedure = found;
                return true;
            }

            procedure = null!;
            return false;
        }

        private static string ProcedureKey(string name, PropertyAccessorKind? accessor) =>
            name + "|" + (accessor?.ToString() ?? "Method");

        private void PredeclareGlobals()
        {
            foreach (var variable in _inputs.SelectMany(input => input.SemanticModel.ModuleVariables)
                         .Concat(_additionalGlobals))
            {
                if (_globals.ContainsKey(variable.Symbol) ||
                    _constantValues.ContainsKey(variable.Symbol))
                {
                    continue;
                }

                if (variable.IsConstant && variable.Initializer is not null)
                {
                    _constantValues.Add(variable.Symbol, variable.Initializer);
                    _constantValuesByName.TryAdd(variable.Symbol.Name, variable.Initializer);
                    continue;
                }

                _globals.Add(variable.Symbol, new IrGlobal(
                    variable.Symbol,
                    Mangle(variable.Symbol.Name),
                    variable.Symbol.Type,
                    null,
                    variable.IsConstant));
            }
        }

        private IrProcedure LowerModuleInitializer(ImmutableArray<BoundModuleVariable> variables)
        {
            var symbol = new ProcedureSymbol(".cctor", ImmutableArray<ParameterSymbol>.Empty, null);
            var procedure = new BoundProcedure(
                symbol,
                ImmutableArray<LocalVariableSymbol>.Empty,
                new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty));
            return new ProcedureLowerer(this, procedure, variables).Lower();
        }

        private IrProcedure LowerClassConstructor(ClassTypeSymbol classType)
        {
            var instructions = ImmutableArray.CreateBuilder<IrInstruction>();
            if (_classVariables.TryGetValue(classType, out var fields))
            {
                foreach (var variable in fields)
                {
                    var field = _classFields[variable.Symbol];
                    var target = new IrFieldPlace(new IrThisPlace(classType), field);
                    if (variable.IsDesignerControl && variable.Symbol.Type is ArrayTypeSymbol controlArray)
                    {
                        var bounds = variable.ArrayDimensions
                            .Select(dimension => new IrArrayBound(
                                new IrConstantExpression(ReadConstantLong(dimension.LowerBound), TypeSymbol.Long),
                                new IrConstantExpression(ReadConstantLong(dimension.UpperBound), TypeSymbol.Long)))
                            .ToImmutableArray();
                        instructions.Add(new IrStoreInstruction(
                            target,
                            new IrNewVBArrayExpression(controlArray, bounds)));

                        var designerIndices = variable.DesignerArrayIndices.IsDefaultOrEmpty
                            ? EnumerateArrayIndices(bounds)
                            : variable.DesignerArrayIndices.Select(index =>
                                ImmutableArray.Create((long)index));
                        foreach (var indices in designerIndices)
                        {
                            var displayName = variable.Symbol.Name + "(" +
                                string.Join(",", indices.Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture))) +
                                ")";
                            var elementTarget = new IrArrayElementPlace(
                                new IrLoadExpression(target),
                                indices.Select(index => (IrExpression)new IrConstantExpression(index, TypeSymbol.Long)).ToImmutableArray(),
                                controlArray.ElementType);
                            instructions.Add(new IrStoreInstruction(
                                elementTarget,
                                CreateDesignerControl(
                                    classType,
                                    displayName,
                                    controlArray.ElementType,
                                    variable.DesignerParentName,
                                    variable.DesignerTypeName)));
                            AddDesignerInitializers(instructions, elementTarget, variable.DesignerInitializers);
                        }
                    }
                    else if (variable.IsDesignerControl && variable.Symbol.Type is ClassTypeSymbol controlType)
                    {
                        instructions.Add(new IrStoreInstruction(
                            target,
                            CreateDesignerControl(
                                classType,
                                variable.Symbol.Name,
                                controlType,
                                variable.DesignerParentName,
                                variable.DesignerTypeName)));
                        AddDesignerInitializers(instructions, target, variable.DesignerInitializers);
                    }
                    else if (variable.Symbol.Type is FixedLengthStringTypeSymbol fixedStringField)
                    {
                        instructions.Add(new IrStoreInstruction(
                            target,
                            ProcedureLowerer.FixedStringInitialValue(fixedStringField)));
                    }
                    else if (variable.Symbol.Type == TypeSymbol.String)
                    {
                        instructions.Add(new IrStoreInstruction(
                            target,
                            new IrConstantExpression(string.Empty, TypeSymbol.String)));
                    }
                    else if (variable.Symbol.Type is ArrayTypeSymbol array &&
                             !variable.ArrayDimensions.IsDefaultOrEmpty)
                    {
                        var bounds = variable.ArrayDimensions
                            .Select(dimension => new IrArrayBound(
                                new IrConstantExpression(ReadConstantLong(dimension.LowerBound), TypeSymbol.Long),
                                new IrConstantExpression(ReadConstantLong(dimension.UpperBound), TypeSymbol.Long)))
                            .ToImmutableArray();
                        instructions.Add(new IrStoreInstruction(
                            target,
                            new IrNewVBArrayExpression(array, bounds)));
                    }
                }
            }

            if (_classDesignerInitializers.TryGetValue(classType, out var designerInitializers))
            {
                AddDesignerInitializers(
                    instructions,
                    new IrThisPlace(classType),
                    designerInitializers);
            }

            if (TryGetClassProcedure(classType, "Class_Initialize", null, out var initializer))
            {
                instructions.Add(new IrEvaluateInstruction(
                    new IrProcedureCallExpression(
                        initializer,
                        ImmutableArray<IrCallArgument>.Empty,
                        TypeSymbol.Error,
                        new IrLoadExpression(new IrThisPlace(classType)))));
            }

            return new IrProcedure(
                null,
                ".ctor",
                null,
                ImmutableArray<IrParameter>.Empty,
                ImmutableArray<IrLocal>.Empty,
                ImmutableArray.Create(new IrBasicBlock(
                    0,
                    "ctor_entry",
                    instructions.ToImmutable(),
                    new IrReturnTerminator(null))),
                IsStatic: false,
                IsCompilerGenerated: true,
                DeclaringClass: classType);

            static IrExpression CreateDesignerControl(
                ClassTypeSymbol classType,
                string displayName,
                TypeSymbol controlType,
                string? parentName,
                string? designerTypeName)
            {
                var qualifiedName = parentName is null
                    ? displayName
                    : parentName + "." + displayName;
                return new IrRuntimeCallExpression(
                    IrRuntimeMethod.InteractionCreateControl,
                    ImmutableArray.Create(
                        new IrCallArgument(
                            new IrLoadExpression(new IrThisPlace(classType)),
                            IrCallArgumentKind.Value),
                        new IrCallArgument(
                            new IrConstantExpression(qualifiedName, TypeSymbol.String),
                            IrCallArgumentKind.Value),
                        new IrCallArgument(
                            new IrConstantExpression(designerTypeName ?? controlType.Name, TypeSymbol.String),
                            IrCallArgumentKind.Value)),
                    controlType);
            }

            static void AddDesignerInitializers(
                ImmutableArray<IrInstruction>.Builder instructions,
                IrPlace target,
                ImmutableArray<DesignerPropertyInitializer> initializers)
            {
                foreach (var initializer in initializers)
                {
                    var value = CreateDesignerConstant(initializer.Value);
                    if (value is null)
                    {
                        continue;
                    }

                    instructions.Add(new IrEvaluateInstruction(
                        new IrRuntimeCallExpression(
                            IrRuntimeMethod.InteractionSetMember,
                            ImmutableArray.Create(
                                new IrCallArgument(new IrLoadExpression(target)),
                                new IrCallArgument(new IrConstantExpression(initializer.Name, TypeSymbol.String)),
                                new IrCallArgument(value)),
                            TypeSymbol.Error)));
                }
            }

            static IrConstantExpression? CreateDesignerConstant(object value) => value switch
            {
                string text => new IrConstantExpression(text, TypeSymbol.String),
                bool boolean => new IrConstantExpression(boolean, TypeSymbol.Boolean),
                int integer => new IrConstantExpression(integer, TypeSymbol.Long),
                long longValue when longValue is >= int.MinValue and <= int.MaxValue =>
                    new IrConstantExpression((int)longValue, TypeSymbol.Long),
                _ => null
            };

            static IEnumerable<ImmutableArray<long>> EnumerateArrayIndices(
                ImmutableArray<IrArrayBound> bounds)
            {
                var values = new long[bounds.Length];

                IEnumerable<ImmutableArray<long>> Walk(int dimension)
                {
                    if (dimension == bounds.Length)
                    {
                        yield return values.ToImmutableArray();
                        yield break;
                    }

                    var lower = ((IrConstantExpression)bounds[dimension].Lower).Value;
                    var upper = ((IrConstantExpression)bounds[dimension].Upper).Value;
                    var first = Convert.ToInt64(lower, System.Globalization.CultureInfo.InvariantCulture);
                    var last = Convert.ToInt64(upper, System.Globalization.CultureInfo.InvariantCulture);
                    for (var index = first; index <= last; index++)
                    {
                        values[dimension] = index;
                        foreach (var result in Walk(dimension + 1))
                        {
                            yield return result;
                        }

                        if (index == long.MaxValue)
                        {
                            yield break;
                        }
                    }
                }

                if (bounds.Length > 0)
                {
                    foreach (var result in Walk(0))
                    {
                        yield return result;
                    }
                }
            }
        }

        private static IrProcedure LowerInterfaceProcedure(
            BoundProcedure procedure,
            ClassTypeSymbol containingClass)
        {
            return new IrProcedure(
                procedure.Symbol,
                $"__vb6_{Mangle(procedure.Symbol.Name)}",
                procedure.Symbol.ReturnType,
                procedure.Symbol.Parameters
                    .Select((parameter, index) => new IrParameter(
                        parameter,
                        index,
                        Mangle(parameter.Name),
                        parameter.Type,
                        parameter.PassingMode))
                    .ToImmutableArray(),
                ImmutableArray<IrLocal>.Empty,
                ImmutableArray<IrBasicBlock>.Empty,
                IsStatic: false,
                DeclaringClass: containingClass);
        }

        private static IrProcedure LowerClassFinalizer(
            ClassTypeSymbol classType,
            ProcedureSymbol terminator)
        {
            return new IrProcedure(
                null,
                "Finalize",
                null,
                ImmutableArray<IrParameter>.Empty,
                ImmutableArray<IrLocal>.Empty,
                ImmutableArray.Create(new IrBasicBlock(
                    0,
                    "finalize_entry",
                    ImmutableArray.Create<IrInstruction>(
                        new IrEvaluateInstruction(new IrProcedureCallExpression(
                            terminator,
                            ImmutableArray<IrCallArgument>.Empty,
                            TypeSymbol.Error,
                            new IrLoadExpression(new IrThisPlace(classType)))),
                        new IrBaseFinalizeInstruction()),
                    new IrReturnTerminator(null))),
                IsStatic: false,
                IsCompilerGenerated: true,
                DeclaringClass: classType);
        }

        private static long ReadConstantLong(BoundExpression expression) => expression switch
        {
            BoundLiteralExpression literal => Convert.ToInt64(literal.Value, System.Globalization.CultureInfo.InvariantCulture),
            BoundConversionExpression conversion => ReadConstantLong(conversion.Expression),
            _ => throw new NotSupportedException(
                "Class field array bounds must be compile-time constants for managed class initialization.")
        };

        private static bool NeedsModuleInitialization(BoundModuleVariable variable) =>
            !variable.IsConstant &&
            (variable.Symbol.Type == TypeSymbol.String ||
             variable.Symbol.Type is FixedLengthStringTypeSymbol ||
             variable.Symbol.Type is ArrayTypeSymbol && !variable.ArrayDimensions.IsDefaultOrEmpty);

        /// <summary>The bound value of a module-level constant, which is substituted at each read.</summary>
        public bool TryGetConstantValue(ModuleVariableSymbol symbol, out BoundExpression value)
        {
            if (_constantValues.TryGetValue(symbol, out value!))
            {
                return true;
            }

            return symbol.IsConstant && _constantValuesByName.TryGetValue(symbol.Name, out value!);
        }

        private void PredeclareTypes()
        {
            var seen = new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance);

            // A Private Type shadows a Public one of the same name, so a project can hold several
            // distinct types called Point. Each needs its own storage name - two definitions under
            // one name produce an assembly the runtime rejects as having a duplicate type.
            var namesInUse = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var type in EnumerateTypes())
            {
                if (!seen.Add(type))
                {
                    continue;
                }

                var fields = type.Members.Select(member =>
                {
                    var field = new IrField(Mangle(member.Name), member.Type);
                    _fields.Add(member, field);
                    return field;
                }).ToImmutableArray();
                _types.Add(type, new IrTypeDefinition(
                    type,
                    UniqueTypeName($"__vb6_udt_{Mangle(type.Name)}", namesInUse),
                    fields,
                    ImmutableArray<IrProcedure>.Empty));
            }
        }

        private void PredeclareClasses()
        {
            foreach (var input in _inputs)
            {
                if (input.SemanticModel.ContainingClass is not { } classType ||
                    _classVariables.ContainsKey(classType))
                {
                    continue;
                }

                _classVariables[classType] = input.SemanticModel.InstanceVariables;
                _classDesignerInitializers[classType] = input.SemanticModel.DesignerInitializers;
                var procedures = new Dictionary<string, ProcedureSymbol>(StringComparer.OrdinalIgnoreCase);
                foreach (var procedure in input.SemanticModel.Procedures)
                {
                    procedures[ProcedureKey(procedure.Symbol.Name, procedure.Symbol.PropertyAccessor)] =
                        procedure.Symbol;
                }

                _classProcedures[classType] = procedures;
                foreach (var variable in input.SemanticModel.InstanceVariables)
                {
                    _classFields[variable.Symbol] = new IrField(
                        Mangle(variable.Symbol.Name),
                        variable.Symbol.Type,
                        IsPublic: variable.Symbol.IsPublic);

                    if (variable.IsWithEvents && variable.Symbol.Type is ClassTypeSymbol sourceType)
                    {
                        var handlers = ImmutableArray.CreateBuilder<(EventSymbol, ProcedureSymbol)>();
                        foreach (var @event in sourceType.Events)
                        {
                            var handlerName = variable.Symbol.Name + "_" + @event.Name;
                            if (procedures.TryGetValue(ProcedureKey(handlerName, null), out var handler))
                            {
                                handlers.Add((@event, handler));
                            }
                        }

                        _withEventsHandlers[variable.Symbol] = handlers.ToImmutable();
                    }
                }
            }
        }

        private static string UniqueTypeName(string name, Dictionary<string, int> namesInUse)
        {
            if (namesInUse.TryGetValue(name, out var used))
            {
                namesInUse[name] = used + 1;
                return $"{name}_{used + 1}";
            }

            namesInUse.Add(name, 1);
            return name;
        }

        private IEnumerable<UserDefinedTypeSymbol> EnumerateTypes()
        {
            var pending = new Stack<TypeSymbol>();
            foreach (var model in _inputs.Select(input => input.SemanticModel))
            {
                foreach (var global in model.ModuleVariables)
                {
                    pending.Push(global.Symbol.Type);
                }

                foreach (var instance in model.InstanceVariables)
                {
                    pending.Push(instance.Symbol.Type);
                }

                foreach (var procedure in model.Procedures)
                {
                    if (procedure.Symbol.ReturnType is not null)
                    {
                        pending.Push(procedure.Symbol.ReturnType);
                    }

                    foreach (var parameter in procedure.Symbol.Parameters)
                    {
                        pending.Push(parameter.Type);
                    }

                    foreach (var local in procedure.Locals)
                    {
                        pending.Push(local.Type);
                    }
                }

                foreach (var external in model.ExternalProcedures)
                {
                    if (external.ReturnType is not null)
                    {
                        pending.Push(external.ReturnType);
                    }

                    foreach (var parameter in external.Parameters)
                    {
                        pending.Push(parameter.Type);
                    }
                }
            }

            foreach (var staticVariable in _additionalGlobals)
            {
                pending.Push(staticVariable.Symbol.Type);
            }

            var emitted = new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance);
            while (pending.Count > 0)
            {
                var type = pending.Pop();
                if (type is ArrayTypeSymbol array)
                {
                    pending.Push(array.ElementType);
                    continue;
                }

                if (type is not UserDefinedTypeSymbol udt || !emitted.Add(udt))
                {
                    continue;
                }

                yield return udt;
                foreach (var member in udt.Members)
                {
                    pending.Push(member.Type);
                }
            }
        }
    }

    private sealed class ProcedureLowerer
    {
        private readonly ProgramLoweringState _program;
        private readonly BoundProcedure _procedure;
        private readonly ClassTypeSymbol? _containingClass;
        private readonly ImmutableArray<BoundModuleVariable> _moduleInitializers;
        private readonly Dictionary<LocalVariableSymbol, IrLocal> _locals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ParameterSymbol, IrParameter> _parameters =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<int, IrPlace> _withPlaces = new();
        private readonly Dictionary<int, int> _loopExits = new();
        private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<BoundGoSubStatement, (int TargetBlockId, int ReturnIndex)> _goSubs =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<BoundOnGoSubStatement, (ImmutableArray<int> TargetBlockIds, int ReturnIndex)> _onGoSubs =
            new(ReferenceEqualityComparer.Instance);
        private readonly List<int> _goSubReturnBlocks = new();
        private readonly List<BlockBuilder> _blocks = new();
        private readonly List<IrLocal> _allLocals = new();
        private BlockBuilder _current = null!;
        private IrLocal? _returnLocal;
        private int _nextLocalId;
        private bool _resumeNext;
        private string? _errorHandler;

        /// <summary>Where the statement being lowered was written; stamped onto its instructions.</summary>
        private IrSourceLocation? _location;

        public ProcedureLowerer(
            ProgramLoweringState program,
            BoundProcedure procedure,
            ImmutableArray<BoundModuleVariable> moduleInitializers = default,
            ClassTypeSymbol? containingClass = null)
        {
            _program = program;
            _procedure = procedure;
            _containingClass = containingClass;
            _moduleInitializers = moduleInitializers.IsDefault
                ? ImmutableArray<BoundModuleVariable>.Empty
                : moduleInitializers;
        }

        private bool IsModuleInitializer => !_moduleInitializers.IsDefaultOrEmpty;

        public IrProcedure Lower()
        {
            for (var index = 0; index < _procedure.Symbol.Parameters.Length; index++)
            {
                var symbol = _procedure.Symbol.Parameters[index];
                _parameters.Add(symbol, new IrParameter(
                    symbol,
                    index,
                    Mangle(symbol.Name),
                    symbol.Type,
                    symbol.PassingMode));
            }

            foreach (var symbol in _procedure.Locals)
            {
                var local = NewLocal(Mangle(symbol.Name), symbol.Type);
                _locals.Add(symbol, local);
            }

            if (_procedure.Symbol.ReturnType is not null)
            {
                _returnLocal = NewLocal("__return", _procedure.Symbol.ReturnType, compilerGenerated: true);
            }

            PredeclareLabels(_procedure.Body);
            PredeclareGoSubs(_procedure.Body);
            _current = NewBlock("entry");
            if (IsModuleInitializer)
            {
                EmitModuleInitializers();
            }
            else
            {
                EmitProcedurePrologue();
            }
            LowerBlock(_procedure.Body);
            if (!_current.HasTerminator)
            {
                Terminate(ReturnTerminator());
            }

            foreach (var block in _blocks.Where(block => !block.HasTerminator))
            {
                block.Terminator = ReturnTerminator();
            }

            return new IrProcedure(
                IsModuleInitializer ? null : _procedure.Symbol,
                IsModuleInitializer
                    ? ".cctor"
                    : string.Equals(_procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase) &&
                        !_procedure.Symbol.IsFunction
                        ? "Main"
                        : $"__vb6_{Mangle(_procedure.Symbol.Name)}",
                _procedure.Symbol.ReturnType,
                _parameters.Values.OrderBy(parameter => parameter.Index).ToImmutableArray(),
                _allLocals.ToImmutableArray(),
                _blocks.Select(block => block.Build()).ToImmutableArray(),
                IsStatic: _containingClass is null,
                IsCompilerGenerated: IsModuleInitializer,
                DeclaringClass: _containingClass);
        }

        private void PredeclareLabels(BoundBlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                switch (statement)
                {
                    case BoundLabelStatement label:
                        if (!_labels.ContainsKey(label.Name))
                        {
                            _labels.Add(label.Name, NewBlock($"label_{Mangle(label.Name)}").Id);
                        }
                        break;
                    case BoundIfStatement @if:
                        PredeclareLabels(@if.Body);
                        foreach (var clause in @if.ElseIfClauses) PredeclareLabels(clause.Body);
                        if (@if.ElseBody is not null) PredeclareLabels(@if.ElseBody);
                        break;
                    case BoundForStatement @for:
                        PredeclareLabels(@for.Body);
                        break;
                    case BoundForEachStatement forEach:
                        PredeclareLabels(forEach.Body);
                        break;
                    case BoundWhileStatement @while:
                        PredeclareLabels(@while.Body);
                        break;
                    case BoundDoStatement @do:
                        PredeclareLabels(@do.Body);
                        break;
                    case BoundWithStatement with:
                        PredeclareLabels(with.Body);
                        break;
                    case BoundSelectCaseStatement select:
                        foreach (var @case in select.Cases) PredeclareLabels(@case.Body);
                        break;
                }
            }
        }

        private void PredeclareGoSubs(BoundBlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                switch (statement)
                {
                    case BoundGoSubStatement goSub:
                        if (!_goSubs.ContainsKey(goSub))
                        {
                            if (!_labels.TryGetValue(goSub.Name, out var target))
                            {
                                throw new InvalidOperationException($"Label '{goSub.Name}' was not predeclared.");
                            }

                            var returnIndex = _goSubReturnBlocks.Count;
                            var continuation = NewBlock($"gosub_return_{returnIndex}").Id;
                            _goSubReturnBlocks.Add(continuation);
                            _goSubs.Add(goSub, (target, returnIndex));
                        }
                        break;
                    case BoundOnGoSubStatement onGoSub:
                        if (!_onGoSubs.ContainsKey(onGoSub))
                        {
                            var targets = onGoSub.Labels
                                .Select(label => _labels.TryGetValue(label, out var target)
                                    ? target
                                    : throw new InvalidOperationException($"Label '{label}' was not predeclared."))
                                .ToImmutableArray();
                            var returnIndex = _goSubReturnBlocks.Count;
                            var continuation = NewBlock($"on_gosub_return_{returnIndex}").Id;
                            _goSubReturnBlocks.Add(continuation);
                            _onGoSubs.Add(onGoSub, (targets, returnIndex));
                        }
                        break;
                    case BoundIfStatement @if:
                        PredeclareGoSubs(@if.Body);
                        foreach (var clause in @if.ElseIfClauses) PredeclareGoSubs(clause.Body);
                        if (@if.ElseBody is not null) PredeclareGoSubs(@if.ElseBody);
                        break;
                    case BoundForStatement @for:
                        PredeclareGoSubs(@for.Body);
                        break;
                    case BoundForEachStatement forEach:
                        PredeclareGoSubs(forEach.Body);
                        break;
                    case BoundWhileStatement @while:
                        PredeclareGoSubs(@while.Body);
                        break;
                    case BoundDoStatement @do:
                        PredeclareGoSubs(@do.Body);
                        break;
                    case BoundWithStatement with:
                        PredeclareGoSubs(with.Body);
                        break;
                    case BoundSelectCaseStatement select:
                        foreach (var @case in select.Cases) PredeclareGoSubs(@case.Body);
                        break;
                }
            }
        }

        private void LowerBlock(BoundBlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                LowerStatement(statement);
            }
        }

        /// <summary>
        /// Lowers one statement, stamping every instruction it produces with the statement's
        /// source position. One statement becomes many instructions - and nested statements bring
        /// their own position - so the previous value is restored on the way out.
        /// </summary>
        private void LowerStatement(BoundStatement statement)
        {
            var enclosing = _location;
            _location = ToIrLocation(statement.SourceLocation) ?? enclosing;
            var protect = (_resumeNext || _errorHandler is not null) && CanProtectForErrorHandling(statement);
            var startBlock = _current;
            var startIndex = _current.Instructions.Count;
            try
            {
                LowerStatementCore(statement);

                // A protected region may not cross a basic block - the emitted try region would
                // span branches and the method fails CLR verification. Whether a statement stays
                // inside one block is only known after lowering it: a Put or Get of an array
                // expands into an element loop. So the region is inserted afterwards, and only
                // when the statement really stayed put.
                if (protect &&
                    ReferenceEquals(_current, startBlock) &&
                    !_current.HasTerminator)
                {
                    startBlock.Instructions.Insert(
                        startIndex,
                        new IrErrorBoundaryStartInstruction(
                            _errorHandler is null ? null : _labels[_errorHandler]));
                    Emit(new IrErrorBoundaryEndInstruction());
                }
            }
            finally
            {
                _location = enclosing;
            }
        }

        private static IrSourceLocation? ToIrLocation(SourceLocation? location) =>
            location?.FilePath is null
                ? null
                : new IrSourceLocation(location.FilePath, location.Span, location.Lines);

        private void LowerStatementCore(BoundStatement statement)
        {
            switch (statement)
            {
                case BoundVariableDeclarationStatement:
                    // Locals have procedure lifetime in VB6. Storage that differs from the CLR
                    // zero value is initialized once in the entry prologue, not at the Dim site.
                    break;
                case BoundAssignmentStatement assignment:
                {
                    // Ein String * n behaelt seine Breite auch als einfache Variable: der
                    // gespeicherte Wert wird abgeschnitten oder mit Leerzeichen aufgefuellt.
                    // Array-Elemente und Member liefen schon darueber, Locals und
                    // Modulvariablen bisher nicht.
                    var variablePlace = LowerVariablePlace(assignment.Variable);
                    Emit(new IrStoreInstruction(
                        variablePlace,
                        LowerFixedStringWrite(variablePlace.Type, LowerAssignedValueCopy(assignment.Expression, assignment.IsSetAssignment))));
                    LowerWithEventsSubscriptions(assignment.Variable);
                    break;
                }
                case BoundMidAssignmentStatement midAssignment:
                    LowerMidAssignment(midAssignment);
                    break;
                case BoundArrayElementAssignmentStatement assignment:
                    var arrayType = (ArrayTypeSymbol)assignment.Array.Type;
                    Emit(new IrStoreInstruction(
                        new IrArrayElementPlace(
                            new IrLoadExpression(LowerVariablePlace(assignment.Array)),
                            assignment.Indices.Select(LowerExpression).ToImmutableArray(),
                            arrayType.ElementType),
                        LowerFixedStringWrite(arrayType.ElementType, LowerAssignedValueCopy(assignment.Expression))));
                    break;
                case BoundMemberAssignmentStatement assignment:
                {
                    if (assignment.Target is BoundVariantArrayAccessExpression variantArray)
                    {
                        Emit(new IrVariantArraySetInstruction(
                            LowerExpression(variantArray.Receiver),
                            variantArray.Indices.Select(LowerExpression).ToImmutableArray(),
                            LowerAssignedValueCopy(assignment.Expression)));
                        break;
                    }

                    if (assignment.Target is BoundPropertyAccessExpression screenProperty &&
                        IsScreenObject(screenProperty.Receiver))
                    {
                        Emit(new IrEvaluateInstruction(LowerScreenPropertySet(
                            screenProperty.Property.Name,
                            LowerValueCopy(assignment.Expression))));
                        break;
                    }

                    if (assignment.Target is BoundPropertyAccessExpression printerProperty &&
                        IsPrinterObject(printerProperty.Receiver))
                    {
                        Emit(new IrEvaluateInstruction(LowerPrinterPropertySet(
                            printerProperty.Property,
                            LowerValueCopy(assignment.Expression))));
                        break;
                    }

                    if (assignment.Target is BoundPropertyAccessExpression dynamicProperty &&
                        (dynamicProperty.Property.IsLateBound || IsRuntimeObject(dynamicProperty.Receiver)))
                    {
                        Emit(new IrEvaluateInstruction(LowerDynamicSet(
                            dynamicProperty.Receiver,
                            dynamicProperty.Property.Name,
                            ImmutableArray<BoundArgument>.Empty,
                            LowerValueCopy(assignment.Expression))));
                        break;
                    }

                    if (assignment.Target is BoundPropertyInvocationExpression indexedDynamicProperty &&
                        (indexedDynamicProperty.Property.IsLateBound ||
                         IsRuntimeObject(indexedDynamicProperty.Receiver)))
                    {
                        Emit(new IrEvaluateInstruction(LowerDynamicSet(
                            indexedDynamicProperty.Receiver,
                            indexedDynamicProperty.Property.Name,
                            indexedDynamicProperty.Arguments,
                            LowerValueCopy(assignment.Expression))));
                        break;
                    }

                    var memberTarget = LowerPlace(assignment.Target);
                    Emit(new IrStoreInstruction(
                        memberTarget,
                        LowerFixedStringWrite(memberTarget.Type, LowerAssignedValueCopy(assignment.Expression, assignment.IsSetAssignment))));
                    break;
                }
                case BoundReDimStatement reDim:
                    LowerReDim(reDim);
                    break;
                case BoundControlArrayElementStatement controlArrayElement:
                    LowerControlArrayElement(controlArrayElement);
                    break;
                case BoundEraseStatement erase:
                    LowerErase(erase);
                    break;
                case BoundIfStatement @if:
                    LowerIf(@if);
                    break;
                case BoundForStatement @for:
                    LowerFor(@for);
                    break;
                case BoundForEachStatement forEach:
                    LowerForEach(forEach);
                    break;
                case BoundWhileStatement @while:
                    LowerWhile(@while);
                    break;
                case BoundDoStatement @do:
                    LowerDo(@do);
                    break;
                case BoundWithStatement with:
                    LowerWith(with);
                    break;
                case BoundExitLoopStatement exit:
                    LowerExit(exit);
                    break;
                case BoundReturnStatement:
                    Terminate(ReturnTerminator(clearsActiveErrorHandler: true));
                    _current = NewBlock("after_return");
                    break;
                case BoundEndStatement:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.EndProgram,
                        TypeSymbol.Error)));
                    break;
                case BoundOnErrorStatement onError:
                    _resumeNext = onError.Mode == BoundErrorHandlingMode.ResumeNext;
                    _errorHandler = onError.Mode == BoundErrorHandlingMode.GoToLabel
                        ? onError.HandlerLabel
                        : null;
                    break;
                case BoundResumeStatement resume:
                    // Only Resume <label> may run inside a protected region. A bare Resume and
                    // Resume Next leave the procedure through the resume dispatch switch, and a
                    // switch out of a protected region is not a valid leave - the emitted method
                    // fails verification with InvalidProgramException instead of running.
                    var protectResume = _resumeNext && resume.TargetLabel is not null;
                    if (protectResume)
                    {
                        Emit(new IrErrorBoundaryStartInstruction(
                            _errorHandler is null ? null : _labels[_errorHandler]));
                    }

                    if (resume.TargetLabel is not null)
                    {
                        if (!_labels.TryGetValue(resume.TargetLabel, out var resumeTarget))
                        {
                            throw new InvalidOperationException($"Label '{resume.TargetLabel}' was not predeclared.");
                        }

                        Emit(new IrResumeInstruction(IrResumeKind.Label));
                        var afterResume = NewBlock("after_resume");
                        if (protectResume)
                        {
                            Emit(new IrErrorBoundaryEndInstruction(afterResume.Id));
                        }

                        Terminate(new IrGotoTerminator(resumeTarget));
                        _current = afterResume;
                    }
                    else
                    {
                        Emit(new IrResumeInstruction(
                            resume.IsNext ? IrResumeKind.Next : IrResumeKind.Same));
                        if (protectResume)
                        {
                            Emit(new IrErrorBoundaryEndInstruction());
                        }
                    }
                    break;
                case BoundGoSubStatement goSub:
                    if (!_goSubs.TryGetValue(goSub, out var site))
                    {
                        throw new InvalidOperationException($"GoSub '{goSub.Name}' was not predeclared.");
                    }

                    Terminate(new IrGoSubTerminator(site.TargetBlockId, site.ReturnIndex));
                    _current = _blocks[_goSubReturnBlocks[site.ReturnIndex]];
                    break;
                case BoundGoSubReturnStatement:
                    Terminate(new IrGoSubReturnTerminator(_goSubReturnBlocks.ToImmutableArray()));
                    _current = NewBlock("after_gosub_return");
                    break;
                case BoundOnGoSubStatement onGoSub:
                    if (!_onGoSubs.TryGetValue(onGoSub, out var onGoSubSite))
                    {
                        throw new InvalidOperationException("On GoSub was not predeclared.");
                    }

                    var onGoSubContinuation = _blocks[_goSubReturnBlocks[onGoSubSite.ReturnIndex]];
                    Terminate(new IrOnGoSubTerminator(
                        LowerExpression(onGoSub.Expression),
                        onGoSubSite.TargetBlockIds,
                        onGoSubSite.ReturnIndex,
                        onGoSubContinuation.Id));
                    _current = onGoSubContinuation;
                    break;
                case BoundOnGoToStatement onGoTo:
                    var onGoToContinuation = NewBlock("after_on_goto");
                    var onGoToTargets = onGoTo.Labels
                        .Select(label => _labels.TryGetValue(label, out var target)
                            ? target
                            : throw new InvalidOperationException($"Label '{label}' was not predeclared."))
                        .ToImmutableArray();
                    Terminate(new IrOnGoToTerminator(
                        LowerExpression(onGoTo.Expression),
                        onGoToTargets,
                        onGoToContinuation.Id));
                    _current = onGoToContinuation;
                    break;
                case BoundSelectCaseStatement select:
                    LowerSelect(select);
                    break;
                case BoundDebugPrintStatement print:
                    var debugExpressions = print.Expressions.IsDefaultOrEmpty
                        ? print.Expression is null
                            ? Array.Empty<BoundExpression>()
                            : new[] { print.Expression }
                        : print.Expressions.ToArray();
                    if (debugExpressions.Length == 0)
                    {
                        // A bare Debug.Print ends the current line; a lone trailing separator
                        // leaves it open and produces nothing at all.
                        if (print.Separators.IsDefaultOrEmpty)
                        {
                            Emit(new IrEvaluateInstruction(Runtime(
                                IrRuntimeMethod.DebugPrintEmptyLine,
                                TypeSymbol.Error)));
                        }

                        break;
                    }

                    if (debugExpressions.Length == 1 && print.Separators.IsDefaultOrEmpty)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.DebugPrint,
                            TypeSymbol.Error,
                            LowerPrintItem(debugExpressions[0]))));
                        break;
                    }

                    for (var index = 0; index < debugExpressions.Length; index++)
                    {
                        var debugSeparator = index == 0
                            ? 0L
                            : (long)print.Separators[index - 1] + 1L;
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.DebugPrintValue,
                            TypeSymbol.Error,
                            LowerPrintItem(debugExpressions[index]),
                            new IrConstantExpression(index >= print.Separators.Length, TypeSymbol.Boolean),
                            new IrConstantExpression(debugSeparator, TypeSymbol.Long))));
                    }
                    break;
                case BoundErrorStatement errorStatement:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.ErrorsRaiseNumber,
                        TypeSymbol.Error,
                        LowerExpression(errorStatement.Number))));
                    break;
                case BoundDebugAssertStatement:
                    // VB6 removes Debug.Assert calls from compiled executables. Keeping the
                    // bound expression for diagnostics while emitting no IR preserves that
                    // release behavior and avoids evaluating assertion side effects.
                    break;
                case BoundGraphicsCircleStatement circle:
                    IrExpression Optional(BoundExpression? value) => value is null
                        ? new IrNullExpression(TypeSymbol.Variant)
                        : LowerExpression(value);
                    var circleArguments = new List<IrExpression>();
                    if (circle.Target is not null)
                    {
                        circleArguments.Add(LowerExpression(circle.Target));
                    }

                    circleArguments.Add(LowerExpression(circle.CenterX));
                    circleArguments.Add(LowerExpression(circle.CenterY));
                    circleArguments.Add(LowerExpression(circle.Radius));
                    circleArguments.Add(Optional(circle.Color));
                    circleArguments.Add(Optional(circle.Start));
                    circleArguments.Add(Optional(circle.End));
                    circleArguments.Add(Optional(circle.Aspect));
                    circleArguments.Add(new IrConstantExpression(circle.IsStep, TypeSymbol.Boolean));
                    Emit(new IrEvaluateInstruction(Runtime(
                        circle.Target is null
                            ? IrRuntimeMethod.GraphicsCircle
                            : IrRuntimeMethod.GraphicsCircleOnTarget,
                        TypeSymbol.Error,
                        circleArguments.ToArray())));
                    break;
                case BoundGraphicsPSetStatement pset:
                    var psetColor = pset.Color is null
                        ? new IrNullExpression(TypeSymbol.Variant)
                        : LowerExpression(pset.Color);
                    var psetStep = new IrConstantExpression(pset.IsStep, TypeSymbol.Boolean);
                    Emit(new IrEvaluateInstruction(pset.Target is null
                        ? Runtime(
                            IrRuntimeMethod.GraphicsPSet,
                            TypeSymbol.Error,
                            LowerExpression(pset.X),
                            LowerExpression(pset.Y),
                            psetColor,
                            psetStep)
                        : Runtime(
                            IrRuntimeMethod.GraphicsPSetOnTarget,
                            TypeSymbol.Error,
                            LowerExpression(pset.Target),
                            LowerExpression(pset.X),
                            LowerExpression(pset.Y),
                            psetColor,
                            psetStep)));
                    break;
                case BoundGraphicsLineStatement line:
                    var graphicsLineMethod = line.Target is null
                        ? IrRuntimeMethod.GraphicsLine
                        : IrRuntimeMethod.GraphicsLineOnTarget;
                    var graphicsLineArguments = line.Target is null
                        ? new[]
                        {
                            LowerExpression(line.StartX),
                            LowerExpression(line.StartY),
                            LowerExpression(line.EndX),
                            LowerExpression(line.EndY),
                            line.Color is null
                                ? new IrNullExpression(TypeSymbol.Variant)
                                : LowerExpression(line.Color),
                            new IrConstantExpression(line.IsStep, TypeSymbol.Boolean),
                            new IrConstantExpression(line.DrawBox, TypeSymbol.Boolean),
                            new IrConstantExpression(line.Fill, TypeSymbol.Boolean)
                        }
                        : new[]
                        {
                            LowerExpression(line.Target),
                            LowerExpression(line.StartX),
                            LowerExpression(line.StartY),
                            LowerExpression(line.EndX),
                            LowerExpression(line.EndY),
                            line.Color is null
                                ? new IrNullExpression(TypeSymbol.Variant)
                                : LowerExpression(line.Color),
                            new IrConstantExpression(line.IsStep, TypeSymbol.Boolean),
                            new IrConstantExpression(line.DrawBox, TypeSymbol.Boolean),
                            new IrConstantExpression(line.Fill, TypeSymbol.Boolean)
                        };
                    Emit(new IrEvaluateInstruction(Runtime(
                        graphicsLineMethod,
                        TypeSymbol.Error,
                        graphicsLineArguments)));
                    break;
                case BoundFilePrintStatement print:
                    var printExpressions = print.Expressions.IsDefaultOrEmpty
                        ? print.Expression is null
                            ? Array.Empty<BoundExpression>()
                            : new[] { print.Expression }
                        : print.Expressions.ToArray();
                    if (printExpressions.Length == 0)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FilePrint,
                            TypeSymbol.Error,
                            LowerExpression(print.FileNumber),
                            new IrConstantExpression(null, TypeSymbol.Variant))));
                        break;
                    }

                    if (printExpressions.Length == 1 && print.Separators.IsDefaultOrEmpty)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FilePrint,
                            TypeSymbol.Error,
                            LowerExpression(print.FileNumber),
                            LowerPrintItem(printExpressions[0]))));
                        break;
                    }

                    for (var index = 0; index < printExpressions.Length; index++)
                    {
                        var separator = index == 0
                            ? 0L
                            : (long)print.Separators[index - 1] + 1L;
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FilePrintValue,
                            TypeSymbol.Error,
                            LowerExpression(print.FileNumber),
                            LowerPrintItem(printExpressions[index]),
                            new IrConstantExpression(index >= print.Separators.Length, TypeSymbol.Boolean),
                            new IrConstantExpression(separator, TypeSymbol.Long))));
                    }
                    break;
                case BoundFileWriteStatement write:
                    for (var index = 0; index < write.Expressions.Length; index++)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FileWrite,
                            TypeSymbol.Error,
                            LowerExpression(write.FileNumber),
                            LowerExpression(write.Expressions[index]),
                            new IrConstantExpression(index == write.Expressions.Length - 1, TypeSymbol.Boolean))));
                    }
                    break;
                case BoundWidthStatement width:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileWidth,
                        TypeSymbol.Error,
                        LowerExpression(width.FileNumber),
                        LowerExpression(width.Width))));
                    break;
                case BoundFileLockStatement fileLock:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileLock,
                        TypeSymbol.Error,
                        LowerExpression(fileLock.FileNumber),
                        fileLock.Start is null
                            ? new IrConstantExpression(0L, TypeSymbol.LongLong)
                            : LowerExpression(fileLock.Start),
                        fileLock.End is null
                            ? new IrConstantExpression(0L, TypeSymbol.LongLong)
                            : LowerExpression(fileLock.End))));
                    break;
                case BoundFileUnlockStatement fileUnlock:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileUnlock,
                        TypeSymbol.Error,
                        LowerExpression(fileUnlock.FileNumber),
                        fileUnlock.Start is null
                            ? new IrConstantExpression(0L, TypeSymbol.LongLong)
                            : LowerExpression(fileUnlock.Start),
                        fileUnlock.End is null
                            ? new IrConstantExpression(0L, TypeSymbol.LongLong)
                            : LowerExpression(fileUnlock.End))));
                    break;
                case BoundInvocationStatement invocation when invocation.Procedure.IntrinsicKind == VBIntrinsicKind.LSet:
                    LowerLSet(invocation);
                    break;
                case BoundInvocationStatement invocation when invocation.Procedure.IntrinsicKind == VBIntrinsicKind.RSet:
                    LowerRSet(invocation);
                    break;
                case BoundInvocationStatement invocation:
                    Emit(new IrEvaluateInstruction(LowerCall(invocation.Procedure, invocation.Arguments)));
                    break;
                case BoundMemberInvocationStatement memberInvocation:
                    Emit(new IrEvaluateInstruction(LowerMemberCall(
                        memberInvocation.Receiver,
                        memberInvocation.Procedure,
                        memberInvocation.Arguments)));
                    break;
                case BoundRaiseEventStatement raiseEvent:
                    Emit(new IrRaiseEventInstruction(
                        raiseEvent.Event,
                        raiseEvent.Arguments.Select(argument => LowerValueCopy(argument.Expression)).ToImmutableArray(),
                        _containingClass));
                    break;
                case BoundLabelStatement label:
                    LowerLabel(label);
                    break;
                case BoundGoToStatement goTo:
                    if (!_labels.TryGetValue(goTo.Name, out var target))
                    {
                        throw new InvalidOperationException($"Label '{goTo.Name}' was not predeclared.");
                    }
                    Terminate(new IrGotoTerminator(target));
                    _current = NewBlock("after_goto");
                    break;
                case BoundOpenStatement open:
                    var openMethod = open.Mode switch
                    {
                        BoundFileOpenMode.Binary => IrRuntimeMethod.FileOpenBinary,
                        BoundFileOpenMode.Input => IrRuntimeMethod.FileOpenInput,
                        BoundFileOpenMode.Output => IrRuntimeMethod.FileOpenOutput,
                        BoundFileOpenMode.Append => IrRuntimeMethod.FileOpenAppend,
                        BoundFileOpenMode.Random => IrRuntimeMethod.FileOpenRandom,
                        _ => throw new InvalidOperationException($"Unknown file open mode '{open.Mode}'.")
                    };
                    var openArguments = open.Mode == BoundFileOpenMode.Random
                        ? new[]
                        {
                            LowerExpression(open.FileNumber),
                            LowerExpression(open.Path),
                            LowerExpression(open.RecordLength!),
                            new IrConstantExpression((long)open.Access, TypeSymbol.Long),
                            new IrConstantExpression((long)open.Sharing, TypeSymbol.Long)
                        }
                        : new[]
                        {
                            LowerExpression(open.FileNumber),
                            LowerExpression(open.Path),
                            new IrConstantExpression((long)open.Access, TypeSymbol.Long),
                            new IrConstantExpression((long)open.Sharing, TypeSymbol.Long)
                        };
                    Emit(new IrEvaluateInstruction(Runtime(
                        openMethod,
                        TypeSymbol.Error,
                        openArguments)));
                    break;
                case BoundNameStatement name:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileRename,
                        TypeSymbol.Error,
                        LowerExpression(name.OldPath),
                        LowerExpression(name.NewPath))));
                    break;
                case BoundCloseStatement close:
                    if (close.FileNumbers.IsDefaultOrEmpty)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(IrRuntimeMethod.FileCloseAll, TypeSymbol.Error)));
                    }
                    else
                    {
                        foreach (var fileNumber in close.FileNumbers)
                        {
                            Emit(new IrEvaluateInstruction(Runtime(
                                IrRuntimeMethod.FileClose,
                                TypeSymbol.Error,
                                LowerExpression(fileNumber))));
                        }
                    }
                    break;
                case BoundSeekStatement seek:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileSeek,
                        TypeSymbol.Error,
                        LowerExpression(seek.FileNumber),
                        LowerExpression(seek.Position))));
                    break;
                case BoundGetStatement get:
                    if (get.Target.Type is ArrayTypeSymbol getArrayType &&
                        UserDefinedTypeFileLayout.IsBinaryTransferableElement(getArrayType.ElementType))
                    {
                        LowerBinaryArrayGet(get, getArrayType);
                    }
                    else if (get.Target.Type is UserDefinedTypeSymbol getType)
                    {
                        LowerBinaryRecordGet(get, getType);
                    }
                    else if (get.Target.Type == TypeSymbol.Variant)
                    {
                        Emit(new IrStoreInstruction(
                            LowerPlace(get.Target),
                            Runtime(
                                IrRuntimeMethod.FileGetVariant,
                                TypeSymbol.Variant,
                                LowerExpression(get.FileNumber),
                                get.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(get.Position))));
                    }
                    else
                    {
                        Emit(new IrStoreInstruction(
                            LowerPlace(get.Target),
                            Runtime(
                                FileGetMethod(get.Target.Type),
                                get.Target.Type,
                                LowerExpression(get.FileNumber),
                                get.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(get.Position))));
                    }
                    break;
                case BoundPutStatement put:
                    if (put.Value.Type is ArrayTypeSymbol putArrayType &&
                        UserDefinedTypeFileLayout.IsBinaryTransferableElement(putArrayType.ElementType))
                    {
                        LowerBinaryArrayPut(put, putArrayType);
                    }
                    else if (put.Value.Type is UserDefinedTypeSymbol putType)
                    {
                        LowerBinaryRecordPut(put, putType);
                    }
                    else if (put.Value.Type == TypeSymbol.Variant)
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FilePutVariant,
                            TypeSymbol.Error,
                            LowerExpression(put.FileNumber),
                            put.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(put.Position),
                            LowerExpression(put.Value))));
                    }
                    else
                    {
                        Emit(new IrEvaluateInstruction(Runtime(
                            IrRuntimeMethod.FilePut,
                            TypeSymbol.Error,
                            LowerExpression(put.FileNumber),
                            put.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(put.Position),
                            LowerExpression(put.Value))));
                    }
                    break;
                case BoundLineInputStatement lineInput:
                    Emit(new IrStoreInstruction(
                        LowerPlace(lineInput.Target),
                        Runtime(
                            IrRuntimeMethod.FileLineInput,
                            TypeSymbol.String,
                            LowerExpression(lineInput.FileNumber))));
                    break;
                case BoundFileInputStatement fileInput:
                    var fileInputNumber = MaterializeFileArgument(
                        fileInput.FileNumber,
                        TypeSymbol.Long,
                        "__file_input_number");
                    foreach (var inputTarget in fileInput.Targets)
                    {
                        var targetPlace = LowerPlace(inputTarget);
                        var inputValueMethod = targetPlace.Type == TypeSymbol.Variant
                            ? IrRuntimeMethod.FileInputValue
                            : IrRuntimeMethod.FileInputField;
                        var inputValueType = targetPlace.Type == TypeSymbol.Variant
                            ? TypeSymbol.Variant
                            : TypeSymbol.String;
                        Emit(new IrStoreInstruction(
                            targetPlace,
                            LowerFileInputValue(
                                Runtime(
                                    inputValueMethod,
                                    inputValueType,
                                    fileInputNumber),
                                targetPlace.Type)));
                    }
                    break;
            }
        }

        private void LowerBinaryArrayGet(BoundGetStatement get, ArrayTypeSymbol arrayType)
        {
            EnsureBinaryArrayElementLayout(arrayType.ElementType);
            var target = NewLocal("__file_get_array", arrayType, compilerGenerated: true);
            var destination = arrayType.HasKnownRank ? null : LowerPlace(get.Target);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(target),
                LowerExpression(get.Target)));
            var fileNumber = MaterializeFileArgument(get.FileNumber, TypeSymbol.Long, "__file_get_array_number");
            var position = get.Position is null
                ? new IrNullExpression(TypeSymbol.LongLong)
                : MaterializeFileArgument(get.Position, TypeSymbol.LongLong, "__file_get_array_position");
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordStart,
                TypeSymbol.Error,
                fileNumber,
                position)));
            if (!arrayType.HasKnownRank)
            {
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(target),
                    Runtime(
                        IrRuntimeMethod.FileGetDynamicArrayIfRandom,
                        arrayType,
                        fileNumber,
                        new IrLoadExpression(new IrLocalPlace(target)))));
                Emit(new IrStoreInstruction(
                    destination!,
                    new IrLoadExpression(new IrLocalPlace(target))));
            }
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.ArrayRequireAllocated,
                TypeSymbol.Variant,
                new IrLoadExpression(new IrLocalPlace(target)))));
            EmitDynamicArrayRecordElements(target, arrayType, fileNumber, forWrite: false);
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordEnd,
                TypeSymbol.Error,
                fileNumber,
                new IrConstantExpression(false, TypeSymbol.Boolean))));
        }

        private void LowerBinaryArrayPut(BoundPutStatement put, ArrayTypeSymbol arrayType)
        {
            EnsureBinaryArrayElementLayout(arrayType.ElementType);
            var source = NewLocal("__file_put_array", arrayType, compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(source),
                LowerValueCopy(put.Value)));
            var fileNumber = MaterializeFileArgument(put.FileNumber, TypeSymbol.Long, "__file_put_array_number");
            var position = put.Position is null
                ? new IrNullExpression(TypeSymbol.LongLong)
                : MaterializeFileArgument(put.Position, TypeSymbol.LongLong, "__file_put_array_position");
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordStart,
                TypeSymbol.Error,
                fileNumber,
                position)));
            if (!arrayType.HasKnownRank)
            {
                Emit(new IrEvaluateInstruction(Runtime(
                    IrRuntimeMethod.FilePutDynamicArrayDescriptorIfRandom,
                    TypeSymbol.Error,
                    fileNumber,
                    new IrLoadExpression(new IrLocalPlace(source)))));
            }
            EmitDynamicArrayRecordElements(source, arrayType, fileNumber, forWrite: true);
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordEnd,
                TypeSymbol.Error,
                fileNumber,
                new IrConstantExpression(true, TypeSymbol.Boolean))));
        }

        private void LowerBinaryRecordGet(BoundGetStatement get, UserDefinedTypeSymbol type)
        {
            EnsureBinaryRecordLayout(type);
            var target = LowerPlace(get.Target);
            var fileNumber = MaterializeFileArgument(get.FileNumber, TypeSymbol.Long, "__file_get_number");
            var position = get.Position is null
                ? new IrNullExpression(TypeSymbol.LongLong)
                : MaterializeFileArgument(get.Position, TypeSymbol.LongLong, "__file_get_position");
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordStart,
                TypeSymbol.Error,
                fileNumber,
                position)));
            EmitBinaryRecordGetFields(target, type, fileNumber);
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordEnd,
                TypeSymbol.Error,
                fileNumber,
                new IrConstantExpression(false, TypeSymbol.Boolean))));
        }

        private void LowerBinaryRecordPut(BoundPutStatement put, UserDefinedTypeSymbol type)
        {
            EnsureBinaryRecordLayout(type);
            var fileNumber = MaterializeFileArgument(put.FileNumber, TypeSymbol.Long, "__file_put_number");
            var position = put.Position is null
                ? new IrNullExpression(TypeSymbol.LongLong)
                : MaterializeFileArgument(put.Position, TypeSymbol.LongLong, "__file_put_position");
            var source = NewLocal("__file_put_record", type, compilerGenerated: true);
            Emit(new IrStoreInstruction(new IrLocalPlace(source), LowerValueCopy(put.Value)));
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordStart,
                TypeSymbol.Error,
                fileNumber,
                position)));
            EmitBinaryRecordPutFields(new IrLocalPlace(source), type, fileNumber);
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FileRecordEnd,
                TypeSymbol.Error,
                fileNumber,
                new IrConstantExpression(true, TypeSymbol.Boolean))));
        }

        private IrExpression MaterializeFileArgument(
            BoundExpression expression,
            TypeSymbol type,
            string localName)
        {
            var local = NewLocal(localName, type, compilerGenerated: true);
            Emit(new IrStoreInstruction(new IrLocalPlace(local), LowerExpression(expression)));
            return new IrLoadExpression(new IrLocalPlace(local));
        }

        private void EmitBinaryRecordGetFields(
            IrPlace target,
            UserDefinedTypeSymbol type,
            IrExpression fileNumber)
        {
            foreach (var member in type.Members)
            {
                var field = new IrFieldPlace(target, _program.GetField(member));
                if (member.Type is ArrayTypeSymbol array)
                {
                    if (!member.HasArrayBounds)
                    {
                        EmitBinaryRecordGetDynamicArray(field, array, fileNumber);
                        continue;
                    }

                    var arrayExpression = new IrEnsureArrayExpression(
                        field,
                        array,
                        MemberArrayBounds(member));
                    foreach (var indices in EnumerateArrayIndices(member.ArrayBounds))
                    {
                        var element = new IrArrayElementPlace(arrayExpression, indices, array.ElementType);
                        EmitBinaryRecordGetElement(element, array.ElementType, fileNumber);
                    }

                    continue;
                }

                if (member.Type is UserDefinedTypeSymbol nested)
                {
                    EmitBinaryRecordGetFields(field, nested, fileNumber);
                    continue;
                }

                EmitBinaryRecordGetElement(field, member.Type, fileNumber);
            }
        }

        private void EmitBinaryRecordGetElement(
            IrPlace target,
            TypeSymbol type,
            IrExpression fileNumber)
        {
            if (type is UserDefinedTypeSymbol nested)
            {
                EmitBinaryRecordGetFields(target, nested, fileNumber);
                return;
            }

            if (type is FixedLengthStringTypeSymbol fixedString)
            {
                Emit(new IrStoreInstruction(
                    target,
                    Runtime(
                        IrRuntimeMethod.FileGetRawFixedString,
                        TypeSymbol.String,
                        fileNumber,
                        new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))));
                return;
            }

            if (type == TypeSymbol.Variant)
            {
                Emit(new IrStoreInstruction(
                    target,
                    Runtime(
                        IrRuntimeMethod.FileGetRawVariant,
                        TypeSymbol.Variant,
                        fileNumber)));
                return;
            }

            Emit(new IrStoreInstruction(
                target,
                Runtime(
                    FileGetRawMethod(type),
                    type,
                    fileNumber)));
        }

        private void EmitBinaryRecordPutFields(
            IrPlace source,
            UserDefinedTypeSymbol type,
            IrExpression fileNumber)
        {
            foreach (var member in type.Members)
            {
                var field = new IrFieldPlace(source, _program.GetField(member));
                if (member.Type is ArrayTypeSymbol array)
                {
                    if (!member.HasArrayBounds)
                    {
                        EmitBinaryRecordPutDynamicArray(field, array, fileNumber);
                        continue;
                    }

                    var arrayExpression = new IrEnsureArrayExpression(
                        field,
                        array,
                        MemberArrayBounds(member));
                    foreach (var indices in EnumerateArrayIndices(member.ArrayBounds))
                    {
                        var element = new IrArrayElementPlace(arrayExpression, indices, array.ElementType);
                        EmitBinaryRecordPutElement(element, array.ElementType, fileNumber);
                    }

                    continue;
                }

                if (member.Type is UserDefinedTypeSymbol nested)
                {
                    EmitBinaryRecordPutFields(field, nested, fileNumber);
                    continue;
                }

                EmitBinaryRecordPutElement(field, member.Type, fileNumber);
            }
        }

        private void EmitBinaryRecordPutElement(
            IrPlace source,
            TypeSymbol type,
            IrExpression fileNumber)
        {
            if (type is UserDefinedTypeSymbol nested)
            {
                EmitBinaryRecordPutFields(source, nested, fileNumber);
                return;
            }

            if (type is FixedLengthStringTypeSymbol fixedString)
            {
                Emit(new IrEvaluateInstruction(Runtime(
                    IrRuntimeMethod.FilePutRawFixedString,
                    TypeSymbol.Error,
                    fileNumber,
                    new IrLoadExpression(source),
                    new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))));
                return;
            }

            if (type == TypeSymbol.Variant)
            {
                Emit(new IrEvaluateInstruction(Runtime(
                    IrRuntimeMethod.FilePutRawVariant,
                    TypeSymbol.Error,
                    fileNumber,
                    new IrLoadExpression(source))));
                return;
            }

            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FilePutRaw,
                TypeSymbol.Error,
                fileNumber,
                new IrLoadExpression(source))));
        }

        private void EmitBinaryRecordGetDynamicArray(
            IrPlace field,
            ArrayTypeSymbol arrayType,
            IrExpression fileNumber)
        {
            Emit(new IrStoreInstruction(
                field,
                Runtime(IrRuntimeMethod.FileGetDynamicArray, arrayType, fileNumber)));

            var array = NewLocal("__file_get_dynamic_array", arrayType, compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(array),
                new IrLoadExpression(field)));
            EmitDynamicArrayRecordElements(array, arrayType, fileNumber, forWrite: false);
        }

        private void EmitBinaryRecordPutDynamicArray(
            IrPlace field,
            ArrayTypeSymbol arrayType,
            IrExpression fileNumber)
        {
            var array = NewLocal("__file_put_dynamic_array", arrayType, compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(array),
                new IrLoadExpression(field)));
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.FilePutDynamicArrayDescriptor,
                TypeSymbol.Error,
                fileNumber,
                new IrLoadExpression(new IrLocalPlace(array)))));
            EmitDynamicArrayRecordElements(array, arrayType, fileNumber, forWrite: true);
        }

        private void EmitDynamicArrayRecordElements(
            IrLocal array,
            ArrayTypeSymbol arrayType,
            IrExpression fileNumber,
            bool forWrite)
        {
            var allocated = NewBlock(forWrite ? "record_put_array_allocated" : "record_get_array_allocated");
            var exit = NewBlock(forWrite ? "record_put_array_exit" : "record_get_array_exit");
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.ArrayIsAllocated,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(new IrLocalPlace(array))),
                allocated.Id,
                exit.Id));

            _current = allocated;
            var index = NewLocal(
                forWrite ? "__file_put_array_index" : "__file_get_array_index",
                TypeSymbol.Long,
                compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(index),
                new IrConstantExpression(0, TypeSymbol.Long)));

            var test = NewBlock(forWrite ? "record_put_array_test" : "record_get_array_test");
            var body = NewBlock(forWrite ? "record_put_array_body" : "record_get_array_body");
            var increment = NewBlock(forWrite ? "record_put_array_increment" : "record_get_array_increment");
            Terminate(new IrGotoTerminator(test.Id));

            _current = test;
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.Less,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(new IrLocalPlace(index)),
                    new IrArrayCallExpression(
                        IrArrayOperation.Length,
                        new IrLoadExpression(new IrLocalPlace(array)),
                        ImmutableArray<IrExpression>.Empty,
                        TypeSymbol.Long)),
                body.Id,
                exit.Id));

            _current = body;
            var element = new IrArrayFlatElementPlace(
                new IrLoadExpression(new IrLocalPlace(array)),
                new IrLoadExpression(new IrLocalPlace(index)),
                arrayType.ElementType);
            if (forWrite)
            {
                EmitBinaryRecordPutElement(element, arrayType.ElementType, fileNumber);
            }
            else
            {
                EmitBinaryRecordGetElement(element, arrayType.ElementType, fileNumber);
            }
            GotoIfOpen(increment.Id);

            _current = increment;
            Emit(new IrStoreInstruction(
                new IrLocalPlace(index),
                Runtime(
                    IrRuntimeMethod.AddLong,
                    TypeSymbol.Long,
                    new IrLoadExpression(new IrLocalPlace(index)),
                    new IrConstantExpression(1, TypeSymbol.Long))));
            Terminate(new IrGotoTerminator(test.Id));
            _current = exit;
        }

        private static void EnsureBinaryRecordLayout(UserDefinedTypeSymbol type)
        {
            if (!UserDefinedTypeFileLayout.IsBinaryTransferable(type))
            {
                throw new NotSupportedException(
                    $"UDT '{type.Name}' does not have a supported scalar binary record layout.");
            }
        }

        private static bool CanProtectForErrorHandling(BoundStatement statement) => statement is not (
            BoundIfStatement or
            BoundForStatement or
            BoundForEachStatement or
            BoundWhileStatement or
            BoundDoStatement or
            BoundWithStatement or
            BoundSelectCaseStatement or
            BoundExitLoopStatement or
            BoundReturnStatement or
            BoundGoToStatement or
            BoundGoSubStatement or
            BoundGoSubReturnStatement or
            BoundOnGoToStatement or
            BoundOnGoSubStatement or
            BoundLabelStatement or
            BoundOnErrorStatement or
            BoundResumeStatement or
            BoundDebugAssertStatement);

        private void LowerWithEventsSubscriptions(VariableSymbol variable)
        {
            if (_containingClass is null ||
                variable is not ModuleVariableSymbol moduleVariable ||
                !_program.TryGetWithEventsHandlers(moduleVariable, out var handlers) ||
                handlers.IsDefaultOrEmpty)
            {
                return;
            }

            var source = new IrLoadExpression(LowerVariablePlace(moduleVariable));
            var target = new IrLoadExpression(new IrThisPlace(_containingClass));
            foreach (var (eventSymbol, handler) in handlers)
            {
                Emit(new IrSubscribeEventInstruction(source, eventSymbol, target, handler));
            }
        }

        private void EmitModuleInitializers()
        {
            foreach (var variable in _moduleInitializers)
            {
                var target = new IrGlobalPlace(_program.GetGlobal(variable.Symbol));
                if (variable.Symbol.Type is ArrayTypeSymbol arrayType && !variable.ArrayDimensions.IsDefaultOrEmpty)
                {
                    Emit(new IrStoreInstruction(
                        target,
                        new IrNewVBArrayExpression(arrayType, LowerBounds(variable.ArrayDimensions))));
                    continue;
                }

                if (variable.Symbol.Type is FixedLengthStringTypeSymbol fixedString)
                {
                    Emit(new IrStoreInstruction(target, FixedStringInitialValue(fixedString)));
                    continue;
                }

                if (variable.Symbol.Type == TypeSymbol.String)
                {
                    Emit(new IrStoreInstruction(
                        target,
                        new IrConstantExpression(string.Empty, TypeSymbol.String)));
                }
            }
        }

        /// <summary>
        /// Initializes procedure-lifetime storage before user control flow starts. CLR InitLocals
        /// already gives numeric/value locals their VB6 zero value; only storage whose VB6 default
        /// differs from the CLR default needs explicit IR here.
        /// </summary>
        private void EmitProcedurePrologue()
        {
            foreach (var declaration in EnumerateVariableDeclarations(_procedure.Body))
            {
                InitializeVariableDeclaration(declaration);
            }

            if (_returnLocal is not null && _procedure.Symbol.ReturnType == TypeSymbol.String)
            {
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(_returnLocal),
                    new IrConstantExpression(string.Empty, TypeSymbol.String)));
            }
        }

        private static IEnumerable<BoundVariableDeclarationStatement> EnumerateVariableDeclarations(
            BoundBlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                switch (statement)
                {
                    case BoundVariableDeclarationStatement declaration:
                        yield return declaration;
                        break;
                    case BoundIfStatement @if:
                        foreach (var declaration in EnumerateVariableDeclarations(@if.Body))
                        {
                            yield return declaration;
                        }
                        foreach (var clause in @if.ElseIfClauses)
                        {
                            foreach (var declaration in EnumerateVariableDeclarations(clause.Body))
                            {
                                yield return declaration;
                            }
                        }
                        if (@if.ElseBody is not null)
                        {
                            foreach (var declaration in EnumerateVariableDeclarations(@if.ElseBody))
                            {
                                yield return declaration;
                            }
                        }
                        break;
                    case BoundForStatement @for:
                        foreach (var declaration in EnumerateVariableDeclarations(@for.Body))
                        {
                            yield return declaration;
                        }
                        break;
                    case BoundForEachStatement forEach:
                        foreach (var declaration in EnumerateVariableDeclarations(forEach.Body))
                        {
                            yield return declaration;
                        }
                        break;
                    case BoundWhileStatement @while:
                        foreach (var declaration in EnumerateVariableDeclarations(@while.Body))
                        {
                            yield return declaration;
                        }
                        break;
                    case BoundDoStatement @do:
                        foreach (var declaration in EnumerateVariableDeclarations(@do.Body))
                        {
                            yield return declaration;
                        }
                        break;
                    case BoundWithStatement with:
                        foreach (var declaration in EnumerateVariableDeclarations(with.Body))
                        {
                            yield return declaration;
                        }
                        break;
                    case BoundSelectCaseStatement select:
                        foreach (var @case in select.Cases)
                        {
                            foreach (var declaration in EnumerateVariableDeclarations(@case.Body))
                            {
                                yield return declaration;
                            }
                        }
                        break;
                }
            }
        }

        private void InitializeVariableDeclaration(BoundVariableDeclarationStatement declaration)
        {
            var target = LowerVariablePlace(declaration.Variable);
            if (declaration.Initializer is not null && !declaration.Variable.IsAsNew)
            {
                Emit(new IrStoreInstruction(target, LowerAssignedValueCopy(declaration.Initializer)));
                return;
            }

            if (declaration.Variable.Type is ArrayTypeSymbol arrayType && !declaration.ArrayDimensions.IsDefaultOrEmpty)
            {
                Emit(new IrStoreInstruction(target, new IrNewVBArrayExpression(
                    arrayType,
                    LowerBounds(declaration.ArrayDimensions))));
                return;
            }

            if (declaration.Variable.Type is FixedLengthStringTypeSymbol fixedString)
            {
                Emit(new IrStoreInstruction(target, FixedStringInitialValue(fixedString)));
                return;
            }

            if (declaration.Variable.Type == TypeSymbol.String)
            {
                Emit(new IrStoreInstruction(target, new IrConstantExpression(string.Empty, TypeSymbol.String)));
            }
        }

        /// <summary>
        /// A <c>String * n</c> starts out as n spaces in VB6, not as the empty string. The width is
        /// part of the type, so the storage is never narrower than it.
        /// </summary>
        internal static IrConstantExpression FixedStringInitialValue(FixedLengthStringTypeSymbol type) =>
            new(new string(' ', type.Length), TypeSymbol.String);

        private void LowerReDim(BoundReDimStatement statement)
        {
            if (statement.Target.Type is not ArrayTypeSymbol arrayType)
            {
                return;
            }

            var target = LowerPlace(statement.Target);
            var bounds = LowerBounds(statement.ArrayDimensions);
            IrExpression value = statement.Preserve
                ? new IrReDimPreserveExpression(new IrLoadExpression(target), arrayType, bounds)
                : new IrNewVBArrayExpression(arrayType, bounds);
            Emit(new IrStoreInstruction(target, value));
        }

        /// <summary>
        /// <c>Load</c>/<c>Unload</c> on a control array follows the ReDim Preserve shape: load the
        /// array place, hand it to the runtime, store the result back. Growing the array replaces
        /// the reference, so writing it back is what makes the new element visible everywhere.
        /// </summary>
        private void LowerControlArrayElement(BoundControlArrayElementStatement statement)
        {
            var target = LowerPlace(statement.Target);
            Emit(new IrStoreInstruction(
                target,
                Runtime(
                    statement.Unload
                        ? IrRuntimeMethod.InteractionUnloadControlArrayElement
                        : IrRuntimeMethod.InteractionLoadControlArrayElement,
                    statement.Target.Type,
                    new IrLoadExpression(target),
                    LowerExpression(statement.Index),
                    new IrConstantExpression(statement.Name, TypeSymbol.String),
                    LowerExpression(statement.Owner))));
        }

        private void LowerErase(BoundEraseStatement statement)
        {
            var target = LowerPlace(statement.Target);
            if (statement.Deallocate)
            {
                Emit(new IrStoreInstruction(target, new IrNullExpression(statement.Target.Type)));
                return;
            }

            Emit(new IrEvaluateInstruction(new IrArrayCallExpression(
                IrArrayOperation.Clear,
                new IrLoadExpression(target),
                ImmutableArray<IrExpression>.Empty,
                TypeSymbol.Error)));
        }

        /// <summary>
        /// Evaluates a control-flow condition inside its own protected region. The statement as a
        /// whole cannot be protected - its body spans several basic blocks, and a protected region
        /// may not cross one - but the condition is evaluated in the current block. Without this an
        /// error there escapes <c>On Error</c> entirely and ends the process, while VB6 records it
        /// and carries on after the statement.
        /// </summary>
        private IrExpression LowerProtectedCondition(BoundExpression condition, int errorContinuationBlockId)
        {
            if (!_resumeNext && _errorHandler is null)
            {
                return LowerExpression(condition);
            }

            var temporary = new IrLocalPlace(
                NewLocal($"__condition_{_nextLocalId}", condition.Type, compilerGenerated: true));
            LowerProtectedHeader(
                errorContinuationBlockId,
                () => Emit(new IrStoreInstruction(temporary, LowerExpression(condition))));
            return new IrLoadExpression(temporary);
        }

        /// <summary>
        /// Runs the instructions of a control-flow statement's header inside a protected region.
        /// The callback must stay within the current basic block - a protected region may not cross
        /// one - which the header of every loop and conditional does.
        /// </summary>
        private void LowerProtectedHeader(int errorContinuationBlockId, Action emit)
        {
            if (!_resumeNext && _errorHandler is null)
            {
                emit();
                return;
            }

            Emit(new IrErrorBoundaryStartInstruction(
                _errorHandler is null ? null : _labels[_errorHandler]));
            emit();
            Emit(new IrErrorBoundaryEndInstruction(errorContinuationBlockId));
        }

        private void LowerIf(BoundIfStatement statement)
        {
            var end = NewBlock("if_end");
            var clauses = new List<(BoundExpression Condition, BoundBlockStatement Body)>
            {
                (statement.Condition, statement.Body)
            };
            clauses.AddRange(statement.ElseIfClauses.Select(clause => (clause.Condition, clause.Body)));

            for (var index = 0; index < clauses.Count; index++)
            {
                var body = NewBlock($"if_body_{index}");
                var next = index == clauses.Count - 1
                    ? statement.ElseBody is null ? end : NewBlock("if_else")
                    : NewBlock($"if_test_{index + 1}");
                Terminate(new IrConditionalTerminator(
                    LowerProtectedCondition(clauses[index].Condition, end.Id),
                    body.Id,
                    next.Id));

                _current = body;
                LowerBlock(clauses[index].Body);
                GotoIfOpen(end.Id);
                _current = next;
            }

            if (statement.ElseBody is not null)
            {
                LowerBlock(statement.ElseBody);
                GotoIfOpen(end.Id);
            }

            _current = end;
        }

        private void LowerFor(BoundForStatement statement)
        {
            var control = LowerVariablePlace(statement.ControlVariable);
            var limit = NewLocal($"__for_limit_{statement.LoopId}", statement.ControlVariable.Type, true);
            var step = NewLocal($"__for_step_{statement.LoopId}", statement.ControlVariable.Type, true);

            // The exit block is needed before the header runs: an error while evaluating the start
            // value, the limit or the step skips the whole loop under On Error Resume Next.
            var sign = NewBlock($"for_sign_{statement.LoopId}");
            var positive = NewBlock($"for_positive_{statement.LoopId}");
            var negative = NewBlock($"for_negative_{statement.LoopId}");
            var body = NewBlock($"for_body_{statement.LoopId}");
            var increment = NewBlock($"for_increment_{statement.LoopId}");
            var exit = NewBlock($"for_exit_{statement.LoopId}");
            _loopExits[statement.LoopId] = exit.Id;

            LowerProtectedHeader(exit.Id, () =>
            {
                Emit(new IrStoreInstruction(control, LowerExpression(statement.InitialValue)));
                Emit(new IrStoreInstruction(new IrLocalPlace(limit), LowerExpression(statement.Limit)));
                Emit(new IrStoreInstruction(new IrLocalPlace(step), LowerExpression(statement.Step)));
            });

            Terminate(new IrGotoTerminator(sign.Id));

            _current = sign;
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.GreaterOrEqual,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(new IrLocalPlace(step)),
                    Zero(statement.ControlVariable.Type)),
                positive.Id,
                negative.Id));

            _current = positive;
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.LessOrEqual,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(control),
                    new IrLoadExpression(new IrLocalPlace(limit))),
                body.Id,
                exit.Id));

            _current = negative;
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.GreaterOrEqual,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(control),
                    new IrLoadExpression(new IrLocalPlace(limit))),
                body.Id,
                exit.Id));

            _current = body;
            LowerBlock(statement.Body);
            GotoIfOpen(increment.Id);

            _current = increment;
            Emit(new IrStoreInstruction(
                control,
                Runtime(
                    AddMethod(statement.ControlVariable.Type),
                    statement.ControlVariable.Type,
                    new IrLoadExpression(control),
                    new IrLoadExpression(new IrLocalPlace(step)))));
            Terminate(new IrGotoTerminator(sign.Id));
            _current = exit;
            _loopExits.Remove(statement.LoopId);
        }

        private void LowerForEach(BoundForEachStatement statement)
        {
            var collection = NewLocal(
                $"__foreach_collection_{statement.LoopId}",
                statement.IsCollection || statement.IsHostCollection
                    ? statement.Collection.Type
                    : statement.ArrayType,
                true);
            var values = statement.IsCollection || statement.IsHostCollection
                ? NewLocal($"__foreach_values_{statement.LoopId}", statement.ArrayType, true)
                : collection;
            var index = NewLocal($"__foreach_index_{statement.LoopId}", TypeSymbol.Long, true);
            Emit(new IrStoreInstruction(new IrLocalPlace(collection), LowerExpression(statement.Collection)));
            if (statement.IsCollection)
            {
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(values),
                    Runtime(
                        IrRuntimeMethod.CollectionEnumerateValues,
                        statement.ArrayType,
                        new IrLoadExpression(new IrLocalPlace(collection)))));
            }
            else if (statement.IsHostCollection)
            {
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(values),
                    Runtime(
                        IrRuntimeMethod.ControlEnumerateValues,
                        statement.ArrayType,
                        new IrLoadExpression(new IrLocalPlace(collection)))));
            }

            Emit(new IrStoreInstruction(new IrLocalPlace(index), new IrConstantExpression(0, TypeSymbol.Long)));

            var test = NewBlock($"foreach_test_{statement.LoopId}");
            var body = NewBlock($"foreach_body_{statement.LoopId}");
            var increment = NewBlock($"foreach_increment_{statement.LoopId}");
            var exit = NewBlock($"foreach_exit_{statement.LoopId}");
            _loopExits[statement.LoopId] = exit.Id;
            Terminate(new IrGotoTerminator(test.Id));

            _current = test;
            Terminate(new IrConditionalTerminator(
                Runtime(
                    IrRuntimeMethod.Less,
                    TypeSymbol.Boolean,
                    new IrLoadExpression(new IrLocalPlace(index)),
                    new IrArrayCallExpression(
                        IrArrayOperation.Length,
                        new IrLoadExpression(new IrLocalPlace(values)),
                        ImmutableArray<IrExpression>.Empty,
                        TypeSymbol.Long)),
                body.Id,
                exit.Id));

            _current = body;
            var item = new IrArrayCallExpression(
                IrArrayOperation.GetFlatValue,
                new IrLoadExpression(new IrLocalPlace(values)),
                ImmutableArray.Create<IrExpression>(new IrLoadExpression(new IrLocalPlace(index))),
                statement.ArrayType.ElementType);
            Emit(new IrStoreInstruction(LowerVariablePlace(statement.ControlVariable), item));
            LowerBlock(statement.Body);
            GotoIfOpen(increment.Id);

            _current = increment;
            Emit(new IrStoreInstruction(
                new IrLocalPlace(index),
                Runtime(
                    IrRuntimeMethod.AddLong,
                    TypeSymbol.Long,
                    new IrLoadExpression(new IrLocalPlace(index)),
                    new IrConstantExpression(1, TypeSymbol.Long))));
            Terminate(new IrGotoTerminator(test.Id));
            _current = exit;
            _loopExits.Remove(statement.LoopId);
        }

        private void LowerWhile(BoundWhileStatement statement)
        {
            var test = NewBlock("while_test");
            var body = NewBlock("while_body");
            var exit = NewBlock("while_exit");
            Terminate(new IrGotoTerminator(test.Id));
            _current = test;
            Terminate(new IrConditionalTerminator(
                LowerProtectedCondition(statement.Condition, exit.Id),
                body.Id,
                exit.Id));
            _current = body;
            LowerBlock(statement.Body);
            GotoIfOpen(test.Id);
            _current = exit;
        }

        private void LowerDo(BoundDoStatement statement)
        {
            var test = NewBlock($"do_test_{statement.LoopId}");
            var body = NewBlock($"do_body_{statement.LoopId}");
            var exit = NewBlock($"do_exit_{statement.LoopId}");
            _loopExits[statement.LoopId] = exit.Id;

            Terminate(new IrGotoTerminator(statement.ConditionIsPostTest || statement.Condition is null ? body.Id : test.Id));
            if (statement.Condition is not null)
            {
                _current = test;
                var condition = LowerProtectedCondition(statement.Condition, exit.Id);
                Terminate(statement.IsUntil
                    ? new IrConditionalTerminator(condition, exit.Id, body.Id)
                    : new IrConditionalTerminator(condition, body.Id, exit.Id));
            }

            _current = body;
            LowerBlock(statement.Body);
            if (!_current.HasTerminator)
            {
                if (statement.Condition is null)
                {
                    Terminate(new IrGotoTerminator(body.Id));
                }
                else if (statement.ConditionIsPostTest)
                {
                    var condition = LowerProtectedCondition(statement.Condition, exit.Id);
                    Terminate(statement.IsUntil
                        ? new IrConditionalTerminator(condition, exit.Id, body.Id)
                        : new IrConditionalTerminator(condition, body.Id, exit.Id));
                }
                else
                {
                    Terminate(new IrGotoTerminator(test.Id));
                }
            }

            _current = exit;
            _loopExits.Remove(statement.LoopId);
        }

        private void LowerWith(BoundWithStatement statement)
        {
            if (statement.Target.Type is ClassTypeSymbol)
            {
                var receiver = NewLocal($"__with_receiver_{statement.WithId}", statement.Target.Type, true);
                _withPlaces.Add(statement.WithId, new IrLocalPlace(receiver));
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(receiver),
                    LowerExpression(statement.Target)));
            }
            else
            {
                var target = LowerPlace(statement.Target);
                var address = NewLocal($"__with_addr_{statement.WithId}", statement.Target.Type, true, managedAddress: true);
                _withPlaces.Add(
                    statement.WithId,
                    new IrIndirectPlace(
                        new IrLocalAddressExpression(address),
                        statement.Target.Type));
                Emit(new IrStoreAddressInstruction(address, new IrAddressExpression(target)));
            }

            LowerBlock(statement.Body);
            _withPlaces.Remove(statement.WithId);
        }

        private void LowerExit(BoundExitLoopStatement statement)
        {
            if (!_loopExits.TryGetValue(statement.TargetLoopId, out var target))
            {
                throw new InvalidOperationException($"Loop {statement.TargetLoopId} has no active IR exit target.");
            }

            Terminate(new IrGotoTerminator(target));
            _current = NewBlock("after_exit");
        }

        private void LowerSelect(BoundSelectCaseStatement statement)
        {
            var value = NewLocal($"__select_{statement.SelectId}", statement.Expression.Type, true);
            var exit = NewBlock($"select_exit_{statement.SelectId}");
            LowerProtectedHeader(
                exit.Id,
                () => Emit(new IrStoreInstruction(
                    new IrLocalPlace(value),
                    LowerExpression(statement.Expression))));
            var nextTest = _current;

            foreach (var caseBlock in statement.Cases)
            {
                _current = nextTest;
                if (caseBlock.Clauses.Any(clause => clause is BoundCaseElseClause))
                {
                    LowerBlock(caseBlock.Body);
                    GotoIfOpen(exit.Id);
                    nextTest = NewBlock("select_after_else");
                    continue;
                }

                var body = NewBlock("select_case_body");
                var miss = NewBlock("select_case_next");
                LowerCaseClauseChain(caseBlock.Clauses, 0, value, body.Id, miss.Id, statement.UseTextCompare);
                _current = body;
                LowerBlock(caseBlock.Body);
                GotoIfOpen(exit.Id);
                nextTest = miss;
            }

            _current = nextTest;
            GotoIfOpen(exit.Id);
            _current = exit;
        }

        private void LowerCaseClauseChain(
            ImmutableArray<BoundCaseClause> clauses,
            int index,
            IrLocal selected,
            int successBlock,
            int failureBlock,
            bool useTextCompare)
        {
            if (index >= clauses.Length)
            {
                Terminate(new IrGotoTerminator(failureBlock));
                return;
            }

            var next = index == clauses.Length - 1 ? failureBlock : NewBlock("select_clause_next").Id;
            LowerCaseClauseTest(clauses[index], selected, successBlock, next, useTextCompare);
            if (next != failureBlock)
            {
                _current = _blocks[next];
                LowerCaseClauseChain(clauses, index + 1, selected, successBlock, failureBlock, useTextCompare);
            }
        }

        private void LowerCaseClauseTest(
            BoundCaseClause clause,
            IrLocal selected,
            int success,
            int failure,
            bool useTextCompare)
        {
            var selectedValue = new IrLoadExpression(new IrLocalPlace(selected));
            static IrRuntimeCallExpression WithTextCompare(
                IrRuntimeCallExpression comparison,
                bool useTextCompare) =>
                useTextCompare ? comparison with { UseTextCompare = true } : comparison;

            switch (clause)
            {
                case BoundCaseValueClause value:
                    Terminate(new IrConditionalTerminator(
                        WithTextCompare(
                            Runtime(IrRuntimeMethod.Equal, TypeSymbol.Boolean, selectedValue, LowerExpression(value.Value)),
                            useTextCompare),
                        success,
                        failure));
                    break;
                case BoundCaseRelationalClause relational:
                    Terminate(new IrConditionalTerminator(
                        WithTextCompare(
                            Runtime(RelationalMethod(relational.OperatorKind), TypeSymbol.Boolean, selectedValue, LowerExpression(relational.Value)),
                            useTextCompare),
                        success,
                        failure));
                    break;
                case BoundCaseRangeClause range:
                {
                    var upperTest = NewBlock("select_range_upper");
                    Terminate(new IrConditionalTerminator(
                        WithTextCompare(
                            Runtime(IrRuntimeMethod.GreaterOrEqual, TypeSymbol.Boolean, selectedValue, LowerExpression(range.LowerBound)),
                            useTextCompare),
                        upperTest.Id,
                        failure));
                    _current = upperTest;
                    Terminate(new IrConditionalTerminator(
                        WithTextCompare(
                            Runtime(IrRuntimeMethod.LessOrEqual, TypeSymbol.Boolean, selectedValue, LowerExpression(range.UpperBound)),
                            useTextCompare),
                        success,
                        failure));
                    break;
                }
                default:
                    Terminate(new IrGotoTerminator(failure));
                    break;
            }
        }

        private void LowerLabel(BoundLabelStatement statement)
        {
            var labelBlock = _blocks[_labels[statement.Name]];
            GotoIfOpen(labelBlock.Id);
            _current = labelBlock;

            // VB6's Erl is based on numeric line labels, not on the physical source line. Keep
            // the value in the runtime immediately when control enters such a label; named labels
            // remain ordinary branch targets and must not alter the error context.
            if (int.TryParse(
                    statement.Name,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var lineNumber) &&
                lineNumber > 0)
            {
                Emit(new IrEvaluateInstruction(Runtime(
                    IrRuntimeMethod.ErrorSetLineNumber,
                    TypeSymbol.Error,
                    new IrConstantExpression(lineNumber, TypeSymbol.Long))));
            }
        }

        private static void EnsureBinaryArrayElementLayout(TypeSymbol type)
        {
            if (!UserDefinedTypeFileLayout.IsBinaryTransferableElement(type))
            {
                throw new NotSupportedException(
                    $"Array element type '{type.Name}' does not have a supported binary file layout.");
            }
        }

        private IrExpression LowerExpression(BoundExpression expression)
        {
            return expression switch
            {
                BoundLiteralExpression literal => new IrConstantExpression(literal.Value, literal.LiteralType),
                BoundAddressOfExpression addressOf => new IrAddressOfExpression(addressOf.Procedure, addressOf.Type),
                BoundNewExpression @new => new IrNewClassExpression(@new.ClassType),
                BoundTypeOfExpression typeOf => new IrTypeOfExpression(
                    LowerExpression(typeOf.Expression),
                    typeOf.TargetType),
                BoundVariableExpression variable => LowerVariableRead(variable.Variable),
                BoundArrayAccessExpression array => LowerFixedStringRead(
                    array.ElementType,
                    new IrLoadExpression(new IrArrayElementPlace(
                        new IrLoadExpression(LowerVariablePlace(array.Array)),
                        array.Indices.Select(LowerExpression).ToImmutableArray(),
                        array.ElementType))),
                BoundArrayLiteralExpression array => LowerArrayLiteral(array),
                BoundElementAccessExpression element => LowerFixedStringRead(
                    element.ElementType,
                    new IrLoadExpression(new IrArrayElementPlace(
                        LowerExpression(element.Receiver),
                        element.Indices.Select(LowerExpression).ToImmutableArray(),
                        element.ElementType))),
                BoundVariantArrayAccessExpression variantElement => new IrVariantArrayCallExpression(
                    IrVariantArrayOperation.GetElement,
                    LowerExpression(variantElement.Receiver),
                    variantElement.Indices.Select(LowerExpression).ToImmutableArray(),
                    TypeSymbol.Variant),
                BoundMemberAccessExpression member => LowerMemberRead(member),
                BoundPropertyAccessExpression property => LowerPropertyRead(property),
                BoundPropertyInvocationExpression propertyInvocation => LowerPropertyInvocation(propertyInvocation),
                BoundMemberInvocationExpression memberInvocation => LowerMemberCall(
                    memberInvocation.Receiver,
                    memberInvocation.Procedure,
                    memberInvocation.Arguments),
                BoundWithReceiverExpression with => new IrLoadExpression(LowerWithPlace(with)),
                BoundArrayBoundExpression bound when bound.Array.Type == TypeSymbol.Variant =>
                    new IrVariantArrayCallExpression(
                        bound.IsUpperBound ? IrVariantArrayOperation.UBound : IrVariantArrayOperation.LBound,
                        LowerExpression(bound.Array),
                        ImmutableArray.Create(LowerExpression(bound.Dimension)),
                        TypeSymbol.Long),
                BoundArrayBoundExpression bound => new IrArrayCallExpression(
                    bound.IsUpperBound ? IrArrayOperation.UBound : IrArrayOperation.LBound,
                    LowerExpression(bound.Array),
                    ImmutableArray.Create(LowerExpression(bound.Dimension)),
                    TypeSymbol.Long),
                BoundInvocationExpression invocation => LowerCall(invocation.Procedure, invocation.Arguments),
                BoundConversionExpression conversion => LowerConversion(conversion),
                BoundUnaryExpression unary => LowerUnary(unary),
                BoundBinaryExpression binary => LowerBinary(binary),
                BoundErrorExpression => new IrDefaultExpression(TypeSymbol.Variant),
                _ => throw new NotSupportedException($"IR lowering does not support bound expression '{expression.GetType().Name}'.")
            };
        }

        private IrExpression LowerArrayLiteral(BoundArrayLiteralExpression array)
        {
            var storage = NewLocal("__array_literal", array.ArrayType, true);
            var upperBound = array.Elements.Length - 1L;
            Emit(new IrStoreInstruction(
                new IrLocalPlace(storage),
                new IrNewVBArrayExpression(
                    array.ArrayType,
                    ImmutableArray.Create(new IrArrayBound(
                        new IrConstantExpression(0L, TypeSymbol.Long),
                        new IrConstantExpression(upperBound, TypeSymbol.Long))))));

            for (var index = 0; index < array.Elements.Length; index++)
            {
                Emit(new IrStoreInstruction(
                    new IrArrayElementPlace(
                        new IrLoadExpression(new IrLocalPlace(storage)),
                        ImmutableArray.Create<IrExpression>(
                            new IrConstantExpression((long)index, TypeSymbol.Long)),
                        array.ArrayType.ElementType),
                    LowerValueCopy(array.Elements[index])));
            }

            return new IrLoadExpression(new IrLocalPlace(storage));
        }

        private IrPlace LowerPlace(BoundExpression expression) => expression switch
        {
            BoundVariableExpression variable => LowerVariablePlace(variable.Variable),
            BoundArrayAccessExpression array => new IrArrayElementPlace(
                new IrLoadExpression(LowerVariablePlace(array.Array)),
                array.Indices.Select(LowerExpression).ToImmutableArray(),
                array.ElementType),
            BoundElementAccessExpression element => new IrArrayElementPlace(
                LowerExpression(element.Receiver),
                element.Indices.Select(LowerExpression).ToImmutableArray(),
                element.ElementType),
            BoundVariantArrayAccessExpression variantElement => new IrVariantArrayElementPlace(
                LowerExpression(variantElement.Receiver),
                variantElement.Indices.Select(LowerExpression).ToImmutableArray()),
                BoundMemberAccessExpression member => LowerMemberPlace(member),
                BoundPropertyAccessExpression property => LowerPropertyPlace(property),
                BoundPropertyInvocationExpression propertyInvocation => LowerPropertyPlace(propertyInvocation),
                BoundWithReceiverExpression with => LowerWithPlace(with),
            _ => throw new InvalidOperationException(
                $"Bound expression '{expression.GetType().Name}' ({expression}) is not an addressable place" +
                (expression.SourceLocation is { } location
                    ? $" ({location.FilePath}:{location.Lines.Start.Line + 1})"
                    : string.Empty) +
                ".")
        };

        /// <summary>
        /// Reads a UDT member. A member declared with fixed array bounds has no storage until it
        /// is first touched, so reading it goes through <see cref="IrEnsureArrayExpression"/>
        /// rather than loading the field directly. Element reads and writes both arrive here,
        /// because the member is the receiver they index into - and the array is a reference, so
        /// one substitution covers both.
        /// </summary>
        /// <summary>
        /// A <c>String * n</c> member keeps its declared width, so a stored value is truncated or
        /// padded on the way in. Other targets pass through unchanged.
        /// </summary>
        private static IrExpression LowerFixedStringWrite(TypeSymbol targetType, IrExpression value) =>
            targetType is FixedLengthStringTypeSymbol fixedString
                ? new IrRuntimeCallExpression(
                    IrRuntimeMethod.FixedStringWrite,
                    ImmutableArray.Create(
                        new IrCallArgument(value),
                        new IrCallArgument(new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))),
                    TypeSymbol.String)
                : value;

        private static IrExpression LowerFixedStringRead(TypeSymbol sourceType, IrExpression value) =>
            sourceType is FixedLengthStringTypeSymbol fixedString
                ? new IrRuntimeCallExpression(
                    IrRuntimeMethod.FixedStringRead,
                    ImmutableArray.Create(
                        new IrCallArgument(value),
                        new IrCallArgument(new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))),
                    TypeSymbol.String)
                : value;

        private IrExpression LowerMemberRead(BoundMemberAccessExpression expression)
        {
            var place = LowerMemberPlace(expression);
            if (expression.Member.Type is FixedLengthStringTypeSymbol fixedString)
            {
                return new IrRuntimeCallExpression(
                    IrRuntimeMethod.FixedStringRead,
                    ImmutableArray.Create(
                        new IrCallArgument(new IrLoadExpression(place)),
                        new IrCallArgument(new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))),
                    TypeSymbol.String);
            }

            if (expression.Member.HasArrayBounds && expression.Member.Type is ArrayTypeSymbol arrayType)
            {
                return new IrEnsureArrayExpression(place, arrayType, MemberArrayBounds(expression.Member));
            }

            return new IrLoadExpression(place);
        }

        private IrExpression LowerPropertyRead(BoundPropertyAccessExpression expression)
        {
            if (IsClipboardGetText(expression.Receiver, expression.Property.Name))
            {
                return Runtime(
                    IrRuntimeMethod.InteractionClipboardGetText,
                    TypeSymbol.String,
                    new IrConstantExpression(1L, TypeSymbol.Long));
            }

            if (IsScreenObject(expression.Receiver))
            {
                return LowerScreenProperty(expression.Property.Name);
            }

            if (IsPrinterObject(expression.Receiver))
            {
                return LowerPrinterProperty(expression.Property);
            }

            if (expression.Property.IsLateBound || IsRuntimeObject(expression.Receiver))
            {
                return LowerDynamicGet(
                    expression.Receiver,
                    expression.Property.Name,
                    ImmutableArray<BoundArgument>.Empty,
                    expression.Property.Type);
            }

            if (IsApplicationObject(expression.Receiver))
            {
                return LowerApplicationProperty(expression.Property.Name);
            }

            if (TryGetClassFieldPlace(expression.Receiver, expression.Property, out var fieldPlace))
            {
                return new IrLoadExpression(fieldPlace);
            }

            if (expression.Receiver.Type is ClassTypeSymbol classType &&
                ReferenceEquals(classType, VBStandardTypes.Collection))
            {
                return LowerCollectionProperty(
                    expression.Property,
                    expression.Receiver,
                    ImmutableArray<BoundArgument>.Empty);
            }

            if (expression.Receiver.Type is not ClassTypeSymbol classTypeForProcedure)
            {
                throw new NotSupportedException(
                    $"Property '{expression.Property.Name}' has no emitted Get accessor.");
            }

            ProcedureSymbol? getter;
            if (classTypeForProcedure.ExternalAssemblyName is not null)
            {
                getter = TryGetExternalPropertyProcedure(
                    classTypeForProcedure,
                    expression.Property.Name,
                    PropertyAccessorKind.Get);
            }
            else
            {
                _program.TryGetClassProcedure(
                    classTypeForProcedure,
                    expression.Property.Name,
                    PropertyAccessorKind.Get,
                    out getter);
            }

            if (getter is null)
            {
                throw new NotSupportedException(
                    $"Property '{expression.Property.Name}' has no emitted Get accessor.");
            }

            return LowerCall(
                getter,
                ImmutableArray<BoundArgument>.Empty,
                LowerExpression(expression.Receiver));
        }

        private IrExpression LowerPropertyInvocation(BoundPropertyInvocationExpression expression)
        {
            if (expression.Arguments.IsDefaultOrEmpty &&
                IsClipboardGetText(expression.Receiver, expression.Property.Name))
            {
                return Runtime(
                    IrRuntimeMethod.InteractionClipboardGetText,
                    TypeSymbol.String,
                    new IrConstantExpression(1L, TypeSymbol.Long));
            }

            if (expression.Arguments.IsDefaultOrEmpty && IsScreenObject(expression.Receiver))
            {
                return LowerScreenProperty(expression.Property.Name);
            }

            if (expression.Arguments.IsDefaultOrEmpty && IsPrinterObject(expression.Receiver))
            {
                return LowerPrinterProperty(expression.Property);
            }

            if (expression.Property.IsLateBound || IsRuntimeObject(expression.Receiver))
            {
                return LowerDynamicGet(
                    expression.Receiver,
                    expression.Property.Name,
                    expression.Arguments,
                    expression.Property.Type);
            }

            if (expression.Receiver.Type is ClassTypeSymbol collectionType &&
                ReferenceEquals(collectionType, VBStandardTypes.Collection))
            {
                return LowerCollectionProperty(
                    expression.Property,
                    expression.Receiver,
                    expression.Arguments);
            }

            if (expression.Arguments.IsDefaultOrEmpty &&
                TryGetClassFieldPlace(expression.Receiver, expression.Property, out var fieldPlace))
            {
                return new IrLoadExpression(fieldPlace);
            }

            if (expression.Receiver.Type is not ClassTypeSymbol classType)
            {
                throw new NotSupportedException(
                    $"Indexed property '{expression.Property.Name}' has no emitted Get accessor.");
            }

            ProcedureSymbol? getter;
            if (classType.ExternalAssemblyName is not null)
            {
                getter = TryGetExternalPropertyProcedure(
                    classType,
                    expression.Property.Name,
                    PropertyAccessorKind.Get);
            }
            else
            {
                _program.TryGetClassProcedure(
                    classType,
                    expression.Property.Name,
                    PropertyAccessorKind.Get,
                    out getter);
            }

            if (getter is null)
            {
                throw new NotSupportedException(
                    $"Indexed property '{expression.Property.Name}' has no emitted Get accessor.");
            }

            return LowerCall(
                getter,
                expression.Arguments,
                LowerExpression(expression.Receiver));
        }

        private IrExpression LowerMemberCall(
            BoundExpression receiver,
            ProcedureSymbol requested,
            ImmutableArray<BoundArgument> arguments)
        {
            if (IsClipboardObject(receiver))
            {
                return LowerClipboardProcedure(requested, arguments);
            }

            if (IsPrinterObject(receiver))
            {
                return LowerPrinterProcedure(requested, arguments);
            }

            if (requested.IsLateBound || IsRuntimeObject(receiver))
            {
                return LowerDynamicInvoke(
                    receiver,
                    requested.Name,
                    arguments,
                    requested.ReturnType ?? TypeSymbol.Variant);
            }

            if (receiver.Type is ClassTypeSymbol collectionType &&
                ReferenceEquals(collectionType, VBStandardTypes.Collection))
            {
                return LowerCollectionProcedure(requested, receiver, arguments);
            }

            if (receiver.Type is not ClassTypeSymbol classType)
            {
                throw new NotSupportedException(
                    $"Instance call receiver '{receiver.Type.Name}' is not a class type.");
            }

            var procedure = classType.ExternalAssemblyName is null
                ? _program.ResolveClassProcedure(classType, requested)
                : requested;
            return LowerCall(procedure, arguments, LowerExpression(receiver));
        }

        private IrPlace LowerPropertyPlace(BoundPropertyAccessExpression expression)
        {
            return LowerPropertyPlace(
                expression.Receiver,
                expression.Property,
                ImmutableArray<BoundArgument>.Empty);
        }

        private IrPlace LowerPropertyPlace(BoundPropertyInvocationExpression expression)
        {
            return LowerPropertyPlace(expression.Receiver, expression.Property, expression.Arguments);
        }

        private IrPlace LowerPropertyPlace(
            BoundExpression receiver,
            PropertySymbol property,
            ImmutableArray<BoundArgument> arguments)
        {
            if (property.IsLateBound)
            {
                throw new NotSupportedException(
                    "A late-bound property cannot be passed ByRef until object write-back semantics are lowered.");
            }

            if (arguments.IsDefaultOrEmpty &&
                TryGetClassFieldPlace(receiver, property, out var fieldPlace))
            {
                return fieldPlace;
            }

            if (receiver.Type is ClassTypeSymbol collectionType &&
                ReferenceEquals(collectionType, VBStandardTypes.Collection))
            {
                throw new NotSupportedException(
                    $"Collection property '{property.Name}' is read-only in the current runtime slice.");
            }

            if (receiver.Type is not ClassTypeSymbol classType)
            {
                throw new NotSupportedException(
                    $"Property receiver '{receiver.Type.Name}' is not a class type.");
            }

            ProcedureSymbol? getter;
            ProcedureSymbol? setter;
            if (classType.ExternalAssemblyName is not null)
            {
                getter = TryGetExternalPropertyProcedure(
                    classType,
                    property.Name,
                    PropertyAccessorKind.Get);
                setter = TryGetExternalPropertyProcedure(
                    classType,
                    property.Name,
                    property.Accessor);
            }
            else
            {
                _program.TryGetClassProcedure(
                    classType,
                    property.Name,
                    PropertyAccessorKind.Get,
                    out getter);
                _program.TryGetClassProcedure(
                    classType,
                    property.Name,
                    property.Accessor,
                    out setter);
            }
            if (property.Accessor is not (PropertyAccessorKind.Let or PropertyAccessorKind.Set))
            {
                setter = null;
            }

            return new IrAccessorPlace(
                LowerExpression(receiver),
                getter,
                setter,
                property.Type,
                arguments.Select(argument => LowerValueCopy(argument.Expression)).ToImmutableArray());
        }

        private bool TryGetClassFieldPlace(
            BoundExpression receiver,
            PropertySymbol property,
            out IrFieldPlace fieldPlace)
        {
            if (receiver.Type is ClassTypeSymbol classType &&
                property.Parameters.IsEmpty &&
                _program.TryGetClassField(classType, property.Name, out var field))
            {
                fieldPlace = new IrFieldPlace(LowerPlace(receiver), field);
                return true;
            }

            fieldPlace = null!;
            return false;
        }

        private static ProcedureSymbol? TryGetExternalPropertyProcedure(
            ClassTypeSymbol classType,
            string propertyName,
            PropertyAccessorKind accessor)
        {
            return classType.TryGetProperty(propertyName, accessor, out var property)
                ? new ProcedureSymbol(
                    property.Name,
                    property.Parameters,
                    accessor == PropertyAccessorKind.Get ? property.Type : null)
                {
                    PropertyAccessor = accessor
                }
                : null;
        }

        private IrExpression LowerCollectionProperty(
            PropertySymbol property,
            BoundExpression receiver,
            ImmutableArray<BoundArgument> arguments)
        {
            var method = property.Name.Equals("Count", StringComparison.OrdinalIgnoreCase)
                ? IrRuntimeMethod.CollectionCount
                : property.Name.Equals("Item", StringComparison.OrdinalIgnoreCase)
                ? IrRuntimeMethod.CollectionItem
                : throw new NotSupportedException(
                    $"Collection property '{property.Name}' has no managed runtime implementation.");

            if (method == IrRuntimeMethod.CollectionCount && !arguments.IsDefaultOrEmpty)
            {
                throw new NotSupportedException("Collection.Count does not accept index arguments.");
            }

            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(arguments.Length + 1);
            lowered.Add(new IrCallArgument(LowerExpression(receiver)));
            lowered.AddRange(arguments.Select(argument =>
                new IrCallArgument(LowerValueCopy(argument.Expression))));
            return new IrRuntimeCallExpression(method, lowered.ToImmutable(), property.Type);
        }

        private IrExpression LowerDynamicGet(
            BoundExpression receiver,
            string memberName,
            ImmutableArray<BoundArgument> arguments,
            TypeSymbol resultType)
        {
            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(arguments.IsDefaultOrEmpty ? 2 : 3);
            lowered.Add(new IrCallArgument(LowerExpression(receiver)));
            lowered.Add(new IrCallArgument(new IrConstantExpression(memberName, TypeSymbol.String)));
            if (!arguments.IsDefaultOrEmpty)
            {
                var dynamicArguments = arguments.Length == 1 && arguments[0].Parameter?.IsParamArray == true
                    ? LowerValueCopy(arguments[0].Expression)
                    : LowerDynamicArguments(arguments.Select(argument => argument.Expression));
                lowered.Add(new IrCallArgument(dynamicArguments));
            }

            return ConvertDynamicResult(
                new IrRuntimeCallExpression(
                    arguments.IsDefaultOrEmpty
                        ? IrRuntimeMethod.DynamicGetMember
                        : IrRuntimeMethod.DynamicGetIndexedMember,
                    lowered.ToImmutable(),
                    IsDynamicResultConverted(resultType) ? TypeSymbol.Variant : resultType),
                resultType);
        }

        /// <summary>
        /// The dynamic dispatch returns <c>object</c>. When the bound tree already knows the
        /// member is numeric, the call has to be converted rather than left on the stack as the
        /// declared type - otherwise the backend reads the boxed reference itself, which shows up
        /// as a plausible but wrong number that changes with every allocation.
        /// </summary>
        private static bool IsDynamicResultConverted(TypeSymbol type) =>
            type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long ||
            type == TypeSymbol.LongLong || type == TypeSymbol.LongPtr || type == TypeSymbol.UShort ||
            type == TypeSymbol.UInteger || type == TypeSymbol.ULong || type == TypeSymbol.Currency ||
            type == TypeSymbol.Date || type == TypeSymbol.Single || type == TypeSymbol.Double ||
            type == TypeSymbol.Boolean;

        private static IrExpression ConvertDynamicResult(IrExpression call, TypeSymbol resultType)
        {
            if (!IsDynamicResultConverted(resultType))
            {
                return call;
            }

            var method = resultType == TypeSymbol.Byte ? IrRuntimeMethod.ConvertCByte
                : resultType == TypeSymbol.Integer ? IrRuntimeMethod.ConvertCInt
                : resultType == TypeSymbol.Long ? IrRuntimeMethod.ConvertCLng
                : resultType == TypeSymbol.LongLong ? IrRuntimeMethod.ConvertCLngLng
                : resultType == TypeSymbol.LongPtr ? IrRuntimeMethod.ConvertCLngPtr
                : resultType == TypeSymbol.UShort ? IrRuntimeMethod.ConvertCUShort
                : resultType == TypeSymbol.UInteger ? IrRuntimeMethod.ConvertCUInt
                : resultType == TypeSymbol.ULong ? IrRuntimeMethod.ConvertCULng
                : resultType == TypeSymbol.Currency ? IrRuntimeMethod.ConvertCCur
                : resultType == TypeSymbol.Date ? IrRuntimeMethod.ConvertCDate
                : resultType == TypeSymbol.Single ? IrRuntimeMethod.ConvertCSng
                : resultType == TypeSymbol.Double ? IrRuntimeMethod.ConvertCDbl
                : IrRuntimeMethod.ConvertCBool;

            return new IrRuntimeCallExpression(
                method,
                ImmutableArray.Create(new IrCallArgument(call)),
                resultType);
        }

        private IrExpression LowerDynamicInvoke(
            BoundExpression receiver,
            string memberName,
            ImmutableArray<BoundArgument> arguments,
            TypeSymbol resultType)
        {
            var dynamicArguments = arguments.Length == 1 && arguments[0].Parameter?.IsParamArray == true
                ? LowerValueCopy(arguments[0].Expression)
                : LowerDynamicArguments(arguments.Select(argument => argument.Expression));
            return ConvertDynamicResult(
                new IrRuntimeCallExpression(
                    IrRuntimeMethod.DynamicInvokeMember,
                    ImmutableArray.Create(
                        new IrCallArgument(LowerExpression(receiver)),
                        new IrCallArgument(new IrConstantExpression(memberName, TypeSymbol.String)),
                        new IrCallArgument(dynamicArguments)),
                    IsDynamicResultConverted(resultType) ? TypeSymbol.Variant : resultType),
                resultType);
        }

        private IrExpression LowerDynamicSet(
            BoundExpression receiver,
            string memberName,
            ImmutableArray<BoundArgument> arguments,
            IrExpression value)
        {
            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(arguments.IsDefaultOrEmpty ? 3 : 4);
            lowered.Add(new IrCallArgument(LowerExpression(receiver)));
            lowered.Add(new IrCallArgument(new IrConstantExpression(memberName, TypeSymbol.String)));
            if (!arguments.IsDefaultOrEmpty)
            {
                var dynamicArguments = arguments.Length == 1 && arguments[0].Parameter?.IsParamArray == true
                    ? LowerValueCopy(arguments[0].Expression)
                    : LowerDynamicArguments(arguments.Select(argument => argument.Expression));
                lowered.Add(new IrCallArgument(dynamicArguments));
            }

            lowered.Add(new IrCallArgument(value));
            return new IrRuntimeCallExpression(
                arguments.IsDefaultOrEmpty
                    ? IrRuntimeMethod.DynamicSetMember
                    : IrRuntimeMethod.DynamicSetIndexedMember,
                lowered.ToImmutable(),
                TypeSymbol.Error);
        }

        private IrExpression LowerDynamicArguments(IEnumerable<BoundExpression> arguments)
        {
            var arrayType = new ArrayTypeSymbol(TypeSymbol.Variant);
            return LowerArrayLiteral(new BoundArrayLiteralExpression(
                arrayType,
                arguments.Select(argument => BindDynamicArgument(argument)).ToImmutableArray()));
        }

        private BoundExpression BindDynamicArgument(BoundExpression expression) =>
            expression.Type == TypeSymbol.Variant
                ? expression
                : new BoundConversionExpression(TypeSymbol.Variant, expression);

        private IrExpression LowerCollectionProcedure(
            ProcedureSymbol procedure,
            BoundExpression receiver,
            ImmutableArray<BoundArgument> arguments)
        {
            var method = procedure.Name.Equals("Add", StringComparison.OrdinalIgnoreCase)
                ? IrRuntimeMethod.CollectionAdd
                : procedure.Name.Equals("Remove", StringComparison.OrdinalIgnoreCase)
                ? IrRuntimeMethod.CollectionRemove
                : throw new NotSupportedException(
                    $"Collection procedure '{procedure.Name}' has no managed runtime implementation.");

            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(arguments.Length + 1);
            lowered.Add(new IrCallArgument(LowerExpression(receiver)));
            lowered.AddRange(arguments.Select(argument =>
                new IrCallArgument(LowerValueCopy(argument.Expression))));
            return new IrRuntimeCallExpression(method, lowered.ToImmutable(), procedure.ReturnType ?? TypeSymbol.Error);
        }

        private IrPlace LowerMemberPlace(BoundMemberAccessExpression expression)
        {
            var receiver = LowerPlace(expression.Receiver);
            return new IrFieldPlace(receiver, _program.GetField(expression.Member));
        }

        private IrPlace LowerWithPlace(BoundWithReceiverExpression expression)
        {
            if (!_withPlaces.TryGetValue(expression.WithId, out var place))
            {
                throw new InvalidOperationException($"With receiver {expression.WithId} is not active while lowering.");
            }

            return place;
        }

        /// <summary>
        /// Reads a variable. A VB6 <c>Const</c> - and the built-in and Enum constants, which are
        /// modelled the same way - is not storage: it is the only module-level declaration that
        /// carries an initializer, and nothing ever assigns to it. So its value is substituted
        /// here instead of being emitted as a field that would need a module initializer to fill.
        /// </summary>
        private IrExpression LowerVariableRead(VariableSymbol symbol)
        {
            if (symbol is ModuleVariableSymbol application && IsApplicationObject(application))
            {
                return Runtime(IrRuntimeMethod.InteractionApplication, application.Type);
            }

            if (symbol is ModuleVariableSymbol screen && IsScreenObject(screen))
            {
                return Runtime(IrRuntimeMethod.InteractionScreen, screen.Type);
            }

            if (symbol is ModuleVariableSymbol printer && IsPrinterObject(printer))
            {
                return Runtime(IrRuntimeMethod.InteractionPrinter, printer.Type);
            }

            if (symbol is ModuleVariableSymbol module &&
                _program.TryGetConstantValue(module, out var value))
            {
                return LowerExpression(value);
            }

            if (symbol is LocalVariableSymbol { IsAsNew: true } local &&
                local.Type is ClassTypeSymbol classType &&
                _locals.TryGetValue(local, out var irLocal))
            {
                return new IrEnsureLocalClassExpression(irLocal, classType);
            }

            return new IrLoadExpression(LowerVariablePlace(symbol));
        }

        private static IrExpression LowerApplicationProperty(string name) =>
            name.ToUpperInvariant() switch
            {
                "EXENAME" => Runtime(IrRuntimeMethod.InteractionApplicationExeName, TypeSymbol.String),
                "PATH" => Runtime(IrRuntimeMethod.InteractionApplicationPath, TypeSymbol.String),
                "TITLE" => Runtime(IrRuntimeMethod.InteractionApplicationTitle, TypeSymbol.String),
                "HINSTANCE" => Runtime(IrRuntimeMethod.InteractionApplicationHInstance, TypeSymbol.Long),
                "MAJOR" => Runtime(IrRuntimeMethod.InteractionApplicationMajor, TypeSymbol.Long),
                "MINOR" => Runtime(IrRuntimeMethod.InteractionApplicationMinor, TypeSymbol.Long),
                "REVISION" => Runtime(IrRuntimeMethod.InteractionApplicationRevision, TypeSymbol.Long),
                _ => throw new NotSupportedException($"App property '{name}' has no runtime contract.")
            };

        private static bool IsApplicationObject(BoundExpression expression) =>
            expression.Type is ClassTypeSymbol classType &&
            ReferenceEquals(classType, VBStandardTypes.App);

        private static IrExpression LowerScreenProperty(string name) =>
            name.ToUpperInvariant() switch
            {
                "ACTIVEFORM" => Runtime(IrRuntimeMethod.InteractionScreenActiveForm, VBStandardTypes.Form),
                "ACTIVECONTROL" => Runtime(IrRuntimeMethod.InteractionScreenActiveControl, VBStandardTypes.Control),
                "TWIPSPERPIXELX" => Runtime(IrRuntimeMethod.InteractionScreenTwipsPerPixelX, TypeSymbol.Single),
                "TWIPSPERPIXELY" => Runtime(IrRuntimeMethod.InteractionScreenTwipsPerPixelY, TypeSymbol.Single),
                "MOUSEPOINTER" => Runtime(IrRuntimeMethod.InteractionScreenMousePointer, TypeSymbol.Long),
                _ => throw new NotSupportedException($"Screen property '{name}' has no runtime contract.")
            };

        private static IrExpression LowerScreenPropertySet(string name, IrExpression value) =>
            name.ToUpperInvariant() switch
            {
                "MOUSEPOINTER" => Runtime(IrRuntimeMethod.InteractionScreenSetMousePointer, TypeSymbol.Error, value),
                _ => throw new NotSupportedException($"Screen property '{name}' is read-only.")
            };

        private static bool IsScreenObject(BoundExpression expression) =>
            expression.Type is ClassTypeSymbol classType &&
            ReferenceEquals(classType, VBStandardTypes.Screen);

        private static IrExpression LowerPrinterProperty(PropertySymbol property)
        {
            var name = new IrConstantExpression(property.Name, TypeSymbol.String);
            if (property.Type == TypeSymbol.String)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterGetString, TypeSymbol.String, name);
            }

            if (property.Type == TypeSymbol.Long)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterGetLong, TypeSymbol.Long, name);
            }

            if (property.Type == TypeSymbol.Single)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterGetSingle, TypeSymbol.Single, name);
            }

            if (property.Type == TypeSymbol.Boolean)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterGetBoolean, TypeSymbol.Boolean, name);
            }

            if (ReferenceEquals(property.Type, VBStandardTypes.Font))
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterGetObject, property.Type, name);
            }

            throw new NotSupportedException($"Printer property '{property.Name}' has no runtime Get contract.");
        }

        private static IrExpression LowerPrinterPropertySet(PropertySymbol property, IrExpression value)
        {
            var name = new IrConstantExpression(property.Name, TypeSymbol.String);
            if (property.Type == TypeSymbol.String)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterSetString, TypeSymbol.Error, name, value);
            }

            if (property.Type == TypeSymbol.Long)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterSetLong, TypeSymbol.Error, name, value);
            }

            if (property.Type == TypeSymbol.Single)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterSetSingle, TypeSymbol.Error, name, value);
            }

            if (property.Type == TypeSymbol.Boolean)
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterSetBoolean, TypeSymbol.Error, name, value);
            }

            if (ReferenceEquals(property.Type, VBStandardTypes.Font))
            {
                return Runtime(IrRuntimeMethod.InteractionPrinterSetObject, TypeSymbol.Error, name, value);
            }

            throw new NotSupportedException($"Printer property '{property.Name}' has no runtime Set contract.");
        }

        private IrExpression LowerPrinterProcedure(
            ProcedureSymbol procedure,
            ImmutableArray<BoundArgument> arguments)
        {
            var lowered = arguments.Select(argument => LowerValueCopy(argument.Expression)).ToArray();
            return procedure.Name.ToUpperInvariant() switch
            {
                "PRINT" => Runtime(IrRuntimeMethod.InteractionPrinterPrint, TypeSymbol.Error, lowered),
                "NEWPAGE" => Runtime(IrRuntimeMethod.InteractionPrinterNewPage, TypeSymbol.Error),
                "ENDDOC" => Runtime(IrRuntimeMethod.InteractionPrinterEndDoc, TypeSymbol.Error),
                "KILLDOC" => Runtime(IrRuntimeMethod.InteractionPrinterKillDoc, TypeSymbol.Error),
                "TEXTWIDTH" => Runtime(IrRuntimeMethod.InteractionPrinterTextWidth, TypeSymbol.Single, lowered),
                "TEXTHEIGHT" => Runtime(IrRuntimeMethod.InteractionPrinterTextHeight, TypeSymbol.Single, lowered),
                "SCALEX" => Runtime(IrRuntimeMethod.InteractionPrinterScaleX, TypeSymbol.Single, lowered),
                "SCALEY" => Runtime(IrRuntimeMethod.InteractionPrinterScaleY, TypeSymbol.Single, lowered),
                "PAINTPICTURE" => Runtime(IrRuntimeMethod.InteractionPrinterPaintPicture, TypeSymbol.Error, lowered),
                _ => throw new NotSupportedException(
                    $"Printer procedure '{procedure.Name}' has no managed runtime implementation.")
            };
        }

        private static bool IsPrinterObject(BoundExpression expression) =>
            expression.Type is ClassTypeSymbol classType &&
            ReferenceEquals(classType, VBStandardTypes.Printer);

        private static bool IsClipboardGetText(BoundExpression receiver, string propertyName) =>
            IsClipboardObject(receiver) &&
            string.Equals(propertyName, "GetText", StringComparison.OrdinalIgnoreCase);

        private static bool IsClipboardObject(BoundExpression receiver) =>
            receiver.Type is ClassTypeSymbol classType &&
            ReferenceEquals(classType, VBStandardTypes.Clipboard);

        private IrExpression LowerClipboardProcedure(
            ProcedureSymbol procedure,
            ImmutableArray<BoundArgument> arguments)
        {
            var lowered = arguments.Select(argument => LowerValueCopy(argument.Expression)).ToArray();
            return procedure.Name.ToUpperInvariant() switch
            {
                "CLEAR" => Runtime(IrRuntimeMethod.InteractionClipboardClear, TypeSymbol.Error),
                "GETDATA" => Runtime(IrRuntimeMethod.InteractionClipboardGetData, TypeSymbol.Variant, lowered),
                "GETFORMAT" => Runtime(IrRuntimeMethod.InteractionClipboardGetFormat, TypeSymbol.Boolean, lowered),
                "GETTEXT" => Runtime(IrRuntimeMethod.InteractionClipboardGetText, TypeSymbol.String, lowered),
                "SETDATA" => Runtime(IrRuntimeMethod.InteractionClipboardSetData, TypeSymbol.Error, lowered),
                "SETTEXT" => Runtime(IrRuntimeMethod.InteractionClipboardSetText, TypeSymbol.Error, lowered),
                _ => throw new NotSupportedException(
                    $"Clipboard procedure '{procedure.Name}' has no managed runtime implementation.")
            };
        }

        private static bool IsRuntimeObject(BoundExpression expression) =>
            expression.Type is ClassTypeSymbol classType &&
            (classType.IsRuntimeObjectContract ||
             classType.IsLateBoundObject ||
             classType.IsControlContract);

        private static bool IsApplicationObject(ModuleVariableSymbol symbol) =>
            ReferenceEquals(symbol.Type, VBStandardTypes.App);

        private static bool IsScreenObject(ModuleVariableSymbol symbol) =>
            ReferenceEquals(symbol.Type, VBStandardTypes.Screen);

        private static bool IsPrinterObject(ModuleVariableSymbol symbol) =>
            ReferenceEquals(symbol.Type, VBStandardTypes.Printer);

        private IrPlace LowerVariablePlace(VariableSymbol symbol)
        {
            return symbol switch
            {
                LocalVariableSymbol local when _locals.TryGetValue(local, out var irLocal) => new IrLocalPlace(irLocal),
                ParameterSymbol parameter when _parameters.TryGetValue(parameter, out var irParameter) => new IrParameterPlace(irParameter),
                ModuleVariableSymbol module when _containingClass is not null &&
                    _program.TryGetClassField(module, out var field) =>
                    new IrFieldPlace(new IrThisPlace(_containingClass), field),
                ModuleVariableSymbol module when _containingClass is not null &&
                    string.Equals(module.Name, "Me", StringComparison.OrdinalIgnoreCase) &&
                    module.Type == _containingClass => new IrThisPlace(_containingClass),
                ModuleVariableSymbol global => new IrGlobalPlace(_program.GetGlobal(global)),
                ReturnValueSymbol when _returnLocal is not null => new IrLocalPlace(_returnLocal),
                _ => throw new InvalidOperationException($"Variable '{symbol.Name}' is not available in the current IR procedure.")
            };
        }

        private IrExpression LowerCall(
            ProcedureSymbol procedure,
            ImmutableArray<BoundArgument> arguments,
            IrExpression? receiver = null)
        {
            if (receiver is null && _containingClass is not null &&
                _program.TryGetClassProcedure(
                    _containingClass,
                    procedure.Name,
                    procedure.PropertyAccessor,
                    out var classProcedure))
            {
                procedure = classProcedure;
                receiver = new IrLoadExpression(new IrThisPlace(_containingClass));
            }

            var omittedRndArgument = procedure.IntrinsicKind == VBIntrinsicKind.Rnd &&
                arguments.Length == 1 &&
                arguments[0].IsOmitted;
            var effectiveArguments = omittedRndArgument
                ? ImmutableArray<BoundArgument>.Empty
                : arguments;
            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(effectiveArguments.Length);
            foreach (var argument in effectiveArguments)
            {
                if (procedure.IsExternal && argument.Parameter?.IsAny == true && argument.IsByValAtCallSite)
                {
                    lowered.Add(LowerAnyPointerArgument(argument.Expression));
                    continue;
                }

                if (procedure.IsExternal &&
                    argument.Parameter?.PassingMode == ParameterPassingMode.ByVal &&
                    argument.Parameter.Type == TypeSymbol.String)
                {
                    var writeBackPlace = TryLowerPlace(argument.Expression);
                    var bufferTemporary = writeBackPlace is null
                        ? null
                        : NewLocal("__declare_buffer", TypeSymbol.Variant, compilerGenerated: true);
                    var writeBackTemporary = writeBackPlace is null
                        ? null
                        : NewLocal("__declare_string", TypeSymbol.String, compilerGenerated: true);
                    lowered.Add(new IrCallArgument(
                        LowerValueCopy(argument.Expression),
                        IrCallArgumentKind.StringBuffer,
                        writeBackPlace,
                        bufferTemporary,
                        writeBackTemporary));
                    continue;
                }

                if (procedure.IsExternal &&
                    argument.Parameter?.PassingMode == ParameterPassingMode.ByRef &&
                    argument.Parameter.Type is ArrayTypeSymbol)
                {
                    var writeBackPlace = TryLowerPlace(argument.Expression);
                    var bufferTemporary = NewLocal(
                        "__declare_array",
                        VBStandardTypes.Object,
                        compilerGenerated: true);
                    var writeBackTemporary = writeBackPlace is null
                        ? null
                        : NewLocal(
                            "__declare_array_result",
                            argument.Parameter.Type,
                            compilerGenerated: true);
                    lowered.Add(new IrCallArgument(
                        LowerValueCopy(argument.Expression),
                        IrCallArgumentKind.ArrayBuffer,
                        writeBackPlace,
                        bufferTemporary,
                        writeBackTemporary));
                    continue;
                }

                if (argument.Parameter?.PassingMode == ParameterPassingMode.ByRef)
                {
                    if (!argument.RequiresByRefTemporary &&
                        StripConversions(argument.Expression) is BoundVariantArrayAccessExpression variantElement)
                    {
                        lowered.Add(LowerVariantArrayElementByRefArgument(
                            argument.Parameter!,
                            variantElement));
                        continue;
                    }

                    // Passing a local As New variable by reference is still a reference to the
                    // variable in VB6, so its implicit instance must exist before the callee can
                    // observe or replace the storage.  LowerPlace deliberately yields only an
                    // address and would otherwise bypass the lazy read above.
                    if (!argument.RequiresByRefTemporary &&
                        StripConversions(argument.Expression) is BoundVariableExpression
                        {
                            Variable: LocalVariableSymbol { IsAsNew: true } asNew
                        })
                    {
                        Emit(new IrEvaluateInstruction(LowerVariableRead(asNew)));
                    }

                    IrPlace place;
                    if (argument.RequiresByRefTemporary)
                    {
                        var temp = NewLocal("__byref_temp", argument.Parameter.Type, true);
                        Emit(new IrStoreInstruction(new IrLocalPlace(temp), LowerExpression(argument.Expression)));
                        place = new IrLocalPlace(temp);
                    }
                    else
                    {
                        place = LowerPlace(argument.Expression);
                    }

                    lowered.Add(new IrCallArgument(new IrAddressExpression(place), IrCallArgumentKind.Address));
                }
                else
                {
                    lowered.Add(new IrCallArgument(procedure.IntrinsicTarget is null
                        ? LowerAssignedValueCopy(argument.Expression)
                        : LowerValueCopy(argument.Expression)));
                }
            }

            if (procedure.IntrinsicTarget is not null)
            {
                var runtimeMethod = procedure.IntrinsicKind == VBIntrinsicKind.Rnd
                    ? effectiveArguments.Length == 0
                        ? IrRuntimeMethod.MathRnd
                        : IrRuntimeMethod.MathRndWithNumber
                    : IntrinsicMethod(procedure.IntrinsicTarget);
                return new IrRuntimeCallExpression(
                    runtimeMethod,
                    lowered.ToImmutable(),
                    procedure.ReturnType ?? TypeSymbol.Error);
            }

            var resultTemporary = procedure.ReturnType is not null &&
                                  lowered.Any(argument => argument.WriteBackPlace is not null)
                ? NewLocal("__declare_return", procedure.ReturnType, compilerGenerated: true)
                : null;

            return new IrProcedureCallExpression(
                procedure,
                lowered.ToImmutable(),
                procedure.ReturnType ?? TypeSymbol.Error,
                receiver,
                resultTemporary);
        }

        /// <summary>
        /// A Variant index may address a native VBArray, a CLR/SAFEARRAY-backed Array, or a
        /// writable default member. Only the first form exposes a CLR managed reference. Spill
        /// the receiver, indices, and value to locals so the callee still receives a real ByRef
        /// variable, then write the changed local back through the dynamic element contract.
        /// </summary>
        private IrCallArgument LowerVariantArrayElementByRefArgument(
            ParameterSymbol parameter,
            BoundVariantArrayAccessExpression element)
        {
            var receiverTemporary = NewLocal(
                "__variant_byref_receiver",
                element.Receiver.Type,
                compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(receiverTemporary),
                LowerExpression(element.Receiver)));

            var indices = ImmutableArray.CreateBuilder<IrExpression>(element.Indices.Length);
            foreach (var index in element.Indices)
            {
                var indexTemporary = NewLocal(
                    "__variant_byref_index",
                    index.Type,
                    compilerGenerated: true);
                Emit(new IrStoreInstruction(
                    new IrLocalPlace(indexTemporary),
                    LowerExpression(index)));
                indices.Add(new IrLoadExpression(new IrLocalPlace(indexTemporary)));
            }

            var receiver = new IrLoadExpression(new IrLocalPlace(receiverTemporary));
            var capturedIndices = indices.ToImmutable();
            var elementPlace = new IrVariantArrayElementPlace(receiver, capturedIndices);
            var valueTemporary = NewLocal("__variant_byref_value", parameter.Type, compilerGenerated: true);
            Emit(new IrStoreInstruction(
                new IrLocalPlace(valueTemporary),
                new IrVariantArrayCallExpression(
                    IrVariantArrayOperation.GetElement,
                    receiver,
                    capturedIndices,
                    TypeSymbol.Variant)));

            return new IrCallArgument(
                new IrAddressExpression(new IrLocalPlace(valueTemporary)),
                IrCallArgumentKind.Address,
                elementPlace);
        }

        private IrPlace? TryLowerPlace(BoundExpression expression)
        {
            try
            {
                return LowerPlace(StripConversions(expression));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private void LowerLSet(BoundInvocationStatement invocation)
        {
            if (invocation.Arguments.Length != 2)
            {
                Emit(new IrEvaluateInstruction(Runtime(IrRuntimeMethod.MemoryLSet, TypeSymbol.Error)));
                return;
            }

            var target = invocation.Arguments[0].Expression;
            var source = invocation.Arguments[1].Expression;

            if (target.Type is FixedLengthStringTypeSymbol || target.Type == TypeSymbol.String)
            {
                var targetPlace = LowerPlace(target);
                Emit(new IrStoreInstruction(
                    targetPlace,
                    LowerFixedStringWrite(target.Type, LowerLSetStringValue(source))));
                return;
            }

            if (target.Type is UserDefinedTypeSymbol targetType && source.Type == targetType)
            {
                Emit(new IrStoreInstruction(
                    LowerPlace(target),
                    LowerValueCopy(source)));
                return;
            }

            if (target.Type is UserDefinedTypeSymbol targetUdt &&
                source.Type is UserDefinedTypeSymbol sourceUdt &&
                IsManagedLSetLayoutSupported(targetUdt) &&
                IsManagedLSetLayoutSupported(sourceUdt))
            {
                Emit(new IrEvaluateInstruction(new IrRuntimeCallExpression(
                    IrRuntimeMethod.MemoryLSet,
                    ImmutableArray.Create(
                        new IrCallArgument(
                            new IrAddressExpression(LowerPlace(target)),
                            IrCallArgumentKind.Address),
                        new IrCallArgument(LowerValueCopy(source))),
                    TypeSymbol.Error)));
                return;
            }

            // Layouts containing references, arrays, dynamic strings, or native-width fields are
            // not safe to treat as a raw managed record. Keep those cases diagnostically guarded.
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.MemoryLSet,
                TypeSymbol.Error,
                LowerValueCopy(target),
                LowerValueCopy(source))));
        }

        private void LowerRSet(BoundInvocationStatement invocation)
        {
            if (invocation.Arguments.Length != 2)
            {
                Emit(new IrEvaluateInstruction(Runtime(IrRuntimeMethod.MemoryRSet, TypeSymbol.Error)));
                return;
            }

            var target = invocation.Arguments[0].Expression;
            var source = invocation.Arguments[1].Expression;

            if (target.Type is FixedLengthStringTypeSymbol fixedString)
            {
                var targetPlace = LowerPlace(target);
                var aligned = new IrRuntimeCallExpression(
                    IrRuntimeMethod.FixedStringRightAlign,
                    ImmutableArray.Create(
                        new IrCallArgument(LowerRSetStringValue(source)),
                        new IrCallArgument(new IrConstantExpression((long)fixedString.Length, TypeSymbol.Long))),
                    TypeSymbol.String);
                Emit(new IrStoreInstruction(
                    targetPlace,
                    LowerFixedStringWrite(target.Type, aligned)));
                return;
            }

            if (target.Type == TypeSymbol.String)
            {
                Emit(new IrStoreInstruction(
                    LowerPlace(target),
                    LowerRSetStringValue(source)));
                return;
            }

            // RSet is a string alignment statement. Keep unsupported UDT/Variant layouts on the
            // explicit runtime guard instead of accidentally applying LSet's left-alignment rules.
            Emit(new IrEvaluateInstruction(Runtime(
                IrRuntimeMethod.MemoryRSet,
                TypeSymbol.Error,
                LowerValueCopy(target),
                LowerValueCopy(source))));
        }

        private void LowerMidAssignment(BoundMidAssignmentStatement statement)
        {
            var targetPlace = LowerPlace(statement.Target);
            var current = LowerFixedStringRead(
                statement.Target.Type,
                new IrLoadExpression(targetPlace));
            var length = statement.Length is null
                ? new IrConstantExpression(-1L, TypeSymbol.Long)
                : LowerExpression(statement.Length);
            var replacement = LowerExpression(statement.Replacement);
            var value = Runtime(
                IrRuntimeMethod.StringMidAssign,
                TypeSymbol.String,
                current,
                LowerExpression(statement.Start),
                replacement,
                length);
            Emit(new IrStoreInstruction(
                targetPlace,
                LowerFixedStringWrite(statement.Target.Type, value)));
        }

        private static bool IsManagedLSetLayoutSupported(UserDefinedTypeSymbol type) =>
            TryGetManagedLSetLayout(
                type,
                new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance),
                out _,
                out _);

        private static bool TryGetManagedLSetLayout(
            UserDefinedTypeSymbol type,
            HashSet<UserDefinedTypeSymbol> activePath,
            out int size,
            out int alignment)
        {
            size = 0;
            alignment = 1;
            if (!activePath.Add(type) || type.Members.IsDefaultOrEmpty)
            {
                return false;
            }

            foreach (var member in type.Members)
            {
                if (!TryGetManagedLSetFieldLayout(member.Type, activePath, out var fieldSize, out var fieldAlignment))
                {
                    activePath.Remove(type);
                    size = 0;
                    alignment = 1;
                    return false;
                }

                fieldAlignment = Math.Min(fieldAlignment, 4);
                size = Align(size, fieldAlignment);
                size = checked(size + fieldSize);
                alignment = Math.Max(alignment, fieldAlignment);
            }

            size = Align(size, alignment);
            activePath.Remove(type);
            return true;
        }

        private static bool TryGetManagedLSetFieldLayout(
            TypeSymbol type,
            HashSet<UserDefinedTypeSymbol> activePath,
            out int size,
            out int alignment)
        {
            if (type is UserDefinedTypeSymbol nested)
            {
                return TryGetManagedLSetLayout(nested, activePath, out size, out alignment);
            }

            size = type == TypeSymbol.Boolean || type == TypeSymbol.Integer || type == TypeSymbol.UShort ? 2
                : type == TypeSymbol.Byte ? 1
                : type == TypeSymbol.Long || type == TypeSymbol.UInteger || type == TypeSymbol.Single ? 4
                : type == TypeSymbol.LongPtr ? IntPtr.Size
                : type == TypeSymbol.LongLong || type == TypeSymbol.ULong ||
                  type == TypeSymbol.Date || type == TypeSymbol.Double || type == TypeSymbol.Currency ? 8
                : 0;
            alignment = size;
            return size > 0;
        }

        private static int Align(int offset, int alignment) =>
            checked((offset + alignment - 1) / alignment * alignment);

        private IrExpression LowerLSetStringValue(BoundExpression source)
        {
            var value = LowerExpression(source);
            return source.Type == TypeSymbol.String || source.Type is FixedLengthStringTypeSymbol
                ? value
                : Runtime(IrRuntimeMethod.ConvertCStr, TypeSymbol.String, value);
        }

        private IrExpression LowerRSetStringValue(BoundExpression source)
        {
            var value = LowerExpression(source);
            return source.Type == TypeSymbol.String || source.Type is FixedLengthStringTypeSymbol
                ? value
                : Runtime(IrRuntimeMethod.ConvertCStr, TypeSymbol.String, value);
        }

        private IrExpression LowerAnyPointerValue(BoundExpression expression)
        {
            if (StripConversions(expression) is BoundInvocationExpression invocation &&
                invocation.Procedure.IntrinsicKind == VBIntrinsicKind.VarPtr &&
                invocation.Arguments.Length == 1)
            {
                var target = StripConversions(invocation.Arguments[0].Expression);
                if (target is BoundVariableExpression or
                    BoundArrayAccessExpression or
                    BoundElementAccessExpression or
                    BoundMemberAccessExpression)
                {
                    return new IrAddressExpression(LowerPlace(target));
                }
            }

            return LowerExpression(expression);
        }

        private IrCallArgument LowerAnyPointerArgument(BoundExpression expression)
        {
            if (StripConversions(expression) is BoundInvocationExpression invocation &&
                invocation.Procedure.IntrinsicKind == VBIntrinsicKind.StrPtr &&
                invocation.Arguments.Length == 1)
            {
                var target = invocation.Arguments[0].Expression;
                var writeBackPlace = TryLowerPlace(target);
                var bufferTemporary = NewLocal(
                    "__declare_strptr",
                    TypeSymbol.Variant,
                    compilerGenerated: true);
                var writeBackTemporary = writeBackPlace is null
                    ? null
                    : NewLocal(
                        "__declare_strptr_result",
                        TypeSymbol.String,
                        compilerGenerated: true);
                return new IrCallArgument(
                    LowerValueCopy(target),
                    IrCallArgumentKind.StringPointer,
                    writeBackPlace,
                    bufferTemporary,
                    writeBackTemporary);
            }

            return new IrCallArgument(LowerAnyPointerValue(expression));
        }

        private static BoundExpression StripConversions(BoundExpression expression)
        {
            while (expression is BoundConversionExpression conversion)
            {
                expression = conversion.Expression;
            }

            return expression;
        }

        /// <summary>
        /// Demands an object where VB6 does. A declared object type is already guaranteed by the
        /// type system, so only a Variant needs the run-time check - and it needs it, because an
        /// Empty Variant is the same CLR null reference a concrete slot uses for Nothing.
        /// </summary>
        private static IrExpression RequireObjectOperand(BoundExpression expression, IrExpression value) =>
            expression.Type == TypeSymbol.Variant
                ? new IrRuntimeCallExpression(
                    IrRuntimeMethod.ObjectRequireOperand,
                    ImmutableArray.Create(new IrCallArgument(value)),
                    TypeSymbol.Variant)
                : value;

        /// <summary>
        /// A value copy that also honours VB6's copy-on-assignment rule for arrays. Whether a
        /// Variant carries an array is only known at run time, so the decision cannot be made here
        /// the way it is for a UDT; the runtime call passes every other payload straight through.
        /// Reading intrinsics keep the plain <see cref="LowerValueCopy"/> - copying an array just
        /// to ask for its bounds would be pure waste.
        /// </summary>
        private IrExpression LowerAssignedValueCopy(BoundExpression expression, bool requireObject = false)
        {
            var value = LowerValueCopy(expression);
            if (expression.Type != TypeSymbol.Variant)
            {
                return value;
            }

            // A Set source must be an object; an array or scalar never reaches the copy rule.
            return requireObject
                ? RequireObjectOperand(expression, value)
                : new IrRuntimeCallExpression(
                    IrRuntimeMethod.ArrayCopyAssignedValue,
                    ImmutableArray.Create(new IrCallArgument(value)),
                    TypeSymbol.Variant);
        }

        private IrExpression LowerValueCopy(BoundExpression expression)
        {
            var value = LowerExpression(expression);
            if (expression.Type is not UserDefinedTypeSymbol udt || !RequiresDeepCopy(udt))
            {
                return value;
            }

            // The CLR struct copy duplicates the scalar members, but an array member is a
            // reference: source and copy would keep indexing one array. Copying through a
            // temporary and re-creating its arrays makes the result an independent VB6 value
            // wherever the caller stores it.
            var temp = new IrLocalPlace(NewLocal($"__vb6_udt_copy_{_nextLocalId}", udt, compilerGenerated: true));
            Emit(new IrStoreInstruction(temp, value));
            EmitArrayMemberCopies(temp, udt);
            return new IrLoadExpression(temp);
        }

        /// <summary>
        /// Reports whether copying a value of this type needs more than the CLR struct copy - that
        /// is, whether it holds a fixed array member anywhere in its member tree.
        /// </summary>
        private static bool RequiresDeepCopy(UserDefinedTypeSymbol type) =>
            type.Members.Any(member =>
                (member.HasArrayBounds && member.Type is ArrayTypeSymbol) ||
                (member.Type is UserDefinedTypeSymbol nested && RequiresDeepCopy(nested)));

        /// <summary>
        /// Gives every fixed array member below <paramref name="place"/> its own storage. Array
        /// elements of a UDT element type keep sharing their own arrays for now; VB6 copies those
        /// too, but that needs an element-wise copy the runtime only exposes as a callback.
        /// </summary>
        private void EmitArrayMemberCopies(IrPlace place, UserDefinedTypeSymbol type)
        {
            foreach (var member in type.Members)
            {
                var field = new IrFieldPlace(place, _program.GetField(member));
                if (member.HasArrayBounds && member.Type is ArrayTypeSymbol arrayType)
                {
                    Emit(new IrStoreInstruction(
                        field,
                        new IrCopyArrayExpression(
                            new IrLoadExpression(field),
                            arrayType,
                            MemberArrayBounds(member))));
                }
                else if (member.Type is UserDefinedTypeSymbol nested && RequiresDeepCopy(nested))
                {
                    EmitArrayMemberCopies(field, nested);
                }
            }
        }

        private static ImmutableArray<IrArrayBound> MemberArrayBounds(UserDefinedTypeMemberSymbol member) =>
            member.ArrayBounds
                .Select(bound => new IrArrayBound(
                    new IrConstantExpression(bound.Lower, TypeSymbol.Long),
                    new IrConstantExpression(bound.Upper, TypeSymbol.Long)))
                .ToImmutableArray();

        private static IEnumerable<ImmutableArray<IrExpression>> EnumerateArrayIndices(
            ImmutableArray<UserDefinedTypeArrayBound> bounds)
        {
            if (bounds.IsDefaultOrEmpty)
            {
                yield break;
            }

            var indices = new long[bounds.Length];
            foreach (var combination in EnumerateArrayIndices(bounds, indices, 0))
            {
                yield return combination;
            }
        }

        private static IEnumerable<ImmutableArray<IrExpression>> EnumerateArrayIndices(
            ImmutableArray<UserDefinedTypeArrayBound> bounds,
            long[] indices,
            int dimension)
        {
            if (dimension == bounds.Length)
            {
                yield return indices
                    .Select(index => (IrExpression)new IrConstantExpression(index, TypeSymbol.Long))
                    .ToImmutableArray();
                yield break;
            }

            var bound = bounds[dimension];
            if (bound.Upper < bound.Lower)
            {
                throw new NotSupportedException("UDT array bounds must be non-empty for binary transfer.");
            }

            var count = checked(bound.Upper - bound.Lower + 1);
            if (count > int.MaxValue)
            {
                throw new NotSupportedException("UDT array dimensions are too large for managed record emission.");
            }

            for (var index = bound.Lower; ; index++)
            {
                indices[dimension] = index;
                foreach (var combination in EnumerateArrayIndices(bounds, indices, dimension + 1))
                {
                    yield return combination;
                }

                if (index == bound.Upper)
                {
                    break;
                }
            }
        }

        private IrExpression LowerConversion(BoundConversionExpression conversion)
        {
            if (conversion.Expression is BoundAddressOfExpression addressOf &&
                (conversion.TargetType == TypeSymbol.Long || conversion.TargetType == TypeSymbol.LongPtr))
            {
                return new IrAddressOfExpression(addressOf.Procedure, conversion.TargetType);
            }

            // Nothing travels as an identity-bearing sentinel while it is a Variant, so the
            // Variant predicates and file format can distinguish it from Empty.  A concrete
            // object slot, however, must receive the CLR null reference.  In particular, a
            // local As New slot relies on that null to reactivate itself at the next reference.
            // The generic Object type is a class symbol too, but its storage stays Variant-shaped
            // and keeps the sentinel so a SAFEARRAY element remains distinguishable from Empty.
            if (conversion.TargetType is ClassTypeSymbol classTarget &&
                !ReferenceEquals(classTarget, VBStandardTypes.Object) &&
                conversion.Expression is BoundInvocationExpression
                {
                    Procedure.IntrinsicKind: VBIntrinsicKind.Nothing
                })
            {
                return new IrNullExpression(conversion.TargetType);
            }

            var operand = LowerExpression(conversion.Expression);
            if (conversion.TargetType == conversion.Expression.Type)
            {
                return operand;
            }

            // The mirror image of the rule above. A declared object slot holds the CLR null
            // reference for Nothing, and a Variant reads that as Empty - VarType, TypeName and
            // IsObject would all answer for the wrong state. Boxing it re-attaches the sentinel.
            if (conversion.TargetType == TypeSymbol.Variant &&
                conversion.Expression.Type is ClassTypeSymbol)
            {
                return Runtime(IrRuntimeMethod.ObjectToVariant, TypeSymbol.Variant, operand);
            }

            var method = conversion.TargetType == TypeSymbol.Variant && conversion.Expression.Type == TypeSymbol.Date
                ? IrRuntimeMethod.DateToVariant
                : conversion.TargetType == TypeSymbol.Boolean && conversion.Expression.Type == TypeSymbol.Variant
                    ? IrRuntimeMethod.VariantToBoolean
                : conversion.TargetType == TypeSymbol.Byte ? IrRuntimeMethod.ConvertCByte
                : conversion.TargetType == TypeSymbol.Integer ? IrRuntimeMethod.ConvertCInt
                : conversion.TargetType == TypeSymbol.Long ? IrRuntimeMethod.ConvertCLng
                : conversion.TargetType == TypeSymbol.LongLong ? IrRuntimeMethod.ConvertCLngLng
                : conversion.TargetType == TypeSymbol.LongPtr ? IrRuntimeMethod.ConvertCLngPtr
                : conversion.TargetType == TypeSymbol.UShort ? IrRuntimeMethod.ConvertCUShort
                : conversion.TargetType == TypeSymbol.UInteger ? IrRuntimeMethod.ConvertCUInt
                : conversion.TargetType == TypeSymbol.ULong ? IrRuntimeMethod.ConvertCULng
                : conversion.TargetType == TypeSymbol.Currency ? IrRuntimeMethod.ConvertCCur
                : conversion.TargetType == TypeSymbol.Date ? IrRuntimeMethod.ConvertCDate
                : conversion.TargetType == TypeSymbol.Single ? IrRuntimeMethod.ConvertCSng
                : conversion.TargetType == TypeSymbol.Double ? IrRuntimeMethod.ConvertCDbl
                : conversion.TargetType == TypeSymbol.Boolean ? IrRuntimeMethod.ConvertCBool
                : conversion.TargetType == TypeSymbol.String ? IrRuntimeMethod.ConvertCStr
                : (IrRuntimeMethod?)null;
            if (method is null)
            {
                return operand;
            }

            // A Date shares its representation with a Double, so boxing it for a helper that takes
            // object loses the Date. CStr would then render the OLE automation serial number
            // instead of a date - the same value Debug.Print shows, and the same reason.
            if (conversion.TargetType == TypeSymbol.String && conversion.Expression.Type == TypeSymbol.Date)
            {
                operand = Runtime(IrRuntimeMethod.DateToVariant, TypeSymbol.Variant, operand);
            }

            return Runtime(method.Value, conversion.TargetType, operand);
        }

        private IrExpression LowerFileInputValue(IrExpression field, TypeSymbol targetType)
        {
            var method = targetType == TypeSymbol.Byte ? IrRuntimeMethod.CByte
                : targetType == TypeSymbol.Integer ? IrRuntimeMethod.CInt
                : targetType == TypeSymbol.Long ? IrRuntimeMethod.CLng
                : targetType == TypeSymbol.LongLong ? IrRuntimeMethod.CLngLng
                : targetType == TypeSymbol.UShort ? IrRuntimeMethod.CUShort
                : targetType == TypeSymbol.UInteger ? IrRuntimeMethod.CUInt
                : targetType == TypeSymbol.ULong ? IrRuntimeMethod.CULng
                : targetType == TypeSymbol.Currency ? IrRuntimeMethod.CCur
                : targetType == TypeSymbol.Date ? IrRuntimeMethod.CDate
                : targetType == TypeSymbol.Single ? IrRuntimeMethod.CSng
                : targetType == TypeSymbol.Double ? IrRuntimeMethod.CDbl
                : targetType == TypeSymbol.Boolean ? IrRuntimeMethod.CBool
                : (IrRuntimeMethod?)null;
            return method is null
                ? field
                : Runtime(method.Value, targetType, field);
        }

        private IrExpression LowerUnary(BoundUnaryExpression unary)
        {
            var operand = LowerExpression(unary.Operand);
            if (unary.OperatorKind == SyntaxKind.PlusToken)
            {
                return operand;
            }

            var method = unary.OperatorKind switch
            {
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Variant => IrRuntimeMethod.NegateVariant,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.LongLong => IrRuntimeMethod.NegateLongLong,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.LongPtr => IrRuntimeMethod.NegateLongPtr,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.UShort => IrRuntimeMethod.NegateUShort,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.UInteger => IrRuntimeMethod.NegateUInteger,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.ULong => IrRuntimeMethod.NegateULong,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Long => IrRuntimeMethod.NegateLong,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Currency => IrRuntimeMethod.NegateCurrency,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Single => IrRuntimeMethod.NegateSingle,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Double => IrRuntimeMethod.NegateDouble,
                SyntaxKind.MinusToken => IrRuntimeMethod.NegateInteger,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.Boolean => IrRuntimeMethod.NotBoolean,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.LongLong => IrRuntimeMethod.NotLongLong,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.LongPtr => IrRuntimeMethod.NotLongPtr,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.UShort => IrRuntimeMethod.NotUShort,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.UInteger => IrRuntimeMethod.NotUInteger,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.ULong => IrRuntimeMethod.NotULong,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.Long => IrRuntimeMethod.NotLong,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.Variant => IrRuntimeMethod.NotVariant,
                SyntaxKind.NotKeyword => IrRuntimeMethod.NotInteger,
                _ => throw new NotSupportedException($"IR lowering does not support unary operator '{unary.OperatorKind}'.")
            };
            return Runtime(method, unary.ResultType, operand);
        }

        private IrExpression LowerBinary(BoundBinaryExpression binary)
        {
            var left = LowerExpression(binary.Left);
            var right = LowerExpression(binary.Right);
            IrRuntimeMethod method;
            switch (binary.OperatorKind)
            {
                case SyntaxKind.CaretToken: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.PowerVariant
                    : IrRuntimeMethod.Power; break;
                case SyntaxKind.LikeKeyword:
                    return Runtime(
                        IrRuntimeMethod.StringLike,
                        TypeSymbol.Boolean,
                        left,
                        right,
                        new IrConstantExpression(binary.UseTextCompare, TypeSymbol.Boolean));
                case SyntaxKind.IsKeyword:
                    return Runtime(
                        IrRuntimeMethod.ObjectIs,
                        TypeSymbol.Boolean,
                        RequireObjectOperand(binary.Left, left),
                        RequireObjectOperand(binary.Right, right));
                case SyntaxKind.EqualsToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantEqual
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantEqual
                        : IrRuntimeMethod.Equal; break;
                case SyntaxKind.LessGreaterToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantNotEqual
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantNotEqual
                        : IrRuntimeMethod.NotEqual; break;
                case SyntaxKind.LessToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantLess
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantLess
                        : IrRuntimeMethod.Less; break;
                case SyntaxKind.LessOrEqualsToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantLessOrEqual
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantLessOrEqual
                        : IrRuntimeMethod.LessOrEqual; break;
                case SyntaxKind.GreaterToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantGreater
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantGreater
                        : IrRuntimeMethod.Greater; break;
                case SyntaxKind.GreaterOrEqualsToken: method = IsStringVariantComparison(binary)
                    ? IrRuntimeMethod.StringVariantGreaterOrEqual
                    : binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                        ? IrRuntimeMethod.VariantGreaterOrEqual
                        : IrRuntimeMethod.GreaterOrEqual; break;
                case SyntaxKind.AmpersandToken: method = binary.Left.Type == TypeSymbol.Variant || binary.Right.Type == TypeSymbol.Variant
                    ? IrRuntimeMethod.ConcatVariant
                    : IrRuntimeMethod.Concat; break;
                case SyntaxKind.PlusToken when binary.ResultType == TypeSymbol.Variant &&
                    (binary.Left.Type == TypeSymbol.String || binary.Right.Type == TypeSymbol.String):
                    method = IrRuntimeMethod.AddStringVariant; break;
                case SyntaxKind.PlusToken when binary.ResultType == TypeSymbol.String: method = IrRuntimeMethod.Concat; break;
                case SyntaxKind.PlusToken: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.AddVariant
                    : AddMethod(binary.ResultType); break;
                case SyntaxKind.MinusToken: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.SubtractVariant
                    : SubtractMethod(binary.ResultType); break;
                case SyntaxKind.StarToken when binary.ResultType == TypeSymbol.Variant: method = IrRuntimeMethod.MultiplyVariant; break;
                case SyntaxKind.StarToken: method = MultiplyMethod(binary.ResultType); break;
                case SyntaxKind.BackslashToken: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.IntegerDivideVariant
                    : IntegerDivideMethod(binary.ResultType); break;
                case SyntaxKind.ModKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.ModVariant
                    : ModMethod(binary.ResultType); break;
                case SyntaxKind.SlashToken: method = binary.ResultType == TypeSymbol.Single
                    ? IrRuntimeMethod.DivideSingle
                    : binary.ResultType == TypeSymbol.Variant
                        ? IrRuntimeMethod.DivideVariant
                        : IrRuntimeMethod.DivideDouble; break;
                case SyntaxKind.AndKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.AndVariant
                    : LogicMethod("And", binary.ResultType); break;
                case SyntaxKind.OrKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.OrVariant
                    : LogicMethod("Or", binary.ResultType); break;
                case SyntaxKind.XorKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.XorVariant
                    : LogicMethod("Xor", binary.ResultType); break;
                case SyntaxKind.EqvKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.EqvVariant
                    : LogicMethod("Eqv", binary.ResultType); break;
                case SyntaxKind.ImpKeyword: method = binary.ResultType == TypeSymbol.Variant
                    ? IrRuntimeMethod.ImpVariant
                    : LogicMethod("Imp", binary.ResultType); break;
                default: throw new NotSupportedException($"IR lowering does not support binary operator '{binary.OperatorKind}'.");
            }

            var result = Runtime(method, binary.ResultType, left, right);
            return binary.UseTextCompare
                ? result with { UseTextCompare = true }
                : result;
        }

        private static bool IsStringVariantComparison(BoundBinaryExpression binary) =>
            (binary.Left.Type == TypeSymbol.String && binary.Right.Type == TypeSymbol.Variant) ||
            (binary.Left.Type == TypeSymbol.Variant && binary.Right.Type == TypeSymbol.String);

        private ImmutableArray<IrArrayBound> LowerBounds(ImmutableArray<BoundArrayDimension> dimensions) =>
            dimensions.Select(dimension => new IrArrayBound(
                LowerExpression(dimension.LowerBound),
                LowerExpression(dimension.UpperBound))).ToImmutableArray();

        private IrReturnTerminator ReturnTerminator(bool clearsActiveErrorHandler = false) => _returnLocal is null
            ? new IrReturnTerminator(null, clearsActiveErrorHandler)
            : new IrReturnTerminator(
                new IrLoadExpression(new IrLocalPlace(_returnLocal)),
                clearsActiveErrorHandler);

        private void Emit(IrInstruction instruction)
        {
            if (_current.HasTerminator)
            {
                _current = NewBlock("unreachable");
            }

            _current.Instructions.Add(
                instruction.SourceLocation is null && _location is not null
                    ? instruction with { SourceLocation = _location }
                    : instruction);
        }

        private void Terminate(IrTerminator terminator)
        {
            if (!_current.HasTerminator)
            {
                _current.Terminator = terminator.SourceLocation is null && _location is not null
                    ? terminator with { SourceLocation = _location }
                    : terminator;
            }
        }

        private void GotoIfOpen(int target)
        {
            if (!_current.HasTerminator)
            {
                Terminate(new IrGotoTerminator(target));
            }
        }

        private BlockBuilder NewBlock(string label)
        {
            var block = new BlockBuilder(_blocks.Count, label);
            _blocks.Add(block);
            return block;
        }

        private IrLocal NewLocal(string name, TypeSymbol type, bool compilerGenerated = false, bool managedAddress = false)
        {
            var local = new IrLocal(_nextLocalId++, name, type, compilerGenerated, managedAddress);
            _allLocals.Add(local);
            return local;
        }

        private static IrRuntimeCallExpression Runtime(
            IrRuntimeMethod method,
            TypeSymbol resultType,
            params IrExpression[] arguments) =>
            new(method, arguments.Select(argument => new IrCallArgument(argument)).ToImmutableArray(), resultType);

        private static IrExpression Zero(TypeSymbol type) =>
            ReferenceEquals(type, TypeSymbol.Byte) ? new IrConstantExpression((byte)0, type) :
            ReferenceEquals(type, TypeSymbol.Integer) ? new IrConstantExpression((short)0, type) :
            ReferenceEquals(type, TypeSymbol.Long) ? new IrConstantExpression(0, type) :
            ReferenceEquals(type, TypeSymbol.LongLong) ? new IrConstantExpression(0L, type) :
            ReferenceEquals(type, TypeSymbol.LongPtr) ? new IrConstantExpression(0L, type) :
            ReferenceEquals(type, TypeSymbol.UShort) ? new IrConstantExpression((ushort)0, type) :
            ReferenceEquals(type, TypeSymbol.UInteger) ? new IrConstantExpression(0u, type) :
            ReferenceEquals(type, TypeSymbol.ULong) ? new IrConstantExpression(0UL, type) :
            ReferenceEquals(type, TypeSymbol.Single) ? new IrConstantExpression(0f, type) :
            ReferenceEquals(type, TypeSymbol.Currency) ? new IrConstantExpression(0m, type) :
            ReferenceEquals(type, TypeSymbol.Date) || ReferenceEquals(type, TypeSymbol.Double)
                ? new IrConstantExpression(0d, type)
                : new IrConstantExpression((short)0, type);

        private static IrRuntimeMethod AddMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.AddByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.AddLongLong
            : type == TypeSymbol.LongPtr ? IrRuntimeMethod.AddLongPtr
            : type == TypeSymbol.UShort ? IrRuntimeMethod.AddUShort
            : type == TypeSymbol.UInteger ? IrRuntimeMethod.AddUInteger
            : type == TypeSymbol.ULong ? IrRuntimeMethod.AddULong
            : type == TypeSymbol.Long ? IrRuntimeMethod.AddLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.AddCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.AddSingle
            : type == TypeSymbol.Date || type == TypeSymbol.Double ? IrRuntimeMethod.AddDouble
            : IrRuntimeMethod.AddInteger;

        /// <summary>
        /// Lowers one Debug.Print item. A typed Date is boxed as a Date value rather than as the
        /// bare OLE automation double it shares a representation with, so the runtime can still
        /// tell the two apart when it renders the item.
        /// </summary>
        private IrExpression LowerPrintItem(BoundExpression expression) =>
            expression.Type == TypeSymbol.Date
                ? Runtime(IrRuntimeMethod.DateToVariant, TypeSymbol.Variant, LowerExpression(expression))
                : LowerExpression(expression);

        private static IrRuntimeMethod SubtractMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.SubtractByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.SubtractLongLong
            : type == TypeSymbol.LongPtr ? IrRuntimeMethod.SubtractLongPtr
            : type == TypeSymbol.UShort ? IrRuntimeMethod.SubtractUShort
            : type == TypeSymbol.UInteger ? IrRuntimeMethod.SubtractUInteger
            : type == TypeSymbol.ULong ? IrRuntimeMethod.SubtractULong
            : type == TypeSymbol.Long ? IrRuntimeMethod.SubtractLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.SubtractCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.SubtractSingle
            // A Date result carries Double operands, the same as the Add row above. Falling
            // through to the Integer row instead asks the backend for a helper that takes two
            // Doubles under an Integer name, which no runtime overload matches.
            : type == TypeSymbol.Date || type == TypeSymbol.Double ? IrRuntimeMethod.SubtractDouble
            : IrRuntimeMethod.SubtractInteger;

        private static IrRuntimeMethod MultiplyMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.MultiplyByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.MultiplyLongLong
            : type == TypeSymbol.LongPtr ? IrRuntimeMethod.MultiplyLongPtr
            : type == TypeSymbol.UShort ? IrRuntimeMethod.MultiplyUShort
            : type == TypeSymbol.UInteger ? IrRuntimeMethod.MultiplyUInteger
            : type == TypeSymbol.ULong ? IrRuntimeMethod.MultiplyULong
            : type == TypeSymbol.Long ? IrRuntimeMethod.MultiplyLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.MultiplyCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.MultiplySingle
            : type == TypeSymbol.Double ? IrRuntimeMethod.MultiplyDouble
            : IrRuntimeMethod.MultiplyInteger;

        private static IrRuntimeMethod IntegerDivideMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.IntegerDivideByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.IntegerDivideLongLong
            : type == TypeSymbol.LongPtr ? IrRuntimeMethod.IntegerDivideLongPtr
            : type == TypeSymbol.UShort ? IrRuntimeMethod.IntegerDivideUShort
            : type == TypeSymbol.UInteger ? IrRuntimeMethod.IntegerDivideUInteger
            : type == TypeSymbol.ULong ? IrRuntimeMethod.IntegerDivideULong
            : type == TypeSymbol.Long ? IrRuntimeMethod.IntegerDivideLong
            : IrRuntimeMethod.IntegerDivideInteger;

        private static IrRuntimeMethod ModMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.ModByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.ModLongLong
            : type == TypeSymbol.LongPtr ? IrRuntimeMethod.ModLongPtr
            : type == TypeSymbol.UShort ? IrRuntimeMethod.ModUShort
            : type == TypeSymbol.UInteger ? IrRuntimeMethod.ModUInteger
            : type == TypeSymbol.ULong ? IrRuntimeMethod.ModULong
            : type == TypeSymbol.Long ? IrRuntimeMethod.ModLong
            : IrRuntimeMethod.ModInteger;

        private static IrRuntimeMethod LogicMethod(string operation, TypeSymbol type)
        {
            var suffix = type == TypeSymbol.Boolean ? "Boolean"
                : type == TypeSymbol.Byte ? "Byte"
                : type == TypeSymbol.LongLong ? "LongLong"
                : type == TypeSymbol.LongPtr ? "LongPtr"
                : type == TypeSymbol.UShort ? "UShort"
                : type == TypeSymbol.UInteger ? "UInteger"
                : type == TypeSymbol.ULong ? "ULong"
                : type == TypeSymbol.Long ? "Long"
                : "Integer";
            return Enum.Parse<IrRuntimeMethod>(operation + suffix);
        }

        private static IrRuntimeMethod RelationalMethod(SyntaxKind kind) => kind switch
        {
            SyntaxKind.EqualsToken => IrRuntimeMethod.Equal,
            SyntaxKind.LessGreaterToken => IrRuntimeMethod.NotEqual,
            SyntaxKind.LessToken => IrRuntimeMethod.Less,
            SyntaxKind.LessOrEqualsToken => IrRuntimeMethod.LessOrEqual,
            SyntaxKind.GreaterToken => IrRuntimeMethod.Greater,
            SyntaxKind.GreaterOrEqualsToken => IrRuntimeMethod.GreaterOrEqual,
            _ => throw new NotSupportedException($"Unsupported Select Case relation '{kind}'.")
        };

        private static IrRuntimeMethod FileGetMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.FileGetByte
            : type == TypeSymbol.Integer ? IrRuntimeMethod.FileGetInteger
            : type == TypeSymbol.Long ? IrRuntimeMethod.FileGetLong
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.FileGetLongLong
            : type == TypeSymbol.Single ? IrRuntimeMethod.FileGetSingle
            : type == TypeSymbol.Date ? IrRuntimeMethod.FileGetDouble
            : type == TypeSymbol.Double ? IrRuntimeMethod.FileGetDouble
            : type == TypeSymbol.Currency ? IrRuntimeMethod.FileGetCurrency
            : type == TypeSymbol.Boolean ? IrRuntimeMethod.FileGetBoolean
            : type == TypeSymbol.String ? IrRuntimeMethod.FileGetString
            : type == TypeSymbol.Variant ? IrRuntimeMethod.FileGetVariant
            : throw new NotSupportedException($"File Get type '{type.Name}' is not supported by IR lowering.");

        private static IrRuntimeMethod FileGetRawMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.FileGetRawByte
            : type == TypeSymbol.Integer ? IrRuntimeMethod.FileGetRawInteger
            : type == TypeSymbol.Long ? IrRuntimeMethod.FileGetRawLong
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.FileGetRawLongLong
            : type == TypeSymbol.Single ? IrRuntimeMethod.FileGetRawSingle
            : type == TypeSymbol.Date ? IrRuntimeMethod.FileGetRawDouble
            : type == TypeSymbol.Double ? IrRuntimeMethod.FileGetRawDouble
            : type == TypeSymbol.Currency ? IrRuntimeMethod.FileGetRawCurrency
            : type == TypeSymbol.Boolean ? IrRuntimeMethod.FileGetRawBoolean
            : type == TypeSymbol.String ? IrRuntimeMethod.FileGetRawString
            : type == TypeSymbol.Variant ? IrRuntimeMethod.FileGetRawVariant
            : throw new NotSupportedException($"File Get type '{type.Name}' is not supported by IR lowering.");

        private static IrRuntimeMethod IntrinsicMethod(string target) => target switch
        {
            "VBStrings.Len" => IrRuntimeMethod.StringLen,
            "VBStrings.LenB" => IrRuntimeMethod.StringLenB,
            "VBStrings.Mid" => IrRuntimeMethod.StringMid,
            "VBStrings.MidB" => IrRuntimeMethod.StringMidB,
            "VBStrings.MidAssign" => IrRuntimeMethod.StringMidAssign,
            "VBStrings.Chr" => IrRuntimeMethod.StringChr,
            "VBStrings.ChrW" => IrRuntimeMethod.StringChrW,
            "VBStrings.Left" => IrRuntimeMethod.StringLeft,
            "VBStrings.LeftB" => IrRuntimeMethod.StringLeftB,
            "VBStrings.Right" => IrRuntimeMethod.StringRight,
            "VBStrings.RightB" => IrRuntimeMethod.StringRightB,
            "VBStrings.UCase" => IrRuntimeMethod.StringUCase,
            "VBStrings.LCase" => IrRuntimeMethod.StringLCase,
            "VBStrings.Trim" => IrRuntimeMethod.StringTrim,
            "VBStrings.LTrim" => IrRuntimeMethod.StringLTrim,
            "VBStrings.RTrim" => IrRuntimeMethod.StringRTrim,
            "VBStrings.Asc" => IrRuntimeMethod.StringAsc,
            "VBStrings.AscW" => IrRuntimeMethod.StringAscW,
            "VBStrings.AscB" => IrRuntimeMethod.StringAscB,
            "VBStrings.ChrB" => IrRuntimeMethod.StringChrB,
            "VBErrors.ErrorText" => IrRuntimeMethod.ErrorsErrorText,
            "VBStrings.Tab" => IrRuntimeMethod.StringTab,
            "VBStrings.Spc" => IrRuntimeMethod.StringSpc,
            "VBStrings.Val" => IrRuntimeMethod.StringVal,
            "VBStrings.Hex" => IrRuntimeMethod.StringHex,
            "VBStrings.Oct" => IrRuntimeMethod.StringOct,
            "VBStrings.Str" => IrRuntimeMethod.StringStr,
            "VBStrings.String" => IrRuntimeMethod.StringRepeat,
            "VBStrings.FormatValue" => IrRuntimeMethod.StringFormat,
            "VBStrings.StrReverse" => IrRuntimeMethod.StringStrReverse,
            "VBStrings.FormatNumber" => IrRuntimeMethod.StringFormatNumber,
            "VBStrings.FormatCurrency" => IrRuntimeMethod.StringFormatCurrency,
            "VBStrings.FormatPercent" => IrRuntimeMethod.StringFormatPercent,
            "VBStrings.FormatDateTime" => IrRuntimeMethod.StringFormatDateTime,
            "VBStrings.Partition" => IrRuntimeMethod.StringPartition,
            "VBStrings.IsNumeric" => IrRuntimeMethod.StringIsNumeric,
            "VBStrings.InStr" => IrRuntimeMethod.StringInStr,
            "VBStrings.InStrB" => IrRuntimeMethod.StringInStrB,
            "VBStrings.InStrRev" => IrRuntimeMethod.StringInStrRev,
            "VBStrings.StrComp" => IrRuntimeMethod.StringStrComp,
            "VBStrings.Replace" => IrRuntimeMethod.StringReplace,
            "VBStrings.Space" => IrRuntimeMethod.StringSpace,
            "VBStrings.Split" => IrRuntimeMethod.StringSplit,
            "VBStrings.Join" => IrRuntimeMethod.StringJoin,
            "VBStrings.Filter" => IrRuntimeMethod.StringFilter,
            "VBStrings.StrConv" => IrRuntimeMethod.StringStrConv,
            "VBConversions.Int" => IrRuntimeMethod.ConversionInt,
            "VBMath.Abs" => IrRuntimeMethod.MathAbs,
            "VBMath.Sgn" => IrRuntimeMethod.MathSgn,
            "VBMath.Fix" => IrRuntimeMethod.MathFix,
            "VBMath.Round" => IrRuntimeMethod.MathRound,
            "VBMath.Sqr" => IrRuntimeMethod.MathSqr,
            "VBMath.Exp" => IrRuntimeMethod.MathExp,
            "VBMath.Log" => IrRuntimeMethod.MathLog,
            "VBMath.Sin" => IrRuntimeMethod.MathSin,
            "VBMath.Cos" => IrRuntimeMethod.MathCos,
            "VBMath.Tan" => IrRuntimeMethod.MathTan,
            "VBMath.Atn" => IrRuntimeMethod.MathAtn,
            "VBFinancial.FV" => IrRuntimeMethod.FinancialFv,
            "VBFinancial.PV" => IrRuntimeMethod.FinancialPv,
            "VBFinancial.PMT" => IrRuntimeMethod.FinancialPmt,
            "VBFinancial.IPMT" => IrRuntimeMethod.FinancialIpmt,
            "VBFinancial.PPMT" => IrRuntimeMethod.FinancialPpmt,
            "VBFinancial.NPER" => IrRuntimeMethod.FinancialNper,
            "VBFinancial.RATE" => IrRuntimeMethod.FinancialRate,
            "VBFinancial.NPV" => IrRuntimeMethod.FinancialNpv,
            "VBFinancial.IRR" => IrRuntimeMethod.FinancialIrr,
            "VBFinancial.MIRR" => IrRuntimeMethod.FinancialMirr,
            "VBFinancial.SLN" => IrRuntimeMethod.FinancialSln,
            "VBFinancial.SYD" => IrRuntimeMethod.FinancialSyd,
            "VBFinancial.DDB" => IrRuntimeMethod.FinancialDdb,
            "VBMath.Rnd" => IrRuntimeMethod.MathRnd,
            "VBMath.Randomize" => IrRuntimeMethod.MathRandomize,
            "VBVariants.EmptyValue" => IrRuntimeMethod.VariantEmpty,
            "VBVariants.NullValue" => IrRuntimeMethod.VariantNull,
            "VBVariants.NothingValue" => IrRuntimeMethod.VariantNothing,
            "VBVariants.MissingValue" => IrRuntimeMethod.VariantMissing,
            "VBVariants.IsEmpty" => IrRuntimeMethod.VariantIsEmpty,
            "VBVariants.IsNull" => IrRuntimeMethod.VariantIsNull,
            "VBVariants.IsMissing" => IrRuntimeMethod.VariantIsMissing,
            "VBVariants.IsError" => IrRuntimeMethod.VariantIsError,
            "VBVariants.IsArray" => IrRuntimeMethod.VariantIsArray,
            "VBVariants.IsDate" => IrRuntimeMethod.VariantIsDate,
            "VBVariants.IsObject" => IrRuntimeMethod.VariantIsObject,
            "VBVariants.VarType" => IrRuntimeMethod.VariantVarType,
            "VBVariants.ToBoolean" => IrRuntimeMethod.VariantToBoolean,
            "VBFiles.FreeFile" => IrRuntimeMethod.FileFreeFile,
            "VBFiles.Length" => IrRuntimeMethod.FileLength,
            "VBFiles.EndOfFile" => IrRuntimeMethod.FileEndOfFile,
            "VBFiles.Input" => IrRuntimeMethod.FileInput,
            "VBFiles.Position" => IrRuntimeMethod.FilePosition,
            "VBFiles.Location" => IrRuntimeMethod.FileLocation,
            "VBFiles.Reset" => IrRuntimeMethod.FileReset,
            "VBFiles.Lock" => IrRuntimeMethod.FileLock,
            "VBFiles.Unlock" => IrRuntimeMethod.FileUnlock,
            "VBFiles.Kill" => IrRuntimeMethod.FileKill,
            "VBFiles.Dir" => IrRuntimeMethod.FileDir,
            "VBFiles.FileCopy" => IrRuntimeMethod.FileCopy,
            "VBFiles.Rename" => IrRuntimeMethod.FileRename,
            "VBFiles.MakeDirectory" => IrRuntimeMethod.FileMakeDirectory,
            "VBFiles.RemoveDirectory" => IrRuntimeMethod.FileRemoveDirectory,
            "VBFiles.ChangeDirectory" => IrRuntimeMethod.FileChangeDirectory,
            "VBFiles.CurrentDirectory" => IrRuntimeMethod.FileCurrentDirectory,
            "VBFiles.GetAttributes" => IrRuntimeMethod.FileGetAttributes,
            "VBFiles.SetAttributes" => IrRuntimeMethod.FileSetAttributes,
            "VBFiles.FileDateTime" => IrRuntimeMethod.FileDateTime,
            "VBFiles.FileLength" => IrRuntimeMethod.FileLengthByPath,
            "VBInteraction.DoEvents" => IrRuntimeMethod.InteractionDoEvents,
            "VBInteraction.MsgBox" => IrRuntimeMethod.InteractionMsgBox,
            "VBInteraction.InputBox" => IrRuntimeMethod.InteractionInputBox,
            "VBInteraction.Load" => IrRuntimeMethod.InteractionLoad,
            "VBInteraction.Unload" => IrRuntimeMethod.InteractionUnload,
            "VBInteraction.CreateObject" => IrRuntimeMethod.InteractionCreateObject,
            "VBInteraction.GetObject" => IrRuntimeMethod.InteractionGetObject,
            "VBInteraction.Shell" => IrRuntimeMethod.InteractionShell,
            "VBInteraction.Command" => IrRuntimeMethod.InteractionCommand,
            "VBInteraction.Environ" => IrRuntimeMethod.InteractionEnviron,
            "VBInteraction.GetSetting" => IrRuntimeMethod.InteractionGetSetting,
            "VBInteraction.SaveSetting" => IrRuntimeMethod.InteractionSaveSetting,
            "VBInteraction.DeleteSetting" => IrRuntimeMethod.InteractionDeleteSetting,
            "VBInteraction.GetAllSettings" => IrRuntimeMethod.InteractionGetAllSettings,
            "VBInteraction.SendKeys" => IrRuntimeMethod.InteractionSendKeys,
            "VBInteraction.PopupMenu" => IrRuntimeMethod.InteractionPopupMenu,
            "VBInteraction.LoadPicture" => IrRuntimeMethod.InteractionLoadPicture,
            "VBInteraction.PropertyChanged" => IrRuntimeMethod.InteractionPropertyChanged,
            "VBInteraction.ScaleX" => IrRuntimeMethod.InteractionScaleX,
            "VBInteraction.ScaleY" => IrRuntimeMethod.InteractionScaleY,
            "VBInteraction.TextWidth" => IrRuntimeMethod.InteractionTextWidth,
            "VBInteraction.TextHeight" => IrRuntimeMethod.InteractionTextHeight,
            "VBInteraction.Print" => IrRuntimeMethod.InteractionPrint,
            "VBInteraction.PaintPicture" => IrRuntimeMethod.InteractionPaintPicture,
            "VBInteraction.Cls" => IrRuntimeMethod.InteractionCls,
            "VBInteraction.GraphicsPoint" => IrRuntimeMethod.GraphicsPoint,
            "VBMemory.VarPtr" => IrRuntimeMethod.MemoryVarPtr,
            "VBMemory.ObjPtr" => IrRuntimeMethod.MemoryObjPtr,
            "VBMemory.StrPtr" => IrRuntimeMethod.MemoryStrPtr,
            "VBMemory.LSet" => IrRuntimeMethod.MemoryLSet,
            "VBMemory.RSet" => IrRuntimeMethod.MemoryRSet,
            "VBDateTime.Date" => IrRuntimeMethod.DateTimeDate,
            "VBDateTime.Time" => IrRuntimeMethod.DateTimeTime,
            "VBDateTime.Now" => IrRuntimeMethod.DateTimeNow,
            "VBDateTime.DateValue" => IrRuntimeMethod.DateTimeValue,
            "VBDateTime.TimeValue" => IrRuntimeMethod.TimeDateValue,
            "VBDateTime.Year" => IrRuntimeMethod.DateTimeYear,
            "VBDateTime.Month" => IrRuntimeMethod.DateTimeMonth,
            "VBDateTime.Day" => IrRuntimeMethod.DateTimeDay,
            "VBDateTime.Hour" => IrRuntimeMethod.DateTimeHour,
            "VBDateTime.Minute" => IrRuntimeMethod.DateTimeMinute,
            "VBDateTime.Second" => IrRuntimeMethod.DateTimeSecond,
            "VBDateTime.Timer" => IrRuntimeMethod.DateTimeTimer,
            "VBDateTime.DateSerial" => IrRuntimeMethod.DateTimeSerial,
            "VBDateTime.TimeSerial" => IrRuntimeMethod.TimeDateSerial,
            "VBDateTime.DateAdd" => IrRuntimeMethod.DateTimeAdd,
            "VBDateTime.DateDiff" => IrRuntimeMethod.DateTimeDiff,
            "VBDateTime.DatePart" => IrRuntimeMethod.DateTimePart,
            "VBDateTime.Weekday" => IrRuntimeMethod.DateTimeWeekday,
            "VBDateTime.WeekdayName" => IrRuntimeMethod.DateTimeWeekdayName,
            "VBDateTime.MonthName" => IrRuntimeMethod.DateTimeMonthName,
            "VBErrors.NumberValue" => IrRuntimeMethod.ErrorNumber,
            "VBErrors.DescriptionValue" => IrRuntimeMethod.ErrorDescription,
            "VBErrors.SourceValue" => IrRuntimeMethod.ErrorSource,
            "VBErrors.HelpFileValue" => IrRuntimeMethod.ErrorHelpFile,
            "VBErrors.HelpContextValue" => IrRuntimeMethod.ErrorHelpContext,
            "VBErrors.LastDllErrorValue" => IrRuntimeMethod.ErrorLastDllError,
            "VBErrors.LineNumber" => IrRuntimeMethod.ErrorLineNumber,
            "VBErrors.SetLineNumber" => IrRuntimeMethod.ErrorSetLineNumber,
            "VBErrors.Clear" => IrRuntimeMethod.ErrorClear,
            "VBErrors.Raise" => IrRuntimeMethod.ErrorRaise,
            "VBFunctions.TypeName" => IrRuntimeMethod.FunctionTypeName,
            "VBFunctions.Array" => IrRuntimeMethod.FunctionArray,
            "VBFunctions.Switch" => IrRuntimeMethod.FunctionSwitch,
            "VBFunctions.Choose" => IrRuntimeMethod.FunctionChoose,
            "VBFunctions.IIf" => IrRuntimeMethod.FunctionIIf,
            "VBFunctions.RGB" => IrRuntimeMethod.FunctionRGB,
            "VBFunctions.CallByName" => IrRuntimeMethod.FunctionCallByName,
            "VBFunctions.QBColor" => IrRuntimeMethod.FunctionQBColor,
            "VBConversions.CByte" => IrRuntimeMethod.CByte,
            "VBConversions.CInt" => IrRuntimeMethod.CInt,
            "VBConversions.CLng" => IrRuntimeMethod.CLng,
            "VBConversions.CLngPtr" => IrRuntimeMethod.CLngPtr,
            "VBConversions.CLngLng" => IrRuntimeMethod.CLngLng,
            "VBConversions.CUShort" => IrRuntimeMethod.CUShort,
            "VBConversions.CUInt" => IrRuntimeMethod.CUInt,
            "VBConversions.CULng" => IrRuntimeMethod.CULng,
            "VBConversions.CCur" => IrRuntimeMethod.CCur,
            "VBConversions.CDec" => IrRuntimeMethod.CDec,
            "VBConversions.CDate" => IrRuntimeMethod.CDate,
            "VBConversions.CVDate" => IrRuntimeMethod.CVDate,
            "VBConversions.CSng" => IrRuntimeMethod.CSng,
            "VBConversions.CDbl" => IrRuntimeMethod.CDbl,
            "VBConversions.CBool" => IrRuntimeMethod.CBool,
            "VBConversions.CStr" => IrRuntimeMethod.CStr,
            "VBConversions.CVar" => IrRuntimeMethod.CVar,
            "VBConversions.CVErr" => IrRuntimeMethod.CVErr,
            _ => throw new NotSupportedException($"Intrinsic runtime target '{target}' has no IR identity.")
        };

        private sealed class BlockBuilder
        {
            public BlockBuilder(int id, string label)
            {
                Id = id;
                Label = $"block_{id}_{label}";
            }

            public int Id { get; }
            public string Label { get; }
            public List<IrInstruction> Instructions { get; } = new();
            public IrTerminator? Terminator { get; set; }
            public bool HasTerminator => Terminator is not null;

            public IrBasicBlock Build() => new(
                Id,
                Label,
                Instructions.ToImmutableArray(),
                Terminator ?? new IrReturnTerminator(null));
        }
    }

    private static string Mangle(string name)
    {
        var characters = name.Select(character =>
            char.IsLetterOrDigit(character) || character == '_' ? character : '_').ToArray();
        return characters.Length == 0 ? "unnamed" : new string(characters);
    }
}
