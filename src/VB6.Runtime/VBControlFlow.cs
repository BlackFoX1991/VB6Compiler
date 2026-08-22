namespace VB6.Runtime;

public static class VBControlFlow
{
    /// <summary>Converts VB6's one-based On ... GoTo selector to a zero-based IL switch index.</summary>
    public static int OnGoToIndex(int value) => value <= 0 ? -1 : value - 1;
}
