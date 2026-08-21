using System.Collections.Immutable;

namespace VB6.Syntax.Text;

public sealed class SourceText
{
    private readonly string _text;

    private SourceText(string text, string? filePath)
    {
        _text = text;
        FilePath = filePath;
        Lines = ParseLines(text);
    }

    public string? FilePath { get; }

    public int Length => _text.Length;

    public ImmutableArray<TextLine> Lines { get; }

    public char this[int index] => _text[index];

    public static SourceText From(string text, string? filePath = null) => new(text, filePath);

    public string ToString(TextSpan span) => _text.Substring(span.Start, span.Length);

    /// <summary>
    /// Translates a character offset into a zero-based line and column. Debug information is
    /// expressed in lines and columns, and this is the only place that knows where the lines are.
    /// </summary>
    public LinePosition GetLinePosition(int position)
    {
        if (position < 0 || position > Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                position,
                $"Position is outside the source text 0..{Length}.");
        }

        var low = 0;
        var high = Lines.Length - 1;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (Lines[middle].Start > position)
            {
                high = middle - 1;
            }
            else
            {
                low = middle;
            }
        }

        return new LinePosition(low, position - Lines[low].Start);
    }

    public LinePositionSpan GetLinePositionSpan(TextSpan span) =>
        new(GetLinePosition(span.Start), GetLinePosition(span.End));

    public override string ToString() => _text;

    private static ImmutableArray<TextLine> ParseLines(string text)
    {
        var builder = ImmutableArray.CreateBuilder<TextLine>();
        var lineStart = 0;
        var position = 0;

        while (position < text.Length)
        {
            var lineBreakWidth = GetLineBreakWidth(text, position);
            if (lineBreakWidth == 0)
            {
                position++;
                continue;
            }

            AddLine(builder, lineStart, position, lineBreakWidth);
            position += lineBreakWidth;
            lineStart = position;
        }

        if (position >= lineStart)
        {
            AddLine(builder, lineStart, position, 0);
        }

        return builder.ToImmutable();
    }

    private static void AddLine(ImmutableArray<TextLine>.Builder builder, int lineStart, int lineEnd, int lineBreakWidth)
    {
        builder.Add(new TextLine(
            lineStart,
            lineEnd - lineStart,
            lineEnd - lineStart + lineBreakWidth));
    }

    private static int GetLineBreakWidth(string text, int position)
    {
        var current = text[position];
        var next = position + 1 >= text.Length ? '\0' : text[position + 1];

        if (current == '\r' && next == '\n')
        {
            return 2;
        }

        return current is '\r' or '\n' ? 1 : 0;
    }
}

public readonly record struct TextLine(int Start, int Length, int LengthIncludingLineBreak)
{
    public int End => Start + Length;

    public int EndIncludingLineBreak => Start + LengthIncludingLineBreak;

    public TextSpan Span => new(Start, Length);

    public TextSpan SpanIncludingLineBreak => new(Start, LengthIncludingLineBreak);
}

/// <summary>A zero-based position in a source file.</summary>
public readonly record struct LinePosition(int Line, int Character);

/// <summary>A zero-based line/column range, the form debug information is written in.</summary>
public readonly record struct LinePositionSpan(LinePosition Start, LinePosition End);
