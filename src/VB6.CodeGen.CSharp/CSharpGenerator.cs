using System.Globalization;
using System.Text;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.CodeGen.CSharp;

public sealed class CSharpGenerator
{
    private readonly StringBuilder _builder = new();
    private int _indent;
    private bool _currentProcedureReturnsValue;

    public string Generate(SemanticModel model)
    {
        _builder.Clear();
        _indent = 0;

        WriteLine("using VB6.Runtime;");
        WriteLine();
        WriteLine("namespace VB6.Generated;");
        WriteLine();
        WriteLine("internal static class Program");
        WriteLine("{");
        _indent++;

        if (!model.ModuleVariables.IsDefaultOrEmpty)
        {
            foreach (var variable in model.ModuleVariables)
            {
                var initializer = variable.Initializer is null
                    ? GetDefaultValue(variable.Symbol.Type)
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

    private static string EmitParameter(ParameterSymbol parameter)
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
                WriteLine($"{GetTypeName(declaration.Variable.Type)} {GetVariableName(declaration.Variable)} = {GetDefaultValue(declaration.Variable.Type)};");
                break;

            case BoundAssignmentStatement assignment:
                WriteLine($"{GetVariableName(assignment.Variable)} = {EmitExpression(assignment.Expression)};");
                break;

            case BoundIfStatement ifStatement:
                EmitIfStatement(ifStatement);
                break;

            case BoundForStatement forStatement:
                EmitForStatement(forStatement);
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

    private string EmitRelationalCaseCondition(string selectName, BoundCaseRelationalClause clause)
    {
        var value = EmitExpression(clause.Value);
        return clause.OperatorKind switch
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
        string.Join(", ", arguments.Select(EmitArgument));

    private string EmitArgument(BoundArgument argument)
    {
        var expression = EmitExpression(argument.Expression);
        return argument.Parameter?.PassingMode == ParameterPassingMode.ByRef
            ? $"ref {expression}"
            : expression;
    }

    private string EmitExpression(BoundExpression expression)
    {
        return expression switch
        {
            BoundLiteralExpression literal => EmitLiteral(literal),
            BoundVariableExpression variable => GetVariableName(variable.Variable),
            BoundInvocationExpression invocation =>
                $"{GetProcedureName(invocation.Procedure)}({EmitArguments(invocation.Arguments)})",
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

    private static string GetTypeName(TypeSymbol type)
    {
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

    private static string GetDefaultValue(TypeSymbol type)
    {
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

    private static string GetProcedureName(ProcedureSymbol procedure) =>
        !procedure.IsFunction && string.Equals(procedure.Name, "Main", StringComparison.OrdinalIgnoreCase)
            ? "Main"
            : $"__vb6_{SanitizeIdentifier(procedure.Name)}";

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
