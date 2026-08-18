using VB6.Syntax.Text;

namespace VB6.Syntax;

public enum SyntaxTriviaKind
{
    Whitespace,
    Comment
}

public readonly record struct SyntaxTrivia(
    SyntaxTriviaKind Kind,
    TextSpan Span,
    string Text);
