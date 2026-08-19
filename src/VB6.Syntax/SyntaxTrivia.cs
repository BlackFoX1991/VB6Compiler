using VB6.Syntax.Text;

namespace VB6.Syntax;

public enum SyntaxTriviaKind
{
    Whitespace,
    Comment,

    /// <summary>
    /// A VB6 line continuation: an underscore at the end of a line, which joins it with the
    /// next one. Kept as trivia so the rest of the compiler sees a single logical line.
    /// </summary>
    LineContinuation
}

public readonly record struct SyntaxTrivia(
    SyntaxTriviaKind Kind,
    TextSpan Span,
    string Text);
