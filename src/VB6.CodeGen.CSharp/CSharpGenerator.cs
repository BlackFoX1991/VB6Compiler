using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.CodeGen.CSharp;

public sealed class CSharpGenerator
{
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<UserDefinedTypeSymbol, string> _userDefinedTypeNames =
        new(ReferenceEqualityComparer.Instance);
    private int _indent;
    private bool _currentProcedureReturnsValue;

    public string Generate(SemanticModel model)
    {
        _builder.Clear();
        _userDefinedTypeNames.Clear();
        _indent = 0;

        var userDefinedTypes = CollectUserDefinedTypes(model);
        RegisterUserDefinedTypeNames(userDefinedTypes);

        WriteLine("using VB6.Runtime;");
        WriteLine();
        WriteLine("namespace VB6.Generated;");
        WriteLine();
        WriteLine("internal static class Program");
        WriteLine("{");
        _indent++;

        if (!userDefinedTypes.IsDefaultOrEmpty)
        {
            foreach (var type in userDefinedTypes)
            {
                EmitUserDefinedType(type);
                WriteLine();
            }
        }

        if (!model.ModuleVariables.IsDefaultOrEmpty)
        {
            foreach (var variable in model.ModuleVariables)
            {
                var initializer = variable.Initializer is null
                    ? EmitVariableInitializer(variable.Symbol.Type, variable.ArrayDimensions)
                    : EmitExpression(variable.Initializer);
                WriteLine($"private static {GetTypeName(variable.Symbol.Type)} {GetVariableName(variable.Symbol)} = {initializer};");
            }

            WriteLine();
        }

        foreach (var procedure in model.Procedures)
        {
            EmitProcedure(procedure);
            WriteLine();
        }

        _indent--;
        WriteLine("}");
        return _builder.ToString();
    }

    private static ImmutableArray<UserDefinedTypeSymbol> CollectUserDefinedTypes(SemanticModel model)
    {
        var types = ImmutableArray.CreateBuilder<UserDefinedTypeSymbol>();
        var seen = new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance);

        void AddType(TypeSymbol type)
        {
            if (type is ArrayTypeSymbol arrayType)
            {
                AddType(arrayType.ElementType);
                return;
            }

            if (type is not UserDefinedTypeSymbol userDefinedType || !seen.Add(userDefinedType))
            {
                return;
            }

            types.Add(userDefinedType);
            foreach (var member in userDefinedType.Members)
            {
                AddType(member.Type);
            }
        }

        foreach (var variable in model.ModuleVariables)
        {
            AddType(variable.Symbol.Type);
        }

        foreach (var procedure in model.Procedures)
        {
            if (procedure.Symbol.ReturnType is not null)
            {
                AddType(procedure.Symbol.ReturnType);
            }

            foreach (var parameter in procedure.Symbol.Parameters)
            {
                AddType(parameter.Type);
            }

            foreach (var local in procedure.Locals)
            {
                AddType(local.Type);
            }
        }

        return types.ToImmutable();
    }

    private void RegisterUserDefinedTypeNames(ImmutableArray<UserDefinedTypeSymbol> types)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            var baseName = $"__vb6_udt_{SanitizeIdentifier(type.Name)}";
            var name = baseName;
            var suffix = 2;
            while (!usedNames.Add(name))
            {
                name = $"{baseName}_{suffix++}";
            }

            _userDefinedTypeNames.Add(type, name);
        }
    }

    private void EmitUserDefinedType(UserDefinedTypeSymbol type)
    {
        WriteLine($"private struct {GetUserDefinedTypeName(type)}");
        WriteLine("{");
        _indent++;
        foreach (var member in type.Members)
        {
            var memberName = SanitizeIdentifier(member.Name);
            if (member.Type is FixedLengthStringTypeSymbol fixedString)
            {
                var backingName = $"__vb6_fixed_{memberName}";
                WriteLine($"private string? {backingName};");
                WriteLine($"public string __vb6_member_{memberName}");
                WriteLine("{");
                _indent++;
                WriteLine($"get => {backingName} ?? new string(' ', {fixedString.Length});");
                WriteLine("set");
                WriteLine("{");
                _indent++;
                WriteLine("var __vb6_value = value ?? string.Empty;");
                WriteLine($"{backingName} = __vb6_value.Length >= {fixedString.Length}");
                _indent++;
                WriteLine($"? __vb6_value[..{fixedString.Length}]");
                WriteLine($": __vb6_value.PadRight({fixedString.Length});");
                _indent--;
                _indent--;
                WriteLine("}");
                _indent--;
                WriteLine("}");
                continue;
            }

            if (member.Type is ArrayTypeSymbol arrayType && member.HasArrayBounds)
            {
                EmitFixedUserDefinedTypeArrayMember(member, memberName, arrayType);
                continue;
            }

            WriteLine($"public {GetTypeName(member.Type)} __vb6_member_{memberName};");
        }

        if (RequiresManagedClone(type))
        {
            WriteLine();
            EmitUserDefinedTypeClone(type);
        }

        _indent--;
        WriteLine("}");
    }

    private void EmitFixedUserDefinedTypeArrayMember(
        UserDefinedTypeMemberSymbol member,
        string memberName,
        ArrayTypeSymbol arrayType)
    {
        var typeName = GetTypeName(arrayType);
        var backingName = GetUserDefinedTypeArrayBackingName(memberName);
        WriteLine($"private {typeName}? {backingName};");
        WriteLine($"public {typeName} __vb6_member_{memberName} =>");
        _indent++;
        WriteLine($"{backingName} ??= new {typeName}({EmitUserDefinedTypeArrayBounds(member.ArrayBounds)});");
        _indent--;
    }

    private void EmitUserDefinedTypeClone(UserDefinedTypeSymbol type)
    {
        var typeName = GetUserDefinedTypeName(type);
        WriteLine($"public {typeName} __vb6_clone()");
        WriteLine("{");
        _indent++;
        WriteLine("var __vb6_copy = this;");

        foreach (var member in type.Members)
        {
            var memberName = SanitizeIdentifier(member.Name);
            if (member.Type is ArrayTypeSymbol arrayType)
            {
                var source = member.HasArrayBounds
                    ? GetUserDefinedTypeArrayBackingName(memberName)
                    : $"__vb6_member_{memberName}";
                var destination = member.HasArrayBounds
                    ? $"__vb6_copy.{GetUserDefinedTypeArrayBackingName(memberName)}"
                    : $"__vb6_copy.__vb6_member_{memberName}";
                WriteLine($"if ({source} is not null)");
                WriteLine("{");
                _indent++;
                WriteLine($"{destination} = {EmitArrayCloneExpression(source, arrayType)};");
                _indent--;
                WriteLine("}");
                continue;
            }

            if (member.Type is UserDefinedTypeSymbol nestedType && RequiresManagedClone(nestedType))
            {
                WriteLine($"__vb6_copy.__vb6_member_{memberName} = __vb6_member_{memberName}.__vb6_clone();");
            }
        }

        WriteLine("return __vb6_copy;");
        _indent--;
        WriteLine("}");
    }

    private string EmitArrayCloneExpression(string source, ArrayTypeSymbol arrayType)
    {
        if (arrayType.ElementType is UserDefinedTypeSymbol elementType && RequiresManagedClone(elementType))
        {
            return $"{source}.Clone(static __vb6_item => __vb6_item.__vb6_clone())";
        }

        return $"{source}.Clone()";
    }

    private static bool RequiresManagedClone(UserDefinedTypeSymbol type) =>
        RequiresManagedClone(
            type,
            new HashSet<UserDefinedTypeSymbol>(ReferenceEqualityComparer.Instance));

    private static bool RequiresManagedClone(
        UserDefinedTypeSymbol type,
        HashSet<UserDefinedTypeSymbol> activePath)
    {
        if (!activePath.Add(type))
        {
            return false;
        }

        foreach (var member in type.Members)
        {
            if (member.Type is ArrayTypeSymbol)
            {
                activePath.Remove(type);
                return true;
            }

            if (member.Type is UserDefinedTypeSymbol nestedType && RequiresManagedClone(nestedType, activePath))
            {
                activePath.Remove(type);
                return true;
            }
        }

        activePath.Remove(type);
        return false;
    }

    private static string GetUserDefinedTypeArrayBackingName(string memberName) =>
        $"__vb6_array_{memberName}";

    private static string EmitUserDefinedTypeArrayBounds(ImmutableArray<UserDefinedTypeArrayBound> bounds) =>
        string.Join(", ", bounds.Select(bound =>
            $"new VBArrayBound({bound.Lower.ToString(CultureInfo.InvariantCulture)}, {bound.Upper.ToString(CultureInfo.InvariantCulture)})"));

    private void EmitProcedure(BoundProcedure procedure)
    {
        var isMain = !procedure.Symbol.IsFunction &&
            string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase);
        var visibility = isMain ? "public" : "private";
        var returnType = procedure.Symbol.ReturnType is null
            ? "void"
            : GetTypeName(procedure.Symbol.ReturnType);
        var name = GetProcedureName(procedure.Symbol);
        var parameters = string.Join(", ", procedure.Symbol.Parameters.Select(EmitParameter));
        _currentProcedureReturnsValue = procedure.Symbol.ReturnType is not null;

        WriteLine($"{visibility} static {returnType} {name}({parameters})");
        WriteLine("{");
        _indent++;

        if (procedure.Symbol.ReturnType is not null)
        {
            WriteLine($"{returnType} __vb6_return = {GetDefaultValue(procedure.Symbol.ReturnType)};");
        }

        EmitBlock(procedure.Body);

        if (procedure.Symbol.ReturnType is not null)
        {
            WriteLine("return __vb6_return;");
        }

        _indent--;
        WriteLine("}");
    }

    private string EmitParameter(ParameterSymbol parameter)
    {
        var modifier = parameter.PassingMode == ParameterPassingMode.ByRef ? "ref " : string.Empty;
        return $"{modifier}{GetTypeName(parameter.Type)} {GetVariableName(parameter)}";
    }

    private void EmitBlock(BoundBlockStatement block)
    {
        foreach (var statement in block.Statements)
        {
            EmitStatement(statement);
        }
    }

    private void EmitStatement(BoundStatement statement)
    {
        switch (statement)
        {
            case BoundVariableDeclarationStatement declaration:
                WriteLine($"{GetTypeName(declaration.Variable.Type)} {GetVariableName(declaration.Variable)} = {EmitVariableInitializer(declaration.Variable.Type, declaration.ArrayDimensions)};");
                break;

            case BoundReDimStatement reDim:
                EmitReDimStatement(reDim);
                break;

            case BoundOpenStatement open:
                WriteLine($"VBFiles.OpenBinary({EmitExpression(open.FileNumber)}, {EmitExpression(open.Path)});");
                break;

            case BoundCloseStatement close:
                if (close.FileNumbers.IsDefaultOrEmpty)
                {
                    WriteLine("VBFiles.CloseAll();");
                    break;
                }

                foreach (var fileNumber in close.FileNumbers)
                {
                    WriteLine($"VBFiles.Close({EmitExpression(fileNumber)});");
                }

                break;

            case BoundSeekStatement seek:
                WriteLine($"VBFiles.Seek({EmitExpression(seek.FileNumber)}, {EmitExpression(seek.Position)});");
                break;

            case BoundGetStatement get:
                WriteLine(
                    $"{EmitExpression(get.Target)} = VBFiles.{GetFileReadMethod(get.Target.Type)}(" +
                    $"{EmitExpression(get.FileNumber)}, {EmitFilePosition(get.Position)});");
                break;

            case BoundPutStatement put:
                WriteLine(
                    $"VBFiles.Put({EmitExpression(put.FileNumber)}, {EmitFilePosition(put.Position)}, " +
                    $"{EmitExpression(put.Value)});");
                break;

            case BoundEraseStatement erase:
            {
                var variable = GetVariableName(erase.Array);
                WriteLine(erase.Deallocate
                    ? $"{variable} = null!;"
                    : $"{variable}.Clear();");
                break;
            }

            case BoundAssignmentStatement assignment:
                WriteLine($"{GetVariableName(assignment.Variable)} = {EmitValueCopy(assignment.Expression)};");
                break;

            case BoundArrayElementAssignmentStatement arrayAssignment:
                WriteLine($"{GetVariableName(arrayAssignment.Array)}[{EmitIndices(arrayAssignment.Indices)}] = {EmitValueCopy(arrayAssignment.Expression)};");
                break;

            case BoundMemberAssignmentStatement memberAssignment:
                WriteLine($"{EmitExpression(memberAssignment.Target)} = {EmitValueCopy(memberAssignment.Expression)};");
                break;

            case BoundIfStatement ifStatement:
                EmitIfStatement(ifStatement);
                break;

            case BoundForStatement forStatement:
                EmitForStatement(forStatement);
                break;

            case BoundForEachStatement forEachStatement:
                EmitForEachStatement(forEachStatement);
                break;

            case BoundWhileStatement whileStatement:
                WriteLine($"while ({EmitExpression(whileStatement.Condition)})");
                WriteLine("{");
                _indent++;
                EmitBlock(whileStatement.Body);
                _indent--;
                WriteLine("}");
                break;

            case BoundDoStatement doStatement:
                EmitDoStatement(doStatement);
                break;

            case BoundWithStatement withStatement:
                EmitWithStatement(withStatement);
                break;

            case BoundExitLoopStatement exitLoop:
                WriteLine($"goto {GetLoopExitLabel(exitLoop.TargetLoopId)};");
                break;

            case BoundReturnStatement:
                WriteLine(_currentProcedureReturnsValue ? "return __vb6_return;" : "return;");
                break;

            case BoundSelectCaseStatement selectCase:
                EmitSelectCaseStatement(selectCase);
                break;

            case BoundDebugPrintStatement debugPrint:
                WriteLine($"VBDebug.Print({EmitExpression(debugPrint.Expression)});");
                break;

            case BoundInvocationStatement invocation:
                WriteLine($"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments)});");
                break;
        }
    }

    private string EmitVariableInitializer(
        TypeSymbol type,
        ImmutableArray<BoundArrayDimension> dimensions)
    {
        if (type is not ArrayTypeSymbol arrayType)
        {
            return GetDefaultValue(type);
        }

        if (dimensions.IsDefaultOrEmpty)
        {
            return "null!";
        }

        return $"new {GetTypeName(arrayType)}({EmitArrayBounds(dimensions)})";
    }

    private void EmitReDimStatement(BoundReDimStatement statement)
    {
        if (statement.Array.Type is not ArrayTypeSymbol arrayType)
        {
            return;
        }

        var variable = GetVariableName(statement.Array);
        var bounds = EmitArrayBounds(statement.ArrayDimensions);
        if (statement.Preserve)
        {
            WriteLine($"{variable} = {variable}.ReDimPreserve({bounds});");
        }
        else
        {
            WriteLine($"{variable} = new {GetTypeName(arrayType)}({bounds});");
        }
    }

    private string EmitArrayBounds(ImmutableArray<BoundArrayDimension> dimensions) =>
        string.Join(", ", dimensions.Select(dimension =>
            $"new VBArrayBound({EmitExpression(dimension.LowerBound)}, {EmitExpression(dimension.UpperBound)})"));

    private void EmitIfStatement(BoundIfStatement statement)
    {
        WriteLine($"if ({EmitExpression(statement.Condition)})");
        WriteLine("{");
        _indent++;
        EmitBlock(statement.Body);
        _indent--;
        WriteLine("}");

        foreach (var elseIfClause in statement.ElseIfClauses)
        {
            WriteLine($"else if ({EmitExpression(elseIfClause.Condition)})");
            WriteLine("{");
            _indent++;
            EmitBlock(elseIfClause.Body);
            _indent--;
            WriteLine("}");
        }

        if (statement.ElseBody is not null)
        {
            WriteLine("else");
            WriteLine("{");
            _indent++;
            EmitBlock(statement.ElseBody);
            _indent--;
            WriteLine("}");
        }
    }

    private void EmitForStatement(BoundForStatement statement)
    {
        var variable = GetVariableName(statement.ControlVariable);
        var typeName = GetTypeName(statement.ControlVariable.Type);
        var limitName = $"__vb6_for_limit_{statement.LoopId}";
        var stepName = $"__vb6_for_step_{statement.LoopId}";

        WriteLine($"{variable} = {EmitExpression(statement.InitialValue)};");
        WriteLine($"{typeName} {limitName} = {EmitExpression(statement.Limit)};");
        WriteLine($"{typeName} {stepName} = {EmitExpression(statement.Step)};");
        WriteLine($"while ({stepName} >= 0 ? VBOperators.LessOrEqual({variable}, {limitName}) : VBOperators.GreaterOrEqual({variable}, {limitName}))");
        WriteLine("{");
        _indent++;
        EmitBlock(statement.Body);
        var addOperator = statement.ControlVariable.Type == TypeSymbol.LongLong
            ? "AddLongLong"
            : statement.ControlVariable.Type == TypeSymbol.Long
                ? "AddLong"
                : "AddInteger";
        WriteLine($"{variable} = VBOperators.{addOperator}({variable}, {stepName});");
        _indent--;
        WriteLine("}");
        EmitLoopExitLabel(statement.LoopId);
    }

    private void EmitForEachStatement(BoundForEachStatement statement)
    {
        var variable = GetVariableName(statement.ControlVariable);
        var itemName = $"__vb6_for_each_item_{statement.LoopId}";

        WriteLine($"foreach (var {itemName} in {EmitExpression(statement.Collection)}.EnumerateValues())");
        WriteLine("{");
        _indent++;
        WriteLine($"{variable} = {itemName};");
        EmitBlock(statement.Body);
        _indent--;
        WriteLine("}");
        EmitLoopExitLabel(statement.LoopId);
    }

    private void EmitDoStatement(BoundDoStatement statement)
    {
        if (statement.Condition is null)
        {
            WriteLine("while (true)");
            WriteLine("{");
            _indent++;
            EmitBlock(statement.Body);
            _indent--;
            WriteLine("}");
            EmitLoopExitLabel(statement.LoopId);
            return;
        }

        var condition = EmitLoopCondition(statement.Condition, statement.IsUntil);
        if (statement.ConditionIsPostTest)
        {
            WriteLine("do");
            WriteLine("{");
            _indent++;
            EmitBlock(statement.Body);
            _indent--;
            WriteLine($"}} while ({condition});");
        }
        else
        {
            WriteLine($"while ({condition})");
            WriteLine("{");
            _indent++;
            EmitBlock(statement.Body);
            _indent--;
            WriteLine("}");
        }

        EmitLoopExitLabel(statement.LoopId);
    }

    private void EmitWithStatement(BoundWithStatement statement)
    {
        WriteLine("{");
        _indent++;
        WriteLine($"ref var __vb6_with_{statement.WithId} = ref {EmitExpression(statement.Target)};");
        EmitBlock(statement.Body);
        _indent--;
        WriteLine("}");
    }

    private string EmitLoopCondition(BoundExpression condition, bool isUntil)
    {
        var expression = EmitExpression(condition);
        return isUntil ? $"!({expression})" : expression;
    }

    private void EmitLoopExitLabel(int loopId)
    {
        WriteLine($"{GetLoopExitLabel(loopId)}:");
        WriteLine(";");
    }

    private static string GetLoopExitLabel(int loopId) => $"__vb6_loop_exit_{loopId}";

    private void EmitSelectCaseStatement(BoundSelectCaseStatement statement)
    {
        var selectName = $"__vb6_select_{statement.SelectId}";
        WriteLine($"var {selectName} = {EmitExpression(statement.Expression)};");

        var hasConditionalBlock = false;
        foreach (var caseBlock in statement.Cases)
        {
            var isElse = caseBlock.Clauses.Any(clause => clause is BoundCaseElseClause);
            if (isElse)
            {
                if (hasConditionalBlock)
                {
                    WriteLine("else");
                }
                else
                {
                    WriteLine("{");
                    _indent++;
                    EmitBlock(caseBlock.Body);
                    _indent--;
                    WriteLine("}");
                    continue;
                }
            }
            else
            {
                var condition = string.Join(" || ", caseBlock.Clauses.Select(clause => EmitCaseCondition(selectName, clause)));
                WriteLine(hasConditionalBlock
                    ? $"else if ({condition})"
                    : $"if ({condition})");
                hasConditionalBlock = true;
            }

            WriteLine("{");
            _indent++;
            EmitBlock(caseBlock.Body);
            _indent--;
            WriteLine("}");
        }
    }

    private string EmitCaseCondition(string selectName, BoundCaseClause clause)
    {
        return clause switch
        {
            BoundCaseValueClause value =>
                $"VBOperators.Equal({selectName}, {EmitExpression(value.Value)})",
            BoundCaseRangeClause range =>
                $"(VBOperators.GreaterOrEqual({selectName}, {EmitExpression(range.LowerBound)}) && VBOperators.LessOrEqual({selectName}, {EmitExpression(range.UpperBound)}))",
            BoundCaseRelationalClause relational =>
                EmitRelationalCaseCondition(selectName, relational),
            _ => "false"
        };
    }

    private string EmitRelationalCaseCondition(string selectName, BoundCaseClause clause)
    {
        var relational = (BoundCaseRelationalClause)clause;
        var value = EmitExpression(relational.Value);
        return relational.OperatorKind switch
        {
            SyntaxKind.EqualsToken => $"VBOperators.Equal({selectName}, {value})",
            SyntaxKind.LessGreaterToken => $"VBOperators.NotEqual({selectName}, {value})",
            SyntaxKind.LessToken => $"VBOperators.Less({selectName}, {value})",
            SyntaxKind.LessOrEqualsToken => $"VBOperators.LessOrEqual({selectName}, {value})",
            SyntaxKind.GreaterToken => $"VBOperators.Greater({selectName}, {value})",
            SyntaxKind.GreaterOrEqualsToken => $"VBOperators.GreaterOrEqual({selectName}, {value})",
            _ => "false"
        };
    }

    /// <summary>An omitted record position continues at the current file position.</summary>
    private string EmitFilePosition(BoundExpression? position) =>
        position is null ? "null" : EmitExpression(position);

    /// <summary>
    /// Each VB6 type reads its own storage size, so the read is selected by type rather than by a
    /// generic helper - four bytes for a Long, two for an Integer, and so on.
    /// </summary>
    private static string GetFileReadMethod(TypeSymbol type)
    {
        if (type == TypeSymbol.Byte) return "GetByte";
        if (type == TypeSymbol.Integer) return "GetInteger";
        if (type == TypeSymbol.Long) return "GetLong";
        if (type == TypeSymbol.LongLong) return "GetLongLong";
        if (type == TypeSymbol.Single) return "GetSingle";
        if (type == TypeSymbol.Double) return "GetDouble";
        if (type == TypeSymbol.Currency) return "GetCurrency";
        if (type == TypeSymbol.Boolean) return "GetBoolean";

        throw new InvalidOperationException(
            $"The binder should have rejected a Get of type '{type.Name}'.");
    }

    private string EmitArguments(IEnumerable<BoundArgument> arguments) =>
        string.Join(", ", arguments.Select(EmitArgument));

    private string EmitArgument(BoundArgument argument)
    {
        if (argument.Parameter?.PassingMode == ParameterPassingMode.ByRef)
        {
            // The callee still takes a reference; only the storage differs. VBByRef.Temp keeps the
            // reference valid for the call and drops the write-back afterwards, matching VB6.
            return argument.RequiresByRefTemporary
                ? $"ref VBByRef.Temp<{GetTypeName(argument.Parameter.Type)}>({EmitExpression(argument.Expression)})"
                : $"ref {EmitExpression(argument.Expression)}";
        }

        return argument.Parameter?.PassingMode == ParameterPassingMode.ByVal
            ? EmitValueCopy(argument.Expression)
            : EmitExpression(argument.Expression);
    }

    private string EmitValueCopy(BoundExpression expression)
    {
        var emitted = EmitExpression(expression);
        return expression.Type is UserDefinedTypeSymbol userDefinedType && RequiresManagedClone(userDefinedType)
            ? $"{emitted}.__vb6_clone()"
            : emitted;
    }

    private string EmitIndices(IEnumerable<BoundExpression> indices) =>
        string.Join(", ", indices.Select(EmitExpression));

    private string EmitExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundLiteralExpression literal => EmitLiteral(literal),
            BoundVariableExpression variable => GetVariableName(variable.Variable),
            BoundArrayAccessExpression arrayAccess =>
                $"{GetVariableName(arrayAccess.Array)}[{EmitIndices(arrayAccess.Indices)}]",
            BoundElementAccessExpression elementAccess =>
                $"{EmitExpression(elementAccess.Receiver)}[{EmitIndices(elementAccess.Indices)}]",
            BoundArrayBoundExpression arrayBound =>
                $"{GetVariableName(arrayBound.Array)}.{(arrayBound.IsUpperBound ? "UBound" : "LBound")}({EmitExpression(arrayBound.Dimension)})",
            BoundInvocationExpression invocation =>
                $"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments)})",
            BoundMemberAccessExpression memberAccess =>
                $"{EmitExpression(memberAccess.Receiver)}.__vb6_member_{SanitizeIdentifier(memberAccess.Member.Name)}",
            BoundWithReceiverExpression withReceiver => $"__vb6_with_{withReceiver.WithId}",
            BoundConversionExpression conversion => EmitConversion(conversion),
            BoundUnaryExpression unary => EmitUnary(unary),
            BoundBinaryExpression binary => EmitBinary(binary),
            BoundErrorExpression => "default",
            _ => "default"
        };
    }

    private static string EmitLiteral(BoundLiteralExpression literal)
    {
        if (literal.LiteralType == TypeSymbol.Byte)
        {
            var value = Convert.ToByte(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CByte({value.ToString(CultureInfo.InvariantCulture)}L)";
        }

        if (literal.LiteralType == TypeSymbol.Integer)
        {
            var value = Convert.ToInt64(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CInt({value.ToString(CultureInfo.InvariantCulture)}L)";
        }

        if (literal.LiteralType == TypeSymbol.Long)
        {
            var value = Convert.ToInt64(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CLng({value.ToString(CultureInfo.InvariantCulture)}L)";
        }

        if (literal.LiteralType == TypeSymbol.LongLong)
        {
            var value = Convert.ToInt64(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CLngLng({value.ToString(CultureInfo.InvariantCulture)}L)";
        }

        if (literal.LiteralType == TypeSymbol.Currency)
        {
            var value = Convert.ToDecimal(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CCur({value.ToString(CultureInfo.InvariantCulture)}m)";
        }

        if (literal.LiteralType == TypeSymbol.Single)
        {
            var value = Convert.ToSingle(literal.Value, CultureInfo.InvariantCulture);
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        if (literal.LiteralType == TypeSymbol.String)
        {
            return QuoteString(Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        if (literal.LiteralType == TypeSymbol.Boolean)
        {
            return Convert.ToBoolean(literal.Value, CultureInfo.InvariantCulture) ? "true" : "false";
        }

        if (literal.LiteralType == TypeSymbol.Double)
        {
            var value = Convert.ToDouble(literal.Value, CultureInfo.InvariantCulture);
            return value.ToString("R", CultureInfo.InvariantCulture) + "d";
        }

        return "default";
    }

    private string EmitConversion(BoundConversionExpression conversion)
    {
        var expression = EmitExpression(conversion.Expression);

        if (conversion.TargetType == TypeSymbol.Byte)
        {
            return $"VBConversions.CByte({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Integer)
        {
            return $"VBConversions.CInt({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Long)
        {
            return $"VBConversions.CLng({expression})";
        }

        if (conversion.TargetType == TypeSymbol.LongLong)
        {
            return $"VBConversions.CLngLng({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Currency)
        {
            return $"VBConversions.CCur({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Single)
        {
            return $"VBConversions.CSng({expression})";
        }

        if (conversion.TargetType == TypeSymbol.String)
        {
            return $"VBConversions.CStr({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Boolean)
        {
            return $"VBConversions.CBool({expression})";
        }

        if (conversion.TargetType == TypeSymbol.Double)
        {
            return $"VBConversions.CDbl({expression})";
        }

        return expression;
    }

    private string EmitUnary(BoundUnaryExpression unary)
    {
        var operand = EmitExpression(unary.Operand);
        return unary.OperatorKind switch
        {
            SyntaxKind.PlusToken => operand,
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.LongLong => $"VBOperators.NegateLongLong({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Long => $"VBOperators.NegateLong({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Currency => $"VBOperators.NegateCurrency({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Single => $"VBOperators.NegateSingle({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Double => $"VBOperators.NegateDouble({operand})",
            SyntaxKind.MinusToken => $"VBOperators.NegateInteger({operand})",
            SyntaxKind.NotKeyword => $"VBOperators.Not{GetRuntimeTypeSuffix(unary.ResultType)}({operand})",
            _ => operand
        };
    }

    private string EmitBinary(BoundBinaryExpression binary)
    {
        var left = EmitExpression(binary.Left);
        var right = EmitExpression(binary.Right);

        return binary.OperatorKind switch
        {
            SyntaxKind.CaretToken => $"VBOperators.Power({left}, {right})",
            SyntaxKind.EqualsToken => $"VBOperators.Equal({left}, {right})",
            SyntaxKind.LessGreaterToken => $"VBOperators.NotEqual({left}, {right})",
            SyntaxKind.LessToken => $"VBOperators.Less({left}, {right})",
            SyntaxKind.LessOrEqualsToken => $"VBOperators.LessOrEqual({left}, {right})",
            SyntaxKind.GreaterToken => $"VBOperators.Greater({left}, {right})",
            SyntaxKind.GreaterOrEqualsToken => $"VBOperators.GreaterOrEqual({left}, {right})",
            SyntaxKind.AndKeyword => EmitArithmeticCall(binary.ResultType, "And", left, right),
            SyntaxKind.OrKeyword => EmitArithmeticCall(binary.ResultType, "Or", left, right),
            SyntaxKind.XorKeyword => EmitArithmeticCall(binary.ResultType, "Xor", left, right),
            SyntaxKind.EqvKeyword => EmitArithmeticCall(binary.ResultType, "Eqv", left, right),
            SyntaxKind.ImpKeyword => EmitArithmeticCall(binary.ResultType, "Imp", left, right),
            SyntaxKind.AmpersandToken => $"VBOperators.Concat({left}, {right})",
            SyntaxKind.PlusToken when binary.ResultType == TypeSymbol.String => $"VBOperators.Concat({left}, {right})",
            SyntaxKind.PlusToken => EmitArithmeticCall(binary.ResultType, "Add", left, right),
            SyntaxKind.MinusToken => EmitArithmeticCall(binary.ResultType, "Subtract", left, right),
            SyntaxKind.StarToken => EmitArithmeticCall(binary.ResultType, "Multiply", left, right),
            SyntaxKind.BackslashToken when binary.ResultType == TypeSymbol.LongLong => $"VBOperators.IntegerDivideLongLong({left}, {right})",
            SyntaxKind.BackslashToken when binary.ResultType == TypeSymbol.Long => $"VBOperators.IntegerDivideLong({left}, {right})",
            SyntaxKind.BackslashToken when binary.ResultType == TypeSymbol.Byte => $"VBOperators.IntegerDivideByte({left}, {right})",
            SyntaxKind.BackslashToken => $"VBOperators.IntegerDivide({left}, {right})",
            SyntaxKind.ModKeyword when binary.ResultType == TypeSymbol.LongLong => $"VBOperators.ModLongLong({left}, {right})",
            SyntaxKind.ModKeyword when binary.ResultType == TypeSymbol.Long => $"VBOperators.ModLong({left}, {right})",
            SyntaxKind.ModKeyword when binary.ResultType == TypeSymbol.Byte => $"VBOperators.ModByte({left}, {right})",
            SyntaxKind.ModKeyword => $"VBOperators.ModInteger({left}, {right})",
            SyntaxKind.SlashToken when binary.ResultType == TypeSymbol.Single => $"VBOperators.DivideSingle({left}, {right})",
            SyntaxKind.SlashToken => $"VBOperators.DivideDouble({left}, {right})",
            _ => "default"
        };
    }

    private static string EmitArithmeticCall(TypeSymbol resultType, string operation, string left, string right) =>
        $"VBOperators.{operation}{GetRuntimeTypeSuffix(resultType)}({left}, {right})";

    private static string GetRuntimeTypeSuffix(TypeSymbol type) =>
        type == TypeSymbol.Boolean
            ? "Boolean"
            : type == TypeSymbol.Currency
                ? "Currency"
                : type == TypeSymbol.Double
                    ? "Double"
                    : type == TypeSymbol.Single
                        ? "Single"
                        : type == TypeSymbol.LongLong
                            ? "LongLong"
                            : type == TypeSymbol.Long
                                ? "Long"
                                : type == TypeSymbol.Byte
                                    ? "Byte"
                                    : "Integer";

    private string GetTypeName(TypeSymbol type)
    {
        if (type is ArrayTypeSymbol arrayType)
        {
            return $"VBArray<{GetTypeName(arrayType.ElementType)}>";
        }

        if (type is UserDefinedTypeSymbol userDefinedType)
        {
            return GetUserDefinedTypeName(userDefinedType);
        }

        if (type is FixedLengthStringTypeSymbol)
        {
            return "string";
        }

        if (type == TypeSymbol.Byte)
        {
            return "byte";
        }

        if (type == TypeSymbol.Integer)
        {
            return "short";
        }

        if (type == TypeSymbol.Long)
        {
            return "int";
        }

        if (type == TypeSymbol.LongLong)
        {
            return "long";
        }

        if (type == TypeSymbol.Currency)
        {
            return "VBCurrency";
        }

        if (type == TypeSymbol.Single)
        {
            return "float";
        }

        if (type == TypeSymbol.String)
        {
            return "string";
        }

        if (type == TypeSymbol.Boolean)
        {
            return "bool";
        }

        if (type == TypeSymbol.Double)
        {
            return "double";
        }

        return "object?";
    }

    private string GetUserDefinedTypeName(UserDefinedTypeSymbol type)
    {
        if (_userDefinedTypeNames.TryGetValue(type, out var name))
        {
            return name;
        }

        throw new InvalidOperationException($"UDT '{type.Name}' was not registered for C# generation.");
    }

    private static string GetDefaultValue(TypeSymbol type)
    {
        if (type is ArrayTypeSymbol)
        {
            return "null!";
        }

        if (type == TypeSymbol.String)
        {
            return "string.Empty";
        }

        if (type == TypeSymbol.Boolean)
        {
            return "false";
        }

        if (type == TypeSymbol.Single)
        {
            return "0f";
        }

        if (type == TypeSymbol.Double)
        {
            return "0d";
        }

        if (type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long || type == TypeSymbol.LongLong)
        {
            return "0";
        }

        if (type == TypeSymbol.Currency)
        {
            return "default";
        }

        return "default";
    }

    private static string GetProcedureName(ProcedureSymbol procedure)
    {
        if (procedure.IntrinsicTarget is not null)
        {
            return procedure.IntrinsicTarget;
        }

        return !procedure.IsFunction && string.Equals(procedure.Name, "Main", StringComparison.OrdinalIgnoreCase)
            ? "Main"
            : $"__vb6_{SanitizeIdentifier(procedure.Name)}";
    }

    private static string GetVariableName(VariableSymbol variable) => variable switch
    {
        ReturnValueSymbol => "__vb6_return",
        ParameterSymbol parameter => $"__vb6_arg_{SanitizeIdentifier(parameter.Name)}",
        _ => $"__vb6_{SanitizeIdentifier(variable.Name)}"
    };

    private static string SanitizeIdentifier(string identifier)
    {
        var builder = new StringBuilder(identifier.Length);
        foreach (var character in identifier)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' ? character : '_');
        }

        return builder.Length == 0 ? "unnamed" : builder.ToString();
    }

    private static string QuoteString(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal) + "\"";

    private void WriteLine(string text = "")
    {
        if (text.Length != 0)
        {
            _builder.Append(' ', _indent * 4);
            _builder.Append(text);
        }

        _builder.AppendLine();
    }
}
