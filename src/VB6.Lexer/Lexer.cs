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

        if (char.IsLetter(Current) || Current == '_')
        {
            _position++;
            while (char.IsLetterOrDigit(Current) || Current == '_')
            {
                _position++;
            }

            var span = TextSpan.FromBounds(start, _position);
            var text = _text.ToString(span);
            var keywordKind = SyntaxFacts.GetKeywordKind(text);
            return new SyntaxToken(keywordKind, span, text, null, leadingTrivia);
        }

        if (char.IsDigit(Current))
        {
            _position++;
            while (char.IsDigit(Current))
            {
                _position++;
            }

            var span = TextSpan.FromBounds(start, _position);
            var text = _text.ToString(span);
            object? value = null;

            if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                value = parsed;
            }
            else
            {
                Report("VB6L0003", "Invalid integer literal.", span);
            }

            return new SyntaxToken(SyntaxKind.IntegerLiteralToken, span, text, value, leadingTrivia);
        }

        if (Current == '"')
        {
            return ReadStringToken(start, leadingTrivia);
        }

        var tokenKind = Current switch
        {
            '+' => SyntaxKind.PlusToken,
            '-' => SyntaxKind.MinusToken,
            '*' => SyntaxKind.StarToken,
            '/' => SyntaxKind.SlashToken,
            '\\' => SyntaxKind.BackslashToken,
            '&' => SyntaxKind.AmpersandToken,
            '=' => SyntaxKind.EqualsToken,
            '(' => SyntaxKind.OpenParenthesisToken,
            ')' => SyntaxKind.CloseParenthesisToken,
            ',' => SyntaxKind.CommaToken,
            '.' => SyntaxKind.DotToken,
            ':' => SyntaxKind.ColonToken,
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
