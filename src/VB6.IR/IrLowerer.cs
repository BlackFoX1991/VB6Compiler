using System.Collections.Immutable;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.IR;

public sealed record IrModuleInput(string Name, string? SourcePath, SemanticModel SemanticModel);

public static class IrLowerer
{
    public static IrProgram Lower(
        IEnumerable<IrModuleInput> modules,
        IEnumerable<BoundModuleVariable>? additionalGlobals = null)
    {
        ArgumentNullException.ThrowIfNull(modules);
        var inputs = modules.ToImmutableArray();
        if (inputs.IsDefaultOrEmpty)
        {
            return new IrProgram(
                ImmutableArray<IrModule>.Empty,
                ImmutableArray<IrTypeDefinition>.Empty,
                null);
        }

        var state = new ProgramLoweringState(inputs, additionalGlobals ?? Array.Empty<BoundModuleVariable>());
        return state.Lower();
    }

    private sealed class ProgramLoweringState
    {
        private readonly ImmutableArray<IrModuleInput> _inputs;
        private readonly ImmutableArray<BoundModuleVariable> _additionalGlobals;
        private readonly Dictionary<ModuleVariableSymbol, IrGlobal> _globals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ModuleVariableSymbol, BoundExpression> _constantValues =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<UserDefinedTypeSymbol, IrTypeDefinition> _types =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<UserDefinedTypeMemberSymbol, IrField> _fields =
            new(ReferenceEqualityComparer.Instance);

        public ProgramLoweringState(
            ImmutableArray<IrModuleInput> inputs,
            IEnumerable<BoundModuleVariable> additionalGlobals)
        {
            _inputs = inputs;
            _additionalGlobals = additionalGlobals.ToImmutableArray();
        }

        public IrProgram Lower()
        {
            PredeclareTypes();
            PredeclareGlobals();

            var modules = ImmutableArray.CreateBuilder<IrModule>(_inputs.Length + 1);
            IrProcedure? entryPoint = null;
            foreach (var input in _inputs)
            {
                var globals = input.SemanticModel.ModuleVariables
                    .Where(variable => _globals.ContainsKey(variable.Symbol))
                    .Select(variable => _globals[variable.Symbol])
                    .Distinct()
                    .ToImmutableArray();

                var procedures = ImmutableArray.CreateBuilder<IrProcedure>();
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
                modules.Add(new IrModule("__CompilerGlobals", null, extraGlobals, ImmutableArray<IrProcedure>.Empty));
            }

            return new IrProgram(modules.ToImmutable(), _types.Values.ToImmutableArray(), entryPoint);
        }

        public IrGlobal GetGlobal(ModuleVariableSymbol symbol) =>
            _globals.TryGetValue(symbol, out var global)
                ? global
                : throw new InvalidOperationException($"Global '{symbol.Name}' was not declared before lowering.");

        public IrField GetField(UserDefinedTypeMemberSymbol symbol) =>
            _fields.TryGetValue(symbol, out var field)
                ? field
                : throw new InvalidOperationException($"UDT field '{symbol.Name}' was not declared before lowering.");

        private void PredeclareGlobals()
        {
            foreach (var variable in _inputs.SelectMany(input => input.SemanticModel.ModuleVariables)
                         .Concat(_additionalGlobals))
            {
                if (_globals.ContainsKey(variable.Symbol))
                {
                    continue;
                }

                if (variable.IsConstant && variable.Initializer is not null)
                {
                    _constantValues.Add(variable.Symbol, variable.Initializer);
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

        /// <summary>The bound value of a module-level constant, which is substituted at each read.</summary>
        public bool TryGetConstantValue(ModuleVariableSymbol symbol, out BoundExpression value) =>
            _constantValues.TryGetValue(symbol, out value!);

        private void PredeclareTypes()
        {
            var seen = new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance);
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
                    $"__vb6_udt_{Mangle(type.Name)}",
                    fields,
                    ImmutableArray<IrProcedure>.Empty));
            }
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
        private readonly Dictionary<LocalVariableSymbol, IrLocal> _locals =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<ParameterSymbol, IrParameter> _parameters =
            new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<int, IrLocal> _withAddresses = new();
        private readonly Dictionary<int, int> _loopExits = new();
        private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<BlockBuilder> _blocks = new();
        private readonly List<IrLocal> _allLocals = new();
        private BlockBuilder _current = null!;
        private IrLocal? _returnLocal;
        private int _nextLocalId;

        public ProcedureLowerer(ProgramLoweringState program, BoundProcedure procedure)
        {
            _program = program;
            _procedure = procedure;
        }

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
            _current = NewBlock("entry");
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
                _procedure.Symbol,
                string.Equals(_procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase) &&
                    !_procedure.Symbol.IsFunction
                    ? "Main"
                    : $"__vb6_{Mangle(_procedure.Symbol.Name)}",
                _procedure.Symbol.ReturnType,
                _parameters.Values.OrderBy(parameter => parameter.Index).ToImmutableArray(),
                _allLocals.ToImmutableArray(),
                _blocks.Select(block => block.Build()).ToImmutableArray());
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

        private void LowerBlock(BoundBlockStatement block)
        {
            foreach (var statement in block.Statements)
            {
                LowerStatement(statement);
            }
        }

        private void LowerStatement(BoundStatement statement)
        {
            switch (statement)
            {
                case BoundVariableDeclarationStatement declaration:
                    LowerVariableDeclaration(declaration);
                    break;
                case BoundAssignmentStatement assignment:
                    Emit(new IrStoreInstruction(LowerVariablePlace(assignment.Variable), LowerValueCopy(assignment.Expression)));
                    break;
                case BoundArrayElementAssignmentStatement assignment:
                    Emit(new IrStoreInstruction(
                        new IrArrayElementPlace(
                            new IrLoadExpression(LowerVariablePlace(assignment.Array)),
                            assignment.Indices.Select(LowerExpression).ToImmutableArray(),
                            ((ArrayTypeSymbol)assignment.Array.Type).ElementType),
                        LowerValueCopy(assignment.Expression)));
                    break;
                case BoundMemberAssignmentStatement assignment:
                {
                    var memberTarget = LowerPlace(assignment.Target);
                    Emit(new IrStoreInstruction(
                        memberTarget,
                        LowerFixedStringWrite(memberTarget.Type, LowerValueCopy(assignment.Expression))));
                    break;
                }
                case BoundReDimStatement reDim:
                    LowerReDim(reDim);
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
                    Terminate(ReturnTerminator());
                    _current = NewBlock("after_return");
                    break;
                case BoundSelectCaseStatement select:
                    LowerSelect(select);
                    break;
                case BoundDebugPrintStatement print:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.DebugPrint,
                        TypeSymbol.Error,
                        LowerExpression(print.Expression))));
                    break;
                case BoundInvocationStatement invocation:
                    Emit(new IrEvaluateInstruction(LowerCall(invocation.Procedure, invocation.Arguments)));
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
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FileOpenBinary,
                        TypeSymbol.Error,
                        LowerExpression(open.FileNumber),
                        LowerExpression(open.Path))));
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
                    Emit(new IrStoreInstruction(
                        LowerPlace(get.Target),
                        Runtime(
                            FileGetMethod(get.Target.Type),
                            get.Target.Type,
                            LowerExpression(get.FileNumber),
                            get.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(get.Position))));
                    break;
                case BoundPutStatement put:
                    Emit(new IrEvaluateInstruction(Runtime(
                        IrRuntimeMethod.FilePut,
                        TypeSymbol.Error,
                        LowerExpression(put.FileNumber),
                        put.Position is null ? new IrNullExpression(TypeSymbol.LongLong) : LowerExpression(put.Position),
                        LowerExpression(put.Value))));
                    break;
            }
        }

        private void LowerVariableDeclaration(BoundVariableDeclarationStatement declaration)
        {
            var target = LowerVariablePlace(declaration.Variable);
            if (declaration.Variable.Type is ArrayTypeSymbol arrayType && !declaration.ArrayDimensions.IsDefaultOrEmpty)
            {
                Emit(new IrStoreInstruction(target, new IrNewVBArrayExpression(
                    arrayType,
                    LowerBounds(declaration.ArrayDimensions))));
                return;
            }

            if (declaration.Variable.Type == TypeSymbol.String)
            {
                Emit(new IrStoreInstruction(target, new IrConstantExpression(string.Empty, TypeSymbol.String)));
            }
        }

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

        private void LowerErase(BoundEraseStatement statement)
        {
            var target = LowerVariablePlace(statement.Array);
            if (statement.Deallocate)
            {
                Emit(new IrStoreInstruction(target, new IrNullExpression(statement.Array.Type)));
                return;
            }

            Emit(new IrEvaluateInstruction(new IrArrayCallExpression(
                IrArrayOperation.Clear,
                new IrLoadExpression(target),
                ImmutableArray<IrExpression>.Empty,
                TypeSymbol.Error)));
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
                Terminate(new IrConditionalTerminator(LowerExpression(clauses[index].Condition), body.Id, next.Id));

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
            Emit(new IrStoreInstruction(control, LowerExpression(statement.InitialValue)));
            Emit(new IrStoreInstruction(new IrLocalPlace(limit), LowerExpression(statement.Limit)));
            Emit(new IrStoreInstruction(new IrLocalPlace(step), LowerExpression(statement.Step)));

            var sign = NewBlock($"for_sign_{statement.LoopId}");
            var positive = NewBlock($"for_positive_{statement.LoopId}");
            var negative = NewBlock($"for_negative_{statement.LoopId}");
            var body = NewBlock($"for_body_{statement.LoopId}");
            var increment = NewBlock($"for_increment_{statement.LoopId}");
            var exit = NewBlock($"for_exit_{statement.LoopId}");
            _loopExits[statement.LoopId] = exit.Id;
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
            var collection = NewLocal($"__foreach_collection_{statement.LoopId}", statement.ArrayType, true);
            var index = NewLocal($"__foreach_index_{statement.LoopId}", TypeSymbol.Long, true);
            Emit(new IrStoreInstruction(new IrLocalPlace(collection), LowerExpression(statement.Collection)));
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
                        new IrLoadExpression(new IrLocalPlace(collection)),
                        ImmutableArray<IrExpression>.Empty,
                        TypeSymbol.Long)),
                body.Id,
                exit.Id));

            _current = body;
            var item = new IrArrayCallExpression(
                IrArrayOperation.GetFlatValue,
                new IrLoadExpression(new IrLocalPlace(collection)),
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
            Terminate(new IrConditionalTerminator(LowerExpression(statement.Condition), body.Id, exit.Id));
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
                var condition = LowerExpression(statement.Condition);
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
                    var condition = LowerExpression(statement.Condition);
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
            var target = LowerPlace(statement.Target);
            var address = NewLocal($"__with_addr_{statement.WithId}", statement.Target.Type, true, managedAddress: true);
            _withAddresses.Add(statement.WithId, address);
            Emit(new IrStoreAddressInstruction(address, new IrAddressExpression(target)));
            LowerBlock(statement.Body);
            _withAddresses.Remove(statement.WithId);
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
            Emit(new IrStoreInstruction(new IrLocalPlace(value), LowerExpression(statement.Expression)));
            var exit = NewBlock($"select_exit_{statement.SelectId}");
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
                LowerCaseClauseChain(caseBlock.Clauses, 0, value, body.Id, miss.Id);
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
            int failureBlock)
        {
            if (index >= clauses.Length)
            {
                Terminate(new IrGotoTerminator(failureBlock));
                return;
            }

            var next = index == clauses.Length - 1 ? failureBlock : NewBlock("select_clause_next").Id;
            LowerCaseClauseTest(clauses[index], selected, successBlock, next);
            if (next != failureBlock)
            {
                _current = _blocks[next];
                LowerCaseClauseChain(clauses, index + 1, selected, successBlock, failureBlock);
            }
        }

        private void LowerCaseClauseTest(BoundCaseClause clause, IrLocal selected, int success, int failure)
        {
            var selectedValue = new IrLoadExpression(new IrLocalPlace(selected));
            switch (clause)
            {
                case BoundCaseValueClause value:
                    Terminate(new IrConditionalTerminator(
                        Runtime(IrRuntimeMethod.Equal, TypeSymbol.Boolean, selectedValue, LowerExpression(value.Value)),
                        success,
                        failure));
                    break;
                case BoundCaseRelationalClause relational:
                    Terminate(new IrConditionalTerminator(
                        Runtime(RelationalMethod(relational.OperatorKind), TypeSymbol.Boolean, selectedValue, LowerExpression(relational.Value)),
                        success,
                        failure));
                    break;
                case BoundCaseRangeClause range:
                {
                    var upperTest = NewBlock("select_range_upper");
                    Terminate(new IrConditionalTerminator(
                        Runtime(IrRuntimeMethod.GreaterOrEqual, TypeSymbol.Boolean, selectedValue, LowerExpression(range.LowerBound)),
                        upperTest.Id,
                        failure));
                    _current = upperTest;
                    Terminate(new IrConditionalTerminator(
                        Runtime(IrRuntimeMethod.LessOrEqual, TypeSymbol.Boolean, selectedValue, LowerExpression(range.UpperBound)),
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
        }

        private IrExpression LowerExpression(BoundExpression expression)
        {
            return expression switch
            {
                BoundLiteralExpression literal => new IrConstantExpression(literal.Value, literal.LiteralType),
                BoundVariableExpression variable => LowerVariableRead(variable.Variable),
                BoundArrayAccessExpression array => new IrLoadExpression(new IrArrayElementPlace(
                    new IrLoadExpression(LowerVariablePlace(array.Array)),
                    array.Indices.Select(LowerExpression).ToImmutableArray(),
                    array.ElementType)),
                BoundElementAccessExpression element => new IrLoadExpression(new IrArrayElementPlace(
                    LowerExpression(element.Receiver),
                    element.Indices.Select(LowerExpression).ToImmutableArray(),
                    element.ElementType)),
                BoundMemberAccessExpression member => LowerMemberRead(member),
                BoundWithReceiverExpression with => new IrLoadExpression(LowerWithPlace(with)),
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
            BoundMemberAccessExpression member => LowerMemberPlace(member),
            BoundWithReceiverExpression with => LowerWithPlace(with),
            _ => throw new InvalidOperationException($"Bound expression '{expression.GetType().Name}' is not an addressable place.")
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

        private IrPlace LowerMemberPlace(BoundMemberAccessExpression expression)
        {
            var receiver = LowerPlace(expression.Receiver);
            return new IrFieldPlace(receiver, _program.GetField(expression.Member));
        }

        private IrPlace LowerWithPlace(BoundWithReceiverExpression expression)
        {
            if (!_withAddresses.TryGetValue(expression.WithId, out var address))
            {
                throw new InvalidOperationException($"With receiver {expression.WithId} is not active while lowering.");
            }

            return new IrIndirectPlace(new IrLocalAddressExpression(address), expression.ReceiverType);
        }

        /// <summary>
        /// Reads a variable. A VB6 <c>Const</c> - and the built-in and Enum constants, which are
        /// modelled the same way - is not storage: it is the only module-level declaration that
        /// carries an initializer, and nothing ever assigns to it. So its value is substituted
        /// here instead of being emitted as a field that would need a module initializer to fill.
        /// </summary>
        private IrExpression LowerVariableRead(VariableSymbol symbol)
        {
            if (symbol is ModuleVariableSymbol module &&
                _program.TryGetConstantValue(module, out var value))
            {
                return LowerExpression(value);
            }

            return new IrLoadExpression(LowerVariablePlace(symbol));
        }

        private IrPlace LowerVariablePlace(VariableSymbol symbol)
        {
            return symbol switch
            {
                LocalVariableSymbol local when _locals.TryGetValue(local, out var irLocal) => new IrLocalPlace(irLocal),
                ParameterSymbol parameter when _parameters.TryGetValue(parameter, out var irParameter) => new IrParameterPlace(irParameter),
                ModuleVariableSymbol global => new IrGlobalPlace(_program.GetGlobal(global)),
                ReturnValueSymbol when _returnLocal is not null => new IrLocalPlace(_returnLocal),
                _ => throw new InvalidOperationException($"Variable '{symbol.Name}' is not available in the current IR procedure.")
            };
        }

        private IrExpression LowerCall(ProcedureSymbol procedure, ImmutableArray<BoundArgument> arguments)
        {
            var lowered = ImmutableArray.CreateBuilder<IrCallArgument>(arguments.Length);
            foreach (var argument in arguments)
            {
                if (argument.Parameter?.PassingMode == ParameterPassingMode.ByRef)
                {
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
                    lowered.Add(new IrCallArgument(LowerValueCopy(argument.Expression)));
                }
            }

            if (procedure.IntrinsicTarget is not null)
            {
                return new IrRuntimeCallExpression(
                    IntrinsicMethod(procedure.IntrinsicTarget),
                    lowered.ToImmutable(),
                    procedure.ReturnType ?? TypeSymbol.Error);
            }

            return new IrProcedureCallExpression(
                procedure,
                lowered.ToImmutable(),
                procedure.ReturnType ?? TypeSymbol.Error);
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

        private IrExpression LowerConversion(BoundConversionExpression conversion)
        {
            var operand = LowerExpression(conversion.Expression);
            if (conversion.TargetType == conversion.Expression.Type)
            {
                return operand;
            }

            var method = conversion.TargetType == TypeSymbol.Byte ? IrRuntimeMethod.CByte
                : conversion.TargetType == TypeSymbol.Integer ? IrRuntimeMethod.CInt
                : conversion.TargetType == TypeSymbol.Long ? IrRuntimeMethod.CLng
                : conversion.TargetType == TypeSymbol.LongLong ? IrRuntimeMethod.CLngLng
                : conversion.TargetType == TypeSymbol.Currency ? IrRuntimeMethod.CCur
                : conversion.TargetType == TypeSymbol.Single ? IrRuntimeMethod.CSng
                : conversion.TargetType == TypeSymbol.Double ? IrRuntimeMethod.CDbl
                : conversion.TargetType == TypeSymbol.Boolean ? IrRuntimeMethod.CBool
                : conversion.TargetType == TypeSymbol.String ? IrRuntimeMethod.CStr
                : (IrRuntimeMethod?)null;
            return method is null
                ? operand
                : Runtime(method.Value, conversion.TargetType, operand);
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
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.LongLong => IrRuntimeMethod.NegateLongLong,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Long => IrRuntimeMethod.NegateLong,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Currency => IrRuntimeMethod.NegateCurrency,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Single => IrRuntimeMethod.NegateSingle,
                SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Double => IrRuntimeMethod.NegateDouble,
                SyntaxKind.MinusToken => IrRuntimeMethod.NegateInteger,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.Boolean => IrRuntimeMethod.NotBoolean,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.LongLong => IrRuntimeMethod.NotLongLong,
                SyntaxKind.NotKeyword when unary.ResultType == TypeSymbol.Long => IrRuntimeMethod.NotLong,
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
                case SyntaxKind.CaretToken: method = IrRuntimeMethod.Power; break;
                case SyntaxKind.EqualsToken: method = IrRuntimeMethod.Equal; break;
                case SyntaxKind.LessGreaterToken: method = IrRuntimeMethod.NotEqual; break;
                case SyntaxKind.LessToken: method = IrRuntimeMethod.Less; break;
                case SyntaxKind.LessOrEqualsToken: method = IrRuntimeMethod.LessOrEqual; break;
                case SyntaxKind.GreaterToken: method = IrRuntimeMethod.Greater; break;
                case SyntaxKind.GreaterOrEqualsToken: method = IrRuntimeMethod.GreaterOrEqual; break;
                case SyntaxKind.AmpersandToken: method = IrRuntimeMethod.Concat; break;
                case SyntaxKind.PlusToken when binary.ResultType == TypeSymbol.String: method = IrRuntimeMethod.Concat; break;
                case SyntaxKind.PlusToken: method = AddMethod(binary.ResultType); break;
                case SyntaxKind.MinusToken: method = SubtractMethod(binary.ResultType); break;
                case SyntaxKind.StarToken when binary.ResultType == TypeSymbol.Variant: method = IrRuntimeMethod.MultiplyVariant; break;
                case SyntaxKind.StarToken: method = MultiplyMethod(binary.ResultType); break;
                case SyntaxKind.BackslashToken: method = IntegerDivideMethod(binary.ResultType); break;
                case SyntaxKind.ModKeyword: method = ModMethod(binary.ResultType); break;
                case SyntaxKind.SlashToken: method = binary.ResultType == TypeSymbol.Single
                    ? IrRuntimeMethod.DivideSingle
                    : IrRuntimeMethod.DivideDouble; break;
                case SyntaxKind.AndKeyword: method = LogicMethod("And", binary.ResultType); break;
                case SyntaxKind.OrKeyword: method = LogicMethod("Or", binary.ResultType); break;
                case SyntaxKind.XorKeyword: method = LogicMethod("Xor", binary.ResultType); break;
                case SyntaxKind.EqvKeyword: method = LogicMethod("Eqv", binary.ResultType); break;
                case SyntaxKind.ImpKeyword: method = LogicMethod("Imp", binary.ResultType); break;
                default: throw new NotSupportedException($"IR lowering does not support binary operator '{binary.OperatorKind}'.");
            }

            return Runtime(method, binary.ResultType, left, right);
        }

        private ImmutableArray<IrArrayBound> LowerBounds(ImmutableArray<BoundArrayDimension> dimensions) =>
            dimensions.Select(dimension => new IrArrayBound(
                LowerExpression(dimension.LowerBound),
                LowerExpression(dimension.UpperBound))).ToImmutableArray();

        private IrReturnTerminator ReturnTerminator() => _returnLocal is null
            ? new IrReturnTerminator(null)
            : new IrReturnTerminator(new IrLoadExpression(new IrLocalPlace(_returnLocal)));

        private void Emit(IrInstruction instruction)
        {
            if (_current.HasTerminator)
            {
                _current = NewBlock("unreachable");
            }
            _current.Instructions.Add(instruction);
        }

        private void Terminate(IrTerminator terminator)
        {
            if (!_current.HasTerminator)
            {
                _current.Terminator = terminator;
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

        private static IrExpression Zero(TypeSymbol type) => type == TypeSymbol.LongLong
            ? new IrConstantExpression(0L, type)
            : type == TypeSymbol.Long
                ? new IrConstantExpression(0, type)
                : new IrConstantExpression((short)0, type);

        private static IrRuntimeMethod AddMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.AddByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.AddLongLong
            : type == TypeSymbol.Long ? IrRuntimeMethod.AddLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.AddCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.AddSingle
            : type == TypeSymbol.Double ? IrRuntimeMethod.AddDouble
            : IrRuntimeMethod.AddInteger;

        private static IrRuntimeMethod SubtractMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.SubtractByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.SubtractLongLong
            : type == TypeSymbol.Long ? IrRuntimeMethod.SubtractLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.SubtractCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.SubtractSingle
            : type == TypeSymbol.Double ? IrRuntimeMethod.SubtractDouble
            : IrRuntimeMethod.SubtractInteger;

        private static IrRuntimeMethod MultiplyMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.MultiplyByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.MultiplyLongLong
            : type == TypeSymbol.Long ? IrRuntimeMethod.MultiplyLong
            : type == TypeSymbol.Currency ? IrRuntimeMethod.MultiplyCurrency
            : type == TypeSymbol.Single ? IrRuntimeMethod.MultiplySingle
            : type == TypeSymbol.Double ? IrRuntimeMethod.MultiplyDouble
            : IrRuntimeMethod.MultiplyInteger;

        private static IrRuntimeMethod IntegerDivideMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.IntegerDivideByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.IntegerDivideLongLong
            : type == TypeSymbol.Long ? IrRuntimeMethod.IntegerDivideLong
            : IrRuntimeMethod.IntegerDivideInteger;

        private static IrRuntimeMethod ModMethod(TypeSymbol type) => type == TypeSymbol.Byte ? IrRuntimeMethod.ModByte
            : type == TypeSymbol.LongLong ? IrRuntimeMethod.ModLongLong
            : type == TypeSymbol.Long ? IrRuntimeMethod.ModLong
            : IrRuntimeMethod.ModInteger;

        private static IrRuntimeMethod LogicMethod(string operation, TypeSymbol type)
        {
            var suffix = type == TypeSymbol.Boolean ? "Boolean"
                : type == TypeSymbol.Byte ? "Byte"
                : type == TypeSymbol.LongLong ? "LongLong"
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
            : type == TypeSymbol.Double ? IrRuntimeMethod.FileGetDouble
            : type == TypeSymbol.Currency ? IrRuntimeMethod.FileGetCurrency
            : type == TypeSymbol.Boolean ? IrRuntimeMethod.FileGetBoolean
            : throw new NotSupportedException($"File Get type '{type.Name}' is not supported by IR lowering.");

        private static IrRuntimeMethod IntrinsicMethod(string target) => target switch
        {
            "VBStrings.Len" => IrRuntimeMethod.StringLen,
            "VBStrings.Mid" => IrRuntimeMethod.StringMid,
            "VBStrings.Chr" => IrRuntimeMethod.StringChr,
            "VBStrings.Left" => IrRuntimeMethod.StringLeft,
            "VBStrings.Right" => IrRuntimeMethod.StringRight,
            "VBStrings.UCase" => IrRuntimeMethod.StringUCase,
            "VBStrings.LCase" => IrRuntimeMethod.StringLCase,
            "VBStrings.Trim" => IrRuntimeMethod.StringTrim,
            "VBStrings.LTrim" => IrRuntimeMethod.StringLTrim,
            "VBStrings.RTrim" => IrRuntimeMethod.StringRTrim,
            "VBStrings.Asc" => IrRuntimeMethod.StringAsc,
            "VBStrings.IsNumeric" => IrRuntimeMethod.StringIsNumeric,
            "VBFiles.FreeFile" => IrRuntimeMethod.FileFreeFile,
            "VBFiles.Length" => IrRuntimeMethod.FileLength,
            "VBFiles.EndOfFile" => IrRuntimeMethod.FileEndOfFile,
            "VBFiles.Position" => IrRuntimeMethod.FilePosition,
            "VBConversions.CByte" => IrRuntimeMethod.CByte,
            "VBConversions.CInt" => IrRuntimeMethod.CInt,
            "VBConversions.CLng" => IrRuntimeMethod.CLng,
            "VBConversions.CSng" => IrRuntimeMethod.CSng,
            "VBConversions.CDbl" => IrRuntimeMethod.CDbl,
            "VBConversions.CBool" => IrRuntimeMethod.CBool,
            "VBConversions.CStr" => IrRuntimeMethod.CStr,
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
