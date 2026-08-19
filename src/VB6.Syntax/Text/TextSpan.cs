namespace VB6.Syntax.Text;

public readonly record struct TextSpan(int Start, int Length)
{
    public int End => Start + Length;

    public static TextSpan FromBounds(int start, int end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "End must be greater than or equal to start.");
        }

        return new TextSpan(start, end - start);
    }

    public override string ToString() => $"{Start}..{End}";
}
