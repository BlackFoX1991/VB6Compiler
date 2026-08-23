using System.Collections.Immutable;
using System.Globalization;
using VB6.Syntax;
using VB6.Syntax.Diagnostics;
using VB6.Syntax.Text;

namespace VB6.Lexer;

public sealed class Lexer
{
    private readonly SourceText _text;
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
    private int _position;

    public Lexer(SourceText text)
    {
        _text = text;
    }

    public LexResult Lex()
    {
        var tokens = ImmutableArray.CreateBuilder<SyntaxToken>();

        while (true)
        {
            var token = NextToken();
            tokens.Add(token);

            if (token.Kind == SyntaxKind.EndOfFileToken)
            {
                break;
            }
        }

        return new LexResult(tokens.ToImmutable(), _diagnostics.ToImmutable());
    }

    private SyntaxToken NextToken()
    {
        var leadingTrivia = ReadLeadingTrivia();
        var start = _position;

        if (Current == '\0')
        {
            return CreateToken(SyntaxKind.EndOfFileToken, start, 0, null, leadingTrivia);
        }

        if (Current is '\r' or '\n')
        {
            var width = Current == '\r' && Peek(1) == '\n' ? 2 : 1;
            _position += width;
            return CreateToken(SyntaxKind.NewLineToken, start, width, null, leadingTrivia);
        }

        if (Current == '[')
        {
            return ReadBracketedIdentifier(start, leadingTrivia);
        }

        if (char.IsLetter(Current) || Current == '_')
        {
            _position++;
            while (char.IsLetterOrDigit(Current) || Current == '_')
            {
                _position++;
            }

            var nameSpan = TextSpan.FromBounds(start, _position);
            var text = _text.ToString(nameSpan);
            var keywordKind = SyntaxFacts.GetKeywordKind(text);
            if (keywordKind == SyntaxKind.AliasKeyword && !IsDeclareAliasKeyword(start))
            {
                keywordKind = SyntaxKind.IdentifierToken;
            }

            if (keywordKind == SyntaxKind.IdentifierToken && IsIdentifierTypeSuffix(Current))
            {
                _position++;
            }

            var span = TextSpan.FromBounds(start, _position);
            return new SyntaxToken(keywordKind, span, text, null, leadingTrivia);
        }

        if (char.IsDigit(Current) || (Current == '.' && char.IsDigit(Peek(1))))
        {
            return ReadNumericToken(start, leadingTrivia);
        }

        if (Current == '"')
        {
            return ReadStringToken(start, leadingTrivia);
        }

        if (Current == '&' && IsRadixPrefix(Peek(1)) && IsRadixDigit(Peek(2), IsHexPrefix(Peek(1))))
        {
            return ReadRadixNumericToken(start, leadingTrivia);
        }

        var tokenKind = Current switch
        {
            '+' => SyntaxKind.PlusToken,
            '-' => SyntaxKind.MinusToken,
            '*' => SyntaxKind.StarToken,
            '/' => SyntaxKind.SlashToken,
            '\\' => SyntaxKind.BackslashToken,
            '^' => SyntaxKind.CaretToken,
            '&' => SyntaxKind.AmpersandToken,
            '=' => SyntaxKind.EqualsToken,
            '(' => SyntaxKind.OpenParenthesisToken,
            ')' => SyntaxKind.CloseParenthesisToken,
            ',' => SyntaxKind.CommaToken,
            '.' => SyntaxKind.DotToken,
            ':' => SyntaxKind.ColonToken,
            // A '#' that reaches here is not an identifier type suffix - those are consumed with
            // the identifier - so it introduces a file number, as in Open ... As #1 or Close #1.
            '#' => SyntaxKind.HashToken,
            '<' when Peek(1) == '=' => SyntaxKind.LessOrEqualsToken,
            '<' when Peek(1) == '>' => SyntaxKind.LessGreaterToken,
            '<' => SyntaxKind.LessToken,
            '>' when Peek(1) == '=' => SyntaxKind.GreaterOrEqualsToken,
            '>' => SyntaxKind.GreaterToken,
            _ => SyntaxKind.BadToken
        };

        var tokenWidth = tokenKind is SyntaxKind.LessOrEqualsToken or SyntaxKind.LessGreaterToken or SyntaxKind.GreaterOrEqualsToken ? 2 : 1;
        _position += tokenWidth;

        var token = CreateToken(tokenKind, start, tokenWidth, null, leadingTrivia);
        if (tokenKind == SyntaxKind.BadToken)
        {
            Report("VB6L0001", $"Unexpected character '{token.Text}'.", token.Span);
        }

        return token;
    }

    private SyntaxToken ReadBracketedIdentifier(
        int start,
        ImmutableArray<SyntaxTrivia> leadingTrivia)
    {
        _position++;
        var nameStart = _position;
        while (Current != '\0' && Current is not '\r' and not '\n' and not ']')
        {
            _position++;
        }

        var nameEnd = _position;
        var terminated = Current == ']';
        if (terminated)
        {
            _position++;
        }

        var span = TextSpan.FromBounds(start, _position);
        if (!terminated)
        {
            Report("VB6L0008", "Unterminated bracketed identifier.", span);
        }

        return new SyntaxToken(
            SyntaxKind.IdentifierToken,
            span,
            _text.ToString(TextSpan.FromBounds(nameStart, nameEnd)),
            null,
            leadingTrivia);
    }

    private SyntaxToken ReadNumericToken(int start, ImmutableArray<SyntaxTrivia> leadingTrivia)
    {
        var isFloating = false;

        while (char.IsDigit(Current))
        {
            _position++;
        }

        if (Current == '.')
        {
            isFloating = true;
            _position++;
            while (char.IsDigit(Current))
            {
                _position++;
            }
        }

        if (Current is 'E' or 'e' && IsValidExponentStart())
        {
            isFloating = true;
            _position++;
            if (Current is '+' or '-')
            {
                _position++;
            }

            while (char.IsDigit(Current))
            {
                _position++;
            }
        }

        var numericEnd = _position;
        var isCurrency = Current == '@';
        if (isCurrency)
        {
            isFloating = true;
            _position++;
        }

        var suffix = IntegerTypeSuffix.None;
        if (!isFloating && Current is '&' or '%')
        {
            suffix = Current == '&' ? IntegerTypeSuffix.Long : IntegerTypeSuffix.Integer;
            _position++;
        }

        var floatingSuffix = '\0';
        if (Current is '!' or '#')
        {
            isFloating = true;
            floatingSuffix = Current;
            _position++;
        }

        var span = TextSpan.FromBounds(start, _position);
        var text = _text.ToString(span);
        var numericText = _text.ToString(TextSpan.FromBounds(start, numericEnd));

        if (isCurrency)
        {
            if (decimal.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var currencyValue) &&
                currencyValue >= -922337203685477.5808m &&
                currencyValue <= 922337203685477.5807m)
            {
                currencyValue = decimal.Round(currencyValue, 4, MidpointRounding.ToEven);
                return new SyntaxToken(SyntaxKind.FloatingLiteralToken, span, text, currencyValue, leadingTrivia);
            }

            Report("VB6L0005", "Invalid or out-of-range Currency literal.", span);
            return new SyntaxToken(SyntaxKind.FloatingLiteralToken, span, text, null, leadingTrivia);
        }

        if (isFloating)
        {
            if (double.TryParse(numericText, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatingValue))
            {
                object value;
                if (floatingSuffix == '!')
                {
                    value = (float)floatingValue;
                }
                else
                {
                    value = floatingValue;
                }

                return new SyntaxToken(SyntaxKind.FloatingLiteralToken, span, text, value, leadingTrivia);
            }

            Report("VB6L0004", "Invalid floating-point literal.", span);
            return new SyntaxToken(SyntaxKind.FloatingLiteralToken, span, text, null, leadingTrivia);
        }

        if (long.TryParse(numericText, NumberStyles.None, CultureInfo.InvariantCulture, out var integerValue))
        {
            return new SyntaxToken(
                SyntaxKind.IntegerLiteralToken,
                span,
                text,
                ApplyIntegerSuffix(integerValue, suffix, span),
                leadingTrivia);
        }

        Report("VB6L0003", "Invalid integer literal.", span);
        return new SyntaxToken(SyntaxKind.IntegerLiteralToken, span, text, null, leadingTrivia);
    }

    private SyntaxToken ReadRadixNumericToken(int start, ImmutableArray<SyntaxTrivia> leadingTrivia)
    {
        var isHex = IsHexPrefix(Peek(1));
        _position += 2;

        var digitsStart = _position;
        while (IsRadixDigit(Current, isHex))
        {
            _position++;
        }

        var digits = _text.ToString(TextSpan.FromBounds(digitsStart, _position));

        var suffix = IntegerTypeSuffix.None;
        if (Current is '&' or '%')
        {
            suffix = Current == '&' ? IntegerTypeSuffix.Long : IntegerTypeSuffix.Integer;
            _position++;
        }

        var span = TextSpan.FromBounds(start, _position);
        var text = _text.ToString(span);
        var radix = isHex ? 16UL : 8UL;
        var magnitude = 0UL;

        foreach (var digit in digits)
        {
            var digitValue = (ulong)Uri.FromHex(digit);
            if (magnitude > (ulong.MaxValue - digitValue) / radix)
            {
                Report("VB6L0006", $"{RadixName(isHex)} literal is outside the supported range.", span);
                return new SyntaxToken(SyntaxKind.IntegerLiteralToken, span, text, null, leadingTrivia);
            }

            magnitude = magnitude * radix + digitValue;
        }

        object? value = suffix switch
        {
            IntegerTypeSuffix.Integer when magnitude <= ushort.MaxValue => unchecked((short)(ushort)magnitude),
            IntegerTypeSuffix.Long when magnitude <= uint.MaxValue => unchecked((int)(uint)magnitude),
            IntegerTypeSuffix.None when magnitude <= ushort.MaxValue => unchecked((short)(ushort)magnitude),
            IntegerTypeSuffix.None when magnitude <= uint.MaxValue => unchecked((int)(uint)magnitude),
            IntegerTypeSuffix.None => unchecked((long)magnitude),
            _ => null
        };

        if (value is null)
        {
            Report("VB6L0006", $"{RadixName(isHex)} literal is outside the range of its type suffix.", span);
        }

        return new SyntaxToken(SyntaxKind.IntegerLiteralToken, span, text, value, leadingTrivia);
    }

    private object? ApplyIntegerSuffix(long value, IntegerTypeSuffix suffix, TextSpan span)
    {
        switch (suffix)
        {
            case IntegerTypeSuffix.Integer when value <= short.MaxValue:
                return (short)value;
            case IntegerTypeSuffix.Long when value <= int.MaxValue:
                return (int)value;
            case IntegerTypeSuffix.None:
                return value;
            default:
                Report("VB6L0007", "Integer literal is outside the range of its type suffix.", span);
                return null;
        }
    }

    private bool TryGetLineContinuationEnd(out int end)
    {
        var index = _position + 1;

        while (index < _text.Length && _text[index] is ' ' or '\t' or '\f')
        {
            index++;
        }

        if (index < _text.Length && _text[index] == '\r')
        {
            index++;
            if (index < _text.Length && _text[index] == '\n')
            {
                index++;
            }

            end = index;
            return true;
        }

        if (index < _text.Length && _text[index] == '\n')
        {
            end = index + 1;
            return true;
        }

        end = 0;
        return false;
    }

    private bool IsDeclareAliasKeyword(int aliasStart)
    {
        var logicalStart = FindLogicalLineStart(aliasStart);
        var prefix = _text.ToString(TextSpan.FromBounds(logicalStart, aliasStart));
        return prefix.Contains("Declare", StringComparison.OrdinalIgnoreCase) &&
               prefix.Contains("Lib", StringComparison.OrdinalIgnoreCase);
    }

    private int FindLogicalLineStart(int position)
    {
        var scan = position - 1;
        while (scan >= 0)
        {
            if (_text[scan] is not '\r' and not '\n')
            {
                scan--;
                continue;
            }

            var newlineStart = scan;
            if (_text[scan] == '\n' && scan > 0 && _text[scan - 1] == '\r')
            {
                newlineStart--;
            }

            var previousContentEnd = newlineStart - 1;
            while (previousContentEnd >= 0 && _text[previousContentEnd] is ' ' or '\t' or '\f')
            {
                previousContentEnd--;
            }

            if (previousContentEnd >= 0 && _text[previousContentEnd] == '_')
            {
                scan = previousContentEnd - 1;
                continue;
            }

            return scan + 1;
        }

        return 0;
    }

    private static string RadixName(bool isHex) => isHex ? "Hexadecimal" : "Octal";

    private static bool IsIdentifierTypeSuffix(char character) =>
        character is '$' or '%' or '&' or '!' or '#' or '@';

    private static bool IsRadixPrefix(char character) => character is 'H' or 'h' or 'O' or 'o';

    private static bool IsHexPrefix(char character) => character is 'H' or 'h';

    private static bool IsRadixDigit(char character, bool isHex) =>
        isHex ? Uri.IsHexDigit(character) : character is >= '0' and <= '7';

    private enum IntegerTypeSuffix
    {
        None,
        Integer,
        Long
    }

    private bool IsValidExponentStart()
    {
        if (char.IsDigit(Peek(1)))
        {
            return true;
        }

        return Peek(1) is '+' or '-' && char.IsDigit(Peek(2));
    }

    private SyntaxToken ReadStringToken(int start, ImmutableArray<SyntaxTrivia> leadingTrivia)
    {
        _position++;
        var value = new System.Text.StringBuilder();
        var terminated = false;

        while (Current != '\0' && Current is not '\r' and not '\n')
        {
            if (Current == '"')
            {
                if (Peek(1) == '"')
                {
                    value.Append('"');
                    _position += 2;
                    continue;
                }

                _position++;
                terminated = true;
                break;
            }

            value.Append(Current);
            _position++;
        }

        var span = TextSpan.FromBounds(start, _position);
        if (!terminated)
        {
            Report("VB6L0002", "Unterminated string literal.", span);
        }

        return new SyntaxToken(
            SyntaxKind.StringLiteralToken,
            span,
            _text.ToString(span),
            value.ToString(),
            leadingTrivia);
    }

    private ImmutableArray<SyntaxTrivia> ReadLeadingTrivia()
    {
        var trivia = ImmutableArray.CreateBuilder<SyntaxTrivia>();

        while (true)
        {
            if (Current is ' ' or '\t' or '\f')
            {
                var start = _position;
                do
                {
                    _position++;
                }
                while (Current is ' ' or '\t' or '\f');

                var span = TextSpan.FromBounds(start, _position);
                trivia.Add(new SyntaxTrivia(SyntaxTriviaKind.Whitespace, span, _text.ToString(span)));
                continue;
            }

            if (Current == '\'')
            {
                var start = _position;
                while (Current != '\0' && Current is not '\r' and not '\n')
                {
                    _position++;
                }

                var span = TextSpan.FromBounds(start, _position);
                trivia.Add(new SyntaxTrivia(SyntaxTriviaKind.Comment, span, _text.ToString(span)));
                continue;
            }

            if (Current == '_' && TryGetLineContinuationEnd(out var continuationEnd))
            {
                var start = _position;
                _position = continuationEnd;
                var span = TextSpan.FromBounds(start, _position);
                trivia.Add(new SyntaxTrivia(SyntaxTriviaKind.LineContinuation, span, _text.ToString(span)));
                continue;
            }

            break;
        }

        return trivia.ToImmutable();
    }

    private SyntaxToken CreateToken(
        SyntaxKind kind,
        int start,
        int length,
        object? value,
        ImmutableArray<SyntaxTrivia> leadingTrivia)
    {
        var span = new TextSpan(start, length);
        return new SyntaxToken(kind, span, _text.ToString(span), value, leadingTrivia);
    }

    private char Current => Peek(0);

    private char Peek(int offset)
    {
        var index = _position + offset;
        return index >= _text.Length ? '\0' : _text[index];
    }

    private void Report(string code, string message, TextSpan span)
    {
        _diagnostics.Add(new Diagnostic(code, DiagnosticSeverity.Error, message, span, _text.FilePath));
    }
}
