using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

namespace VB6.Compiler;

internal static class VBConditionalCompilation
{
    public static VBConditionalCompilationResult Process(
        string source,
        string? filePath,
        VBCompilationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var constants = CreateDefaultConstants(options);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var blocks = new Stack<ConditionalBlock>();
        var output = new StringBuilder(source.Length);
        var position = 0;

        while (position < source.Length)
        {
            var lineStart = position;
            while (position < source.Length && source[position] is not '\r' and not '\n')
            {
                position++;
            }

            var line = source[lineStart..position];
            var newlineStart = position;
            if (position < source.Length && source[position] == '\r')
            {
                position++;
                if (position < source.Length && source[position] == '\n')
                {
                    position++;
                }
            }
            else if (position < source.Length)
            {
                position++;
            }

            var newline = source[newlineStart..position];
            if (TryReadDirective(line, out var directive, out var arguments))
            {
                ProcessDirective(
                    directive,
                    arguments,
                    lineStart,
                    line.Length,
                    filePath,
                    constants,
                    blocks,
                    diagnostics);
                output.Append(new string(' ', line.Length));
            }
            else if (IsActive(blocks))
            {
                output.Append(line);
            }
            else
            {
                output.Append(new string(' ', line.Length));
            }

            output.Append(newline);
        }

        while (blocks.Count > 0)
        {
            var block = blocks.Pop();
            diagnostics.Add(CreateDiagnostic(
                "VB6CC0006",
                "Conditional compilation block is missing '#End If'.",
                block.Position,
                1,
                filePath));
        }

        return new VBConditionalCompilationResult(output.ToString(), diagnostics.ToImmutable());
    }

    private static void ProcessDirective(
        string directive,
        string arguments,
        int position,
        int lineLength,
        string? filePath,
        Dictionary<string, object?> constants,
        Stack<ConditionalBlock> blocks,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        switch (directive)
        {
            case "CONST":
                if (!IsActive(blocks))
                {
                    return;
                }

                if (!TryParseAssignment(arguments, out var name, out var expression) ||
                    !TryEvaluate(expression, constants, out var value))
                {
                    diagnostics.Add(CreateDiagnostic(
                        "VB6CC0001",
                        "Conditional '#Const' requires a constant name and a supported expression.",
                        position,
                        lineLength,
                        filePath));
                    return;
                }

                constants[name] = value;
                return;

            case "IF":
                var parentActive = IsActive(blocks);
                var isSupported = TryEvaluate(
                    RemoveTrailingThen(arguments),
                    constants,
                    out var result);
                var condition = parentActive && isSupported && IsTrue(result);
                if (parentActive && !isSupported)
                {
                    diagnostics.Add(CreateDiagnostic(
                        "VB6CC0002",
                        "Conditional '#If' expression is not supported.",
                        position,
                        lineLength,
                        filePath));
                }

                blocks.Push(new ConditionalBlock(position, parentActive, condition, condition));
                return;

            case "ELSEIF":
                if (blocks.Count == 0)
                {
                    AddDirectiveDiagnostic("VB6CC0003", "'#ElseIf' has no matching '#If'.");
                    return;
                }

                var elseIfBlock = blocks.Peek();
                if (!elseIfBlock.ParentActive || elseIfBlock.BranchTaken)
                {
                    elseIfBlock.CurrentActive = false;
                    return;
                }

                var elseIfExpression = RemoveTrailingThen(arguments);
                if (!TryEvaluate(elseIfExpression, constants, out var elseIfResult))
                {
                    diagnostics.Add(CreateDiagnostic(
                        "VB6CC0002",
                        "Conditional '#ElseIf' expression is not supported.",
                        position,
                        lineLength,
                        filePath));
                    elseIfBlock.CurrentActive = false;
                    return;
                }

                elseIfBlock.CurrentActive = IsTrue(elseIfResult);
                elseIfBlock.BranchTaken = elseIfBlock.CurrentActive;
                return;

            case "ELSE":
                if (blocks.Count == 0)
                {
                    AddDirectiveDiagnostic("VB6CC0004", "'#Else' has no matching '#If'.");
                    return;
                }

                var elseBlock = blocks.Peek();
                elseBlock.CurrentActive = elseBlock.ParentActive && !elseBlock.BranchTaken;
                elseBlock.BranchTaken = true;
                return;

            case "ENDIF":
                if (blocks.Count == 0)
                {
                    AddDirectiveDiagnostic("VB6CC0005", "'#End If' has no matching '#If'.");
                    return;
                }

                blocks.Pop();
                return;

            default:
                return;
        }

        void AddDirectiveDiagnostic(string code, string message) =>
            diagnostics.Add(CreateDiagnostic(code, message, position, lineLength, filePath));
    }

    private static bool TryReadDirective(string line, out string directive, out string arguments)
    {
        directive = string.Empty;
        arguments = string.Empty;
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('#'))
        {
            return false;
        }

        var rest = trimmed[1..].TrimStart();
        var wordEnd = 0;
        while (wordEnd < rest.Length && !char.IsWhiteSpace(rest[wordEnd]))
        {
            wordEnd++;
        }

        var word = rest[..wordEnd];
        arguments = wordEnd < rest.Length ? rest[wordEnd..].Trim() : string.Empty;
        if (word.Equals("End", StringComparison.OrdinalIgnoreCase) &&
            arguments.StartsWith("If", StringComparison.OrdinalIgnoreCase) &&
            (arguments.Length == 2 || char.IsWhiteSpace(arguments[2])))
        {
            directive = "ENDIF";
            arguments = arguments[2..].Trim();
            return true;
        }

        directive = word.ToUpperInvariant();
        return directive is "CONST" or "IF" or "ELSEIF" or "ELSE";
    }

    private static bool TryParseAssignment(
        string arguments,
        out string name,
        out string expression)
    {
        name = string.Empty;
        expression = string.Empty;
        var equals = arguments.IndexOf('=');
        if (equals <= 0)
        {
            return false;
        }

        name = arguments[..equals].Trim();
        expression = arguments[(equals + 1)..].Trim();
        return IsIdentifier(name) && expression.Length > 0;
    }

    private static string RemoveTrailingThen(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.EndsWith(" Then", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^5].TrimEnd();
        }

        return trimmed;
    }

    private static bool IsActive(Stack<ConditionalBlock> blocks) =>
        blocks.Count == 0 || blocks.Peek().CurrentActive;

    private static Dictionary<string, object?> CreateDefaultConstants(VBCompilationOptions? options)
    {
        var is64Bit = options?.TargetIs64Bit ?? (IntPtr.Size == 8);
        var constants = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["VBA7"] = true,
            ["VBA6"] = false,
            ["VBA"] = true,
            ["WIN16"] = false,
            // Win32 means the Win32 API family, which remains available on Win64.
            ["WIN32"] = true,
            ["WIN64"] = is64Bit,
            ["MAC"] = false,
            ["APPLE"] = false
        };
        return constants;
    }

    private static bool TryEvaluate(
        string expression,
        IReadOnlyDictionary<string, object?> constants,
        out object? value)
    {
        try
        {
            var parser = new ConditionalExpressionParser(expression, constants);
            value = parser.Parse();
            return parser.Success;
        }
        catch (Exception)
        {
            value = null;
            return false;
        }
    }

    private static bool IsTrue(object? value) => value switch
    {
        bool boolean => boolean,
        byte number => number != 0,
        short number => number != 0,
        int number => number != 0,
        long number => number != 0,
        float number => number != 0,
        double number => number != 0,
        decimal number => number != 0,
        _ => false
    };

    private static bool IsIdentifier(string value) =>
        value.Length > 0 &&
        (char.IsLetter(value[0]) || value[0] == '_') &&
        value.Skip(1).All(character => char.IsLetterOrDigit(character) || character == '_');

    private static Diagnostic CreateDiagnostic(
        string code,
        string message,
        int position,
        int length,
        string? filePath) =>
        new(code, DiagnosticSeverity.Error, message, new TextSpan(position, Math.Max(length, 1)), filePath);

    private sealed class ConditionalBlock
    {
        public ConditionalBlock(int position, bool parentActive, bool currentActive, bool branchTaken)
        {
            Position = position;
            ParentActive = parentActive;
            CurrentActive = currentActive;
            BranchTaken = branchTaken;
        }

        public int Position { get; }
        public bool ParentActive { get; }
        public bool CurrentActive { get; set; }
        public bool BranchTaken { get; set; }
    }

    private sealed class ConditionalExpressionParser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, object?> _constants;
        private int _position;

        public ConditionalExpressionParser(string text, IReadOnlyDictionary<string, object?> constants)
        {
            _text = text;
            _constants = constants;
        }

        public bool Success { get; private set; } = true;

        public object? Parse()
        {
            var value = ParseImplication();
            SkipWhitespace();
            if (_position != _text.Length)
            {
                Success = false;
            }

            return value;
        }

        private object? ParseImplication()
        {
            var left = ParseEquivalence();
            while (TryWord("Imp"))
            {
                left = ApplyBoolean(left, ParseEquivalence(), (a, b) => !a || b, (a, b) => ~a | b);
            }

            return left;
        }

        private object? ParseEquivalence()
        {
            var left = ParseXor();
            while (TryWord("Eqv"))
            {
                left = ApplyBoolean(left, ParseXor(), (a, b) => a == b, (a, b) => ~(a ^ b));
            }

            return left;
        }

        private object? ParseXorExpression()
        {
            var left = ParseOr();
            while (TryWord("Xor"))
            {
                left = ApplyBoolean(left, ParseOr(), (a, b) => a ^ b, (a, b) => a ^ b);
            }

            return left;
        }

        private object? ParseXor() => ParseXorExpression();

        private object? ParseOr()
        {
            var left = ParseAnd();
            while (TryWord("Or"))
            {
                left = ApplyBoolean(left, ParseAnd(), (a, b) => a || b, (a, b) => a | b);
            }

            return left;
        }

        private object? ParseAnd()
        {
            var left = ParseComparison();
            while (TryWord("And"))
            {
                left = ApplyBoolean(left, ParseComparison(), (a, b) => a && b, (a, b) => a & b);
            }

            return left;
        }

        private object? ParseComparison()
        {
            var left = ParseAdditive();
            while (true)
            {
                SkipWhitespace();
                var operation = ReadComparisonOperator();
                if (operation is null)
                {
                    return left;
                }

                var right = ParseAdditive();
                var comparison = Compare(left, right);
                left = operation switch
                {
                    "=" => comparison == 0,
                    "<>" => comparison != 0,
                    "<" => comparison < 0,
                    "<=" => comparison <= 0,
                    ">" => comparison > 0,
                    ">=" => comparison >= 0,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        private object? ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (true)
            {
                SkipWhitespace();
                if (TryCharacter('+'))
                {
                    left = Numeric(left, ParseMultiplicative(), (a, b) => a + b);
                }
                else if (TryCharacter('-'))
                {
                    left = Numeric(left, ParseMultiplicative(), (a, b) => a - b);
                }
                else if (TryCharacter('&'))
                {
                    left = ConvertString(left) + ConvertString(ParseMultiplicative());
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseMultiplicative()
        {
            var left = ParseUnary();
            while (true)
            {
                SkipWhitespace();
                if (TryCharacter('*'))
                {
                    left = Numeric(left, ParseUnary(), (a, b) => a * b);
                }
                else if (TryCharacter('/'))
                {
                    left = Numeric(left, ParseUnary(), (a, b) => a / b);
                }
                else
                {
                    return left;
                }
            }
        }

        private object? ParseUnary()
        {
            SkipWhitespace();
            if (TryWord("Not"))
            {
                var value = ParseUnary();
                return value is bool boolean ? !boolean : ~ConvertInt64(value);
            }

            if (TryCharacter('+'))
            {
                return Numeric(0L, ParseUnary(), (a, b) => a + b);
            }

            if (TryCharacter('-'))
            {
                return Numeric(0L, ParseUnary(), (a, b) => a - b);
            }

            return ParsePrimary();
        }

        private object? ParsePrimary()
        {
            SkipWhitespace();
            if (TryCharacter('('))
            {
                var value = ParseImplication();
                if (!TryCharacter(')'))
                {
                    Success = false;
                }

                return value;
            }

            if (_position >= _text.Length)
            {
                Success = false;
                return null;
            }

            if (_text[_position] == '"')
            {
                return ReadString();
            }

            if (char.IsDigit(_text[_position]) ||
                (_text[_position] == '&' && _position + 1 < _text.Length &&
                 (_text[_position + 1] is 'H' or 'h' or 'O' or 'o')))
            {
                return ReadNumber();
            }

            var identifier = ReadIdentifier();
            if (identifier is null)
            {
                Success = false;
                return null;
            }

            if (identifier.Equals("True", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (identifier.Equals("False", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (_constants.TryGetValue(identifier, out var constantValue))
            {
                return constantValue;
            }

            Success = false;
            return null;
        }

        private object? ReadNumber()
        {
            var start = _position;
            if (_text[_position] == '&' && _position + 1 < _text.Length &&
                (_text[_position + 1] is 'H' or 'h' or 'O' or 'o'))
            {
                _position += 2;
                while (_position < _text.Length &&
                       (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
                {
                    _position++;
                }

                var digits = _text[(start + 2).._position].Replace("_", string.Empty, StringComparison.Ordinal);
                var radix = _text[start + 1] is 'H' or 'h' ? 16 : 8;
                return long.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hex)
                    ? radix == 16 ? hex : Convert.ToInt64(digits, radix)
                    : FailNumber();
            }

            while (_position < _text.Length &&
                   (char.IsDigit(_text[_position]) || _text[_position] is '.' or 'e' or 'E' or '+' or '-'))
            {
                if (_text[_position] is '+' or '-' &&
                    _position > start && _text[_position - 1] is not 'e' and not 'E')
                {
                    break;
                }

                _position++;
            }

            var text = _text[start.._position];
            if (text.Contains('.') || text.Contains('e', StringComparison.OrdinalIgnoreCase))
            {
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floating)
                    ? floating
                    : FailNumber();
            }

            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                ? integer
                : FailNumber();
        }

        private object? FailNumber()
        {
            Success = false;
            return null;
        }

        private string ReadString()
        {
            _position++;
            var result = new StringBuilder();
            while (_position < _text.Length)
            {
                if (_text[_position] == '"')
                {
                    if (_position + 1 < _text.Length && _text[_position + 1] == '"')
                    {
                        result.Append('"');
                        _position += 2;
                        continue;
                    }

                    _position++;
                    return result.ToString();
                }

                result.Append(_text[_position++]);
            }

            Success = false;
            return result.ToString();
        }

        private string? ReadIdentifier()
        {
            SkipWhitespace();
            var start = _position;
            if (_position >= _text.Length ||
                (!char.IsLetter(_text[_position]) && _text[_position] != '_'))
            {
                return null;
            }

            _position++;
            while (_position < _text.Length &&
                   (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
            {
                _position++;
            }

            return _text[start.._position];
        }

        private string? ReadComparisonOperator()
        {
            if (_position >= _text.Length)
            {
                return null;
            }

            var remaining = _text[_position..];
            foreach (var operation in new[] { "<>", "<=", ">=", "=", "<", ">" })
            {
                if (remaining.StartsWith(operation, StringComparison.Ordinal))
                {
                    _position += operation.Length;
                    return operation;
                }
            }

            return null;
        }

        private bool TryWord(string word)
        {
            SkipWhitespace();
            if (_position + word.Length > _text.Length ||
                !string.Equals(_text[_position..(_position + word.Length)], word, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var end = _position + word.Length;
            if (end < _text.Length && (char.IsLetterOrDigit(_text[end]) || _text[end] == '_'))
            {
                return false;
            }

            _position = end;
            return true;
        }

        private bool TryCharacter(char character)
        {
            SkipWhitespace();
            if (_position >= _text.Length || _text[_position] != character)
            {
                return false;
            }

            _position++;
            return true;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
            {
                _position++;
            }
        }

        private static object Numeric(object? left, object? right, Func<double, double, double> operation)
        {
            var result = operation(ConvertNumber(left), ConvertNumber(right));
            return result == Math.Truncate(result) &&
                   result >= long.MinValue && result <= long.MaxValue
                ? (long)result
                : result;
        }

        private static object ApplyBoolean(
            object? left,
            object? right,
            Func<bool, bool, bool> booleanOperation,
            Func<long, long, long> numericOperation)
        {
            if (left is bool leftBoolean && right is bool rightBoolean)
            {
                return booleanOperation(leftBoolean, rightBoolean);
            }

            return numericOperation(ConvertInt64(left), ConvertInt64(right));
        }

        private static double ConvertNumber(object? value) => value switch
        {
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            float number => number,
            double number => number,
            decimal number => (double)number,
            bool boolean => boolean ? -1 : 0,
            _ => throw new InvalidOperationException()
        };

        private static long ConvertInt64(object? value) => checked((long)ConvertNumber(value));

        private static string ConvertString(object? value) => value switch
        {
            string text => text,
            bool boolean => boolean ? "True" : "False",
            _ => ConvertNumber(value).ToString(CultureInfo.InvariantCulture)
        };

        private static int Compare(object? left, object? right)
        {
            if (left is string leftText && right is string rightText)
            {
                return string.CompareOrdinal(leftText, rightText);
            }

            return ConvertNumber(left).CompareTo(ConvertNumber(right));
        }
    }
}

internal sealed record VBConditionalCompilationResult(
    string Source,
    ImmutableArray<Diagnostic> Diagnostics);
