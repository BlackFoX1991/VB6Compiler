using System.Globalization;
using System.Text;
using VB6.Semantics;
using VB6.Syntax;

namespace VB6.CodeGen.CSharp;

public sealed class CSharpGenerator
{
    private readonly StringBuilder _builder = new();
    private int _indent;

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
        var isMain = string.Equals(procedure.Symbol.Name, "Main", StringComparison.OrdinalIgnoreCase);
        var visibility = isMain ? "public" : "private";
        var name = GetProcedureName(procedure.Symbol);
        var parameters = string.Join(", ", procedure.Symbol.Parameters.Select(EmitParameter));

        WriteLine($"{visibility} static void {name}({parameters})");
        WriteLine("{");
        _indent++;
        EmitBlock(procedure.Body);
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
                WriteLine($"if ({EmitExpression(ifStatement.Condition)})");
                WriteLine("{");
                _indent++;
                EmitBlock(ifStatement.Body);
                _indent--;
                WriteLine("}");
                break;

            case BoundDebugPrintStatement debugPrint:
                WriteLine($"VBDebug.Print({EmitExpression(debugPrint.Expression)});");
                break;

            case BoundInvocationStatement invocation:
                var arguments = string.Join(", ", invocation.Arguments.Select(EmitArgument));
                WriteLine($"{GetProcedureName(invocation.Procedure)}({arguments});");
                break;
        }
    }

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
            BoundConversionExpression conversion => EmitConversion(conversion),
            BoundUnaryExpression unary => EmitUnary(unary),
            BoundBinaryExpression binary => EmitBinary(binary),
            BoundErrorExpression => "default",
            _ => "default"
        };
    }

    private static string EmitLiteral(BoundLiteralExpression literal)
    {
        if (literal.LiteralType == TypeSymbol.Integer)
        {
            var value = Convert.ToInt64(literal.Value, CultureInfo.InvariantCulture);
            return $"VBConversions.CInt({value.ToString(CultureInfo.InvariantCulture)}L)";
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

        if (conversion.TargetType == TypeSymbol.Integer)
        {
            return $"VBConversions.CInt({expression})";
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
            SyntaxKind.MinusToken => $"VBOperators.NegateInteger({operand})",
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
            SyntaxKind.AmpersandToken => $"VBOperators.Concat({left}, {right})",
            SyntaxKind.PlusToken when binary.ResultType == TypeSymbol.String => $"VBOperators.Concat({left}, {right})",
            SyntaxKind.PlusToken => $"VBOperators.AddInteger({left}, {right})",
            SyntaxKind.MinusToken => $"VBOperators.SubtractInteger({left}, {right})",
            SyntaxKind.StarToken => $"VBOperators.MultiplyInteger({left}, {right})",
            SyntaxKind.BackslashToken => $"VBOperators.IntegerDivide({left}, {right})",
            SyntaxKind.SlashToken => $"VBOperators.DivideDouble({left}, {right})",
            _ => "default"
        };
    }

    private static string GetTypeName(TypeSymbol type)
    {
        if (type == TypeSymbol.Integer)
        {
            return "short";
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

        if (type == TypeSymbol.Double)
        {
            return "0d";
        }

        if (type == TypeSymbol.Integer)
        {
            return "0";
        }

        return "default";
    }

    private static string GetProcedureName(ProcedureSymbol procedure) =>
        string.Equals(procedure.Name, "Main", StringComparison.OrdinalIgnoreCase)
            ? "Main"
            : $"__vb6_{SanitizeIdentifier(procedure.Name)}";

    private static string GetVariableName(VariableSymbol variable) => variable switch
    {
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
