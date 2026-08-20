using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.CodeGen.CSharp;

public sealed class CSharpGenerator
{
    private readonly StringBuilder _builder = new();
    private readonly Dictionary<LocalVariableSymbol, string> _staticLocalNames =
        new(ReferenceEqualityComparer.Instance);
    private int _indent;
    private int _nextByRefTemporaryId;
    private bool _currentProcedureReturnsValue;

    public string Generate(SemanticModel model)
    {
        _builder.Clear();
        _staticLocalNames.Clear();
        _indent = 0;
        _nextByRefTemporaryId = 0;
        RegisterStaticLocalNames(model.Procedures);

        WriteLine("using VB6.Runtime;");
        if (!model.UserDefinedTypes.IsDefaultOrEmpty)
        {
            WriteLine("using System.Runtime.InteropServices;");
        }

        WriteLine();
        WriteLine("namespace VB6.Generated;");
        WriteLine();
        WriteLine("internal static class Program");
        WriteLine("{");
        _indent++;

        if (!model.UserDefinedTypes.IsDefaultOrEmpty)
        {
            foreach (var type in model.UserDefinedTypes)
            {
                EmitUserDefinedType(type);
                WriteLine();
            }
        }

        if (!model.ModuleVariables.IsDefaultOrEmpty)
        {
            foreach (var variable in model.ModuleVariables)
            {
                var initializer = !variable.ArrayDimensions.IsDefaultOrEmpty
                    ? EmitArrayCreation(variable.Symbol.Type, variable.ArrayDimensions)
                    : variable.Initializer is null
                        ? GetDefaultValue(variable.Symbol.Type)
                        : EmitExpression(variable.Initializer);
                WriteLine($"private static {GetTypeName(variable.Symbol.Type)} {GetVariableName(variable.Symbol)} = {initializer};");
            }

            WriteLine();
        }

        EmitStaticLocals(model.Procedures);

        foreach (var procedure in model.Procedures)
        {
            EmitProcedure(procedure);
            WriteLine();
        }

        _indent--;
        WriteLine("}");
        return _builder.ToString();
    }

    private void RegisterStaticLocalNames(ImmutableArray<BoundProcedure> procedures)
    {
        foreach (var procedure in procedures)
        {
            if (procedure.StaticLocals.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var local in procedure.StaticLocals)
            {
                _staticLocalNames[local.Symbol] =
                    $"__vb6_static_{SanitizeIdentifier(procedure.Symbol.Name)}_{SanitizeIdentifier(local.Symbol.Name)}";
            }
        }
    }

    private void EmitStaticLocals(ImmutableArray<BoundProcedure> procedures)
    {
        var emittedAny = false;
        foreach (var procedure in procedures)
        {
            if (procedure.StaticLocals.IsDefaultOrEmpty)
            {
                continue;
            }

            foreach (var local in procedure.StaticLocals)
            {
                var initializer = !local.ArrayDimensions.IsDefaultOrEmpty
                    ? EmitArrayCreation(local.Symbol.Type, local.ArrayDimensions)
                    : GetDefaultValue(local.Symbol.Type);
                WriteLine($"private static {GetTypeName(local.Symbol.Type)} {GetVariableName(local.Symbol)} = {initializer};");
                emittedAny = true;
            }
        }

        if (emittedAny)
        {
            WriteLine();
        }
    }

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
            WriteLine($"return {EmitCopyExpression(procedure.Symbol.ReturnType, "__vb6_return")};");
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

    private void EmitUserDefinedType(UserDefinedTypeSymbol type)
    {
        WriteLine("[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]");
        WriteLine($"private sealed class {GetUserDefinedTypeName(type)}");
        WriteLine("{");
        _indent++;

        foreach (var field in type.Fields)
        {
            if (field.FixedStringLength is not null &&
                TryGetConstantInt32(field.FixedStringLength, out var fixedStringLength))
            {
                WriteLine($"[MarshalAs(UnmanagedType.ByValTStr, SizeConst = {fixedStringLength})]");
            }

            WriteLine($"public {GetTypeName(field.Type)} {GetFieldName(field)};");
        }

        var initializedFields = type.Fields
            .Where(RequiresFieldInitialization)
            .ToImmutableArray();
        if (!initializedFields.IsDefaultOrEmpty)
        {
            WriteLine();
            WriteLine($"public {GetUserDefinedTypeName(type)}()");
            WriteLine("{");
            _indent++;
            foreach (var field in initializedFields)
            {
                var initializer = !field.ArrayDimensions.IsDefaultOrEmpty
                    ? EmitArrayCreation(field.Type, field.ArrayDimensions)
                    : GetFieldDefaultValue(field);
                WriteLine($"{GetFieldName(field)} = {initializer};");
            }

            _indent--;
            WriteLine("}");
        }

        WriteLine();
        WriteLine($"public {GetUserDefinedTypeName(type)} Clone()");
        WriteLine("{");
        _indent++;
        WriteLine($"var copy = new {GetUserDefinedTypeName(type)}();");
        foreach (var field in type.Fields)
        {
            var fieldName = GetFieldName(field);
            WriteLine($"copy.{fieldName} = {EmitCopyExpression(field.Type, fieldName)};");
        }

        WriteLine("return copy;");
        _indent--;
        WriteLine("}");

        _indent--;
        WriteLine("}");
    }

    private void EmitStatement(BoundStatement statement)
    {
        switch (statement)
        {
            case BoundVariableDeclarationStatement declaration:
            {
                var initializer = !declaration.ArrayDimensions.IsDefaultOrEmpty
                    ? EmitArrayCreation(declaration.Variable.Type, declaration.ArrayDimensions)
                    : GetDefaultValue(declaration.Variable.Type);
                WriteLine($"{GetTypeName(declaration.Variable.Type)} {GetVariableName(declaration.Variable)} = {initializer};");
                break;
            }

            case BoundAssignmentStatement assignment:
                WriteLine($"{GetVariableName(assignment.Variable)} = {EmitAssignmentValue(assignment.Variable.Type, assignment.Expression)};");
                break;

            case BoundMemberAssignmentStatement assignment:
                WriteLine($"{EmitMemberAccess(assignment.Target, assignment.Field)} = {EmitAssignmentValue(assignment.Field, assignment.Expression)};");
                break;

            case BoundMemberArrayElementAssignmentStatement assignment:
                WriteLine($"{EmitMemberArrayElementAccess(assignment.Target, assignment.Field, assignment.Indices)} = {EmitAssignmentValue(GetArrayElementType(assignment.Field.Type), assignment.Expression)};");
                break;

            case BoundArrayElementAssignmentStatement assignment:
                WriteLine($"{EmitArrayElementAccess(assignment.Array, assignment.Indices)} = {EmitAssignmentValue(GetArrayElementType(assignment.Array.Type), assignment.Expression)};");
                break;

            case BoundReDimStatement redim:
                WriteLine(redim.Preserve
                    ? $"{GetVariableName(redim.Array)} = {GetVariableName(redim.Array)}.ResizePreserve({EmitArrayBounds(redim.ArrayDimensions)});"
                    : $"{GetVariableName(redim.Array)} = {EmitArrayCreation(redim.Array.Type, redim.ArrayDimensions)};");
                break;

            case BoundEraseStatement erase:
                foreach (var variable in erase.Variables)
                {
                    if (IsFixedArray(variable))
                    {
                        WriteLine($"{GetVariableName(variable)}.Clear();");
                    }
                    else
                    {
                        WriteLine($"{GetVariableName(variable)} = default!;");
                    }
                }

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
                WriteLine("{");
                _indent++;
                foreach (var nested in withStatement.Body.Statements)
                {
                    EmitStatement(nested);
                }

                _indent--;
                WriteLine("}");
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
                EmitInvocationStatement(invocation);
                break;
        }
    }

    private void EmitInvocationStatement(BoundInvocationStatement invocation)
    {
        if (!invocation.Arguments.Any(argument => argument.IsByRefTemporary))
        {
            WriteLine($"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments)});");
            return;
        }

        var temporaryNames = new Dictionary<int, string>();
        WriteLine("{");
        _indent++;

        for (var index = 0; index < invocation.Arguments.Length; index++)
        {
            var argument = invocation.Arguments[index];
            if (!argument.IsByRefTemporary || argument.Parameter is null)
            {
                continue;
            }

            var temporaryName = $"__vb6_byref_temp_{_nextByRefTemporaryId++}";
            temporaryNames[index] = temporaryName;
            WriteLine($"{GetTypeName(argument.Parameter.Type)} {temporaryName} = {EmitAssignmentValue(argument.Parameter.Type, argument.Expression)};");
        }

        WriteLine($"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments, temporaryNames)});");

        EmitByRefCopyBacks(invocation.Arguments, temporaryNames);

        _indent--;
        WriteLine("}");
    }

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
        var itemName = $"__vb6_for_each_item_{statement.LoopId}";
        WriteLine($"foreach (var {itemName} in {EmitExpression(statement.Collection)}.Values())");
        WriteLine("{");
        _indent++;
        WriteLine($"{GetVariableName(statement.ControlVariable)} = {EmitConversion(statement.ControlVariable.Type, itemName)};");
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

    private string EmitArguments(IEnumerable<BoundArgument> arguments) =>
        string.Join(", ", arguments.Select(argument => EmitArgument(argument)));

    private string EmitArguments(ImmutableArray<BoundArgument> arguments, IReadOnlyDictionary<int, string> temporaryNames)
    {
        var emitted = new string[arguments.Length];
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            emitted[index] = temporaryNames.TryGetValue(index, out var temporaryName)
                ? EmitArgument(argument, temporaryName)
                : EmitArgument(argument);
        }

        return string.Join(", ", emitted);
    }

    private string EmitArgument(BoundArgument argument, string? byRefTemporaryName = null)
    {
        if (byRefTemporaryName is not null)
        {
            return $"ref {byRefTemporaryName}";
        }

        var expression = argument.Parameter?.PassingMode == ParameterPassingMode.ByRef
            ? EmitByRefArgumentExpression(argument.Expression)
            : EmitExpression(argument.Expression);
        return argument.Parameter?.PassingMode == ParameterPassingMode.ByRef
            ? $"ref {expression}"
            : argument.Parameter is null
                ? expression
                : EmitCopyExpression(argument.Parameter.Type, expression);
    }

    private string EmitByRefArgumentExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundArrayElementExpression arrayElement => EmitArrayElementRefAccess(arrayElement.Array, arrayElement.Indices),
            BoundMemberArrayElementExpression memberArrayElement =>
                EmitMemberArrayElementRefAccess(memberArrayElement.Target, memberArrayElement.Field, memberArrayElement.Indices),
            _ => EmitExpression(expression)
        };
    }

    private string EmitExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundLiteralExpression literal => EmitLiteral(literal),
            BoundVariableExpression variable => GetVariableName(variable.Variable),
            BoundMemberAccessExpression memberAccess => EmitMemberAccess(memberAccess.Target, memberAccess.Field),
            BoundMemberArrayElementExpression memberArrayElement =>
                EmitMemberArrayElementAccess(memberArrayElement.Target, memberArrayElement.Field, memberArrayElement.Indices),
            BoundArrayElementExpression arrayElement => EmitArrayElementAccess(arrayElement.Array, arrayElement.Indices),
            BoundArrayBoundExpression arrayBound => EmitArrayBound(arrayBound),
            BoundInvocationExpression invocation => EmitInvocationExpression(invocation),
            BoundParamArrayExpression paramArray => EmitParamArray(paramArray),
            BoundVariantIntrinsicExpression intrinsic => EmitVariantIntrinsic(intrinsic),
            BoundConversionExpression conversion => EmitConversion(conversion),
            BoundUnaryExpression unary => EmitUnary(unary),
            BoundBinaryExpression binary => EmitBinary(binary),
            BoundErrorExpression => "default",
            _ => "default"
        };
    }

    private string EmitInvocationExpression(BoundInvocationExpression invocation)
    {
        if (!invocation.Arguments.Any(argument => argument.IsByRefTemporary))
        {
            return $"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments)})";
        }

        var temporaryNames = new Dictionary<int, string>();
        var statements = new List<string>();
        for (var index = 0; index < invocation.Arguments.Length; index++)
        {
            var argument = invocation.Arguments[index];
            if (!argument.IsByRefTemporary || argument.Parameter is null)
            {
                continue;
            }

            var temporaryName = $"__vb6_byref_temp_{_nextByRefTemporaryId++}";
            temporaryNames[index] = temporaryName;
            statements.Add(
                $"{GetTypeName(argument.Parameter.Type)} {temporaryName} = {EmitAssignmentValue(argument.Parameter.Type, argument.Expression)};");
        }

        var resultName = $"__vb6_byref_result_{_nextByRefTemporaryId++}";
        statements.Add(
            $"{GetTypeName(invocation.Type)} {resultName} = {GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments, temporaryNames)});");
        AddByRefCopyBacks(statements, invocation.Arguments, temporaryNames);
        statements.Add($"return {resultName};");
        return $"((System.Func<{GetTypeName(invocation.Type)}>)(() => {{ {string.Join(" ", statements)} }}))()";
    }

    private void EmitByRefCopyBacks(
        ImmutableArray<BoundArgument> arguments,
        IReadOnlyDictionary<int, string> temporaryNames)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.CopyBackTarget is null ||
                !temporaryNames.TryGetValue(index, out var temporaryName))
            {
                continue;
            }

            WriteLine(
                $"{GetVariableName(argument.CopyBackTarget)} = {EmitCopyBackValue(argument.CopyBackTarget.Type, temporaryName)};");
        }
    }

    private void AddByRefCopyBacks(
        List<string> statements,
        ImmutableArray<BoundArgument> arguments,
        IReadOnlyDictionary<int, string> temporaryNames)
    {
        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];
            if (argument.CopyBackTarget is null ||
                !temporaryNames.TryGetValue(index, out var temporaryName))
            {
                continue;
            }

            statements.Add(
                $"{GetVariableName(argument.CopyBackTarget)} = {EmitCopyBackValue(argument.CopyBackTarget.Type, temporaryName)};");
        }
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

        if (literal.LiteralType == TypeSymbol.Decimal)
        {
            var value = Convert.ToDecimal(literal.Value, CultureInfo.InvariantCulture);
            return $"{value.ToString(CultureInfo.InvariantCulture)}m";
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

        if (literal.LiteralType == TypeSymbol.Variant &&
            literal.Value is VBVariantLiteral variantLiteral)
        {
            return variantLiteral switch
            {
                VBVariantLiteral.Empty => "VBVariant.Empty",
                VBVariantLiteral.Null => "VBVariant.Null",
                VBVariantLiteral.Nothing => "VBVariant.Nothing",
                VBVariantLiteral.Missing => "VBVariant.Missing",
                _ => "VBVariant.Empty"
            };
        }

        if (literal.LiteralType == TypeSymbol.Object || literal.LiteralType is ClassTypeSymbol)
        {
            return "null";
        }

        return "default";
    }

    private string EmitVariantIntrinsic(BoundVariantIntrinsicExpression intrinsic) =>
        $"VBVariantFunctions.{intrinsic.Name}({string.Join(", ", intrinsic.Arguments.Select(EmitExpression))})";

    private string EmitParamArray(BoundParamArrayExpression paramArray)
    {
        var elementTypeName = GetTypeName(paramArray.ArrayType.ElementType);
        return $"VBArray<{elementTypeName}>.FromValues({string.Join(", ", paramArray.Values.Select(EmitExpression))})";
    }

    private string EmitConversion(BoundConversionExpression conversion)
    {
        return EmitConversion(conversion.TargetType, EmitExpression(conversion.Expression));
    }

    private static string EmitConversion(TypeSymbol targetType, string expression)
    {
        if (targetType == TypeSymbol.Byte)
        {
            return $"VBConversions.CByte({expression})";
        }

        if (targetType == TypeSymbol.Integer)
        {
            return $"VBConversions.CInt({expression})";
        }

        if (targetType == TypeSymbol.Long)
        {
            return $"VBConversions.CLng({expression})";
        }

        if (targetType is EnumTypeSymbol)
        {
            return $"VBConversions.CLng({expression})";
        }

        if (targetType == TypeSymbol.LongLong)
        {
            return $"VBConversions.CLngLng({expression})";
        }

        if (targetType == TypeSymbol.Currency)
        {
            return $"VBConversions.CCur({expression})";
        }

        if (targetType == TypeSymbol.Decimal)
        {
            return $"VBConversions.CDec({expression})";
        }

        if (targetType == TypeSymbol.Single)
        {
            return $"VBConversions.CSng({expression})";
        }

        if (targetType == TypeSymbol.String)
        {
            return $"VBConversions.CStr({expression})";
        }

        if (targetType == TypeSymbol.Boolean)
        {
            return $"VBConversions.CBool({expression})";
        }

        if (targetType == TypeSymbol.Double)
        {
            return $"VBConversions.CDbl({expression})";
        }

        if (targetType == TypeSymbol.Variant)
        {
            return $"VBVariant.From({expression})";
        }

        return expression;
    }

    private string EmitUnary(BoundUnaryExpression unary)
    {
        var operand = EmitExpression(unary.Operand);
        if (unary.ResultType == TypeSymbol.Variant)
        {
            return unary.OperatorKind switch
            {
                SyntaxKind.PlusToken => operand,
                SyntaxKind.MinusToken => $"VBVariantOperators.Negate({operand})",
                SyntaxKind.NotKeyword => $"VBVariantOperators.Not({operand})",
                _ => operand
            };
        }

        return unary.OperatorKind switch
        {
            SyntaxKind.PlusToken => operand,
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.LongLong => $"VBOperators.NegateLongLong({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Long => $"VBOperators.NegateLong({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Currency => $"VBOperators.NegateCurrency({operand})",
            SyntaxKind.MinusToken when unary.ResultType == TypeSymbol.Decimal => $"VBOperators.NegateDecimal({operand})",
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

        if (binary.Left.Type == TypeSymbol.Variant ||
            binary.Right.Type == TypeSymbol.Variant ||
            binary.ResultType == TypeSymbol.Variant)
        {
            return EmitVariantBinary(binary, left, right);
        }

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
            SyntaxKind.SlashToken when binary.ResultType == TypeSymbol.Decimal => $"VBOperators.DivideDecimal({left}, {right})",
            SyntaxKind.SlashToken when binary.ResultType == TypeSymbol.Single => $"VBOperators.DivideSingle({left}, {right})",
            SyntaxKind.SlashToken => $"VBOperators.DivideDouble({left}, {right})",
            _ => "default"
        };
    }

    private static string EmitVariantBinary(BoundBinaryExpression binary, string left, string right)
    {
        var method = binary.OperatorKind switch
        {
            SyntaxKind.CaretToken => "Power",
            SyntaxKind.EqualsToken => "Equal",
            SyntaxKind.LessGreaterToken => "NotEqual",
            SyntaxKind.LessToken => "Less",
            SyntaxKind.LessOrEqualsToken => "LessOrEqual",
            SyntaxKind.GreaterToken => "Greater",
            SyntaxKind.GreaterOrEqualsToken => "GreaterOrEqual",
            SyntaxKind.AndKeyword => "And",
            SyntaxKind.OrKeyword => "Or",
            SyntaxKind.XorKeyword => "Xor",
            SyntaxKind.EqvKeyword => "Eqv",
            SyntaxKind.ImpKeyword => "Imp",
            SyntaxKind.AmpersandToken => "Concat",
            SyntaxKind.PlusToken => "Add",
            SyntaxKind.MinusToken => "Subtract",
            SyntaxKind.StarToken => "Multiply",
            SyntaxKind.BackslashToken => "IntegerDivide",
            SyntaxKind.ModKeyword => "Mod",
            SyntaxKind.SlashToken => "Divide",
            _ => null
        };

        return method is null
            ? "default"
            : $"VBVariantOperators.{method}({left}, {right})";
    }

    private static string EmitArithmeticCall(TypeSymbol resultType, string operation, string left, string right) =>
        $"VBOperators.{operation}{GetRuntimeTypeSuffix(resultType)}({left}, {right})";

    private string EmitArrayCreation(TypeSymbol type, ImmutableArray<BoundArrayDimension> dimensions)
    {
        var arrayType = type as ArrayTypeSymbol;
        var elementType = arrayType?.ElementType ?? TypeSymbol.Error;
        return $"new VBArray<{GetTypeName(elementType)}>({EmitArrayBounds(dimensions)})";
    }

    private string EmitArrayBounds(ImmutableArray<BoundArrayDimension> dimensions) =>
        string.Join(", ", dimensions.Select(dimension =>
            $"new VBArrayBound({EmitExpression(dimension.LowerBound)}, {EmitExpression(dimension.UpperBound)})"));

    private string EmitArrayElementAccess(VariableSymbol array, ImmutableArray<BoundExpression> indices) =>
        $"{GetVariableName(array)}[{string.Join(", ", indices.Select(EmitExpression))}]";

    private string EmitArrayElementRefAccess(VariableSymbol array, ImmutableArray<BoundExpression> indices) =>
        $"{GetVariableName(array)}.Element({string.Join(", ", indices.Select(EmitExpression))})";

    private string EmitAssignmentValue(TypeSymbol targetType, BoundExpression expression) =>
        EmitCopyExpression(targetType, EmitExpression(expression));

    private static string EmitCopyBackValue(TypeSymbol targetType, string temporaryName) =>
        EmitCopyExpression(targetType, EmitConversion(targetType, temporaryName));

    private string EmitAssignmentValue(UserDefinedFieldSymbol field, BoundExpression expression)
    {
        var value = EmitAssignmentValue(field.Type, expression);
        return field.FixedStringLength is null
            ? value
            : $"VBStrings.FixedLength({value}, {EmitFixedStringLength(field)})";
    }

    private string GetFieldDefaultValue(UserDefinedFieldSymbol field) =>
        field.FixedStringLength is null
            ? GetDefaultValue(field.Type)
            : $"VBStrings.FixedLength(string.Empty, {EmitFixedStringLength(field)})";

    private string EmitFixedStringLength(UserDefinedFieldSymbol field) =>
        $"checked((int){EmitExpression(field.FixedStringLength!)})";

    private static bool TryGetConstantInt32(BoundExpression expression, out int value)
    {
        switch (expression)
        {
            case BoundLiteralExpression literal:
                value = Convert.ToInt32(literal.Value, CultureInfo.InvariantCulture);
                return true;
            case BoundConversionExpression conversion:
                return TryGetConstantInt32(conversion.Expression, out value);
            default:
                value = 0;
                return false;
        }
    }

    private static TypeSymbol GetArrayElementType(TypeSymbol type) =>
        type is ArrayTypeSymbol arrayType ? arrayType.ElementType : TypeSymbol.Error;

    private static string EmitCopyExpression(TypeSymbol type, string expression)
    {
        if (type is UserDefinedTypeSymbol)
        {
            return $"{expression}.Clone()";
        }

        if (type is ArrayTypeSymbol arrayType)
        {
            if (arrayType.ElementType is UserDefinedTypeSymbol)
            {
                return $"{expression}.Clone(static value => value is null ? null! : value.Clone())";
            }

            return $"{expression}.Clone()";
        }

        return expression;
    }

    private string EmitMemberAccess(BoundExpression target, UserDefinedFieldSymbol field) =>
        $"{EmitExpression(target)}.{GetFieldName(field)}";

    private string EmitMemberArrayElementAccess(
        BoundExpression target,
        UserDefinedFieldSymbol field,
        ImmutableArray<BoundExpression> indices) =>
        $"{EmitMemberAccess(target, field)}[{string.Join(", ", indices.Select(EmitExpression))}]";

    private string EmitMemberArrayElementRefAccess(
        BoundExpression target,
        UserDefinedFieldSymbol field,
        ImmutableArray<BoundExpression> indices) =>
        $"{EmitMemberAccess(target, field)}.Element({string.Join(", ", indices.Select(EmitExpression))})";

    private string EmitArrayBound(BoundArrayBoundExpression arrayBound)
    {
        var method = arrayBound.IsUpperBound ? "UBound" : "LBound";
        return $"{GetVariableName(arrayBound.Array)}.{method}({EmitExpression(arrayBound.Dimension)})";
    }

    private static bool IsFixedArray(VariableSymbol variable) => variable switch
    {
        LocalVariableSymbol local => local.IsFixedArray,
        ModuleVariableSymbol module => module.IsFixedArray,
        ParameterSymbol parameter => parameter.IsFixedArray,
        _ => false
    };

    private static string GetRuntimeTypeSuffix(TypeSymbol type) =>
        type == TypeSymbol.Boolean
            ? "Boolean"
            : type == TypeSymbol.Currency
                ? "Currency"
                : type == TypeSymbol.Decimal
                    ? "Decimal"
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

    private static string GetTypeName(TypeSymbol type)
    {
        if (type is ArrayTypeSymbol arrayType)
        {
            return $"VBArray<{GetTypeName(arrayType.ElementType)}>";
        }

        if (type is UserDefinedTypeSymbol userDefinedType)
        {
            return GetUserDefinedTypeName(userDefinedType);
        }

        if (type is EnumTypeSymbol)
        {
            return "int";
        }

        if (type == TypeSymbol.Object || type is ClassTypeSymbol)
        {
            return "object?";
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

        if (type == TypeSymbol.Decimal)
        {
            return "decimal";
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

        if (type == TypeSymbol.Variant)
        {
            return "VBVariant";
        }

        return "object?";
    }

    private static string GetDefaultValue(TypeSymbol type)
    {
        if (type == TypeSymbol.Variant)
        {
            return "VBVariant.Empty";
        }

        if (type == TypeSymbol.Object || type is ClassTypeSymbol)
        {
            return "null";
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

        if (type is EnumTypeSymbol)
        {
            return "0";
        }

        if (type == TypeSymbol.Double)
        {
            return "0d";
        }

        if (type == TypeSymbol.Decimal)
        {
            return "0m";
        }

        if (type == TypeSymbol.Byte || type == TypeSymbol.Integer || type == TypeSymbol.Long || type == TypeSymbol.LongLong)
        {
            return "0";
        }

        if (type == TypeSymbol.Currency)
        {
            return "default";
        }

        if (type is ArrayTypeSymbol)
        {
            return "default!";
        }

        if (type is UserDefinedTypeSymbol userDefinedType)
        {
            return $"new {GetUserDefinedTypeName(userDefinedType)}()";
        }

        return "default";
    }

    private static bool RequiresFieldInitialization(UserDefinedFieldSymbol field) =>
        !field.ArrayDimensions.IsDefaultOrEmpty ||
        field.Type == TypeSymbol.String ||
        field.Type is UserDefinedTypeSymbol;

    private static string GetProcedureName(ProcedureSymbol procedure) =>
        !procedure.IsFunction && string.Equals(procedure.Name, "Main", StringComparison.OrdinalIgnoreCase)
            ? "Main"
            : $"__vb6_{SanitizeIdentifier(procedure.Name)}";

    private string GetVariableName(VariableSymbol variable) => variable switch
    {
        ReturnValueSymbol => "__vb6_return",
        LocalVariableSymbol { IsStatic: true } local when _staticLocalNames.TryGetValue(local, out var name) => name,
        ParameterSymbol parameter => $"__vb6_arg_{SanitizeIdentifier(parameter.Name)}",
        _ => $"__vb6_{SanitizeIdentifier(variable.Name)}"
    };

    private static string GetFieldName(UserDefinedFieldSymbol field) =>
        $"__vb6_field_{SanitizeIdentifier(field.Name)}";

    private static string GetUserDefinedTypeName(UserDefinedTypeSymbol type) =>
        $"__vb6_type_{SanitizeIdentifier(type.Name)}";

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
